using System;

namespace CasaEngine.Editor;

public sealed class EditorContextService
{
    public static EditorContextService Current { get; } = new();

    public event Action<EditorDocumentContext?>? ActiveDocumentChanged;

    public event Action<EditorSelectionState>? SelectionChanged;

    public EditorDocumentContext? ActiveDocument { get; private set; }

    public EditorSelectionState Selection { get; private set; } = EditorSelectionState.Empty;

    private EditorContextService()
    {
    }

    public void SetActiveDocument(EditorDocumentContext? document)
    {
        if (Equals(ActiveDocument, document))
        {
            return;
        }

        ActiveDocument = document;
        ActiveDocumentChanged?.Invoke(ActiveDocument);
    }

    public void SetSelection(EditorSelectionState selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        if (Equals(Selection, selection))
        {
            return;
        }

        Selection = selection;
        SelectionChanged?.Invoke(Selection);
    }

    public void ClearSelection()
    {
        SetSelection(EditorSelectionState.Empty);
    }
}