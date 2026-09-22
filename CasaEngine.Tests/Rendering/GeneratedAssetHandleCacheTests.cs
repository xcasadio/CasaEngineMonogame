using System.Collections;
using System.Reflection;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Rendering.Environment;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// ADR-0037, P8/P11: the bookkeeping the three environment cubemap generators share through
/// <see cref="GeneratedAssetHandleCache{T}"/> — first build through <c>Register</c>, later stale rebuild
/// through <c>Replace</c> under the same hold. Exercised here with a plain generated object instead of a
/// real cubemap, since building one needs a <see cref="Microsoft.Xna.Framework.Graphics.GraphicsDevice"/>
/// unavailable headless; the bookkeeping itself does not depend on the asset's type.
/// </summary>
public class GeneratedAssetHandleCacheTests
{
    private static readonly Guid GeneratedId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    private sealed class GeneratedCubemapStub
    {
        public bool IsDisposed;
        public int SequenceNumber;
    }

    private static AssetContentManager NewManager()
    {
        return new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(null, Path.GetTempPath(), _ => null),
        };
    }

    // The manager exposes no hold count; read its private lease to check the cache keeps exactly one.
    private static int HoldCount(AssetContentManager manager, Guid id)
    {
        var leases = (IDictionary)typeof(AssetContentManager).GetField("_leases", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
        var lease = leases[id]!;
        return (int)lease.GetType().GetField("HandleCount")!.GetValue(lease)!;
    }

    [Fact]
    public void Acquire_FirstCall_BuildsOnce_AndRegistersTheHold()
    {
        var manager = NewManager();
        int buildCount = 0;

        var asset = GeneratedAssetHandleCache<GeneratedCubemapStub>.Acquire(
            manager,
            GeneratedId,
            () => { buildCount++; return new GeneratedCubemapStub { SequenceNumber = buildCount }; },
            stub => stub.IsDisposed);

        Assert.Equal(1, buildCount);
        Assert.Equal(1, asset.SequenceNumber);

        // Held: a collection right after must not free it.
        Assert.Equal(0, manager.CollectUnreferenced());
    }

    [Fact]
    public void Acquire_CalledAgain_NotStale_ReturnsTheCachedInstance_WithoutRebuilding()
    {
        var manager = NewManager();
        int buildCount = 0;
        Func<GeneratedCubemapStub> build = () => { buildCount++; return new GeneratedCubemapStub { SequenceNumber = buildCount }; };

        var first = GeneratedAssetHandleCache<GeneratedCubemapStub>.Acquire(manager, GeneratedId, build, stub => stub.IsDisposed);
        var second = GeneratedAssetHandleCache<GeneratedCubemapStub>.Acquire(manager, GeneratedId, build, stub => stub.IsDisposed);

        Assert.Same(first, second);
        Assert.Equal(1, buildCount);
    }

    [Fact]
    public void Acquire_CachedInstanceIsStale_RebuildsUnderTheSameId_DoesNotThrow_AndCachesTheNewInstanceUnderOneHold()
    {
        var manager = NewManager();
        int buildCount = 0;
        Func<GeneratedCubemapStub> build = () => { buildCount++; return new GeneratedCubemapStub { SequenceNumber = buildCount }; };

        var first = GeneratedAssetHandleCache<GeneratedCubemapStub>.Acquire(manager, GeneratedId, build, stub => stub.IsDisposed);
        Assert.Equal(1, HoldCount(manager, GeneratedId));
        first.IsDisposed = true;

        var rebuilt = GeneratedAssetHandleCache<GeneratedCubemapStub>.Acquire(manager, GeneratedId, build, stub => stub.IsDisposed);

        Assert.Equal(2, buildCount);
        Assert.NotSame(first, rebuilt);
        Assert.Equal(2, rebuilt.SequenceNumber);
        Assert.Equal(1, HoldCount(manager, GeneratedId));

        // The rebuilt instance is what the cache now holds: a third call neither rebuilds nor replaces it.
        var third = GeneratedAssetHandleCache<GeneratedCubemapStub>.Acquire(manager, GeneratedId, build, stub => stub.IsDisposed);

        Assert.Same(rebuilt, third);
        Assert.Equal(2, buildCount);
        Assert.Equal(1, HoldCount(manager, GeneratedId));
        Assert.True(GeneratedAssetHandleCache<GeneratedCubemapStub>.TryGetCached(manager, GeneratedId, stub => stub.IsDisposed, out var cached));
        Assert.Same(rebuilt, cached);

        // The generator's hold on the id was kept across the rebuild (P8): still not collected.
        Assert.Equal(0, manager.CollectUnreferenced());
    }

    [Fact]
    public void TryGetCached_BeforeAnyAcquire_ReturnsFalse()
    {
        var manager = NewManager();

        bool found = GeneratedAssetHandleCache<GeneratedCubemapStub>.TryGetCached(manager, GeneratedId, stub => stub.IsDisposed, out var asset);

        Assert.False(found);
        Assert.Null(asset);
    }

    [Fact]
    public void TryGetCached_AfterAcquire_NotStale_ReturnsTheCachedInstance()
    {
        var manager = NewManager();
        var built = GeneratedAssetHandleCache<GeneratedCubemapStub>.Acquire(manager, GeneratedId, () => new GeneratedCubemapStub(), stub => stub.IsDisposed);

        bool found = GeneratedAssetHandleCache<GeneratedCubemapStub>.TryGetCached(manager, GeneratedId, stub => stub.IsDisposed, out var asset);

        Assert.True(found);
        Assert.Same(built, asset);
    }
}
