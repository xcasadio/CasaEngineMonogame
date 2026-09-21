using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Application;

/// <summary>
/// ADR-0037 (T3.3): World reads its entity templates and its world asset through
/// <see cref="AssetContentManager.LoadCopy{T}"/> (uncached, uncounted), and holds
/// <see cref="PlayerStartupSettings"/> and <see cref="GameplayModeAsset"/> through counted handles
/// that it gives back - the former in <see cref="World.Clear"/>, the latter right after
/// <see cref="GameplayModeAsset.CreateMode"/> is called.
/// </summary>
public class WorldAssetHandleTests
{
    private static readonly Guid EntityAssetId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid PlayerStartupSettingsId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid GameplayModeAssetId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    private sealed class CountingLoader<T> : IAssetLoader where T : class, new()
    {
        public int Loads;

        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            Loads++;
            return new T();
        }

        public bool IsFileSupported(string fileName) => true;
    }

    private static AssetContentManager NewManager(out CountingLoader<Entity> entityLoader)
    {
        var infos = new Dictionary<Guid, AssetInfo>
        {
            [EntityAssetId] = new AssetInfo(EntityAssetId) { Name = "template", FileName = "template.entity" },
            [PlayerStartupSettingsId] = new AssetInfo(PlayerStartupSettingsId) { Name = "startup", FileName = "startup.player_startup_settings" },
            [GameplayModeAssetId] = new AssetInfo(GameplayModeAssetId) { Name = "mode", FileName = "mode.gameplay_mode" },
        };

        var manager = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(null, Path.GetTempPath(), id => infos.GetValueOrDefault(id)),
        };

        entityLoader = new CountingLoader<Entity>();
        manager.RegisterAssetLoader(typeof(Entity), entityLoader);
        manager.RegisterAssetLoader(typeof(PlayerStartupSettings), new CountingLoader<PlayerStartupSettings>());
        manager.RegisterAssetLoader(typeof(GameplayModeAsset), new CountingLoader<GameplayModeAsset>());
        return manager;
    }

    private static World NewWorldWithGame(AssetContentManager assetContentManager)
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        SetBackingField(game, nameof(CasaEngineGame.AssetContentManager), assetContentManager);

        var world = new World();
        SetBackingField(world, nameof(World.Game), game);
        return world;
    }

    private static void SetBackingField(object instance, string propertyName, object value)
    {
        var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static void InvokePrivate(object instance, string methodName)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(instance, null);
    }

    // ---- SpawnEntity: LoadCopy, not the shared/cached Load<T> ----

    [Fact]
    public void SpawnEntity_ByGuid_ReadsAFreshUncachedCopyEachTime()
    {
        var manager = NewManager(out var entityLoader);
        var world = NewWorldWithGame(manager);

        var first = world.SpawnEntity<Entity>(EntityAssetId);
        var second = world.SpawnEntity<Entity>(EntityAssetId);

        // LoadCopy never touches the cache or the leases: each spawn reads the file again, and the
        // manager holds nothing for this id afterwards.
        Assert.Equal(2, entityLoader.Loads);
        Assert.NotSame(first, second);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(0, manager.CollectUnreferenced());
    }

    // ---- PlayerStartupSettings: acquired for the world's lifetime, released by Clear ----

    [Fact]
    public void LoadPlayerStartupSettings_HoldsTheHandleUntilReleased()
    {
        var manager = NewManager(out _);
        var world = NewWorldWithGame(manager);
        world.PlayerStartupSettingsAssetId = PlayerStartupSettingsId;

        world.LoadPlayerStartupSettings();

        Assert.NotNull(world.PlayerStartupSettings);
        Assert.Equal(0, manager.CollectUnreferenced());

        world.ReleasePlayerStartupSettings();

        Assert.Equal(1, manager.CollectUnreferenced());
    }

    [Fact]
    public void LoadPlayerStartupSettings_CalledAgain_ReleasesThePreviousHandleFirst()
    {
        var manager = NewManager(out _);
        var world = NewWorldWithGame(manager);
        world.PlayerStartupSettingsAssetId = PlayerStartupSettingsId;
        world.LoadPlayerStartupSettings();

        // Reloading the world's content must not leak the earlier handle.
        world.LoadPlayerStartupSettings();
        world.ReleasePlayerStartupSettings();

        Assert.Equal(1, manager.CollectUnreferenced());
    }

    // ---- GameplayModeAsset: acquired only for the CreateMode call ----

    [Fact]
    public void StartGameplayModeAsset_AcquiresTheAsset_OnlyForCreateMode()
    {
        var manager = NewManager(out _);
        var world = NewWorldWithGame(manager);
        world.GameplayModeAssetId = GameplayModeAssetId;

        InvokePrivate(world, "StartGameplayModeAsset");

        // Held by nobody once the call returns: freeable at once.
        Assert.Equal(1, manager.CollectUnreferenced());
    }
}
