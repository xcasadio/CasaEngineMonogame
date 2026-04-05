namespace CasaEngine.Editor.History;

public enum EditorDirtyStateChangeKind
{
    Execute,
    Undo,
    Redo,
    Clear,
    Save,
    Reset,
}