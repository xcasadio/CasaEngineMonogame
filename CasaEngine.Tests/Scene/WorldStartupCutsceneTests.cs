using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Core.Logging;
using CasaEngine.Core.Time;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Cutscenes;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Scene;

/// <summary>
/// The optional <c>startup_cutscene_asset_id</c> key of a <c>.world</c> (ADR-0088): written only when an id is set,
/// absent after a reload of a document without it, and played at the end of <c>World.BeginPlay</c> when the
/// execution policy runs it.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class WorldStartupCutsceneTests
{
    private static readonly Guid CutsceneId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid UnknownId = Guid.Parse("00000000-0000-0000-0000-0000000000a2");

    private sealed class CutsceneLoader : IAssetLoader
    {
        public CutsceneAsset Next { get; set; }

        public object LoadAsset(string fileName, AssetContentManager assetContentManager) => Next;

        public bool IsFileSupported(string fileName) => true;
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Errors { get; } = new();

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) { }
        public void WriteError(string msg) => Errors.Add(msg);
    }

    private static JObject SavedWorld(World world)
    {
        var node = new JObject();
        EditorEntityJsonSerializer.SaveWorld(world, node);
        return node;
    }

    private static CutsceneAsset ValidAsset(int frames)
    {
        return new CutsceneAsset { RootAction = new WaitFramesCutsceneActionData { Frames = frames } };
    }

    private static World NewWorld(CutsceneAsset asset, GameplayExecutionPolicy policy)
    {
        var infos = new Dictionary<Guid, AssetInfo>
        {
            [CutsceneId] = new AssetInfo(CutsceneId) { Name = "intro", FileName = "intro.cutscene" },
        };

        var manager = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(null, Path.GetTempPath(), id => infos.GetValueOrDefault(id)),
        };
        manager.RegisterAssetLoader(typeof(CutsceneAsset), new CutsceneLoader { Next = asset });

        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        SetBackingField(game, nameof(CasaEngineGame.AssetContentManager), manager);
        game.ExecutionPolicy = policy;

        var world = new World { Name = "Startup" };
        SetBackingField(world, nameof(World.Game), game);
        return world;
    }

    private static void SetBackingField(object instance, string propertyName, object value)
    {
        var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsTheId()
    {
        var node = SavedWorld(new World { Name = "W", StartupCutsceneAssetId = CutsceneId });

        Assert.Equal(CutsceneId, node["startup_cutscene_asset_id"]!.ToObject<Guid>());

        var loaded = new World();
        loaded.Load(node);

        Assert.Equal(CutsceneId, loaded.StartupCutsceneAssetId);
    }

    [Fact]
    public void AWorldWithoutAnId_WritesNoKey_AndLoadsEmpty()
    {
        var node = SavedWorld(new World { Name = "W" });

        Assert.False(node.ContainsKey("startup_cutscene_asset_id"));

        var loaded = new World();
        loaded.Load(node);
        Assert.Equal(Guid.Empty, loaded.StartupCutsceneAssetId);
    }

    [Fact]
    public void AReload_WithoutTheKey_DropsThePreviousId()
    {
        var withId = SavedWorld(new World { Name = "W", StartupCutsceneAssetId = CutsceneId });
        var without = SavedWorld(new World { Name = "W" });

        var world = new World();
        world.Load(withId);
        Assert.Equal(CutsceneId, world.StartupCutsceneAssetId);

        world.Load(without);
        Assert.Equal(Guid.Empty, world.StartupCutsceneAssetId);
    }

    [Fact]
    public void BeginPlay_UnderRuntime_PlaysTheAsset()
    {
        var world = NewWorld(ValidAsset(3), GameplayExecutionPolicies.Runtime);
        world.StartupCutsceneAssetId = CutsceneId;

        world.BeginPlay();

        var snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Playing, snapshot.State);
        Assert.Equal(CutsceneId, snapshot.AssetId);
    }

    [Fact]
    public void BeginPlay_UnderEditorSimulation_PlaysTheAsset()
    {
        var world = NewWorld(ValidAsset(3), GameplayExecutionPolicies.EditorSimulation);
        world.StartupCutsceneAssetId = CutsceneId;

        world.BeginPlay();

        Assert.Equal(CutsceneRuntimeState.Playing, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void BeginPlay_UnderEditorPreview_PlaysNothing()
    {
        var world = NewWorld(ValidAsset(3), GameplayExecutionPolicies.EditorPreview);
        world.StartupCutsceneAssetId = CutsceneId;

        world.BeginPlay();

        Assert.Equal(CutsceneRuntimeState.Idle, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void BeginPlay_WithoutAnId_PlaysNothing()
    {
        var world = NewWorld(ValidAsset(3), GameplayExecutionPolicies.Runtime);

        world.BeginPlay();

        Assert.Equal(CutsceneRuntimeState.Idle, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void BeginPlay_WithAnUnknownId_ThrowsNamingTheWorldAndTheId()
    {
        var world = NewWorld(ValidAsset(3), GameplayExecutionPolicies.Runtime);
        world.StartupCutsceneAssetId = UnknownId;

        var exception = Assert.Throws<InvalidOperationException>(() => world.BeginPlay());

        Assert.Contains("Startup", exception.Message);
        Assert.Contains(UnknownId.ToString(), exception.Message);
    }

    [Fact]
    public void BeginPlay_WithAnInvalidCutscene_DoesNotThrow_LogsOnce_AndTheDirectorIsInvalid()
    {
        var invalid = new CutsceneAsset { RootAction = new WaitFramesCutsceneActionData { Frames = -1 } };
        var world = NewWorld(invalid, GameplayExecutionPolicies.Runtime);
        world.StartupCutsceneAssetId = CutsceneId;

        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            world.BeginPlay();
        }
        finally
        {
            Logs.Close();
        }

        Assert.Equal(CutsceneRuntimeState.Invalid, world.CutsceneDirector.GetDebugSnapshot().State);
        var error = Assert.Single(logger.Errors);
        Assert.Contains("Startup", error);
        Assert.Contains("Frames", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheFirstAction_RunsAtTheFirstWorldUpdateAfterBeginPlay()
    {
        var world = NewWorld(ValidAsset(0), GameplayExecutionPolicies.Runtime);
        world.StartupCutsceneAssetId = CutsceneId;

        world.BeginPlay();
        Assert.Equal(CutsceneRuntimeState.Playing, world.CutsceneDirector.GetDebugSnapshot().State);

        world.Update(FrameTime.FromElapsedTime(0f));

        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
    }
}
