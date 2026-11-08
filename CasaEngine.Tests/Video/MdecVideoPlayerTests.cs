using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.UI.Backend.MonoGame.Assets;
using CasaEngine.Framework.Video;
using CasaEngine.Framework.Video.Mdec;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MGUI.Shared.Assets;
using MGUI.Shared.Text;
using Xunit;

namespace CasaEngine.Tests.Video;

/// <summary>
/// Tests of <see cref="MdecVideoPlayer"/> and of <see cref="CasaUIAssetProvider.TryCreateVideo"/> (ADR-0089, plan choices
/// C3 and C5). They run on the real device: the player owns a texture. The pictures are one macroblock (16 x 16) whose
/// six blocks carry constant DC values that change with the frame index, so every frame is a flat colour that the
/// decoder gives and that differs from its neighbours.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class MdecVideoPlayerTests : IDisposable
{
    private static readonly Guid VideoId = Guid.Parse("a1a1a1a1-0000-0000-0000-000000000001");
    private static readonly Guid SpriteLikeId = Guid.Parse("a1a1a1a1-0000-0000-0000-000000000002");

    private readonly List<string> _files = new();

    public MdecVideoPlayerTests()
    {
        AssetCatalog.ClearInternal();
    }

    public void Dispose()
    {
        AssetCatalog.ClearInternal();
        foreach (var file in _files)
        {
            File.Delete(file);
        }
    }

    private static byte[] Frame(int index)
    {
        var luma = -300 + index * 20;
        var blocks = new[]
        {
            new MdecTestBlock(index % 5 * 8 - 16),
            new MdecTestBlock(16 - index % 3 * 8),
            new MdecTestBlock(luma),
            new MdecTestBlock(luma),
            new MdecTestBlock(luma),
            new MdecTestBlock(luma),
        };
        return MdecTestEncoder.Encode(1 + index % 3, index % 4 + 1, blocks);
    }

    private string WriteVideo(int frameCount, uint rateNumerator, uint rateDenominator)
    {
        var frames = new List<byte[]>();
        for (var i = 0; i < frameCount; i++)
        {
            frames.Add(Frame(i));
        }

        var path = Path.Combine(Path.GetTempPath(), $"player-{Guid.NewGuid():N}.mdecvideo");
        File.WriteAllBytes(path, MdecTestContainerWriter.Write(frames, 16, 16, rateNumerator, rateDenominator));
        _files.Add(path);
        return path;
    }

    private static byte[] Expected(int frameIndex)
    {
        var frame = Frame(frameIndex);
        var words = new ushort[MdecBitstreamDecoder.MaxWordCount(frame.Length)];
        var count = MdecBitstreamDecoder.Decode(frame, frameIndex, 1, words);
        var rgba = new byte[16 * 16 * 4];
        MdecFrameDecoder.Decode(words.AsSpan(0, count), frameIndex, 16, 16, rgba);
        return rgba;
    }

    private static byte[] ReadTexture(MdecVideoPlayer player)
    {
        var texture = ((CasaMonoGameImageResource)player.CurrentImage).Texture;
        var pixels = new byte[texture.Width * texture.Height * 4];
        texture.GetData(pixels);
        return pixels;
    }

    [GpuFact]
    public void Constructor_ShowsFrameZero_AsExactlyTheDecoderOutput()
    {
        var path = WriteVideo(6, 15, 1);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var player = new MdecVideoPlayer(GpuDeviceHost.Instance.GraphicsDevice, MdecVideoAsset.Load(path));

            Assert.Equal(0, player.CurrentFrameIndex);
            Assert.Equal(1, player.DecodedFrameCount);
            Assert.False(player.IsEnded);
            Assert.Null(player.CurrentSourceRect);
            Assert.Equal(16, player.CurrentImage.Width);
            Assert.Equal(16, player.CurrentImage.Height);
            Assert.Equal(Expected(0), ReadTexture(player));
        });
    }

    [GpuFact]
    public void Texture_AfterEachNewFrame_EqualsTheDecoderOutput_AndStaysTheSameInstance()
    {
        var path = WriteVideo(6, 15, 1);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var player = new MdecVideoPlayer(GpuDeviceHost.Instance.GraphicsDevice, MdecVideoAsset.Load(path));
            var image = player.CurrentImage;

            for (var frame = 1; frame < 6; frame++)
            {
                player.Advance(TimeSpan.FromTicks(666667)); // one period of 666666.67 ticks, rounded up
                Assert.Equal(frame, player.CurrentFrameIndex);
                Assert.Same(image, player.CurrentImage);
                Assert.Equal(Expected(frame), ReadTexture(player));
            }

            // The frames differ, so the checks above prove that the texture really changes.
            Assert.NotEqual(Expected(0), Expected(5));
        });
    }

    [GpuFact]
    public void Clock_At15FramesPerSecond_WithStepsOf166667Ticks_ShowsFrameKFromUpdate4kTo4kPlus3()
    {
        var path = WriteVideo(6, 15, 1);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var player = new MdecVideoPlayer(GpuDeviceHost.Instance.GraphicsDevice, MdecVideoAsset.Load(path));
            var step = TimeSpan.FromTicks(166667);

            // Update 0 is the state before the first Advance.
            Assert.Equal(0, player.CurrentFrameIndex);
            for (var update = 1; update < 24; update++)
            {
                player.Advance(step);
                Assert.Equal(update / 4, player.CurrentFrameIndex);
                Assert.False(player.IsEnded, $"ended at update {update}");
            }

            // 24 steps are 4000008 ticks, which cover the 6 periods (4000000 ticks).
            player.Advance(step);
            Assert.True(player.IsEnded);
            Assert.Equal(5, player.CurrentFrameIndex);
            Assert.Equal(Expected(5), ReadTexture(player));

            player.Advance(step);
            Assert.Equal(5, player.CurrentFrameIndex);
            Assert.Equal(6, player.DecodedFrameCount);
        });
    }

    [GpuFact]
    public void Clock_UsesTheRationalRate_WithoutRoundingThePeriod()
    {
        // 30000 / 1001 images per second: the period is 333666.67 ticks, 4 images last 1334666.67 ticks (1334667 rounded up).
        var path = WriteVideo(4, 30000, 1001);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var player = new MdecVideoPlayer(GpuDeviceHost.Instance.GraphicsDevice, MdecVideoAsset.Load(path));

            player.Advance(TimeSpan.FromTicks(333666));
            Assert.Equal(0, player.CurrentFrameIndex);
            player.Advance(TimeSpan.FromTicks(1));
            Assert.Equal(1, player.CurrentFrameIndex);

            // Three periods are exactly 1001000 ticks.
            player.Advance(TimeSpan.FromTicks(1001000 - 333667));
            Assert.Equal(3, player.CurrentFrameIndex);
            Assert.False(player.IsEnded);
            player.Advance(TimeSpan.FromTicks(333667 + 1));
            Assert.True(player.IsEnded);
        });
    }

    [GpuFact]
    public void ALateFrame_IsNotDecoded()
    {
        var path = WriteVideo(8, 10, 1);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var player = new MdecVideoPlayer(GpuDeviceHost.Instance.GraphicsDevice, MdecVideoAsset.Load(path));
            Assert.Equal(1, player.DecodedFrameCount);

            // 0.35 s at 10 images per second: frame 3 is due, frames 1 and 2 are late.
            player.Advance(TimeSpan.FromSeconds(0.35));
            Assert.Equal(3, player.CurrentFrameIndex);
            Assert.Equal(2, player.DecodedFrameCount);
            Assert.Equal(Expected(3), ReadTexture(player));

            // Past the end: only the last frame is decoded.
            player.Advance(TimeSpan.FromSeconds(5));
            Assert.True(player.IsEnded);
            Assert.Equal(7, player.CurrentFrameIndex);
            Assert.Equal(3, player.DecodedFrameCount);
        });
    }

    [GpuFact]
    public void Restart_ShowsFrameZeroAgain_AndClearsTheEnd()
    {
        var path = WriteVideo(3, 10, 1);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var player = new MdecVideoPlayer(GpuDeviceHost.Instance.GraphicsDevice, MdecVideoAsset.Load(path));
            player.Advance(TimeSpan.FromSeconds(10));
            Assert.True(player.IsEnded);

            player.Restart();

            Assert.False(player.IsEnded);
            Assert.Equal(0, player.CurrentFrameIndex);
            Assert.Equal(Expected(0), ReadTexture(player));
            player.Advance(TimeSpan.FromSeconds(0.1));
            Assert.Equal(1, player.CurrentFrameIndex);
        });
    }

    [GpuFact]
    public void Advance_AllocatesNothing_AfterTheFirst()
    {
        var path = WriteVideo(40, 30, 1);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var player = new MdecVideoPlayer(GpuDeviceHost.Instance.GraphicsDevice, MdecVideoAsset.Load(path));
            var step = TimeSpan.FromSeconds(1.0 / 30);

            player.Advance(step);
            player.Advance(step);

            var decodedBefore = player.DecodedFrameCount;
            var before = AllocationWindow.Start();
            for (var i = 0; i < 30; i++)
            {
                player.Advance(step);
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(player.DecodedFrameCount - decodedBefore >= 25, "the loop must decode new frames");
            Assert.Equal(0, allocated);
        });
    }

    [GpuFact]
    public void ACorruptFrame_EndsTheVideo_AndIsLoggedOnce()
    {
        var path = WriteVideo(4, 10, 1);
        var bytes = File.ReadAllBytes(path);
        var asset = MdecVideoAsset.Load(path);
        // Break the identifier of frame 2.
        bytes[(int)asset.GetFrameOffset(2) + 2] ^= 0xFF;
        File.WriteAllBytes(path, bytes);

        var errors = new List<string>();
        Logs.AddLogger(new CapturingLogger(errors, null));
        try
        {
            GpuDeviceHost.Instance.Invoke(() =>
            {
                using var player = new MdecVideoPlayer(GpuDeviceHost.Instance.GraphicsDevice, asset);
                player.Advance(TimeSpan.FromSeconds(0.1));
                Assert.False(player.IsEnded);
                player.Advance(TimeSpan.FromSeconds(0.1));
                Assert.True(player.IsEnded);
                Assert.Equal(1, player.CurrentFrameIndex);

                player.Advance(TimeSpan.FromSeconds(0.1));
                player.Restart();
                player.Advance(TimeSpan.FromSeconds(0.2));
                Assert.True(player.IsEnded);
            });
        }
        finally
        {
            Logs.Close();
        }

        Assert.Single(errors);
        Assert.Contains("frame 2", errors[0]);
        Assert.Contains(path, errors[0]);
    }

    [GpuFact]
    public void Dispose_ReleasesTheTextureAndTheFile_Notifies_AndIsIdempotent()
    {
        var path = WriteVideo(3, 10, 1);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var notified = 0;
            var player = new MdecVideoPlayer(GpuDeviceHost.Instance.GraphicsDevice, MdecVideoAsset.Load(path), _ => notified++);
            var image = player.CurrentImage;
            Assert.False(image.IsDisposed);

            player.Dispose();
            player.Dispose();

            Assert.True(image.IsDisposed);
            Assert.Equal(1, notified);

            player.Advance(TimeSpan.FromSeconds(1));
            player.Restart();

            // The file is closed: it can be deleted at once.
            File.Delete(path);
            Assert.False(File.Exists(path));
        });
    }

    [GpuFact]
    public void ADisposedPlayer_KeepsIsEndedUnchanged_AdvancesNothing_AndShowsNoImage()
    {
        var path = WriteVideo(4, 10, 1);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var player = new MdecVideoPlayer(GpuDeviceHost.Instance.GraphicsDevice, MdecVideoAsset.Load(path));
            player.Advance(TimeSpan.FromMilliseconds(150));
            var decoded = player.DecodedFrameCount;
            var frame = player.CurrentFrameIndex;

            player.Dispose();

            Assert.False(player.IsEnded);
            Assert.Null(player.CurrentImage);

            player.Advance(TimeSpan.FromSeconds(10));
            player.Restart();

            Assert.False(player.IsEnded);
            Assert.Equal(decoded, player.DecodedFrameCount);
            Assert.Equal(frame, player.CurrentFrameIndex);
        });
    }

    [GpuFact]
    public void ADisposedPlayer_OfAnEndedVideo_StaysEnded()
    {
        var path = WriteVideo(2, 10, 1);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var player = new MdecVideoPlayer(GpuDeviceHost.Instance.GraphicsDevice, MdecVideoAsset.Load(path));
            player.Advance(TimeSpan.FromSeconds(10));
            Assert.True(player.IsEnded);

            player.Dispose();

            Assert.True(player.IsEnded);
        });
    }

    [GpuFact]
    public void Constructor_WhenTheFileCannotBeOpened_DisposesTheTextureItCreated()
    {
        var path = WriteVideo(2, 10, 1);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var device = GpuDeviceHost.Instance.GraphicsDevice;
            var asset = MdecVideoAsset.Load(path);
            File.Delete(path);
            var before = LiveTextureCount(device);

            Assert.ThrowsAny<IOException>(() => new MdecVideoPlayer(device, asset));

            Assert.Equal(before, LiveTextureCount(device));
        });
    }

    // The device keeps weak references to its resources: the textures it still has that nobody disposed. No collection
    // runs here, since a collected texture would hide a leak.
    private static int LiveTextureCount(GraphicsDevice device)
    {
        var field = typeof(GraphicsDevice).GetField("_resources", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        var resources = (System.Collections.IEnumerable)field.GetValue(device)!;
        var count = 0;
        lock (resources)
        {
            foreach (var entry in resources)
            {
                if (((WeakReference)entry).Target is Texture2D { IsDisposed: false })
                {
                    count++;
                }
            }
        }

        return count;
    }

    private sealed class VideoLoader : IAssetLoader
    {
        private readonly string _path;
        public int Loads;

        public VideoLoader(string path) => _path = path;

        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            Loads++;
            return MdecVideoAsset.Load(_path);
        }

        public bool IsFileSupported(string fileName) => true;
    }

    private sealed class NullServiceProvider : IServiceProvider
    {
        public static readonly NullServiceProvider Instance = new();
        public object GetService(Type serviceType) => null;
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly List<string> _errors;
        private readonly List<string> _warnings;

        public CapturingLogger(List<string> errors, List<string> warnings)
        {
            _errors = errors;
            _warnings = warnings;
        }

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) => _warnings?.Add(msg);
        public void WriteError(string msg) => _errors?.Add(msg);
    }

    private static CasaUIAssetProvider NewProvider(AssetContentManager manager)
        => new(new ContentManager(NullServiceProvider.Instance), new FontManager(new ContentManager(NullServiceProvider.Instance), "Arial"), manager);

    private static AssetContentManager NewManager(GraphicsDevice device, VideoLoader loader)
    {
        var manager = new AssetContentManager();
        manager.Initialize(device);
        manager.RegisterAssetLoader(typeof(MdecVideoAsset), loader);
        return manager;
    }

    [GpuFact]
    public void TryCreateVideo_ByNameAndById_CreatesAnIndependentPlayerEachTime_LoadingTheContainerOnce()
    {
        var path = WriteVideo(5, 10, 1);
        AssetCatalog.AddInternal(new AssetInfo(VideoId) { Name = "intro", FileName = "intro.mdecvideo", AssetType = "mdecvideo" });
        var loader = new VideoLoader(path);

        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var provider = NewProvider(NewManager(GpuDeviceHost.Instance.GraphicsDevice, loader));

            Assert.True(provider.TryCreateVideo("intro", out var byName));
            Assert.True(provider.TryCreateVideo(VideoId.ToString(), out var byId));
            Assert.True(provider.TryCreateVideo("intro", out var again));

            Assert.NotSame(byName, byId);
            Assert.NotSame(byName, again);
            Assert.NotSame(byName.CurrentImage, byId.CurrentImage);
            byName.Advance(TimeSpan.FromSeconds(0.3));
            Assert.Equal(3, ((MdecVideoPlayer)byName).CurrentFrameIndex);
            Assert.Equal(0, ((MdecVideoPlayer)byId).CurrentFrameIndex);
            Assert.Equal(1, loader.Loads); // one container per asset, shared by the three players

            var nameImage = byName.CurrentImage;
            byName.Dispose();
            Assert.True(nameImage.IsDisposed);
            Assert.False(byId.CurrentImage.IsDisposed);
        });
    }

    [GpuFact]
    public void TryCreateVideo_UnknownName_ReturnsFalse_AndWarnsOnce()
    {
        var path = WriteVideo(2, 10, 1);
        AssetCatalog.AddInternal(new AssetInfo(SpriteLikeId) { Name = "sheet", FileName = "sheet.sprite", AssetType = "sprite" });
        var warnings = new List<string>();
        Logs.AddLogger(new CapturingLogger(null, warnings));
        try
        {
            GpuDeviceHost.Instance.Invoke(() =>
            {
                using var provider = NewProvider(NewManager(GpuDeviceHost.Instance.GraphicsDevice, new VideoLoader(path)));

                Assert.False(provider.TryCreateVideo("missing", out var video));
                Assert.Null(video);
                Assert.False(provider.TryCreateVideo("missing", out _));
                Assert.False(provider.TryCreateVideo("sheet", out _));
            });
        }
        finally
        {
            Logs.Close();
        }

        Assert.Equal(2, warnings.Count);
        Assert.Contains("missing", warnings[0]);
        Assert.Contains("sheet", warnings[1]);
    }

    [GpuFact]
    public void ProviderDispose_DisposesThePlayersThatWereNotDisposed()
    {
        var path = WriteVideo(2, 10, 1);
        AssetCatalog.AddInternal(new AssetInfo(VideoId) { Name = "intro", FileName = "intro.mdecvideo", AssetType = "mdecvideo" });

        GpuDeviceHost.Instance.Invoke(() =>
        {
            var provider = NewProvider(NewManager(GpuDeviceHost.Instance.GraphicsDevice, new VideoLoader(path)));
            Assert.True(provider.TryCreateVideo("intro", out var video));
            var image = video.CurrentImage;

            provider.Dispose();

            Assert.True(image.IsDisposed);
            Assert.Null(video.CurrentImage);
        });
    }
}
