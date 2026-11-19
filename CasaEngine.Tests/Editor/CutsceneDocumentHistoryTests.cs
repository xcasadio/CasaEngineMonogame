using CasaEngine.Editor;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Workspaces;
using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Cutscenes;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>
/// The history context of a cutscene document (ADR-0134): the document kind maps to it, so that the global undo and redo
/// reach the active cutscene tab. The editor history and dirty state are global, hence the serialized collection.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class CutsceneDocumentHistoryTests : IDisposable
{
    private static readonly string PanelId = $"{EditorPanelIds.CutsceneAssetDocumentPrefix}deadbeef";
    private static readonly EditorHistoryContext Context = new(EditorHistoryContextKind.Cutscene, PanelId);

    public CutsceneDocumentHistoryTests()
    {
        // The dirty state service listens to the history from its creation, as it does from the start of the editor.
        _ = EditorDirtyStateService.Current;
    }

    public void Dispose()
    {
        EditorHistoryService.Current.Remove(Context);
        EditorHistoryService.Current.Deactivate();
        EditorDirtyStateService.Current.Remove(Context);
    }

    [Fact]
    public void FromDocument_WithCutsceneDocument_ReturnsCutsceneHistoryContext()
    {
        var document = new EditorDocumentContext(EditorDocumentKind.Cutscene, PanelId, "Intro", null);

        var context = EditorHistoryContext.FromDocument(document);

        Assert.Equal(EditorHistoryContextKind.Cutscene, context.Kind);
        Assert.Equal(PanelId, context.Id);
        Assert.False(context.IsEmpty);
    }

    [Fact]
    public void FromDocument_WithCutsceneDocumentWithoutId_ReturnsEmptyContext()
    {
        var document = new EditorDocumentContext(EditorDocumentKind.Cutscene, string.Empty, "Intro", null);

        Assert.Equal(EditorHistoryContext.Empty, EditorHistoryContext.FromDocument(document));
    }

    [Fact]
    public void GlobalUndoAndRedo_ReachTheActiveCutsceneDocument()
    {
        var session = CutsceneEditSession.Load(new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "Intro",
            ["type"] = "CutsceneAsset",
            ["version"] = 1,
            ["schema_version"] = 1,
            ["root_action"] = new JObject { ["type"] = "Wait", ["seconds"] = 1.0 },
        });
        var wait = Assert.IsType<WaitCutsceneActionData>(session.Asset.RootAction);
        var property = CutsceneActionPropertyEditor.FindProperty(wait, "seconds")!;

        var document = new EditorDocumentContext(EditorDocumentKind.Cutscene, PanelId, "Intro", null);
        EditorHistoryService.Current.SetActiveContext(EditorHistoryContext.FromDocument(document));
        EditorHistoryService.Current.Execute(Context, new SetCutsceneActionPropertyCommand(session, wait, property, 2.5f));

        Assert.Equal(2.5f, wait.Seconds);
        Assert.True(EditorDirtyStateService.Current.IsDirty(Context));
        Assert.True(EditorHistoryService.Current.CanUndo);

        Assert.True(EditorHistoryService.Current.Undo());

        Assert.Equal(1f, wait.Seconds);
        Assert.False(EditorDirtyStateService.Current.IsDirty(Context));

        Assert.True(EditorHistoryService.Current.Redo());

        Assert.Equal(2.5f, wait.Seconds);
        Assert.True(EditorDirtyStateService.Current.IsDirty(Context));
    }
}
