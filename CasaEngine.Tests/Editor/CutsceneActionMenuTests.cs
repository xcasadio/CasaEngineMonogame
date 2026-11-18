using CasaEngine.Editor;
using CasaEngine.Editor.Controls;
using CasaEngine.Editor.Controls.Cutscenes;
using CasaEngine.Editor.History;
using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Tests.ContentBrowser;
using MGUI.Core.UI;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>Reads a context menu without drawing it: its labels, in order, and its submenus.</summary>
internal static class CutsceneMenuProbe
{
    internal const string Separator = "-";

    internal static List<string> Labels(MGContextMenu menu)
    {
        var labels = new List<string>();
        foreach (var item in menu.Items)
        {
            labels.Add(Label(item));
        }

        return labels;
    }

    internal static string Label(MGContextMenuItem item)
        => item is MGContextMenuSeparator ? Separator : ((MGTextBlock)((MGContextMenuButton)item).MenuItemContent).Text;

    internal static MGContextMenuButton Button(MGContextMenu menu, string label)
    {
        foreach (var item in menu.Items)
        {
            if (item is MGContextMenuButton button && Label(button) == label)
            {
                return button;
            }
        }

        throw new Xunit.Sdk.XunitException($"The menu has no entry '{label}': {string.Join(", ", Labels(menu))}.");
    }

    internal static void Trigger(MGContextMenuButton button) => button.Action!(button);
}

