using CasaEngine.Editor.Controls;
using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Selection;

namespace CasaEngine.Editor.Workspaces;

public sealed class UIScreenWorkspaceContext
{
    public UIScreenWorkspaceContext(UIScreenSelectionService selectionService)
    {
        SelectionService = selectionService;
    }

    public UIScreenSelectionService SelectionService { get; }

    public UIScreenPreviewPanel? ActivePreviewPanel { get; private set; }

    public UIScreenDocument? ActiveDocument => ActivePreviewPanel?.CurrentDocument;

    public void SetActivePreviewPanel(UIScreenPreviewPanel? previewPanel)
    {
        ActivePreviewPanel = previewPanel;
    }
}