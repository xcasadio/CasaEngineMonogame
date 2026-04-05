using System;
using CasaEngine.EditorServices.History;

namespace CasaEngine.Editor.History;

public sealed class EditorHistoryChangedEventArgs : EventArgs
{
    public EditorHistoryChangedEventArgs(EditorHistoryContext context, EditorHistoryStackChangeKind changeKind, long currentRevision)
    {
        Context = context;
        ChangeKind = changeKind;
        CurrentRevision = currentRevision;
    }

    public EditorHistoryContext Context { get; }

    public EditorHistoryStackChangeKind ChangeKind { get; }

    public long CurrentRevision { get; }
}