using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using Xunit;

namespace CasaEngine.Tests.Assets;

/// <summary>
/// ADR-0036: counted handles over one shared instance per asset id, with release deferred to
/// <see cref="AssetContentManager.CollectUnreferenced"/>. The catalog is supplied through a local
/// <see cref="EngineRuntimeContext"/>, so these tests touch no global state.
/// </summary>
public class AssetContentManagerHandleTests
{
    private static readonly Guid LeafId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ParentId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private sealed class Leaf : IDisposable
    {
        public int DisposeCount;

        public void Dispose() => DisposeCount++;
    }

    private sealed class Parent : IDisposable
    {
        public AssetHandle<Leaf> Child;

        public void Dispose() => Child.Dispose();
    }

    private sealed class LeafLoader : IAssetLoader
    {
        public int Loads;

        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            Loads++;
            return new Leaf();
        }

        public bool IsFileSupported(string fileName) => true;
    }

    private sealed class ParentLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            return new Parent { Child = assetContentManager.Acquire<Leaf>(LeafId) };
        }

        public bool IsFileSupported(string fileName) => true;
    }

    private static AssetContentManager NewManager(out LeafLoader leafLoader)
    {
        var infos = new Dictionary<Guid, AssetInfo>
        {
            [LeafId] = new AssetInfo(LeafId) { Name = "shared-name", FileName = "leaf.asset" },
            [ParentId] = new AssetInfo(ParentId) { Name = "shared-name", FileName = "parent.asset" },
        };

        var manager = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(null, Path.GetTempPath(), id => infos.GetValueOrDefault(id)),
        };

        leafLoader = new LeafLoader();
        manager.RegisterAssetLoader(typeof(Leaf), leafLoader);
        manager.RegisterAssetLoader(typeof(Parent), new ParentLoader());
        return manager;
    }

    [Fact]
    public void Acquire_Twice_SharesOneInstance_LoadedOnce()
    {
        var manager = NewManager(out var loader);

        using var first = manager.Acquire<Leaf>(LeafId);
        using var second = manager.Acquire<Leaf>(LeafId);

        Assert.Same(first.Asset, second.Asset);
        Assert.Equal(1, loader.Loads);
    }

    [Fact]
    public void Released_ThenAcquiredAgainBeforeACollection_IsTheSameInstance_NotReloaded()
    {
        var manager = NewManager(out var loader);
        var first = manager.Acquire<Leaf>(LeafId);
        var asset = first.Asset;
        first.Dispose();

        using var again = manager.Acquire<Leaf>(LeafId);

        Assert.Same(asset, again.Asset);
        Assert.Equal(1, loader.Loads);
        Assert.Equal(0, asset.DisposeCount);
    }

    [Fact]
    public void Released_ThenCollected_IsDisposed_AndTheNextAcquireLoadsAgain()
    {
        var manager = NewManager(out var loader);
        var handle = manager.Acquire<Leaf>(LeafId);
        var asset = handle.Asset;
        handle.Dispose();

        Assert.Equal(1, manager.CollectUnreferenced());
        Assert.Equal(1, asset.DisposeCount);

        using var reloaded = manager.Acquire<Leaf>(LeafId);
        Assert.NotSame(asset, reloaded.Asset);
        Assert.Equal(2, loader.Loads);
    }

    [Fact]
    public void Held_IsNeverCollected()
    {
        var manager = NewManager(out _);
        using var handle = manager.Acquire<Leaf>(LeafId);

        Assert.Equal(0, manager.CollectUnreferenced());
        Assert.Equal(0, handle.Asset.DisposeCount);
    }

    [Fact]
    public void LoadedThroughLoad_IsPinned_EvenWhenEveryHandleIsReleased()
    {
        var manager = NewManager(out var loader);
        var loaded = manager.Load<Leaf>(LeafId);
        var handle = manager.Acquire<Leaf>(LeafId);
        Assert.Same(loaded, handle.Asset);
        handle.Dispose();

        Assert.Equal(0, manager.CollectUnreferenced());
        Assert.Equal(0, loaded.DisposeCount);
        Assert.Equal(1, loader.Loads);
    }

    [Fact]
    public void AcquiredFirst_ThenLoadedThroughLoad_BecomesPinned()
    {
        var manager = NewManager(out _);
        var handle = manager.Acquire<Leaf>(LeafId);
        var loaded = manager.Load<Leaf>(LeafId);
        Assert.Same(handle.Asset, loaded);
        handle.Dispose();

        Assert.Equal(0, manager.CollectUnreferenced());
        Assert.Equal(0, loaded.DisposeCount);
    }

    [Fact]
    public void ADependencyReleasedByAFreedAsset_IsFreedInTheSameCollection()
    {
        var manager = NewManager(out var loader);
        var parent = manager.Acquire<Parent>(ParentId);
        var leaf = parent.Asset.Child.Asset;
        parent.Dispose();

        Assert.Equal(2, manager.CollectUnreferenced());
        Assert.Equal(1, leaf.DisposeCount);
        Assert.Equal(1, loader.Loads);
    }

    [Fact]
    public void ADependencyStillHeldElsewhere_SurvivesItsParentsCollection()
    {
        var manager = NewManager(out _);
        using var leafHandle = manager.Acquire<Leaf>(LeafId);
        var parent = manager.Acquire<Parent>(ParentId);
        Assert.Same(leafHandle.Asset, parent.Asset.Child.Asset);
        parent.Dispose();

        Assert.Equal(1, manager.CollectUnreferenced());
        Assert.Equal(0, leafHandle.Asset.DisposeCount);
    }

    [Fact]
    public void Dispose_Twice_GivesTheHoldBackOnlyOnce()
    {
        var manager = NewManager(out _);
        using var kept = manager.Acquire<Leaf>(LeafId);
        var extra = manager.Acquire<Leaf>(LeafId);

        extra.Dispose();
        extra.Dispose();

        Assert.Equal(0, manager.CollectUnreferenced());
        Assert.Equal(0, kept.Asset.DisposeCount);
        Assert.True(extra.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => extra.Asset);
    }

    [Fact]
    public void Unload_KeepsAHeldAsset_AndFreesTheOthers()
    {
        var manager = NewManager(out _);
        using var held = manager.Acquire<Leaf>(LeafId);
        var pending = manager.Acquire<Parent>(ParentId);
        var pendingChild = pending.Asset.Child;
        pending.Dispose();

        manager.Unload(AssetContentManager.DefaultCategory);

        Assert.Equal(0, held.Asset.DisposeCount);
        Assert.True(pendingChild.IsDisposed);
        Assert.Same(held.Asset, manager.GetAsset<Leaf>(LeafId));
    }

    [Fact]
    public void AnEntityCannotBeAcquired()
    {
        var manager = NewManager(out _);

        Assert.Throws<InvalidOperationException>(
            () => manager.Acquire<CasaEngine.Framework.Scene.Entities.Entity>(LeafId));
    }
}
