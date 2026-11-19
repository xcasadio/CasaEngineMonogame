using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Tests.UI.Backend;
using Xunit;

namespace CasaEngine.Tests.Video;

/// <summary>
/// <c>VideoSpriteComponent.Play(bool hideWhenEnded)</c> (ADR-0132) with the real player: the entity is hidden in the very
/// update where the film ends, a decode failure included; <c>Play()</c>, <c>Play(false)</c>, a later <c>Play()</c> and
/// <c>Stop()</c> leave it visible; and <c>Update</c> still does not allocate, the update of the end included.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public sealed class VideoSpriteHideWhenEndedGpuTests : IDisposable
{
    private static readonly Guid VideoId = Guid.Parse("d4000000-0000-4000-8000-000000000001");
    private const string VideoName = "hide-film";

    /// <summary>One film frame per update: the test films run at 10 frames per second.</summary>
    private const float Step = 0.1f;

    private readonly List<string> _files = new();

    public VideoSpriteHideWhenEndedGpuTests()
    {
        AssetCatalog.ClearInternal();
        AssetCatalog.AddInternal(new AssetInfo(VideoId) { Name = VideoName, FileName = "hide-film.mdecvideo", AssetType = "mdecvideo" });
    }

    public void Dispose()
    {
        AssetCatalog.ClearInternal();
        foreach (var file in _files)
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // Left to the system.
            }
        }
    }

    private string NewVideo(int frameCount, uint rate, int corruptFrame = -1)
    {
        var path = WorldVideoTestFiles.Write(frameCount, rate, 1, corruptFrame);
        _files.Add(path);
        return path;
    }

    /// <summary>A film entity added to a world, made visible as a cut-scene action does before it plays.</summary>
    private static Entity ShownFilm(string path)
    {
        var harness = new WorldVideoHarness(new WorldVideoTestFiles.Loader(path), GpuDeviceHost.Instance.GraphicsDevice);
        var entity = new Entity { Name = "Film", RootComponent = new VideoSpriteComponent { VideoName = VideoName } };
        harness.AddToWorld(entity);
        entity.IsVisible = true;
        return entity;
    }

    private static VideoSpriteComponent Video(Entity entity) => (VideoSpriteComponent)entity.RootComponent!;

    [GpuFact]
    public void PlayTrue_HidesTheEntity_InTheUpdateWhereTheFilmEnds()
    {
        var path = NewVideo(3, 10);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var entity = ShownFilm(path);
            var video = Video(entity);

            video.Play(true);
            Assert.True(video.HidesWhenEnded);

            // At 10 frames per second and one tenth of a second per update, a film of 3 pictures ends in update 3.
            var update = 0;
            while (!video.IsEnded && update < 20)
            {
                Assert.True(entity.IsVisible, $"visible before update {update + 1}");
                video.Update(Step);
                update++;
            }

            Assert.Equal(3, update);
            Assert.False(entity.IsVisible);
            Assert.False(video.IsPlaying);
            Assert.False(video.HidesWhenEnded);
        });
    }

    [GpuTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void PlayWithoutTheFlag_KeepsTheEntityVisibleAfterTheEnd(bool throughTheOverload)
    {
        var path = NewVideo(3, 10);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var entity = ShownFilm(path);
            var video = Video(entity);

            if (throughTheOverload)
            {
                video.Play(false);
            }
            else
            {
                video.Play();
            }

            Assert.False(video.HidesWhenEnded);
            for (var update = 0; update < 5; update++)
            {
                video.Update(Step);
            }

            Assert.True(video.IsEnded);
            Assert.True(entity.IsVisible);
        });
    }

    [GpuFact]
    public void PlayTrue_ThenStop_ClearsTheFlag_AndNeverHidesTheEntity()
    {
        var path = NewVideo(3, 10);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var entity = ShownFilm(path);
            var video = Video(entity);

            video.Play(true);
            video.Update(Step);
            video.Stop();

            Assert.False(video.HidesWhenEnded);
            for (var update = 0; update < 5; update++)
            {
                video.Update(Step);
            }

            Assert.False(video.IsPlaying);
            Assert.True(entity.IsVisible);
        });
    }

    [GpuFact]
    public void PlayTrue_ThenPlay_KeepsTheEntityVisibleAtTheEnd()
    {
        var path = NewVideo(3, 10);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var entity = ShownFilm(path);
            var video = Video(entity);

            video.Play(true);
            video.Update(Step);
            video.Play();

            Assert.False(video.HidesWhenEnded);
            for (var update = 0; update < 5; update++)
            {
                video.Update(Step);
            }

            Assert.True(video.IsEnded);
            Assert.True(entity.IsVisible);
        });
    }

    [GpuFact]
    public void PlayTrue_OnAPictureThatCannotBeDecoded_HidesTheEntity_InTheUpdateWhereThePlayerEnds()
    {
        var path = NewVideo(3, 10, corruptFrame: 1);
        var errors = new List<string>();
        Logs.AddLogger(new CapturingLogger(errors));
        try
        {
            GpuDeviceHost.Instance.Invoke(() =>
            {
                var entity = ShownFilm(path);
                var video = Video(entity);

                video.Play(true);
                Assert.True(entity.IsVisible);

                // Update 1 reaches picture 1, which cannot be decoded: the player ends there.
                video.Update(Step);

                Assert.True(video.IsEnded);
                Assert.False(video.IsPlaying);
                Assert.False(entity.IsVisible);
                Assert.False(video.HidesWhenEnded);
            });
        }
        finally
        {
            Logs.Close();
        }

        Assert.Single(errors, message => message.Contains("frame 1"));
    }

    [GpuFact]
    public void Update_OfAFilmThatHidesItsEntity_DoesNotAllocate_TheUpdateOfTheEndIncluded()
    {
        var path = NewVideo(32, 30);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var entity = ShownFilm(path);
            var video = Video(entity);
            video.Play(true);
            const float step = 1f / 30f;
            video.Update(step);
            video.Update(step);

            // 1/30 s rounds down to whole ticks, so 32 updates fall just short of 32 pictures: 34 reach the end.
            var before = AllocationWindow.Start();
            for (var i = 0; i < 32; i++)
            {
                video.Update(step);
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(video.IsEnded, "the loop must reach the end of the film");
            Assert.False(entity.IsVisible);
            Assert.Equal(0, allocated);
        });
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly List<string> _errors;

        public CapturingLogger(List<string> errors) => _errors = errors;

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) { }
        public void WriteError(string msg) => _errors.Add(msg);
    }
}
