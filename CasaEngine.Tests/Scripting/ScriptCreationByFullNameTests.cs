using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Core.Logging;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Scripting;

/// <summary>An entity script that fails while it initializes: not a resolution problem, an ordinary fault.</summary>
public class FaultyInitializationEntityScriptStub : GameplayProxy
{
    protected override void InitializePrivate() => throw new InvalidOperationException("faulty script initialization");

    public override IGameplayProxy Clone() => new FaultyInitializationEntityScriptStub();
    public override void InitializeWithWorld(World world) { }
    public override void Update(float elapsedTime) { }
    public override void Draw() { }
    public override void OnHit(Collision collision) { }
    public override void OnHitEnded(Collision collision) { }
    public override void OnBeginPlay(World world) { }
    public override void OnEndPlay(World world) { }
}

/// <summary>A world script that fails when the world initializes it: an ordinary fault that leaves the world load.</summary>
public class FaultyInitializationWorldScriptStub : GameplayProxy
{
    public override void InitializeWithWorld(World world) => throw new InvalidOperationException("faulty world script");

    public override IGameplayProxy Clone() => new FaultyInitializationWorldScriptStub();
    public override void Update(float elapsedTime) { }
    public override void Draw() { }
    public override void OnHit(Collision collision) { }
    public override void OnHitEnded(Collision collision) { }
    public override void OnBeginPlay(World world) { }
    public override void OnEndPlay(World world) { }
}

/// <summary>A world script that makes an entity with a faulty script name be born, initialized at once, from its own initialization.</summary>
public class SpawningWorldScriptStub : GameplayProxy
{
    public const string SpawnedScriptName = "Nope.SpawnedScript";

    public override void InitializeWithWorld(World world)
    {
        // Initialized at once, as a spawn factory does, before the world adds it.
        var spawned = new Entity { Name = "spawned", GameplayProxyClassName = SpawnedScriptName };
        spawned.Initialize();
        world.AddEntity(spawned);
    }

    public override IGameplayProxy Clone() => new SpawningWorldScriptStub();
    public override void Update(float elapsedTime) { }
    public override void Draw() { }
    public override void OnHit(Collision collision) { }
    public override void OnHitEnded(Collision collision) { }
    public override void OnBeginPlay(World world) { }
    public override void OnEndPlay(World world) { }
}

/// <summary>A player controller defined by the gameplay assembly, named by its full name in the startup settings.</summary>
public class CustomPlayerControllerStub : PlayerController
{
}

