using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
using Xunit;

namespace CasaEngine.Tests.Audio;

/// <summary>
/// ADR-0037 (T3.3, P2): <see cref="AssetContentManagerAudioClipProvider"/> acquires each distinct clip
/// once and holds it through a handle for its own lifetime (as clips were pinned for the whole game
/// before handles existed), and <see cref="AssetContentManagerAudioClipProvider.Dispose"/> gives every
/// held clip back.
/// </summary>
public class AssetContentManagerAudioClipProviderHandleTests
{
    private static readonly Guid ClipAssetId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");

    private sealed class FakeAudioClip : IAudioClip
    {
        public int SampleRate => 44100;
        public int ChannelCount => 1;
        public TimeSpan Duration => TimeSpan.FromSeconds(1);
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    private sealed class ClipLoader : IAssetLoader
    {
        public int Loads;

        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            Loads++;
            return new FakeAudioClip();
        }

        public bool IsFileSupported(string fileName) => true;
    }

    private static AssetContentManager NewManager(out ClipLoader loader)
    {
        var infos = new Dictionary<Guid, AssetInfo>
        {
            [ClipAssetId] = new AssetInfo(ClipAssetId) { Name = "clip", FileName = "clip.wav" },
        };

        var manager = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(null, Path.GetTempPath(), id => infos.GetValueOrDefault(id)),
        };

        loader = new ClipLoader();
        manager.RegisterAssetLoader(typeof(IAudioClip), loader);
        return manager;
    }

    [Fact]
    public void GetClip_CalledTwiceForTheSameId_LoadsOnceAndReturnsTheSameInstance()
    {
        var manager = NewManager(out var loader);
        var provider = new AssetContentManagerAudioClipProvider(manager);

        var first = provider.GetClip(ClipAssetId);
        var second = provider.GetClip(ClipAssetId);

        Assert.Same(first, second);
        Assert.Equal(1, loader.Loads);
        // Held by the provider: not freeable while it is alive.
        Assert.Equal(0, manager.CollectUnreferenced());
    }

    [Fact]
    public void GetClip_WithEmptyId_ReturnsNullAndLoadsNothing()
    {
        var manager = NewManager(out var loader);
        var provider = new AssetContentManagerAudioClipProvider(manager);

        var clip = provider.GetClip(Guid.Empty);

        Assert.Null(clip);
        Assert.Equal(0, loader.Loads);
    }

    [Fact]
    public void Dispose_ReleasesEveryHeldClipHandle()
    {
        var manager = NewManager(out _);
        var provider = new AssetContentManagerAudioClipProvider(manager);
        var clip = provider.GetClip(ClipAssetId);

        provider.Dispose();

        Assert.Equal(1, manager.CollectUnreferenced());
        Assert.True(((FakeAudioClip)clip).IsDisposed);
    }
}
