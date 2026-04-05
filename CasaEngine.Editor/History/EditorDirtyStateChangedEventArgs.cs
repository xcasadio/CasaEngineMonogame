using System;

namespace CasaEngine.Editor.History;

public sealed class EditorDirtyStateChangedEventArgs : EventArgs
{
    public EditorDirtyStateChangedEventArgs(EditorHistoryContext context, bool isDirty, EditorDirtyStateChangeKind changeKind)
    {
        Context = context;
        IsDirty = isDirty;
        ChangeKind = changeKind;
    }

    public EditorHistoryContext Context { get; }

    public bool IsDirty { get; }

    public EditorDirtyStateChangeKind ChangeKind { get; }
}