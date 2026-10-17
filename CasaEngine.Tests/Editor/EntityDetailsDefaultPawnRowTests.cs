using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Editor.Controls;
using CasaEngine.Editor.History;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Tests.UI;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Editor;

/// <summary>
/// ADR-0077 (plan world-game-mode-inspector T3, D3): the inspector of a selected world shows a "Default Pawn" row that edits
/// the default pawn of the world's game mode. The pawn lives on the <see cref="PlayerStartupSettings"/> instance shared by
/// every world of the mode, so a change goes through the editor history on that instance and records the mode in the
/// <see cref="PendingGameModeEdits"/> registry (on execute and on undo), whose handle keeps the instance alive. The asset
/// catalogue is global state, hence the serialized collection.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class EntityDetailsDefaultPawnRowTests : IDisposable
{
    private static readonly EditorHistoryContext Context = new(EditorHistoryContextKind.World, "entity-details-default-pawn-row-tests");

    private static readonly Guid ModeA = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid PawnA = Guid.Parse("c1000000-0000-0000-0000-000000000001");
    private static readonly Guid PawnB = Guid.Parse("c1000000-0000-0000-0000-000000000002");
    private static readonly Guid GameplayA = Guid.Parse("b1000000-0000-0000-0000-000000000001");

    private AssetContentManager _manager;

    public EntityDetailsDefaultPawnRowTests()
    {
        EditorHistoryService.Current.Remove(Context);

        AssetCatalog.ClearInternal();
        AssetCatalog.AddInternal(new AssetInfo(ModeA) { Name = "mode_a", FileName = "Modes/mode_a.gameMode" });
        AssetCatalog.AddInternal(new AssetInfo(GameplayA) { Name = "gameplay_a", FileName = "Modes/gameplay_a.gameplayMode" });
        AssetCatalog.AddInternal(new AssetInfo(PawnA) { Name = "pawn_a", FileName = "Entities/pawn_a.entity" });
        AssetCatalog.AddInternal(new AssetInfo(PawnB) { Name = "pawn_b", FileName = "Entities/pawn_b.entity" });
    }

    public void Dispose()
    {
        EditorHistoryService.Current.IsSuspended = false;
        EditorHistoryService.Current.Remove(Context);
        EditorHistoryService.Current.Deactivate();
        AssetCatalog.ClearInternal();
    }

    /// <summary>A new instance per load (pawn A), so a freed then reloaded mode is told apart from the same instance.</summary>
    private sealed class FreshSettingsLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
            => new PlayerStartupSettings { DefaultPawnAssetId = PawnA };

        public bool IsFileSupported(string fileName) => true;
    }

    private World NewWorldWithGame(Guid modeId)
    {
        var infos = new Dictionary<Guid, AssetInfo>
        {
            [ModeA] = new AssetInfo(ModeA) { Name = "mode_a", FileName = "mode_a.gameMode" },
        };

        _manager = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(null, Path.GetTempPath(), id => infos.GetValueOrDefault(id)),
        };
        _manager.RegisterAssetLoader(typeof(PlayerStartupSettings), new FreshSettingsLoader());

        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        SetBackingField(game, nameof(CasaEngineGame.AssetContentManager), _manager);

        var world = new World { PlayerStartupSettingsAssetId = modeId };
        SetBackingField(world, nameof(World.Game), game);
        world.LoadPlayerStartupSettings();
        return world;
    }

    private static void SetBackingField(object instance, string propertyName, object value)
    {
        var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static (EntityDetailsPanel Panel, MGElement Content) OpenPanel(PendingGameModeEdits? edits)
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var window = new MGWindow(desktop, 0, 0, 480, 640);
        desktop.Windows.Add(window);

        var panel = new EntityDetailsPanel(window, includeComponentTree: false)
        {
            HistoryContext = Context,
            GameModeEdits = edits,
        };
        var content = panel.CreateContent();
        return (panel, content);
    }

    // The name block comes before the Browse button in the selector's tree.
    private static string DisplayedName(AssetSelector selector)
        => selector.TraverseVisualTree().OfType<MGTextBlock>().First().Text;

    private static void Undo()
    {
        EditorHistoryService.Current.SetActiveContext(Context);
        Assert.True(EditorHistoryService.Current.Undo());
    }

    // ---- (i) the row ----

    [Fact]
    public void WorldWithAMode_ShowsTheDefaultPawnRow_WithTheModesPawn_AndATextNamingTheMode()
    {
        var world = NewWorldWithGame(ModeA);
        var (panel, content) = OpenPanel(new PendingGameModeEdits());

        panel.SetSelectedWorld(world);

        Assert.NotNull(panel.DefaultPawnSelector);
        Assert.True(panel.DefaultPawnSelector.IsEnabled);
        Assert.Equal(PawnA, panel.DefaultPawnSelector.AssetId);
        Assert.Equal("pawn_a", DisplayedName(panel.DefaultPawnSelector));
        Assert.Contains(
            content.TraverseVisualTree().OfType<MGTextBlock>(),
            text => text.Text != null && text.Text.Contains("mode_a") && text.Text.Contains("every world that uses this game mode"));
        Assert.Contains(
            content.TraverseVisualTree().OfType<MGTextBlock>(),
            text => text.Text == "Default Pawn");
    }

    [Fact]
    public void WorldWithoutAMode_DisablesTheDefaultPawnSelector()
    {
        var world = NewWorldWithGame(Guid.Empty);
        var (panel, _) = OpenPanel(new PendingGameModeEdits());

        panel.SetSelectedWorld(world);

        Assert.NotNull(panel.DefaultPawnSelector);
        Assert.False(panel.DefaultPawnSelector.IsEnabled);
    }

    [Fact]
    public void PanelWithoutARegistry_DisablesTheDefaultPawnSelector()
    {
        var world = NewWorldWithGame(ModeA);
        var (panel, _) = OpenPanel(null);

        panel.SetSelectedWorld(world);

        Assert.NotNull(panel.DefaultPawnSelector);
        Assert.False(panel.DefaultPawnSelector.IsEnabled);
    }

    [Fact]
    public void WorldNotAttachedToAGame_DisablesTheDefaultPawnSelector()
    {
        var (panel, _) = OpenPanel(new PendingGameModeEdits());

        panel.SetSelectedWorld(new World { PlayerStartupSettingsAssetId = ModeA });

        Assert.False(panel.DefaultPawnSelector.IsEnabled);
    }

    // ---- (ii) the filter ----

    [Fact]
    public void TheDefaultPawnSelector_ListsOnlyEntityAssets()
    {
        var (panel, _) = OpenPanel(new PendingGameModeEdits());
        panel.SetSelectedWorld(NewWorldWithGame(ModeA));

        Assert.Equal(new[] { PawnA, PawnB }, panel.DefaultPawnSelector.GetPickableAssets().Select(a => a.Id));
    }

    // ---- (iii) the change ----

    [Fact]
    public void PickingAPawn_ChangesTheSharedInstance_RecordsTheMode_AndUndoGivesThePawnBack()
    {
        var edits = new PendingGameModeEdits();
        var world = NewWorldWithGame(ModeA);
        var (panel, _) = OpenPanel(edits);
        panel.SetSelectedWorld(world);
        var instance = world.PlayerStartupSettings;

        panel.DefaultPawnSelector.SelectAsset(AssetCatalog.Get(PawnB)!);

        Assert.Equal(PawnB, instance.DefaultPawnAssetId);
        Assert.Same(instance, world.PlayerStartupSettings);
        Assert.True(edits.IsPending(ModeA));
        Assert.Equal(PawnB, panel.DefaultPawnSelector.AssetId);
        Assert.True(EditorHistoryService.Current.TryGet(Context, out var stack));
        Assert.Equal("Change Game Mode Default Pawn", stack.UndoDescription);

        Undo();

        Assert.Equal(PawnA, instance.DefaultPawnAssetId);
        Assert.Equal(PawnA, panel.DefaultPawnSelector.AssetId);
    }

    [Fact]
    public void PickingThePawnAlreadySet_RecordsNothing()
    {
        var edits = new PendingGameModeEdits();
        var (panel, _) = OpenPanel(edits);
        panel.SetSelectedWorld(NewWorldWithGame(ModeA));

        panel.DefaultPawnSelector.SelectAsset(AssetCatalog.Get(PawnA)!);

        Assert.False(EditorHistoryService.Current.TryGet(Context, out var stack) && stack.CanUndo);
        Assert.Equal(0, edits.Count);
    }

    [Fact]
    public void ChangeSaveThenUndo_RecordsTheModeAgain_BecauseTheUndoRecordsBeforeChangingThePawn()
    {
        var edits = new PendingGameModeEdits();
        var world = NewWorldWithGame(ModeA);
        var (panel, _) = OpenPanel(edits);
        panel.SetSelectedWorld(world);
        panel.DefaultPawnSelector.SelectAsset(AssetCatalog.Get(PawnB)!);

        // A save empties the registry (the written entry is released) without writing here: the registry is emptied directly.
        edits.DiscardAll();
        Assert.Equal(0, edits.Count);
        // The discard put pawn A back; the world is still attached to the instance.
        Assert.Equal(PawnA, world.PlayerStartupSettings.DefaultPawnAssetId);

        Undo();

        Assert.True(edits.IsPending(ModeA));
    }

    // ---- (iv) the handle ----

    [Fact]
    public void AfterAChange_TheRegistryKeepsTheInstanceAlive_AcrossAWorldReload()
    {
        var edits = new PendingGameModeEdits();
        var world = NewWorldWithGame(ModeA);
        var (panel, _) = OpenPanel(edits);
        panel.SetSelectedWorld(world);
        var instance = world.PlayerStartupSettings;
        panel.DefaultPawnSelector.SelectAsset(AssetCatalog.Get(PawnB)!);

        world.ReleasePlayerStartupSettings();
        int freed = _manager.CollectUnreferenced();
        world.LoadPlayerStartupSettings();

        Assert.Equal(0, freed);
        Assert.Same(instance, world.PlayerStartupSettings);
        Assert.Equal(PawnB, world.PlayerStartupSettings.DefaultPawnAssetId);
    }

    [Fact]
    public void WithoutARecordedChange_TheModeIsFreedWhenTheWorldReleasesIt()
    {
        var world = NewWorldWithGame(ModeA);
        var (panel, _) = OpenPanel(new PendingGameModeEdits());
        panel.SetSelectedWorld(world);

        world.ReleasePlayerStartupSettings();

        Assert.Equal(1, _manager.CollectUnreferenced());
    }

    // ---- not a world ----

    [Fact]
    public void SelectedEntity_HasNoDefaultPawnSelector()
    {
        var (panel, _) = OpenPanel(new PendingGameModeEdits());
        panel.SetSelectedWorld(NewWorldWithGame(ModeA));
        Assert.NotNull(panel.DefaultPawnSelector);

        panel.SetSelectedEntity(new CasaEngine.Framework.Scene.Entities.Entity { Name = "e" });

        Assert.Null(panel.DefaultPawnSelector);
    }
}
