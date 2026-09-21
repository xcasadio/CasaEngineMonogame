using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Component = CasaEngine.Framework.Scene.Entities.Components.SkinnedMeshComponent;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Animation;

/// <summary>
/// ADR-0037: SkinnedMeshComponent holds its SkinnedMesh through a counted handle and gives it back in
/// Detach.
/// </summary>
public class SkinnedMeshComponentAssetHandleTests
{
    private static readonly Guid SkinnedMeshId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    private sealed class SkinnedMeshLoader : IAssetLoader
    {
        public int Loads;

        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            Loads++;
            // No RiggedModelAssetId and no SkeletonAssetId: Initialize() stops before acquiring anything
            // else, so the test needs no GraphicsDevice and no further loaders.
            return new SkinnedMesh();
        }

        public bool IsFileSupported(string fileName) => true;
    }

    private static AssetContentManager NewManager(out SkinnedMeshLoader loader)
    {
        var infos = new Dictionary<Guid, AssetInfo>
        {
            [SkinnedMeshId] = new AssetInfo(SkinnedMeshId) { Name = "hero", FileName = "hero.model" },
        };

        var manager = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(null, Path.GetTempPath(), id => infos.GetValueOrDefault(id)),
        };

        loader = new SkinnedMeshLoader();
        manager.RegisterAssetLoader(typeof(SkinnedMesh), loader);
        return manager;
    }

    private static Component CreateAttachedComponent(AssetContentManager assetContentManager, Guid skinnedMeshAssetId, out Entity entity)
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var componentsField = typeof(Microsoft.Xna.Framework.Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!;
        componentsField.SetValue(game, new Microsoft.Xna.Framework.GameComponentCollection());
        SetBackingField(game, nameof(CasaEngineGame.AssetContentManager), assetContentManager);

        var world = new World();
        SetBackingField(world, nameof(World.Game), game);

        entity = new Entity();
        var component = new Component { SkinnedMeshAssetId = skinnedMeshAssetId };
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
    public void InitializeWithWorld_AcquiresTheSkinnedMesh_HeldUntilDetach()
    {
        var manager = NewManager(out var loader);
        var component = CreateAttachedComponent(manager, SkinnedMeshId, out _);

        Assert.NotNull(component.SkinnedMesh);
        Assert.Equal(1, loader.Loads);
        Assert.Equal(0, manager.CollectUnreferenced());

        component.Detach();

        Assert.Equal(1, manager.CollectUnreferenced());
    }

    [Fact]
    public void SecondInitializeWithWorld_ReleasesThePreviousSkinnedMeshHandle()
    {
        var manager = NewManager(out var loader);
        var component = CreateAttachedComponent(manager, SkinnedMeshId, out var entity);

        component.InitializeWithWorld(entity.World);

        // Nothing collected in between: the asset is still cached, so the second acquire reuses it
        // instead of leaking the first handle.
        Assert.Equal(1, loader.Loads);
        Assert.Equal(0, manager.CollectUnreferenced());

        component.Detach();
        Assert.Equal(1, manager.CollectUnreferenced());
    }
}
