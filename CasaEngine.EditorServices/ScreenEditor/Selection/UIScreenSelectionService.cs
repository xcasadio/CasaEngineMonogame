using CasaEngine.EditorServices.ScreenEditor.DocumentModel;

namespace CasaEngine.EditorServices.ScreenEditor.Selection;

/// <summary>
/// Centralises the selection state for the screen editor.
/// Supports both single and multi-node selection.
/// All editor panels (hierarchy, inspector, preview) share this single instance.
/// </summary>
public sealed class UIScreenSelectionService
{
    private DocumentNodeId? _selectedNodeId;
    private readonly HashSet<DocumentNodeId> _multiSelection = new();

    /// <summary>Fired whenever the selected node changes. Argument is null when nothing is selected.</summary>
    public event Action<DocumentNodeId?>? SelectionChanged;

    /// <summary>
    /// Fired when the multi-selection set changes.
    /// Provides the complete current selection (empty = nothing selected).
    /// </summary>
    public event Action<IReadOnlyCollection<DocumentNodeId>>? MultiSelectionChanged;

    /// <summary>Currently selected (primary) node id, or null.</summary>
    public DocumentNodeId? SelectedNodeId => _selectedNodeId;

    /// <summary>All currently selected node IDs (includes the primary selection, if any).</summary>
    public IReadOnlyCollection<DocumentNodeId> MultiSelection => _multiSelection;

    // ─── Single-select (primary) ──────────────────────────────────────────

    /// <summary>
    /// Sets the primary selection to <paramref name="nodeId"/>.
    /// Passing null clears the selection.  Fires <see cref="SelectionChanged"/> only when the value changes.
    /// Also replaces the multi-selection with just this one node.
    /// </summary>
    public void Select(DocumentNodeId? nodeId)
    {
        if (_selectedNodeId == nodeId && _multiSelection.Count <= 1)
        {
            return;
        }

        _selectedNodeId = nodeId;
        _multiSelection.Clear();
        if (nodeId.HasValue)
        {
            _multiSelection.Add(nodeId.Value);
        }

        SelectionChanged?.Invoke(_selectedNodeId);
        MultiSelectionChanged?.Invoke(_multiSelection);
    }

    /// <summary>Clears the current selection.</summary>
    public void ClearSelection() => Select(null);

    // ─── Multi-select ─────────────────────────────────────────────────────

    /// <summary>
    /// Adds <paramref name="nodeId"/> to the multi-selection without changing the primary selection.
    /// Fires <see cref="MultiSelectionChanged"/>.
    /// </summary>
    public void AddToSelection(DocumentNodeId nodeId)
    {
        if (_multiSelection.Add(nodeId))
        {
            MultiSelectionChanged?.Invoke(_multiSelection);
        }
    }

    /// <summary>
    /// Removes <paramref name="nodeId"/> from the multi-selection.
    /// If it was the primary selection, the primary selection is cleared.
    /// Fires <see cref="SelectionChanged"/> and/or <see cref="MultiSelectionChanged"/> as appropriate.
    /// </summary>
    public void RemoveFromSelection(DocumentNodeId nodeId)
    {
        var wasMulti = _multiSelection.Remove(nodeId);
        if (!wasMulti)
        {
            return;
        }

        if (_selectedNodeId == nodeId)
        {
            _selectedNodeId = _multiSelection.Count > 0
                ? _multiSelection.First()
                : null;
            SelectionChanged?.Invoke(_selectedNodeId);
        }

        MultiSelectionChanged?.Invoke(_multiSelection);
    }

    /// <summary>
    /// Toggles <paramref name="nodeId"/> in the multi-selection.
    /// If it was the only selected node, clears the selection.
    /// Fires events accordingly.
    /// </summary>
    public void ToggleSelection(DocumentNodeId nodeId)
    {
        if (_multiSelection.Contains(nodeId))
        {
            RemoveFromSelection(nodeId);
        }
        else
        {
            AddToSelection(nodeId);
            if (!_selectedNodeId.HasValue)
            {
                _selectedNodeId = nodeId;
                SelectionChanged?.Invoke(_selectedNodeId);
            }
        }
    }

    /// <summary>Replaces the entire multi-selection with <paramref name="nodeIds"/>.</summary>
    public void SetMultiSelection(IEnumerable<DocumentNodeId> nodeIds)
    {
        _multiSelection.Clear();
        _selectedNodeId = null;

        foreach (var id in nodeIds)
        {
            _multiSelection.Add(id);
            if (!_selectedNodeId.HasValue)
            {
                _selectedNodeId = id;
            }
        }

        SelectionChanged?.Invoke(_selectedNodeId);
        MultiSelectionChanged?.Invoke(_multiSelection);
    }
}
