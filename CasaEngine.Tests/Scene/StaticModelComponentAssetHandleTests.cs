using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Component = CasaEngine.Framework.Scene.Entities.Components.StaticModelComponent;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Scene;

/// <summary>
/// ADR-0037: StaticModelComponent holds its StaticModel through a counted handle and gives it back in
/// Detach.
/// </summary>
public class StaticModelComponentAssetHandleTests
{
    private static readonly Guid StaticModelId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private sealed class StaticModelLoader : IAssetLoader
    {
        public int Loads;

        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            Loads++;
            // No RootNode and no meshes: Initialize/NormalizeMaterialOverrides stay pure data and the
            // generated-hierarchy build is skipped, so the test needs no GraphicsDevice.
            return new StaticModel();
        }

        public bool IsFileSupported(string fileName) => true;
    }

    private static AssetContentManager NewManager(out StaticModelLoader loader)
    {
        var infos = new Dictionary<Guid, AssetInfo>
        {
            [StaticModelId] = new AssetInfo(StaticModelId) { Name = "prop", FileName = "prop.static_model" },
        };

        var manager = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(null, Path.GetTempPath(), id => infos.GetValueOrDefault(id)),
        };

        loader = new StaticModelLoader();
        manager.RegisterAssetLoader(typeof(StaticModel), loader);
        return manager;
    }

    private static Component CreateAttachedComponent(AssetContentManager assetContentManager, Guid staticModelAssetId, out Entity entity)
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        SetBackingField(game, nameof(CasaEngineGame.AssetContentManager), assetContentManager);

        var world = new World();
        SetBackingField(world, nameof(World.Game), game);

        entity = new Entity();
        var component = new Component { StaticModelAssetId = staticModelAssetId };
        entity.RootComponent = component;
        entity.InitializeWithWorld(world);

        return component;
    }

    private static void SetBackingField(object instance, string propertyName, object value)
    {
        var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    [Fact]
    public void InitializeWithWorld_AcquiresTheStaticModel_HeldUntilDetach()
    {
        var manager = NewManager(out var loader);
        var component = CreateAttachedComponent(manager, StaticModelId, out _);

        Assert.NotNull(component.StaticModel);
        Assert.Equal(1, loader.Loads);
        Assert.Equal(0, manager.CollectUnreferenced());

        component.Detach();

        Assert.Equal(1, manager.CollectUnreferenced());
    }

    [Fact]
    public void SecondInitializeWithWorld_ReleasesThePreviousStaticModelHandle()
    {
        var manager = NewManager(out var loader);
        var component = CreateAttachedComponent(manager, StaticModelId, out var entity);

        // A component that already holds a model for this asset id does not reacquire it.
        component.InitializeWithWorld(entity.World);

        Assert.Equal(1, loader.Loads);
        Assert.Equal(0, manager.CollectUnreferenced());

        component.Detach();
        Assert.Equal(1, manager.CollectUnreferenced());
    }

    [Fact]
    public void ClearingTheAssetId_ReleasesTheHandle()
    {
        var manager = NewManager(out _);
        var component = CreateAttachedComponent(manager, StaticModelId, out var entity);

        component.StaticModelAssetId = Guid.Empty;
        component.InitializeWithWorld(entity.World);

        Assert.Null(component.StaticModel);
        Assert.Equal(1, manager.CollectUnreferenced());
    }
}
