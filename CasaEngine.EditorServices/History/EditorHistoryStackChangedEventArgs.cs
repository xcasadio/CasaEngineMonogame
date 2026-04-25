namespace CasaEngine.EditorServices.History;

public sealed class EditorHistoryStackChangedEventArgs : EventArgs
{
    public EditorHistoryStackChangedEventArgs(EditorHistoryStackChangeKind changeKind, long currentRevision)
    {
        ChangeKind = changeKind;
        CurrentRevision = currentRevision;
    }

    public EditorHistoryStackChangeKind ChangeKind { get; }

    public long CurrentRevision { get; }
}