/// <summary>
/// Plan T3.1 (ADR-0075): the entity and world gameplay proxies, the player controller, the gameplay mode and the
/// design-time view model are created by full name from the registered gameplay assemblies; a name that does not
/// resolve throws <see cref="ScriptTypeResolutionException"/> naming the file that holds it; the editor preview
/// keeps an entity and a world whose script does not resolve, without a script, with a warning.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class ScriptCreationByFullNameTests
{
    private const string FaultyScriptName = "Nope.MissingScript";
    private static readonly Guid PrefabAssetId = Guid.Parse("a1a1a1a1-0000-0000-0000-000000000001");
    private static readonly Guid StartupSettingsAssetId = Guid.Parse("a1a1a1a1-0000-0000-0000-000000000002");

    private sealed class FactoryLoader : IAssetLoader
    {
        private readonly Func<object> _create;

        public FactoryLoader(Func<object> create) => _create = create;

        public object LoadAsset(string fileName, AssetContentManager assetContentManager) => _create();

        public bool IsFileSupported(string fileName) => true;
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) => Warnings.Add(msg);
        public void WriteError(string msg) => Errors.Add(msg);
    }

    private static AssetContentManager NewAssets(string prefabScriptName = FaultyScriptName, string startupControllerName = "")
    {
        var infos = new Dictionary<Guid, AssetInfo>
        {
            [PrefabAssetId] = new AssetInfo(PrefabAssetId) { Name = "faulty prefab", FileName = "Prefabs/faulty.entity" },
            [StartupSettingsAssetId] = new AssetInfo(StartupSettingsAssetId) { Name = "startup", FileName = "Game/startup.gameMode" },
        };

        var manager = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(null, Path.GetTempPath(), id => infos.GetValueOrDefault(id)),
        };

        manager.RegisterAssetLoader(typeof(Entity), new FactoryLoader(() => new Entity { GameplayProxyClassName = prefabScriptName }));
        manager.RegisterAssetLoader(typeof(PlayerStartupSettings),
            new FactoryLoader(() => new PlayerStartupSettings { PlayerControllerClass = startupControllerName }));
        return manager;
    }

    private static World NewWorld(GameplayExecutionPolicy policy, AssetContentManager assets, string fileName = "Worlds/level.world")
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        SetBackingField(game, nameof(CasaEngineGame.AssetContentManager), assets);
        game.ExecutionPolicy = policy;

        // The fields World.Update and the entity initialization reach for, as in TransformComponentTests.
        typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(game, new GameComponentCollection());
        var gameManager = (GameManager)RuntimeHelpers.GetUninitializedObject(typeof(GameManager));
        SetBackingField(gameManager, nameof(GameManager.ViewManager), new CasaEngine.Framework.Rendering.ViewManager());
        SetBackingField(game, nameof(CasaEngineGame.GameManager), gameManager);

        var world = new World { FileName = fileName };
        SetBackingField(world, nameof(World.Game), game);
        return world;
    }

    private static void SetBackingField(object instance, string propertyName, object value)
    {
        var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static JObject InlineReference(Entity entity)
    {
        var entityNode = new JObject();
        EditorEntityJsonSerializer.SaveEntity(entity, entityNode);
        return new JObject { ["asset_id"] = Guid.Empty, ["entity"] = entityNode };
    }

    private static JObject PrefabReference(Guid assetId)
    {
        var transformNode = new JObject();
        new CasaEngine.Core.Math.LocalTransform().Save(transformNode);
        return new JObject { ["asset_id"] = assetId, ["name"] = "prefab instance", ["initial_local_transform"] = transformNode };
    }

    private static void LoadWorldDocument(World world, string worldScriptName, params JObject[] references)
    {
        world.Load(new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "level",
            ["entity_references"] = new JArray(references),
            ["script_class_name"] = worldScriptName,
            ["space_policy"] = string.Empty,
        });
    }

    /// <summary>Runs the world's content loading (the private overload that skips the physics context).</summary>
    private static void LoadContent(World world, bool withReferences = true)
    {
        var method = typeof(World).GetMethod("LoadContent", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(bool) });
        Assert.NotNull(method);
        try
        {
            method!.Invoke(world, new object[] { withReferences });
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
        }
    }

    private static Entity InlineEntity(string name, string scriptName) => new() { Name = name, GameplayProxyClassName = scriptName };

    // ---- (i) the entity ----

    [Fact]
    public void Entity_FromAnAssetCopy_WithAWrongScript_NamesTheAssetFileInTheMessage()
    {
        var assets = NewAssets();

        var entity = assets.LoadCopy<Entity>(PrefabAssetId).Clone();
        var exception = Assert.Throws<ScriptTypeResolutionException>(() => entity.Initialize());

        Assert.Contains("Prefabs/faulty.entity", exception.Message);
        Assert.Contains(FaultyScriptName, exception.Message);
        Assert.Contains("script_class_name", exception.Message);
    }

    [Fact]
    public void Entity_WithAFullName_GetsItsScript_AndAClonedProxyIsKept()
    {
        var entity = InlineEntity("hero", typeof(StatefulEntityScriptStub).FullName!);
        entity.Initialize();
        var proxy = Assert.IsType<StatefulEntityScriptStub>(entity.GameplayProxy);
        proxy.HitPoints = 42;

        var clone = entity.Clone();
        clone.Initialize();

        // Recreated only when the class name no longer matches: a clone carrying its state keeps it.
        var clonedProxy = Assert.IsType<StatefulEntityScriptStub>(clone.GameplayProxy);
        Assert.Equal(42, clonedProxy.HitPoints);
    }

    [Fact]
    public void Entity_WithAShortName_IsRefusedWithTheFullNameAsHint()
    {
        var entity = InlineEntity("hero", nameof(StatefulEntityScriptStub));

        var exception = Assert.Throws<ScriptTypeResolutionException>(() => entity.Initialize());

        Assert.Contains(typeof(StatefulEntityScriptStub).FullName!, exception.Hint);
    }

    // ---- Load never needs the gameplay assembly (D4) ----

    [Fact]
    public void Load_WithoutAnyGameplayAssembly_DoesNotThrow_TheCreationDoes()
    {
        ScriptTestAssemblyRegistration.WithTestAssemblyUnregistered(() =>
        {
            var entity = InlineEntity("hero", FaultyScriptName);
            var entityNode = new JObject();
            EditorEntityJsonSerializer.SaveEntity(entity, entityNode);
            var loaded = new Entity();
            loaded.Load(entityNode);

            var world = new World();
            LoadWorldDocument(world, FaultyScriptName, InlineReference(entity));

            Assert.Equal(FaultyScriptName, loaded.GameplayProxyClassName);
            Assert.Equal(FaultyScriptName, world.GameplayProxyClassName);
            Assert.Throws<ScriptTypeResolutionException>(() => loaded.Initialize());
        });
    }

    // ---- (ii) a world that loads for real ----

    [Fact]
    public void LoadingAWorld_WithAFaultyInlineEntity_Throws_NamingTheWorldFile()
    {
        var world = NewWorld(GameplayExecutionPolicies.Runtime, NewAssets());
        LoadWorldDocument(world, string.Empty, InlineReference(InlineEntity("inline", FaultyScriptName)));

        var exception = Assert.Throws<ScriptTypeResolutionException>(() => LoadContent(world));

        Assert.Contains("Worlds/level.world", exception.Message);
        Assert.Contains(FaultyScriptName, exception.Message);
    }

    [Fact]
    public void LoadingAWorld_ThatReferencesAFaultyPrefab_Throws_NamingThePrefabFile()
    {
        var world = NewWorld(GameplayExecutionPolicies.Runtime, NewAssets());
        LoadWorldDocument(world, string.Empty, PrefabReference(PrefabAssetId));

        var exception = Assert.Throws<ScriptTypeResolutionException>(() => LoadContent(world));

        Assert.Contains("Prefabs/faulty.entity", exception.Message);
        Assert.DoesNotContain("Worlds/level.world", exception.Message);
    }

    [Fact]
    public void LoadingAWorld_WithAnotherEntityFault_LogsOmitsTheEntityAndFinishesTheLoad()
    {
        var world = NewWorld(GameplayExecutionPolicies.Runtime, NewAssets());
        LoadWorldDocument(world, string.Empty,
            InlineReference(InlineEntity("broken", typeof(FaultyInitializationEntityScriptStub).FullName!)),
            InlineReference(InlineEntity("fine", typeof(StatefulEntityScriptStub).FullName!)));

        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            LoadContent(world);
        }
        finally
        {
            Logs.Close();
        }

        Assert.Contains(world.Entities, entity => entity.Name == "fine");
        Assert.DoesNotContain(world.Entities, entity => entity.Name == "broken");
        Assert.Contains(logger.Errors, error => error.Contains("faulty script initialization"));
    }

    // ---- (iii) the world script ----

    [Fact]
    public void LoadingAWorld_WithAFaultyWorldScript_Throws_NamingTheWorldFile()
    {
        var world = NewWorld(GameplayExecutionPolicies.Runtime, NewAssets());
        LoadWorldDocument(world, FaultyScriptName);

        var exception = Assert.Throws<ScriptTypeResolutionException>(() => LoadContent(world));

        Assert.Contains("Worlds/level.world", exception.Message);
        Assert.Contains(FaultyScriptName, exception.Message);
    }

    [Fact]
    public void LoadingAWorld_WithAWorldScriptFullName_CreatesIt()
    {
        var world = NewWorld(GameplayExecutionPolicies.Runtime, NewAssets());
        LoadWorldDocument(world, typeof(StatefulWorldScriptStub).FullName!);

        LoadContent(world);

        Assert.IsType<StatefulWorldScriptStub>(world.GameplayProxy);
    }

    // ---- (iv) the player controller and the gameplay mode ----

    [Fact]
    public void PlayerController_WithAFaultyName_Throws_NamingTheStartupSettingsFile()
    {
        var world = NewWorld(GameplayExecutionPolicies.Runtime, NewAssets(startupControllerName: FaultyScriptName));
        world.PlayerStartupSettingsAssetId = StartupSettingsAssetId;
        world.LoadPlayerStartupSettings();

        var exception = Assert.Throws<ScriptTypeResolutionException>(
            () => world.CreateLocalPlayerController(PlayerIndex.One, new Entity(), null));

        Assert.Contains("Game/startup.gameMode", exception.Message);
        Assert.Contains("player_controller_class", exception.Message);
    }

    [Fact]
    public void PlayerController_WithAnEmptyName_IsTheEngineControllerCreatedByCode()
    {
        Assert.Equal(string.Empty, new PlayerStartupSettings().PlayerControllerClass);

        var world = new World();
        var controller = world.CreateLocalPlayerController(PlayerIndex.One, new Entity(), null);

        Assert.Equal(typeof(PlayerController), controller.GetType());
    }

    [Fact]
    public void PlayerController_WithAFullName_IsCreatedFromTheGameplayAssembly()
    {
        var world = NewWorld(GameplayExecutionPolicies.Runtime, NewAssets(startupControllerName: typeof(CustomPlayerControllerStub).FullName!));
        world.PlayerStartupSettingsAssetId = StartupSettingsAssetId;
        world.LoadPlayerStartupSettings();

        var controller = world.CreateLocalPlayerController(PlayerIndex.One, new Entity(), null);

        Assert.IsType<CustomPlayerControllerStub>(controller);
    }

    [Fact]
    public void GameplayMode_WithAFaultyName_Throws_NamingTheModeFile()
    {
        var modeAsset = new GameplayModeAsset { ModeClassName = FaultyScriptName, FileName = "Game/rules.gameplay_mode" };

        var exception = Assert.Throws<ScriptTypeResolutionException>(() => modeAsset.CreateMode());

        Assert.Contains("Game/rules.gameplay_mode", exception.Message);
        Assert.Contains("mode_class_name", exception.Message);
    }

    [Fact]
    public void GameplayMode_WithAnEmptyName_CreatesNothing()
    {
        Assert.Null(new GameplayModeAsset { ModeClassName = string.Empty }.CreateMode());
    }

    // ---- (v) the editor preview and the execution policy ----

    [Fact]
    public void Policy_ThrowsOnResolution_ForRuntimeAndSimulation_NotForThePreview()
    {
        Assert.True(GameplayExecutionPolicies.Runtime.ThrowOnScriptTypeResolution);
        Assert.True(GameplayExecutionPolicies.EditorSimulation.ThrowOnScriptTypeResolution);
        Assert.False(GameplayExecutionPolicies.EditorPreview.ThrowOnScriptTypeResolution);
    }

    [Fact]
    public void Preview_KeepsEveryFaultyEntityWithoutScript_AndWarnsOncePerEntity()
    {
        var world = NewWorld(GameplayExecutionPolicies.EditorPreview, NewAssets());
        LoadWorldDocument(world, SpawningWorldScriptStubName,
            InlineReference(InlineEntity("inline", FaultyScriptName)),
            PrefabReference(PrefabAssetId));

        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            LoadContent(world);
        }
        finally
        {
            Logs.Close();
        }

        // The inline entity, the entity of the prefab and the entity born from the world script are all there.
        var inline = Assert.Single(world.Entities, entity => entity.Name == "inline");
        var prefab = Assert.Single(world.Entities, entity => entity.Name == "prefab instance");
        var spawned = Assert.Single(world.Entities, entity => entity.Name == "spawned");

        foreach (var entity in new[] { inline, prefab, spawned })
        {
            Assert.Null(entity.GameplayProxy);
        }

        Assert.Equal(FaultyScriptName, inline.GameplayProxyClassName);
        Assert.Equal(FaultyScriptName, prefab.GameplayProxyClassName);
        Assert.Equal(SpawningWorldScriptStub.SpawnedScriptName, spawned.GameplayProxyClassName);
        Assert.IsType<SpawningWorldScriptStub>(world.GameplayProxy);

        Assert.Equal(3, logger.Warnings.Count);
        Assert.Contains(logger.Warnings, warning => warning.Contains(FaultyScriptName) && warning.Contains("Worlds/level.world"));
        Assert.Contains(logger.Warnings, warning => warning.Contains(FaultyScriptName) && warning.Contains("Prefabs/faulty.entity"));
        Assert.Contains(logger.Warnings, warning => warning.Contains(SpawningWorldScriptStub.SpawnedScriptName));
    }

    private static string SpawningWorldScriptStubName => typeof(SpawningWorldScriptStub).FullName!;

    [Fact]
    public void Preview_OpensAWorldWithAFaultyWorldScript_WithoutItsScript()
    {
        var world = NewWorld(GameplayExecutionPolicies.EditorPreview, NewAssets());
        LoadWorldDocument(world, FaultyScriptName);

        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            LoadContent(world);
        }
        finally
        {
            Logs.Close();
        }

        Assert.Null(world.GameplayProxy);
        Assert.Equal(FaultyScriptName, world.GameplayProxyClassName);
        var warning = Assert.Single(logger.Warnings);
        Assert.Contains("Worlds/level.world", warning);
    }

    [Fact]
    public void Simulation_Throws_ForEachOfTheSameCases()
    {
        Assert.Throws<ScriptTypeResolutionException>(() =>
        {
            var world = NewWorld(GameplayExecutionPolicies.EditorSimulation, NewAssets());
            LoadWorldDocument(world, string.Empty, InlineReference(InlineEntity("inline", FaultyScriptName)));
            LoadContent(world);
        });

        Assert.Throws<ScriptTypeResolutionException>(() =>
        {
            var world = NewWorld(GameplayExecutionPolicies.EditorSimulation, NewAssets());
            LoadWorldDocument(world, string.Empty, PrefabReference(PrefabAssetId));
            LoadContent(world);
        });

        Assert.Throws<ScriptTypeResolutionException>(() =>
        {
            var world = NewWorld(GameplayExecutionPolicies.EditorSimulation, NewAssets());
            LoadWorldDocument(world, FaultyScriptName);
            LoadContent(world);
        });

        Assert.Throws<ScriptTypeResolutionException>(() =>
        {
            var world = NewWorld(GameplayExecutionPolicies.EditorSimulation, NewAssets());
            LoadWorldDocument(world, SpawningWorldScriptStubName);
            LoadContent(world);
        });
    }

    [Fact]
    public void Preview_AWorldAlreadyLoaded_KeepsAnEntityAddedLaterWithAFaultyScript()
    {
        var world = NewWorld(GameplayExecutionPolicies.EditorPreview, NewAssets());
        LoadWorldDocument(world, string.Empty);
        LoadContent(world);

        // The path of a prefab preview and of the editor's additions: AddEntity, then Update.
        world.AddEntity(InlineEntity("late", FaultyScriptName));
        world.Update(0f);

        var late = Assert.Single(world.Entities, entity => entity.Name == "late");
        Assert.Null(late.GameplayProxy);
        Assert.Equal(FaultyScriptName, late.GameplayProxyClassName);
    }

    [Fact]
    public void Simulation_AWorldAlreadyLoaded_ThrowsForAnEntityAddedLater()
    {
        var world = NewWorld(GameplayExecutionPolicies.EditorSimulation, NewAssets());
        LoadWorldDocument(world, string.Empty);
        LoadContent(world);

        world.AddEntity(InlineEntity("late", FaultyScriptName));

        Assert.Throws<ScriptTypeResolutionException>(() => world.Update(0f));
    }

    [Fact]
    public void ToleranceScope_IsClosedAfterAnException()
    {
        var world = NewWorld(GameplayExecutionPolicies.EditorPreview, NewAssets());
        LoadWorldDocument(world, typeof(FaultyInitializationWorldScriptStub).FullName!);

        // The world script fails inside the scope the preview opened: the exception leaves the world load.
        Assert.Throws<InvalidOperationException>(() => LoadContent(world));

        // Outside any world, a name that does not resolve throws again: the scope did not stay open.
        Assert.Throws<ScriptTypeResolutionException>(
            () => ElementFactory.CreateScriptObject<GameplayProxy>(FaultyScriptName, "script_class_name"));
    }

    [Fact]
    public void ToleranceScope_IsNotOpenForAWorldWithoutAGame()
    {
        // A world without a game has no policy to read: strict.
        var world = new World { GameplayProxyClassName = FaultyScriptName };

        Assert.Throws<ScriptTypeResolutionException>(() => world.CreateGameplayProxy());
    }

    // ---- the tolerance scope itself ----

    [Fact]
    public void ScriptObject_InsideTheToleranceScope_ReturnsNullAndWarns_OutsideItThrows()
    {
        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            using (ScriptTypeResolutionTolerance.Open("Worlds/level.world"))
            {
                Assert.Null(ElementFactory.CreateScriptObject<GameplayProxy>(FaultyScriptName, "script_class_name"));
            }

            Assert.Throws<ScriptTypeResolutionException>(
                () => ElementFactory.CreateScriptObject<GameplayProxy>(FaultyScriptName, "script_class_name"));
        }
        finally
        {
            Logs.Close();
        }

        var warning = Assert.Single(logger.Warnings);
        Assert.Contains("Worlds/level.world", warning);
    }
}
