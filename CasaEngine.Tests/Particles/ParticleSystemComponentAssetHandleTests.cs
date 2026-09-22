using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Component = CasaEngine.Framework.Scene.Entities.Components.ParticleSystemComponent;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Particles;

/// <summary>
/// ADR-0037: ParticleSystemComponent holds its particle effect asset through a counted handle
/// (loaded by <see cref="Component.ParticleEffectAssetId"/>) and gives it back in Detach or when the
/// asset is superseded.
/// </summary>
public class ParticleSystemComponentAssetHandleTests
{
    private static readonly Guid ParticleEffectId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private sealed class ParticleEffectAssetLoader : IAssetLoader
    {
        public int Loads;

        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            Loads++;
            return new ParticleEffectAsset();
        }

        public bool IsFileSupported(string fileName) => true;
    }

    private static AssetContentManager NewManager(out ParticleEffectAssetLoader loader)
    {
        var infos = new Dictionary<Guid, AssetInfo>
        {
            [ParticleEffectId] = new AssetInfo(ParticleEffectId) { Name = "effect", FileName = "effect.particles" },
        };

        var manager = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(null, Path.GetTempPath(), id => infos.GetValueOrDefault(id)),
        };

        loader = new ParticleEffectAssetLoader();
        manager.RegisterAssetLoader(typeof(ParticleEffectAsset), loader);
        return manager;
    }

    private static Component CreateAttachedComponent(AssetContentManager assetContentManager, Guid particleEffectAssetId, out Entity entity)
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var componentsField = typeof(Microsoft.Xna.Framework.Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!;
        componentsField.SetValue(game, new Microsoft.Xna.Framework.GameComponentCollection());
        SetBackingField(game, nameof(CasaEngineGame.AssetContentManager), assetContentManager);

        var world = new World();
        SetBackingField(world, nameof(World.Game), game);

        entity = new Entity();
        var component = new Component { ParticleEffectAssetId = particleEffectAssetId };
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
    public void InitializeWithWorld_AcquiresTheParticleEffectAsset_HeldUntilDetach()
    {
        var manager = NewManager(out var loader);
        var component = CreateAttachedComponent(manager, ParticleEffectId, out _);

        Assert.NotNull(component.ParticleEffectAsset);
        Assert.Equal(1, loader.Loads);
        Assert.Equal(0, manager.CollectUnreferenced());

        component.Detach();

        Assert.Equal(1, manager.CollectUnreferenced());
    }

    [Fact]
    public void SetParticleEffectAsset_ReleasesTheHandleAcquiredFromTheCatalog()
    {
        var manager = NewManager(out _);
        var component = CreateAttachedComponent(manager, ParticleEffectId, out _);

        component.SetParticleEffectAsset(new ParticleEffectAsset());

        // The catalog-acquired asset is no longer referenced by a handle this component holds.
        Assert.Equal(1, manager.CollectUnreferenced());
    }

    [Fact]
    public void ClearParticleEffectAsset_ReleasesTheHandleAcquiredFromTheCatalog()
    {
        var manager = NewManager(out _);
        var component = CreateAttachedComponent(manager, ParticleEffectId, out _);

        component.ClearParticleEffectAsset();

        Assert.Equal(1, manager.CollectUnreferenced());
        Assert.Equal(Guid.Empty, component.ParticleEffectAssetId);
    }
}
