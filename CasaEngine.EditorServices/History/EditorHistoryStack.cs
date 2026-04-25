namespace CasaEngine.EditorServices.History;

/// <summary>
/// Maintains a bounded history of reversible editor commands.
/// </summary>
public sealed class EditorHistoryStack
{
    private readonly record struct HistoryEntry(long Revision, IEditorCommand Command);

    private const int DefaultCapacity = 100;

    private readonly LinkedList<HistoryEntry> _undoStack = new();
    private readonly LinkedList<HistoryEntry> _redoStack = new();
    private readonly int _capacity;
    private List<IEditorCommand>? _pendingTransactionCommands;
    private string? _pendingTransactionDescription;
    private long _nextRevision = 1;

    public event EventHandler<EditorHistoryStackChangedEventArgs>? StackChanged;

    public EditorHistoryStack(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _capacity = capacity;
    }

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public string? UndoDescription => _undoStack.Last?.Value.Command.Description;

    public string? RedoDescription => _redoStack.Last?.Value.Command.Description;

    public long CurrentRevision => _undoStack.Last?.Value.Revision ?? 0;

    public bool IsTransactionOpen => _pendingTransactionCommands != null;

    public string? PendingTransactionDescription => _pendingTransactionDescription;

    public EditorHistoryTransactionScope OpenTransaction(string description)
    {
        BeginTransaction(description);
        return new EditorHistoryTransactionScope(this);
    }

    public void BeginTransaction(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (IsTransactionOpen)
        {
            throw new InvalidOperationException("A history transaction is already open.");
        }

        _pendingTransactionDescription = description;
        _pendingTransactionCommands = [];
    }

    public void Execute(IEditorCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        command.Execute();

        if (IsTransactionOpen)
        {
            _pendingTransactionCommands!.Add(command);
            return;
        }

        PushExecutedCommand(command);
    }

    public void Undo()
    {
        if (IsTransactionOpen || _undoStack.Last == null)
        {
            return;
        }

        var entry = _undoStack.Last.Value;
        _undoStack.RemoveLast();
        entry.Command.Undo();
        _redoStack.AddLast(entry);

        OnStackChanged(EditorHistoryStackChangeKind.Undone);
    }

    public void Redo()
    {
        if (IsTransactionOpen || _redoStack.Last == null)
        {
            return;
        }

        var entry = _redoStack.Last.Value;
        _redoStack.RemoveLast();
        entry.Command.Execute();
        _undoStack.AddLast(entry);

        OnStackChanged(EditorHistoryStackChangeKind.Redone);
    }

    public void CommitTransaction(string? description = null)
    {
        if (!IsTransactionOpen)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            _pendingTransactionDescription = description;
        }

        var commands = _pendingTransactionCommands!;
        var transactionDescription = _pendingTransactionDescription;
        _pendingTransactionCommands = null;
        _pendingTransactionDescription = null;

        if (commands.Count == 0)
        {
            return;
        }

        IEditorCommand command = commands.Count == 1
            ? commands[0]
            : new EditorCompositeCommand(transactionDescription ?? "Transaction", commands);

        PushExecutedCommand(command);
    }

    public void CancelTransaction()
    {
        if (!IsTransactionOpen)
        {
            return;
        }

        var commands = _pendingTransactionCommands!;
        _pendingTransactionCommands = null;
        _pendingTransactionDescription = null;

        for (int index = commands.Count - 1; index >= 0; index--)
        {
            commands[index].Undo();
        }
    }

    public void Clear()
    {
        if (IsTransactionOpen)
        {
            CancelTransaction();
        }

        _undoStack.Clear();
        _redoStack.Clear();
        OnStackChanged(EditorHistoryStackChangeKind.Cleared);
    }

    private void PushExecutedCommand(IEditorCommand command)
    {
        var entry = new HistoryEntry(_nextRevision++, command);
        _redoStack.Clear();
        _undoStack.AddLast(entry);

        while (_undoStack.Count > _capacity)
        {
            _undoStack.RemoveFirst();
        }

        OnStackChanged(EditorHistoryStackChangeKind.Executed);
    }

    private void OnStackChanged(EditorHistoryStackChangeKind changeKind)
    {
        StackChanged?.Invoke(this, new EditorHistoryStackChangedEventArgs(changeKind, CurrentRevision));
    }
}