using System;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Application;

/// <summary>
/// Pins the failure mode of a world load requested by name. Gameplay code can ask for a world with
/// <see cref="GameManager.SetWorldToLoad(string)"/>, which defers to the next
/// <see cref="GameManager.UpdateWorld"/>; a path missing from the asset catalog used to surface one
/// frame later as a NullReferenceException that named nothing.
/// </summary>
public class GameManagerWorldLoadTests
{
    [Fact]
    public void UpdateWorld_WithWorldPathMissingFromTheCatalog_ThrowsAndNamesThePath()
    {
        var gameManager = new GameManager(null);
        const string missingWorldPath = @"Maps\No Such Zone\No Such Map-4242\No Such Map-4242.world";
        gameManager.SetWorldToLoad(missingWorldPath);

        var exception = Assert.Throws<InvalidOperationException>(() => gameManager.UpdateWorld(new GameTime()));

        Assert.Contains(missingWorldPath, exception.Message);
    }

    [Fact]
    public void UpdateWorld_WithNoPendingWorldLoad_DoesNotThrow()
    {
        var gameManager = new GameManager(null);

        gameManager.UpdateWorld(new GameTime());

        Assert.Null(gameManager.CurrentWorld);
    }

    private static readonly Guid PendingId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid HeldId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private sealed class Disposable : IDisposable
    {
        public bool IsDisposed;

        public void Dispose() => IsDisposed = true;
    }

    private sealed class DisposableLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager) => new Disposable();

        public bool IsFileSupported(string fileName) => true;
    }

    private static AssetContentManager NewAssets()
    {
        var infos = new Dictionary<Guid, AssetInfo>
        {
            [PendingId] = new AssetInfo(PendingId) { Name = "pending", FileName = "pending.asset" },
            [HeldId] = new AssetInfo(HeldId) { Name = "held", FileName = "held.asset" },
        };

        var assets = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(null, Path.GetTempPath(), id => infos.GetValueOrDefault(id)),
        };
        assets.RegisterAssetLoader(typeof(Disposable), new DisposableLoader());
        return assets;
    }

    /// <summary>ADR-0036: the collection runs at the very start of a world change, before the new world is
    /// even looked up - the missing path below throws only after it.</summary>
    [Fact]
    public void UpdateWorld_WhenAWorldChangeStarts_FreesWhatNobodyHolds_AndKeepsWhatIsHeld()
    {
        var assets = NewAssets();
        var released = assets.Acquire<Disposable>(PendingId);
        var pendingAsset = released.Asset;
        released.Dispose();
        using var held = assets.Acquire<Disposable>(HeldId);

        var gameManager = new GameManager(null, assets);
        gameManager.SetWorldToLoad(@"Maps\No Such Zone\No Such Map-4242\No Such Map-4242.world");
        Assert.Throws<InvalidOperationException>(() => gameManager.UpdateWorld(new GameTime()));

        Assert.True(pendingAsset.IsDisposed);
        Assert.False(held.Asset.IsDisposed);
    }

    [Fact]
    public void UpdateWorld_WithNoPendingWorldLoad_FreesNothing()
    {
        var assets = NewAssets();
        var released = assets.Acquire<Disposable>(PendingId);
        var pendingAsset = released.Asset;
        released.Dispose();

        var gameManager = new GameManager(null, assets);
        gameManager.UpdateWorld(new GameTime());

        Assert.False(pendingAsset.IsDisposed);
    }
}
