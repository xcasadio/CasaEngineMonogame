using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Scene.World;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// ADR-0037, P11: <see cref="EnvironmentAssetLookup"/> acquires a resolution-cache miss's environment
/// asset only once per id, held for the whole game instead of a fresh <c>Acquire</c> per resolution.
/// The asset catalog is global (<see cref="AssetCatalog"/>), so these tests run in the
/// <see cref="ProjectEnvironmentCollection"/> and clean it up on both sides.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class EnvironmentAssetLookupHandleTests : IDisposable
{
    private static readonly Guid EnvironmentAssetId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private sealed class CountingLoader : IAssetLoader
    {
        public int Loads;

        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            Loads++;
            return new EnvironmentAsset();
        }

        public bool IsFileSupported(string fileName) => true;
    }

    public EnvironmentAssetLookupHandleTests()
    {
        AssetCatalog.ClearInternal();
    }

    public void Dispose()
    {
        AssetCatalog.ClearInternal();
    }

    private static RenderView CreateView(AssetContentManager assetContentManager)
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        SetBackingField(game, nameof(CasaEngineGame.AssetContentManager), assetContentManager);

        var world = new World();
        SetBackingField(world, nameof(World.Game), game);

        return new RenderView(world, null, null);
    }

    private static void SetBackingField(object instance, string propertyName, object value)
    {
        var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    /// <summary>Reads the private lease of <paramref name="id"/> in <paramref name="manager"/>'s default
    /// category, to check the number of holds without a public accessor for it (ADR-0036's own tests use
    /// the same reflection route on other private state).</summary>
    private static int GetHandleCount(AssetContentManager manager, Guid id)
    {
        var leasesField = typeof(AssetContentManager).GetField("_leases", BindingFlags.Instance | BindingFlags.NonPublic);
        var leases = (System.Collections.IDictionary)leasesField!.GetValue(manager)!;
        Assert.True(leases.Contains(id));
        var lease = leases[id]!;
        var handleCountField = lease.GetType().GetField("HandleCount", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return (int)handleCountField!.GetValue(lease)!;
    }

    [Fact]
    public void TryLoadEnvironmentAsset_ResolvedTwice_AcquiresOnce_AndHoldsExactlyOneHandle()
    {
        AssetCatalog.AddInternal(new AssetInfo(EnvironmentAssetId) { Name = "sky", FileName = "sky.environment" });

        var manager = new AssetContentManager();
        var loader = new CountingLoader();
        manager.RegisterAssetLoader(typeof(EnvironmentAsset), loader);

        var view = CreateView(manager);

        var first = EnvironmentAssetLookup.TryLoadEnvironmentAsset(view, EnvironmentAssetId);
        var second = EnvironmentAssetLookup.TryLoadEnvironmentAsset(view, EnvironmentAssetId);

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Equal(1, loader.Loads);
        Assert.Equal(1, GetHandleCount(manager, EnvironmentAssetId));
    }

    [Fact]
    public void TryLoadEnvironmentAsset_UnknownId_ReturnsNull_AndLoadsNothing()
    {
        var manager = new AssetContentManager();
        var loader = new CountingLoader();
        manager.RegisterAssetLoader(typeof(EnvironmentAsset), loader);

        var view = CreateView(manager);

        var result = EnvironmentAssetLookup.TryLoadEnvironmentAsset(view, Guid.NewGuid());

        Assert.Null(result);
        Assert.Equal(0, loader.Loads);
    }

    [Fact]
    public void TryLoadEnvironmentAsset_EmptyId_ReturnsNull_WithoutTouchingTheCatalog()
    {
        var manager = new AssetContentManager();
        var view = CreateView(manager);

        var result = EnvironmentAssetLookup.TryLoadEnvironmentAsset(view, Guid.Empty);

        Assert.Null(result);
    }
}
