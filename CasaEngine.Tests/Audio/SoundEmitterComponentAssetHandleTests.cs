using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Component = CasaEngine.Framework.Scene.Entities.Components.SoundEmitterComponent;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Audio;

/// <summary>
/// ADR-0037: SoundEmitterComponent holds its sound asset through a counted handle and gives it back
/// in Detach.
/// </summary>
public class SoundEmitterComponentAssetHandleTests
{
    private static readonly Guid SoundAssetId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private sealed class SoundAssetLoader : IAssetLoader
    {
        public int Loads;

        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            Loads++;
            return new SoundAsset();
        }

        public bool IsFileSupported(string fileName) => true;
    }

    private static AssetContentManager NewManager(out SoundAssetLoader loader)
    {
        var infos = new Dictionary<Guid, AssetInfo>
        {
            [SoundAssetId] = new AssetInfo(SoundAssetId) { Name = "engine_loop", FileName = "engine_loop.sound" },
        };

        var manager = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(null, Path.GetTempPath(), id => infos.GetValueOrDefault(id)),
        };

        loader = new SoundAssetLoader();
        manager.RegisterAssetLoader(typeof(SoundAsset), loader);
        return manager;
    }

    private static Component CreateAttachedComponent(AssetContentManager assetContentManager, Guid soundAssetId, out Entity entity)
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        SetBackingField(game, nameof(CasaEngineGame.AssetContentManager), assetContentManager);

        var world = new World();
        SetBackingField(world, nameof(World.Game), game);

        entity = new Entity();
        var component = new Component { SoundAssetId = soundAssetId };
        entity.AddComponent(component);
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
    public void InitializeWithWorld_AcquiresTheSoundAsset_HeldUntilDetach()
    {
        var manager = NewManager(out var loader);
        var component = CreateAttachedComponent(manager, SoundAssetId, out _);

        Assert.NotNull(component.SoundAsset);
        Assert.Equal(1, loader.Loads);
        Assert.Equal(0, manager.CollectUnreferenced());

        component.Detach();

        Assert.Equal(1, manager.CollectUnreferenced());
    }

    [Fact]
    public void SecondInitializeWithWorld_ReleasesThePreviousSoundAssetHandle()
    {
        var manager = NewManager(out var loader);
        var component = CreateAttachedComponent(manager, SoundAssetId, out var entity);

        component.InitializeWithWorld(entity.World);

        // Nothing collected in between: the asset is still cached, so the second acquire reuses it.
        Assert.Equal(1, loader.Loads);
        Assert.Equal(0, manager.CollectUnreferenced());

        component.Detach();
        Assert.Equal(1, manager.CollectUnreferenced());
    }
}
