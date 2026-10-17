using System.Runtime.CompilerServices;
using CasaEngine.Core.Logging;
using CasaEngine.Editor;
using CasaEngine.Editor.Controls;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Workspaces;
using CasaEngine.Engine.Plugins;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scripting;
using CasaEngine.Tests.Scripting;
using CasaEngine.Tests.UI;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Editor;

/// <summary>
/// Plan T3.2 (D10): the inspector shows the script of the selected entity or world again, as a list of the full names
/// of the script classes of the registered assemblies, with a warning element and the resolution message when a
/// non-empty name does not resolve. The real path is exercised: under the editor preview a loaded world keeps an
/// entity whose script is missing (T3.1), and the inspector is built on a headless desktop, without
/// <see cref="EditorIcons.Load"/> (no texture: the warning falls back to a "!" text).
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class EntityDetailsScriptRowTests : IDisposable
{
    private static readonly EditorHistoryContext Context = new(EditorHistoryContextKind.World, "entity-details-script-row-tests");

    private static readonly string ResolvedName = typeof(StatefulEntityScriptStub).FullName!;
    private static readonly string SecondResolvedName = typeof(SpawningWorldScriptStub).FullName!;

    public EntityDetailsScriptRowTests()
    {
        EditorHistoryService.Current.Remove(Context);
    }

    public void Dispose()
    {
        EditorHistoryService.Current.IsSuspended = false;
        EditorHistoryService.Current.Remove(Context);
        EditorHistoryService.Current.Deactivate();
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "casa-script-row-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Register(ScriptAssemblyHost host, string assemblyPath) =>
        ElementFactory.RegisterScriptAssembly(host.Load(assemblyPath));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Unregister(ScriptAssemblyHost host)
    {
        if (host.LoadedAssembly != null)
        {
            ElementFactory.UnregisterScriptAssembly(host.LoadedAssembly);
        }
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

    private static List<string> ItemsOf(EntityDetailsPanel panel) => panel.ScriptCombo.ItemsSource.ToList();

    /// <summary>A preview world loaded from a document, as the editor opens it: the entities are in <see cref="World.Entities"/>.</summary>
    private static World LoadPreviewWorld(string worldScript, params Entity[] inlineEntities)
    {
        var world = ScriptCreationByFullNameTests.NewWorld(
            GameplayExecutionPolicies.EditorPreview, ScriptCreationByFullNameTests.NewAssets());
        ScriptCreationByFullNameTests.LoadWorldDocument(
            world,
            worldScript,
            inlineEntities.Select(ScriptCreationByFullNameTests.InlineReference).ToArray());

        var logger = new ScriptCreationByFullNameTests.CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            ScriptCreationByFullNameTests.LoadContent(world);
        }
        finally
        {
            Logs.Close();
        }

        return world;
    }

    // ---- (i) the list ----

    [Fact]
    public void ScriptChoices_AreTheFullNamesOfTheTopLevelPublicProxyTypesOfTheRegisteredAssembliesOnly()
    {
        string directory = CreateTempDirectory();
        var registered = new ScriptAssemblyHost();
        var neverRegistered = new ScriptAssemblyHost();
        try
        {
            Register(registered, ScriptAssemblyCompiler.CompileScriptAssembly(directory, "RowRegistered", version: 1));
            string otherDirectory = Path.Combine(directory, "other");
            neverRegistered.Load(ScriptAssemblyCompiler.CompileScriptAssembly(otherDirectory, "RowNeverRegistered", version: 1));

            var names = ElementFactory.GetScriptTypeNames(typeof(GameplayProxy));

            Assert.Contains("TestScripts.TestScriptProxy", names);
            Assert.Contains(ResolvedName, names);
            Assert.DoesNotContain(typeof(CustomPlayerControllerStub).FullName!, names); // does not derive from GameplayProxy
            Assert.DoesNotContain(typeof(ScriptResolutionOuterStub.NestedProxy).FullName!, names); // nested
            Assert.DoesNotContain(typeof(ScriptResolutionOuterStub.NestedProxy).FullName!.Replace('+', '.'), names);
            Assert.DoesNotContain(typeof(ScriptResolutionAbstractStub).FullName!, names); // abstract
            Assert.DoesNotContain(typeof(GameplayProxy).FullName!, names); // the base itself
            Assert.All(names, name => Assert.False(string.IsNullOrWhiteSpace(name)));
        }
        finally
        {
            Unregister(registered);
            registered.Unload();
            neverRegistered.Unload();
            DeleteDirectory(directory);
        }

        // After the unregistration the dynamic type is gone (the test assembly stays registered).
        Assert.DoesNotContain("TestScripts.TestScriptProxy", ElementFactory.GetScriptTypeNames(typeof(GameplayProxy)));
    }

    [Fact]
    public void ScriptChoices_ListTheCurrentNameEvenWhenAbsent_AndNeverAnEmptyEntry()
    {
        var withMissing = EntityDetailsPanel.GetScriptChoices("Nope.MissingScript");
        Assert.Contains("Nope.MissingScript", withMissing);
        Assert.Contains(ResolvedName, withMissing);

        var withResolved = EntityDetailsPanel.GetScriptChoices(ResolvedName);
        Assert.Single(withResolved, name => name == ResolvedName);

        foreach (var empty in new[] { "", "   ", null })
        {
            var names = EntityDetailsPanel.GetScriptChoices(empty!);
            Assert.All(names, name => Assert.False(string.IsNullOrWhiteSpace(name)));
            Assert.Contains(ResolvedName, names);
        }
    }

    // ---- (ii) the display state of a name ----

    [Fact]
    public void ScriptProblem_IsNoneForAResolvedOrAnEmptyName()
    {
        Assert.False(EntityDetailsPanel.TryGetScriptProblem(ResolvedName, out var resolvedMessage));
        Assert.Equal(string.Empty, resolvedMessage);

        foreach (var empty in new[] { "", " ", null })
        {
            Assert.False(EntityDetailsPanel.TryGetScriptProblem(empty!, out var message));
            Assert.Equal(string.Empty, message);
        }
    }

    [Fact]
    public void ScriptProblem_IsReportedForANotFoundNameAShortNameAndATypeThatIsNotAProxy()
    {
        Assert.True(EntityDetailsPanel.TryGetScriptProblem("Nope.MissingScript", out var missing));
        Assert.Contains("Nope.MissingScript", missing);
        Assert.Contains("script_class_name", missing);

        // A short name is refused with the full name as the hint.
        Assert.True(EntityDetailsPanel.TryGetScriptProblem(nameof(StatefulEntityScriptStub), out var shortName));
        Assert.Contains(ResolvedName, shortName);

        // A type of the registered assembly that does not derive from GameplayProxy.
        Assert.True(EntityDetailsPanel.TryGetScriptProblem(typeof(CustomPlayerControllerStub).FullName!, out var notAProxy));
        Assert.Contains(typeof(CustomPlayerControllerStub).FullName!, notAProxy);
    }

    [Fact]
    public void ScriptProblem_IsReportedForAnAmbiguousName()
    {
        string directory1 = CreateTempDirectory();
        string directory2 = CreateTempDirectory();
        var host1 = new ScriptAssemblyHost();
        var host2 = new ScriptAssemblyHost();
        try
        {
            Register(host1, ScriptAssemblyCompiler.CompileScriptAssembly(directory1, "RowAmbiguousFirst", version: 1));
            Register(host2, ScriptAssemblyCompiler.CompileScriptAssembly(directory2, "RowAmbiguousSecond", version: 2));

            Assert.True(EntityDetailsPanel.TryGetScriptProblem("TestScripts.TestScriptProxy", out var message));
            Assert.Contains("ambiguous", message);
        }
        finally
        {
            Unregister(host1);
            Unregister(host2);
            host1.Unload();
            host2.Unload();
            DeleteDirectory(directory1);
            DeleteDirectory(directory2);
        }
    }

    // ---- (iii) the real path ----

    [Fact]
    public void Preview_EntityWithAMissingScript_ShowsTheValue_TheWarningElementAndTheMessage()
    {
        var world = LoadPreviewWorld(string.Empty, ScriptCreationByFullNameTests.InlineEntity("faulty", ScriptCreationByFullNameTests.FaultyScriptName));
        var entity = Assert.Single(world.Entities, e => e.Name == "faulty");
        Assert.Null(entity.GameplayProxy); // kept by T3.1, without its script

        var panel = OpenPanel();
        panel.SetSelectedEntity(entity);

        Assert.NotNull(panel.ScriptCombo);
        Assert.Equal(ScriptCreationByFullNameTests.FaultyScriptName, panel.ScriptCombo.SelectedItem);
        Assert.Contains(ScriptCreationByFullNameTests.FaultyScriptName, ItemsOf(panel));
        Assert.NotNull(panel.ScriptWarningElement); // no EditorIcons.Load: the text fallback
        Assert.Equal("!", Assert.IsType<MGTextBlock>(panel.ScriptWarningElement).Text);
        Assert.NotNull(panel.ScriptMessageText);
        Assert.Contains(ScriptCreationByFullNameTests.FaultyScriptName, panel.ScriptMessageText.Text);
        Assert.Contains("script_class_name", panel.ScriptMessageText.Text);
    }

    [Fact]
    public void Preview_EntityWithAResolvedScript_ShowsNoWarning()
    {
        var world = LoadPreviewWorld(string.Empty, ScriptCreationByFullNameTests.InlineEntity("fine", ResolvedName));
        var entity = Assert.Single(world.Entities, e => e.Name == "fine");
        Assert.NotNull(entity.GameplayProxy);

        var panel = OpenPanel();
        panel.SetSelectedEntity(entity);

        Assert.Equal(ResolvedName, panel.ScriptCombo.SelectedItem);
        Assert.Null(panel.ScriptWarningElement);
        Assert.Null(panel.ScriptMessageText);
    }

    [Fact]
    public void Preview_EntityWithoutScript_ShowsNoValue_NoWarning_AndTheFirstClassCanBeChosen()
    {
        var world = LoadPreviewWorld(string.Empty, ScriptCreationByFullNameTests.InlineEntity("plain", string.Empty));
        var entity = Assert.Single(world.Entities, e => e.Name == "plain");

        var panel = OpenPanel();
        panel.SetSelectedEntity(entity);

        // No selection, not the first item of the list (CreateStringCombo would select it).
        Assert.NotNull(panel.ScriptCombo);
        Assert.Null(panel.ScriptCombo.SelectedItem);
        Assert.Equal(-1, panel.ScriptCombo.SelectedIndex);
        Assert.Null(panel.ScriptWarningElement);
        Assert.Null(panel.ScriptMessageText);
        Assert.True(string.IsNullOrEmpty(entity.GameplayProxyClassName));

        string first = ItemsOf(panel)[0];
        panel.ScriptCombo.SelectedItem = first;

        Assert.Equal(first, entity.GameplayProxyClassName);
    }

    [Fact]
    public void Preview_WorldWithAMissingScript_ShowsTheValue_TheWarningElementAndTheMessage()
    {
        var world = LoadPreviewWorld(ScriptCreationByFullNameTests.FaultyScriptName);
        Assert.Null(world.GameplayProxy);

        var panel = OpenPanel();
        panel.SetSelectedWorld(world);

        Assert.Equal(ScriptCreationByFullNameTests.FaultyScriptName, panel.ScriptCombo.SelectedItem);
        Assert.NotNull(panel.ScriptWarningElement);
        Assert.NotNull(panel.ScriptMessageText);
        Assert.Contains(ScriptCreationByFullNameTests.FaultyScriptName, panel.ScriptMessageText.Text);
    }

    [Fact]
    public void World_WithoutScript_ShowsNoValue_AndTheSameListAsAnEntity()
    {
        var world = LoadPreviewWorld(string.Empty, ScriptCreationByFullNameTests.InlineEntity("plain", string.Empty));
        var entity = Assert.Single(world.Entities, e => e.Name == "plain");

        var panel = OpenPanel();
        panel.SetSelectedWorld(world);

        Assert.Null(panel.ScriptCombo.SelectedItem);
        Assert.Null(panel.ScriptWarningElement);
        var worldItems = ItemsOf(panel);

        panel.SetSelectedEntity(entity);
        Assert.Equal(worldItems, ItemsOf(panel));

        panel.SetSelectedWorld(world);
        string first = worldItems[0];
        panel.ScriptCombo.SelectedItem = first;
        Assert.Equal(first, world.GameplayProxyClassName);
    }

    // ---- (iv) the choice ----

    [Fact]
    public void ChoosingAScript_ChangesTheName_IsUndoable_AndClearsTheWarning()
    {
        var world = LoadPreviewWorld(string.Empty, ScriptCreationByFullNameTests.InlineEntity("faulty", ScriptCreationByFullNameTests.FaultyScriptName));
        var entity = Assert.Single(world.Entities, e => e.Name == "faulty");

        var panel = OpenPanel();
        panel.SetSelectedEntity(entity);
        Assert.NotNull(panel.ScriptWarningElement);

        panel.ScriptCombo.SelectedItem = SecondResolvedName;

        Assert.Equal(SecondResolvedName, entity.GameplayProxyClassName);
        Assert.True(EditorHistoryService.Current.TryGet(Context, out var stack));
        Assert.True(stack.CanUndo);
        Assert.Equal("Change Entity Script", stack.UndoDescription);

        // The details are rebuilt: the new name resolves, no warning any more.
        Assert.Equal(SecondResolvedName, panel.ScriptCombo.SelectedItem);
        Assert.Null(panel.ScriptWarningElement);
        Assert.Null(panel.ScriptMessageText);

        EditorHistoryService.Current.SetActiveContext(Context);
        Assert.True(EditorHistoryService.Current.Undo());

        Assert.Equal(ScriptCreationByFullNameTests.FaultyScriptName, entity.GameplayProxyClassName);
        Assert.Equal(ScriptCreationByFullNameTests.FaultyScriptName, panel.ScriptCombo.SelectedItem);
        Assert.NotNull(panel.ScriptWarningElement);
    }

    [Fact]
    public void ChoosingAScript_ForTheWorld_IsUndoable()
    {
        var world = LoadPreviewWorld(string.Empty);

        var panel = OpenPanel();
        panel.SetSelectedWorld(world);

        panel.ScriptCombo.SelectedItem = ResolvedName;

        Assert.Equal(ResolvedName, world.GameplayProxyClassName);
        Assert.True(EditorHistoryService.Current.TryGet(Context, out var stack));
        Assert.Equal("Change World Script", stack.UndoDescription);

        EditorHistoryService.Current.SetActiveContext(Context);
        Assert.True(EditorHistoryService.Current.Undo());
        Assert.True(string.IsNullOrEmpty(world.GameplayProxyClassName));
        Assert.Null(panel.ScriptCombo.SelectedItem);
    }

    [Fact]
    public void OtherListsOfThePanel_StillHaveASelection()
    {
        // CreateStringCombo is unchanged, and the Script list is the only one that may show no selection.
        var world = LoadPreviewWorld(string.Empty, ScriptCreationByFullNameTests.InlineEntity("plain", string.Empty));
        var entity = Assert.Single(world.Entities, e => e.Name == "plain");

        var panel = OpenPanel();
        panel.SetSelectedEntity(entity);

        // The Policy Source, Mobility, Tick, Spatial and Render lists of the same page keep their selection.
        var policyCombos = panel.CreateContent().TraverseVisualTree()
            .OfType<MGComboBox<string>>()
            .Where(combo => !ReferenceEquals(combo, panel.ScriptCombo))
            .ToList();
        Assert.NotEmpty(policyCombos);
        Assert.All(policyCombos, combo => Assert.NotNull(combo.SelectedItem));
    }

    [Fact]
    public void RefreshScriptRow_RecomputesTheListAfterAScriptAssemblyChange()
    {
        var world = LoadPreviewWorld(string.Empty, ScriptCreationByFullNameTests.InlineEntity("late", "TestScripts.TestScriptProxy"));
        var entity = Assert.Single(world.Entities, e => e.Name == "late");

        var panel = OpenPanel();
        panel.SetSelectedEntity(entity);
        Assert.NotNull(panel.ScriptWarningElement); // the assembly is not registered yet

        string directory = CreateTempDirectory();
        var host = new ScriptAssemblyHost();
        try
        {
            Register(host, ScriptAssemblyCompiler.CompileScriptAssembly(directory, "RowReload", version: 1));

            panel.RefreshScriptRow();

            Assert.Null(panel.ScriptWarningElement);
            Assert.Null(panel.ScriptMessageText);
            Assert.Equal("TestScripts.TestScriptProxy", panel.ScriptCombo.SelectedItem);
        }
        finally
        {
            Unregister(host);
            host.Unload();
            DeleteDirectory(directory);
        }
    }
}
