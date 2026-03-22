using CasaEngine.EditorServices.ScreenEditor.DocumentModel;

namespace CasaEngine.EditorServices.ScreenEditor.Selection;

/// <summary>
/// Centralises the selection state for the screen editor.
/// All editor panels (hierarchy, inspector, preview) share this single instance.
/// </summary>
public sealed class UIScreenSelectionService
{
    private DocumentNodeId? _selectedNodeId;

    /// <summary>Fired whenever the selected node changes. Argument is null when nothing is selected.</summary>
    public event Action<DocumentNodeId?>? SelectionChanged;

    /// <summary>Currently selected node id, or null.</summary>
    public DocumentNodeId? SelectedNodeId => _selectedNodeId;

    /// <summary>
    /// Sets the selection to <paramref name="nodeId"/>.
    /// Passing null clears the selection.
    /// Fires <see cref="SelectionChanged"/> only when the value actually changes.
    /// </summary>
    public void Select(DocumentNodeId? nodeId)
    {
        if (_selectedNodeId == nodeId)
        {
            return;
        }

        _selectedNodeId = nodeId;
        SelectionChanged?.Invoke(_selectedNodeId);
    }

    /// <summary>Clears the current selection.</summary>
    public void ClearSelection() => Select(null);
}