/// <summary>
/// The context menu of the structure operations of a cutscene (P8): which entries a selected action gets, that an entry the
/// document would refuse is absent, that separators never follow each other or end the menu, and that an entry does its
/// operation. The menus are read without being drawn.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class CutsceneActionMenuTests : IDisposable
{
    private const string ContextId = CasaEngine.Editor.Workspaces.EditorPanelIds.CutsceneAssetDocumentPrefix + "menu";

    private static readonly EditorHistoryContext Context = new(EditorHistoryContextKind.Cutscene, ContextId);

    private readonly string _projectDirectory;
    private readonly string _previousProjectPath;
    private readonly List<CutsceneAssetInspectorPanel> _panels = [];

    public CutsceneActionMenuTests()
    {
        _projectDirectory = Path.Combine(Path.GetTempPath(), "casa-cutscene-menu-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectDirectory);

        _previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = _projectDirectory;
        AssetCatalog.ClearInternal();
    }

    public void Dispose()
    {
        foreach (var panel in _panels)
        {
            panel.Dispose();
        }

        EditorHistoryService.Current.IsSuspended = false;
        EditorHistoryService.Current.Remove(Context);
        EditorHistoryService.Current.Deactivate();
        EditorDirtyStateService.Current.Remove(Context);

        EngineEnvironment.ProjectPath = _previousProjectPath;
        AssetCatalog.ClearInternal();

        try
        {
            Directory.Delete(_projectDirectory, recursive: true);
        }
        catch (IOException)
        {
            // A temporary folder left behind is harmless.
        }
    }

    private sealed class Rig
    {
        public required ContentBrowserViewTestHarness Harness { get; init; }

        public required CutsceneAssetInspectorPanel Panel { get; init; }

        public required CutsceneEditSession Session { get; init; }

        public SequenceCutsceneActionData Root => (SequenceCutsceneActionData)Session.Asset.RootAction!;

        public List<string?> Results { get; } = [];

        public MGContextMenu? Menu() => CutsceneActionMenu.Create(Harness.Window, Panel, Results.Add);

        public List<string> Labels() => CutsceneMenuProbe.Labels(Menu()!);
    }

    private static JObject Container(string type, params JObject[] children)
        => new() { ["type"] = type, ["actions"] = new JArray(children) };

    private static JObject WaitJson(object seconds) => new() { ["type"] = "Wait", ["seconds"] = JToken.FromObject(seconds) };

    // Root: Wait, Parallel(Wait), Wait, FadeScreen.
    private static JObject DefaultRoot() => Container(
        "Sequence",
        WaitJson(1),
        Container("Parallel", WaitJson(2)),
        WaitJson(3),
        new JObject { ["type"] = "FadeScreen" });

    private Rig Open(JObject? rootAction = null)
    {
        var document = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "Menu",
            ["type"] = "CutsceneAsset",
            ["version"] = 1,
            ["schema_version"] = 1,
            ["root_action"] = rootAction ?? DefaultRoot(),
        };
        string fullPath = Path.Combine(_projectDirectory, "Menu.cutscene");
        File.WriteAllText(fullPath, document.ToString());

        Assert.True(CutsceneAssetInspectorPanel.TryLoadAsset(fullPath, out var session));
        Assert.NotNull(session);

        var harness = ContentBrowserViewTestHarness.Create(900, 900);
        var panel = new CutsceneAssetInspectorPanel(harness.Window);
        _panels.Add(panel);
        panel.SetHistoryContextId(ContextId);
        panel.LoadAsset(session, fullPath);
        harness.Window.SetContent(panel.CreateContent());
        return new Rig { Harness = harness, Panel = panel, Session = session };
    }

    private static void AssertNoBadSeparator(IReadOnlyList<string> labels)
    {
        Assert.NotEqual(CutsceneMenuProbe.Separator, labels[0]);
        Assert.NotEqual(CutsceneMenuProbe.Separator, labels[^1]);
        for (int index = 1; index < labels.Count; index++)
        {
            Assert.False(labels[index] == CutsceneMenuProbe.Separator && labels[index - 1] == CutsceneMenuProbe.Separator);
        }
    }

    [Fact]
    public void ALeafInTheMiddle_GetsEveryEntryThatAppliesToIt()
    {
        var rig = Open();
        rig.Panel.SelectAction(rig.Root.Actions[2]);

        var labels = rig.Labels();

        Assert.Equal(
            ["Add", "Wrap in", "-", "Move up", "Move down", "-", "Cut", "Copy", "Duplicate", "-", "Delete"],
            labels);
        AssertNoBadSeparator(labels);
    }

    [Fact]
    public void AContainer_GetsUngroup()
    {
        var rig = Open();
        rig.Panel.SelectAction(rig.Root.Actions[1]);

        var labels = rig.Labels();

        Assert.Equal(
            ["Add", "Wrap in", "Ungroup", "-", "Move up", "Move down", "-", "Cut", "Copy", "Duplicate", "-", "Delete"],
            labels);
    }

    [Fact]
    public void TheRoot_CanOnlyBeAddedTo_WrappedAndCopied()
    {
        var rig = Open();
        rig.Panel.SelectAction(rig.Root);

        var labels = rig.Labels();

        Assert.Equal(["Add", "Wrap in", "-", "Copy"], labels);
        AssertNoBadSeparator(labels);
    }

    [Fact]
    public void TheFirstAndTheLastActionOfAContainer_LackMoveUpAndMoveDownRespectively()
    {
        var rig = Open();

        rig.Panel.SelectAction(rig.Root.Actions[0]);
        var first = rig.Labels();
        Assert.DoesNotContain("Move up", first);
        Assert.Contains("Move down", first);

        rig.Panel.SelectAction(rig.Root.Actions[3]);
        var last = rig.Labels();
        Assert.Contains("Move up", last);
        Assert.DoesNotContain("Move down", last);
    }

    [Fact]
    public void Paste_IsPresentOnlyWhenTheClipboardHoldsSomething()
    {
        var rig = Open();
        rig.Panel.SelectAction(rig.Root.Actions[2]);
        Assert.DoesNotContain("Paste", rig.Labels());

        Assert.Null(rig.Panel.CopySelectedAction());

        var labels = rig.Labels();
        Assert.Equal(
            ["Add", "Wrap in", "-", "Move up", "Move down", "-", "Cut", "Copy", "Paste", "Duplicate", "-", "Delete"],
            labels);
    }

    [Fact]
    public void WithoutSelection_OnlyAddAndPasteAreOffered()
    {
        var rig = Open();
        Assert.Null(rig.Panel.SelectedAction);
        Assert.Equal(["Add"], rig.Labels());

        rig.Panel.SelectAction(rig.Root.Actions[0]);
        Assert.Null(rig.Panel.CopySelectedAction());
        rig.Panel.SelectAction(null);

        var labels = rig.Labels();
        Assert.Equal(["Add", "-", "Paste"], labels);
    }

    [Fact]
    public void AnActionThatHoldsALoadLoss_HasNeitherCutNorCopy()
    {
        // The string "2" is read as a number and rewritten as a number: the loader notes the loss on that Wait.
        var rig = Open(Container("Sequence", WaitJson(1), Container("Parallel", WaitJson("2"))));
        Assert.NotEmpty(rig.Session.LoadLosses);

        rig.Panel.SelectAction(rig.Root.Actions[1]);
        var lossy = rig.Labels();
        Assert.DoesNotContain("Cut", lossy);
        Assert.DoesNotContain("Copy", lossy);
        Assert.DoesNotContain("Duplicate", lossy);
        Assert.Contains("Delete", lossy);
        AssertNoBadSeparator(lossy);

        rig.Panel.SelectAction(rig.Root.Actions[0]);
        var clean = rig.Labels();
        Assert.Contains("Cut", clean);
        Assert.Contains("Copy", clean);
    }

    [Fact]
    public void TheAddEntry_HasASubmenuPerCategory_ThenOneEntryPerType()
    {
        var rig = Open();
        var add = CutsceneMenuProbe.Button(rig.Menu()!, "Add");
        Assert.NotNull(add.Submenu);
        Assert.True(add.Submenu!.IsSubmenu);

        var categories = new List<string>();
        int types = 0;
        foreach (var kind in CutsceneActionCatalog.Kinds)
        {
            if (!categories.Contains(kind.Category))
            {
                categories.Add(kind.Category);
            }
        }

        Assert.Equal(categories, CutsceneMenuProbe.Labels(add.Submenu));
        foreach (var category in categories)
        {
            var categoryButton = CutsceneMenuProbe.Button(add.Submenu, category);
            Assert.NotNull(categoryButton.Submenu);
            Assert.True(categoryButton.Submenu!.IsSubmenu);
            types += categoryButton.Submenu.Items.Count;
        }

        Assert.Equal(CutsceneActionCatalog.Kinds.Count, types);
    }

    [Fact]
    public void TheAddEntry_AddsTheChosenType_WhereTheInsertionRuleSays()
    {
        var rig = Open();
        var leaf = rig.Root.Actions[2];
        rig.Panel.SelectAction(leaf);

        var add = CutsceneMenuProbe.Button(rig.Menu()!, "Add");
        var sprites = CutsceneMenuProbe.Button(add.Submenu!, CutsceneActionCatalog.SpritesCategory);
        CutsceneMenuProbe.Trigger(CutsceneMenuProbe.Button(sprites.Submenu!, CutsceneActionTypes.TintSprite));

        Assert.Equal(5, rig.Root.Actions.Count);
        Assert.Same(leaf, rig.Root.Actions[2]);
        Assert.IsType<TintSpriteCutsceneActionData>(rig.Root.Actions[3]);
        Assert.Same(rig.Root.Actions[3], rig.Panel.SelectedAction);
        Assert.Equal([(string?)null], rig.Results);
    }

    [Fact]
    public void TheWrapEntry_WrapsTheSelectionInTheChosenContainer()
    {
        var rig = Open();
        var leaf = rig.Root.Actions[2];
        rig.Panel.SelectAction(leaf);

        var wrap = CutsceneMenuProbe.Button(rig.Menu()!, "Wrap in");
        Assert.Equal(["Sequence", "Parallel", "Repeat"], CutsceneMenuProbe.Labels(wrap.Submenu!));
        CutsceneMenuProbe.Trigger(CutsceneMenuProbe.Button(wrap.Submenu!, "Repeat"));

        var wrapper = Assert.IsType<RepeatCutsceneActionData>(rig.Root.Actions[2]);
        Assert.Same(leaf, Assert.Single(wrapper.Actions));
        Assert.Same(wrapper, rig.Panel.SelectedAction);
    }

    [Fact]
    public void TheOtherEntries_DoTheirOperation()
    {
        var rig = Open();
        var first = rig.Root.Actions[0];
        var container = rig.Root.Actions[1];
        var third = rig.Root.Actions[2];

        // Move down swaps the first action with the container.
        rig.Panel.SelectAction(first);
        CutsceneMenuProbe.Trigger(CutsceneMenuProbe.Button(rig.Menu()!, "Move down"));
        Assert.Same(container, rig.Root.Actions[0]);
        Assert.Same(first, rig.Root.Actions[1]);

        // Move up puts it back.
        CutsceneMenuProbe.Trigger(CutsceneMenuProbe.Button(rig.Menu()!, "Move up"));
        Assert.Same(first, rig.Root.Actions[0]);

        // Copy, then Paste after the third action: a new instance of the same type.
        rig.Panel.SelectAction(third);
        CutsceneMenuProbe.Trigger(CutsceneMenuProbe.Button(rig.Menu()!, "Copy"));
        CutsceneMenuProbe.Trigger(CutsceneMenuProbe.Button(rig.Menu()!, "Paste"));
        Assert.Equal(5, rig.Root.Actions.Count);
        Assert.IsType<WaitCutsceneActionData>(rig.Root.Actions[3]);
        Assert.NotSame(third, rig.Root.Actions[3]);

        // Duplicate the pasted copy, ungroup the container, then cut and delete.
        CutsceneMenuProbe.Trigger(CutsceneMenuProbe.Button(rig.Menu()!, "Duplicate"));
        Assert.Equal(6, rig.Root.Actions.Count);

        rig.Panel.SelectAction(container);
        CutsceneMenuProbe.Trigger(CutsceneMenuProbe.Button(rig.Menu()!, "Ungroup"));
        Assert.Equal(6, rig.Root.Actions.Count);
        Assert.DoesNotContain(container, rig.Root.Actions);

        rig.Panel.SelectAction(rig.Root.Actions[0]);
        CutsceneMenuProbe.Trigger(CutsceneMenuProbe.Button(rig.Menu()!, "Cut"));
        Assert.Equal(5, rig.Root.Actions.Count);
        Assert.DoesNotContain(first, rig.Root.Actions);

        rig.Panel.SelectAction(rig.Root.Actions[0]);
        CutsceneMenuProbe.Trigger(CutsceneMenuProbe.Button(rig.Menu()!, "Delete"));
        Assert.Equal(4, rig.Root.Actions.Count);

        Assert.All(rig.Results, Assert.Null);
        Assert.Equal(8, rig.Results.Count);
    }

    [Fact]
    public void AnEntryWhoseOperationIsRefusedAfterTheMenuWasBuilt_ReportsTheRefusal()
    {
        var rig = Open();
        var leaf = rig.Root.Actions[0];
        rig.Panel.SelectAction(leaf);
        var delete = CutsceneMenuProbe.Button(rig.Menu()!, "Delete");

        // The selection moved to the root before the click: the operation is refused, and the message is passed on.
        rig.Panel.SelectAction(rig.Root);
        CutsceneMenuProbe.Trigger(delete);

        Assert.Equal(4, rig.Root.Actions.Count);
        Assert.NotNull(Assert.Single(rig.Results));
    }

    [Fact]
    public void ADocumentWithoutAnAsset_OffersNothing()
    {
        var harness = ContentBrowserViewTestHarness.Create(600, 600);
        var panel = new CutsceneAssetInspectorPanel(harness.Window);
        _panels.Add(panel);

        Assert.Null(CutsceneActionMenu.Create(harness.Window, panel, null));
    }
}
