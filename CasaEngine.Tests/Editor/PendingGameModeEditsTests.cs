using CasaEngine.Core.Logging;
using CasaEngine.Editor.Controls;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Gameplay;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>
/// ADR-0077 (plan world-game-mode-inspector T3, D4): the registry of the game modes whose default pawn changed since the
/// last save. It holds a counted handle on each recorded instance and the pawn the mode had when it was recorded; the project
/// save writes the modes whose pawn differs, an abandon puts the original pawn back, and a failed write keeps its entry.
/// The asset catalogue and the project path are global state, hence the serialized collection.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class PendingGameModeEditsTests : IDisposable
{
    private static readonly Guid ModeA = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid ModeB = Guid.Parse("a1000000-0000-0000-0000-000000000002");
    private static readonly Guid PawnA = Guid.Parse("c1000000-0000-0000-0000-000000000001");
    private static readonly Guid PawnB = Guid.Parse("c1000000-0000-0000-0000-000000000002");
    private static readonly Guid PawnC = Guid.Parse("c1000000-0000-0000-0000-000000000003");

    private const string ModeAFile = "Modes/mode_a.gameMode";
    private const string ModeBFile = "Modes/mode_b.gameMode";

    private readonly string _projectPath;
    private readonly string? _previousProjectPath;
    private readonly AssetContentManager _manager;

    public PendingGameModeEditsTests()
    {
        _projectPath = Path.Combine(Path.GetTempPath(), "casa-pending-gamemode-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_projectPath, "Modes"));
        _previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = _projectPath;

        AssetCatalog.ClearInternal();
        AssetCatalog.AddInternal(new AssetInfo(ModeA) { Name = "mode_a", FileName = ModeAFile });
        AssetCatalog.AddInternal(new AssetInfo(ModeB) { Name = "mode_b", FileName = ModeBFile });

        var infos = new Dictionary<Guid, AssetInfo>
        {
            [ModeA] = new AssetInfo(ModeA) { Name = "mode_a", FileName = "mode_a.gameMode" },
            [ModeB] = new AssetInfo(ModeB) { Name = "mode_b", FileName = "mode_b.gameMode" },
        };
        _manager = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(null, Path.GetTempPath(), id => infos.GetValueOrDefault(id)),
        };
        _manager.RegisterAssetLoader(typeof(PlayerStartupSettings), new FreshSettingsLoader());
    }

    public void Dispose()
    {
        EngineEnvironment.ProjectPath = _previousProjectPath;
        AssetCatalog.ClearInternal();
        try
        {
            var file = Path.Combine(_projectPath, ModeAFile);
            if (File.Exists(file))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
        }
        finally
        {
            Directory.Delete(_projectPath, recursive: true);
        }
    }

    /// <summary>A new instance per load (pawn A), so a freed then reloaded mode is told apart from the same instance.</summary>
    private sealed class FreshSettingsLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
            => new PlayerStartupSettings { DefaultPawnAssetId = PawnA };

        public bool IsFileSupported(string fileName) => true;
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Warnings { get; } = new();

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) => Warnings.Add(msg);
        public void WriteError(string msg) { }
    }

    private string ModeAPath => Path.Combine(_projectPath, ModeAFile);

    private Guid WrittenPawn() => Guid.Parse((string)JObject.Parse(File.ReadAllText(ModeAPath))["default_pawn_asset_id"]!);

    /// <summary>Records the mode, as the inspector command does, then changes its pawn on the shared instance.</summary>
    private PlayerStartupSettings Edit(PendingGameModeEdits edits, Guid modeId, Guid pawn)
    {
        edits.Record(_manager, modeId);
        using var handle = _manager.Acquire<PlayerStartupSettings>(modeId);
        handle.Asset.DefaultPawnAssetId = pawn;
        return handle.Asset;
    }

    [Fact]
    public void Record_KeepsTheModePending_AndTheFirstCallKeepsTheOriginalPawn()
    {
        var edits = new PendingGameModeEdits();

        Edit(edits, ModeA, PawnB);
        // A second record (the undo of the command) must not take the changed pawn as the original one.
        edits.Record(_manager, ModeA);

        Assert.True(edits.IsPending(ModeA));
        Assert.False(edits.IsPending(ModeB));
        Assert.Equal(1, edits.Count);

        edits.DiscardAll();
        using var handle = _manager.Acquire<PlayerStartupSettings>(ModeA);
        Assert.Equal(PawnA, handle.Asset.DefaultPawnAssetId);
    }

    [Fact]
    public void TheRegistry_HoldsTheInstance_SoCollectUnreferencedKeepsItAndAReloadFindsTheChange()
    {
        var edits = new PendingGameModeEdits();
        var instance = Edit(edits, ModeA, PawnB);

        Assert.Equal(0, _manager.CollectUnreferenced());

        using var handle = _manager.Acquire<PlayerStartupSettings>(ModeA);
        Assert.Same(instance, handle.Asset);
        Assert.Equal(PawnB, handle.Asset.DefaultPawnAssetId);
    }

    [Fact]
    public void TrySaveAll_WritesTheChangedMode_ReleasesItsHandle_AndEmptiesTheRegistry()
    {
        var edits = new PendingGameModeEdits();
        Edit(edits, ModeA, PawnB);

        Assert.True(edits.TrySaveAll(out var message));

        Assert.Equal(string.Empty, message);
        Assert.Equal(PawnB, WrittenPawn());
        Assert.Equal(0, edits.Count);
        Assert.False(edits.IsPending(ModeA));
        // Nobody holds the mode any more: it is freed.
        Assert.Equal(1, _manager.CollectUnreferenced());
    }

    [Fact]
    public void TrySaveAll_DoesNotWriteAModeWhosePawnWentBackToTheOriginalOne_ButReleasesIt()
    {
        var edits = new PendingGameModeEdits();
        Edit(edits, ModeA, PawnB);
        Edit(edits, ModeA, PawnA);

        Assert.True(edits.TrySaveAll(out _));

        Assert.False(File.Exists(ModeAPath));
        Assert.Equal(0, edits.Count);
        Assert.Equal(1, _manager.CollectUnreferenced());
    }

    [Fact]
    public void ChangeSaveUndoSave_WritesThePreviousPawnAgain()
    {
        var edits = new PendingGameModeEdits();
        Edit(edits, ModeA, PawnB);
        Assert.True(edits.TrySaveAll(out _));
        Assert.Equal(PawnB, WrittenPawn());

        // The undo: the registry is empty after the save, so the command records the mode again before the change.
        // A world still holds the mode, so the instance still has the saved pawn B.
        using var worldHold = _manager.Acquire<PlayerStartupSettings>(ModeA);
        edits.Record(_manager, ModeA);
        worldHold.Asset.DefaultPawnAssetId = PawnA;

        Assert.True(edits.IsPending(ModeA));
        Assert.True(edits.TrySaveAll(out _));

        Assert.Equal(PawnA, WrittenPawn());
    }

    [Fact]
    public void DiscardAll_PutsTheOriginalPawnBack_ReleasesTheHandles_AndEmptiesTheRegistry()
    {
        var edits = new PendingGameModeEdits();
        var instance = Edit(edits, ModeA, PawnB);
        Edit(edits, ModeB, PawnC);

        edits.DiscardAll();

        Assert.Equal(PawnA, instance.DefaultPawnAssetId);
        Assert.Equal(0, edits.Count);
        Assert.Equal(2, _manager.CollectUnreferenced());
    }

    [Fact]
    public void TrySaveAll_WithAFailingWrite_LogsKeepsTheEntry_AndASecondSaveAfterTheFixWritesAndEmptiesTheRegistry()
    {
        var edits = new PendingGameModeEdits();
        var instance = Edit(edits, ModeA, PawnB);
        File.WriteAllText(ModeAPath, "{}");
        File.SetAttributes(ModeAPath, FileAttributes.ReadOnly);

        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        bool saved;
        string message;
        try
        {
            saved = edits.TrySaveAll(out message);
        }
        finally
        {
            Logs.Close();
        }

        Assert.False(saved);
        Assert.Contains("mode_a", message);
        Assert.Contains(logger.Warnings, warning => warning.Contains("mode_a"));
        Assert.True(edits.IsPending(ModeA));
        // The handle is still held: the instance survives a collection.
        Assert.Equal(0, _manager.CollectUnreferenced());
        Assert.Equal("{}", File.ReadAllText(ModeAPath));

        File.SetAttributes(ModeAPath, FileAttributes.Normal);
        Assert.True(edits.TrySaveAll(out _));

        Assert.Equal(PawnB, WrittenPawn());
        Assert.Equal(0, edits.Count);
        Assert.Equal(PawnB, instance.DefaultPawnAssetId);
    }

    [Fact]
    public void FailedEntry_KeepsItsOriginalPawn_SoADiscardPutsItBack()
    {
        var edits = new PendingGameModeEdits();
        var instance = Edit(edits, ModeA, PawnB);
        File.WriteAllText(ModeAPath, "{}");
        File.SetAttributes(ModeAPath, FileAttributes.ReadOnly);

        Assert.False(edits.TrySaveAll(out _));
        edits.DiscardAll();

        Assert.Equal(PawnA, instance.DefaultPawnAssetId);
        Assert.Equal(0, edits.Count);
    }

    [Fact]
    public void TrySaveAll_WithAModeMissingFromTheCatalog_FailsWithAMessageAndKeepsTheEntry()
    {
        var edits = new PendingGameModeEdits();
        Edit(edits, ModeA, PawnB);
        AssetCatalog.ClearInternal();

        Assert.False(edits.TrySaveAll(out var message));

        Assert.Contains(ModeA.ToString(), message);
        Assert.True(edits.IsPending(ModeA));
    }

    [Fact]
    public void AFailedEntry_DoesNotStopTheOthersFromBeingWritten()
    {
        var edits = new PendingGameModeEdits();
        Edit(edits, ModeA, PawnB);
        Edit(edits, ModeB, PawnC);
        File.WriteAllText(ModeAPath, "{}");
        File.SetAttributes(ModeAPath, FileAttributes.ReadOnly);

        Assert.False(edits.TrySaveAll(out _));

        Assert.True(edits.IsPending(ModeA));
        Assert.False(edits.IsPending(ModeB));
        Assert.True(File.Exists(Path.Combine(_projectPath, ModeBFile)));
    }
}
