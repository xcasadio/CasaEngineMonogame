using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scripting.Coroutines;
using CasaEngine.Tests.UI.Backend;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Video;

/// <summary>
/// The PlayVideoSprite cutscene action (ADR-0091) over the real player: it shows the entity, starts the film, waits for
/// its end and hides the entity; a stopped or replaced play takes the film down too. The frames the film starts and the
/// wait ends on are measured with a loop that runs the coroutines, then the entity, as a frame of the game does.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public sealed class PlayVideoSpriteCutsceneActionGpuTests : IDisposable
{
    private static readonly Guid VideoId = Guid.Parse("d3000000-0000-4000-8000-000000000001");
    private const string VideoName = "action-film";
    private const float Step = 0.1f;

    private readonly List<string> _files = new();

    public PlayVideoSpriteCutsceneActionGpuTests()
    {
        AssetCatalog.ClearInternal();
        AssetCatalog.AddInternal(new AssetInfo(VideoId) { Name = VideoName, FileName = "action-film.mdecvideo", AssetType = "mdecvideo" });
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

    private string NewVideo(int frameCount)
    {
        var path = WorldVideoTestFiles.Write(frameCount, 10, 1);
        _files.Add(path);
        return path;
    }

    private static WorldVideoHarness NewHarness(string path)
        => new(new WorldVideoTestFiles.Loader(path), GpuDeviceHost.Instance.GraphicsDevice);

    private static Entity AddFilm(WorldVideoHarness harness)
    {
        var entity = new Entity { Name = "Film", RootComponent = new VideoSpriteComponent { VideoName = VideoName } };
        harness.World.Entities.Add(entity);
        harness.AddToWorld(entity);
        return entity;
    }

    private static VideoSpriteComponent Video(Entity entity) => (VideoSpriteComponent)entity.RootComponent!;

    private static void Play(World world, CutsceneActionData root)
        => world.CutsceneDirector.Play(new CutsceneAsset { Name = "Film", RootAction = root });

    /// <summary>One frame of the game: the coroutines, then the entity.</summary>
    private static void Frame(World world, Entity entity, ref long frame)
    {
        frame++;
        world.CoroutineManager.Update(new CoroutineUpdateContext(Step, Step, 1f, frame));
        entity.Update(Step);
    }

    [GpuFact]
    public void TheAction_ShowsTheEntity_StartsTheFilm_WaitsForItsEnd_ThenHidesTheEntity()
    {
        var path = NewVideo(3);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var harness = NewHarness(path);
            var entity = AddFilm(harness);
            var video = Video(entity);
            Assert.False(entity.IsVisible);

            Play(harness.World, new PlayVideoSpriteCutsceneActionData { EntityName = "Film" });
            long frame = 0;

            Frame(harness.World, entity, ref frame);
            Assert.True(entity.IsVisible);
            Assert.True(video.IsPlaying);
            var pictureAfterFirstFrame = video.CurrentFrameIndex;

            var visibleFrames = 1;
            while (harness.World.CutsceneDirector.GetDebugSnapshot().State == CutsceneRuntimeState.Playing && frame < 20)
            {
                Frame(harness.World, entity, ref frame);
                if (entity.IsVisible)
                {
                    visibleFrames++;
                }
            }

            Assert.Equal(CutsceneRuntimeState.Completed, harness.World.CutsceneDirector.GetDebugSnapshot().State);
            Assert.False(entity.IsVisible);
            Assert.False(video.IsPlaying);
            Assert.True(video.IsEnded);

            // Measured: see the plan note of T1.2.
            Assert.Equal(1, pictureAfterFirstFrame);
            Assert.Equal(4, frame);
            Assert.Equal(3, visibleFrames);
        });
    }

    [GpuFact]
    public void AStoppedPlay_StopsTheFilm_AndHidesTheEntity()
    {
        var path = NewVideo(30);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var harness = NewHarness(path);
            var entity = AddFilm(harness);
            var video = Video(entity);
            Play(harness.World, new PlayVideoSpriteCutsceneActionData { EntityName = "Film" });
            long frame = 0;
            Frame(harness.World, entity, ref frame);
            Frame(harness.World, entity, ref frame);
            Assert.True(video.IsPlaying);
            Assert.True(entity.IsVisible);

            harness.World.CutsceneDirector.Stop();

            // The coroutine manager unwinds a stopped coroutine at its next update, which runs the cleanup.
            harness.World.CoroutineManager.Update(new CoroutineUpdateContext(Step, Step, 1f, 10));

            Assert.False(video.IsPlaying);
            Assert.False(entity.IsVisible);
        });
    }

    [GpuFact]
    public void AReplacedPlay_StopsTheFilm_AndHidesTheEntity()
    {
        var path = NewVideo(30);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var harness = NewHarness(path);
            var entity = AddFilm(harness);
            var video = Video(entity);
            Play(harness.World, new PlayVideoSpriteCutsceneActionData { EntityName = "Film" });
            long frame = 0;
            Frame(harness.World, entity, ref frame);
            Assert.True(video.IsPlaying);

            Play(harness.World, new WaitCutsceneActionData { Seconds = 5f });
            harness.World.CoroutineManager.Update(new CoroutineUpdateContext(Step, Step, 1f, 10));

            Assert.False(video.IsPlaying);
            Assert.False(entity.IsVisible);
        });
    }

    [GpuFact]
    public void AClearedWorld_LeavesTheReleasedComponentAlone()
    {
        var path = NewVideo(30);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var harness = NewHarness(path);
            var entity = AddFilm(harness);
            var video = Video(entity);
            Play(harness.World, new PlayVideoSpriteCutsceneActionData { EntityName = "Film" });
            long frame = 0;
            Frame(harness.World, entity, ref frame);

            // What World.Clear does to the coroutines, then to the entities (the physics part needs a full game).
            harness.World.RuntimeSystems.Clear();
            harness.World.ClearEntities(true);
            var visibleBeforeCleanup = entity.IsVisible;

            // The stopped coroutine's cleanup runs at the next update of the coroutine manager: with the world
            // cleared, its guard must leave the released component and the discarded entity untouched.
            harness.World.CoroutineManager.Update(new CoroutineUpdateContext(Step, Step, 1f, 10));

            Assert.False(video.HasPlayer);
            Assert.False(video.IsPlaying);
            Assert.Equal(visibleBeforeCleanup, entity.IsVisible);
        });
    }

    [GpuFact]
    public void AnEntityDroppedByAFailedCreation_IsAnUnknownEntity_ForTheAction()
    {
        var path = NewVideo(3);
        File.Delete(path);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var harness = NewHarness(path);
            var entity = new Entity { Name = "Film", RootComponent = new VideoSpriteComponent { VideoName = VideoName } };

            harness.World.AddEntity(entity);
            harness.World.Update(1f / 60f);
            Assert.DoesNotContain(entity, harness.World.Entities);

            Play(harness.World, new PlayVideoSpriteCutsceneActionData { EntityName = "Film" });
            harness.World.CoroutineManager.Update(new CoroutineUpdateContext(Step, Step, 1f, 1));

            var snapshot = harness.World.CutsceneDirector.GetDebugSnapshot();
            Assert.Equal(CutsceneRuntimeState.Failed, snapshot.State);
            Assert.Contains("'Film'", snapshot.ActiveActionStopReason);
            Assert.Contains("not found", snapshot.ActiveActionStopReason);
        });
    }
}
