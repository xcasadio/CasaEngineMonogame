using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Editor.Controls;
using CasaEngine.Editor.History;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Tests.UI;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Editor;

/// <summary>
/// The inspector of a selected world shows its "Game Mode" and "Gameplay Mode" rows as asset pickers (D1, D2, D7): the
/// pickers list only the <c>.gameMode</c>, resp. <c>.gameplayMode</c> assets, a change goes through the editor history,
/// and changing the game mode reloads the world's <see cref="PlayerStartupSettings"/> (the new mode's instance) while the
/// undo gives the previous mode's instance back. The world has a <see cref="CasaEngineGame"/> with a test
/// <see cref="AssetContentManager"/>; the asset catalogue is global state, hence the serialized collection.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class EntityDetailsGameModeRowsTests : IDisposable
{
    private static readonly EditorHistoryContext Context = new(EditorHistoryContextKind.World, "entity-details-game-mode-rows-tests");

    private static readonly Guid ModeA = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid ModeB = Guid.Parse("a1000000-0000-0000-0000-000000000002");
    private static readonly Guid GameplayA = Guid.Parse("b1000000-0000-0000-0000-000000000001");
    private static readonly Guid GameplayB = Guid.Parse("b1000000-0000-0000-0000-000000000002");
    private static readonly Guid EntityA = Guid.Parse("c1000000-0000-0000-0000-000000000001");
    private static readonly Guid EntityB = Guid.Parse("c1000000-0000-0000-0000-000000000002");
    private static readonly Guid WorldAsset = Guid.Parse("d1000000-0000-0000-0000-000000000001");

    private readonly PlayerStartupSettings _settingsA = new() { DefaultPawnAssetId = EntityA };
    private readonly PlayerStartupSettings _settingsB = new() { DefaultPawnAssetId = EntityB };

    public EntityDetailsGameModeRowsTests()
    {
        EditorHistoryService.Current.Remove(Context);

        AssetCatalog.ClearInternal();
        AssetCatalog.AddInternal(new AssetInfo(ModeA) { Name = "mode_a", FileName = "Modes/mode_a.gameMode" });
        AssetCatalog.AddInternal(new AssetInfo(ModeB) { Name = "mode_b", FileName = "Modes/mode_b.gameMode" });
        AssetCatalog.AddInternal(new AssetInfo(GameplayA) { Name = "gameplay_a", FileName = "Modes/gameplay_a.gameplayMode" });
        AssetCatalog.AddInternal(new AssetInfo(GameplayB) { Name = "gameplay_b", FileName = "Modes/gameplay_b.gameplayMode" });
        AssetCatalog.AddInternal(new AssetInfo(EntityA) { Name = "pawn_a", FileName = "Entities/pawn_a.entity" });
        AssetCatalog.AddInternal(new AssetInfo(EntityB) { Name = "pawn_b", FileName = "Entities/pawn_b.entity" });
        AssetCatalog.AddInternal(new AssetInfo(WorldAsset) { Name = "level", FileName = "Worlds/level.world" });
    }

    public void Dispose()
    {
        EditorHistoryService.Current.IsSuspended = false;
        EditorHistoryService.Current.Remove(Context);
        EditorHistoryService.Current.Deactivate();
        AssetCatalog.ClearInternal();
    }

    /// <summary>Loads the game mode instance registered for the file name, so each mode has its own distinguishable settings.</summary>
    private sealed class SettingsLoader : IAssetLoader
    {
        private readonly Dictionary<string, PlayerStartupSettings> _byFileName;

        public SettingsLoader(Dictionary<string, PlayerStartupSettings> byFileName) => _byFileName = byFileName;

        public object LoadAsset(string fileName, AssetContentManager assetContentManager) => _byFileName[Path.GetFileName(fileName)];

        public bool IsFileSupported(string fileName) => true;
    }

    private World NewWorldWithGame()
    {
        var infos = new Dictionary<Guid, AssetInfo>
        {
            [ModeA] = new AssetInfo(ModeA) { Name = "mode_a", FileName = "mode_a.gameMode" },
            [ModeB] = new AssetInfo(ModeB) { Name = "mode_b", FileName = "mode_b.gameMode" },
        };

        var manager = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(null, Path.GetTempPath(), id => infos.GetValueOrDefault(id)),
        };
        manager.RegisterAssetLoader(typeof(PlayerStartupSettings), new SettingsLoader(new Dictionary<string, PlayerStartupSettings>
        {
            ["mode_a.gameMode"] = _settingsA,
            ["mode_b.gameMode"] = _settingsB,
        }));

        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        SetBackingField(game, nameof(CasaEngineGame.AssetContentManager), manager);

        var world = new World
        {
            PlayerStartupSettingsAssetId = ModeA,
            GameplayModeAssetId = GameplayA,
        };
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

    private static EntityDetailsPanel OpenPanel()
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var window = new MGWindow(desktop, 0, 0, 480, 640);
        desktop.Windows.Add(window);

        var panel = new EntityDetailsPanel(window, includeComponentTree: false)
        {
            HistoryContext = Context,
        };
        panel.CreateContent();
        return panel;
    }

    // The name block comes before the Browse button in the selector's tree.
    private static string DisplayedName(AssetSelector selector)
        => selector.TraverseVisualTree().OfType<MGTextBlock>().First().Text;

    private static bool CanUndo() => EditorHistoryService.Current.TryGet(Context, out var stack) && stack.CanUndo;

    private static void Undo()
    {
        EditorHistoryService.Current.SetActiveContext(Context);
        Assert.True(EditorHistoryService.Current.Undo());
    }

    // ---- (i) the rows ----

    [Fact]
    public void SelectedWorld_ShowsTheGameModeAndGameplayModeSelectors_WithTheCurrentIdsAndNames()
    {
        var world = NewWorldWithGame();
        var panel = OpenPanel();

        panel.SetSelectedWorld(world);

        Assert.NotNull(panel.GameModeSelector);
        Assert.NotNull(panel.GameplayModeSelector);
        Assert.Equal(ModeA, panel.GameModeSelector.AssetId);
        Assert.Equal("mode_a", DisplayedName(panel.GameModeSelector));
        Assert.Equal(GameplayA, panel.GameplayModeSelector.AssetId);
        Assert.Equal("gameplay_a", DisplayedName(panel.GameplayModeSelector));
    }

    [Fact]
    public void WorldWithoutModes_ShowsNoneInBothSelectors()
    {
        var panel = OpenPanel();

        panel.SetSelectedWorld(new World());

        Assert.Equal(Guid.Empty, panel.GameModeSelector.AssetId);
        Assert.Equal("[i]None[/i]", DisplayedName(panel.GameModeSelector));
        Assert.Equal(Guid.Empty, panel.GameplayModeSelector.AssetId);
        Assert.Equal("[i]None[/i]", DisplayedName(panel.GameplayModeSelector));
    }

    // ---- (ii) the filters ----

    [Fact]
    public void TheSelectors_ListOnlyTheirOwnAssetType()
    {
        var panel = OpenPanel();
        panel.SetSelectedWorld(NewWorldWithGame());

        Assert.Equal(new[] { ModeA, ModeB }, panel.GameModeSelector.GetPickableAssets().Select(a => a.Id));
        Assert.Equal(new[] { GameplayA, GameplayB }, panel.GameplayModeSelector.GetPickableAssets().Select(a => a.Id));
    }

    // ---- (iii) the game mode ----

    [Fact]
    public void PickingAnotherGameMode_ChangesTheId_ReloadsTheSettings_AndUndoGivesThePreviousModeBack()
    {
        var world = NewWorldWithGame();
        var panel = OpenPanel();
        panel.SetSelectedWorld(world);
        Assert.Same(_settingsA, world.PlayerStartupSettings);

        panel.GameModeSelector.SelectAsset(AssetCatalog.Get(ModeB)!);

        Assert.Equal(ModeB, world.PlayerStartupSettingsAssetId);
        Assert.Same(_settingsB, world.PlayerStartupSettings);
        Assert.Equal(ModeB, panel.GameModeSelector.AssetId);
        Assert.Equal("mode_b", DisplayedName(panel.GameModeSelector));

        Undo();

        Assert.Equal(ModeA, world.PlayerStartupSettingsAssetId);
        Assert.Same(_settingsA, world.PlayerStartupSettings);
        Assert.Equal(ModeA, panel.GameModeSelector.AssetId);
    }

    [Fact]
    public void ClearingNothingBetweenModes_PickingTheSameGameModeAgain_RecordsNoHistoryEntry()
    {
        var world = NewWorldWithGame();
        var panel = OpenPanel();
        panel.SetSelectedWorld(world);

        panel.GameModeSelector.SelectAsset(AssetCatalog.Get(ModeA)!);
        panel.GameplayModeSelector.SelectAsset(AssetCatalog.Get(GameplayA)!);

        Assert.False(CanUndo());
    }

    // ---- (iv) the gameplay mode ----

    [Fact]
    public void PickingAnotherGameplayMode_ChangesTheId_AndUndoGivesThePreviousOneBack()
    {
        var world = NewWorldWithGame();
        var panel = OpenPanel();
        panel.SetSelectedWorld(world);

        panel.GameplayModeSelector.SelectAsset(AssetCatalog.Get(GameplayB)!);

        Assert.Equal(GameplayB, world.GameplayModeAssetId);
        Assert.Equal(GameplayB, panel.GameplayModeSelector.AssetId);
        // Only read at play start: the loaded game mode is left alone.
        Assert.Same(_settingsA, world.PlayerStartupSettings);
        Assert.True(EditorHistoryService.Current.TryGet(Context, out var stack));
        Assert.Equal("Change World Gameplay Mode", stack.UndoDescription);

        Undo();

        Assert.Equal(GameplayA, world.GameplayModeAssetId);
        Assert.Equal(GameplayA, panel.GameplayModeSelector.AssetId);
    }

    [Fact]
    public void ChangingTheGameMode_RecordsTheNamedHistoryEntry()
    {
        var world = NewWorldWithGame();
        var panel = OpenPanel();
        panel.SetSelectedWorld(world);

        panel.GameModeSelector.SelectAsset(AssetCatalog.Get(ModeB)!);

        Assert.True(EditorHistoryService.Current.TryGet(Context, out var stack));
        Assert.Equal("Change World Game Mode", stack.UndoDescription);
    }

    [Fact]
    public void WorldNotAttachedToAGame_ChangesTheId_WithoutLoadingTheSettings()
    {
        var world = new World { PlayerStartupSettingsAssetId = ModeA };
        var panel = OpenPanel();
        var settingsBefore = world.PlayerStartupSettings;
        panel.SetSelectedWorld(world);

        panel.GameModeSelector.SelectAsset(AssetCatalog.Get(ModeB)!);

        Assert.Equal(ModeB, world.PlayerStartupSettingsAssetId);
        Assert.Same(settingsBefore, world.PlayerStartupSettings);
    }

    // ---- (vi) not a world ----

    [Fact]
    public void SelectedEntity_HasNoGameModeSelectors_AndTheyGoAwayWhenTheWorldIsDeselected()
    {
        var world = NewWorldWithGame();
        var panel = OpenPanel();
        panel.SetSelectedWorld(world);
        Assert.NotNull(panel.GameModeSelector);

        panel.SetSelectedEntity(new Entity { Name = "e" });

        Assert.Null(panel.GameModeSelector);
        Assert.Null(panel.GameplayModeSelector);
    }
}
