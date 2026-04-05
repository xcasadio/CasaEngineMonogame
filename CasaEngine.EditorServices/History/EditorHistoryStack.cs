using System.Collections.Generic;

namespace CasaEngine.EditorServices.History;

/// <summary>
/// Maintains a bounded history of reversible editor commands.
/// </summary>
public sealed class EditorHistoryStack
{
    private const int DefaultCapacity = 100;

    private readonly LinkedList<IEditorCommand> _undoStack = new();
    private readonly LinkedList<IEditorCommand> _redoStack = new();
    private readonly int _capacity;
    private List<IEditorCommand>? _pendingTransactionCommands;
    private string? _pendingTransactionDescription;

    public event Action? StackChanged;

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

    public string? UndoDescription => _undoStack.Last?.Value.Description;

    public string? RedoDescription => _redoStack.Last?.Value.Description;

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

        var command = _undoStack.Last.Value;
        _undoStack.RemoveLast();
        command.Undo();
        _redoStack.AddLast(command);

        StackChanged?.Invoke();
    }

    public void Redo()
    {
        if (IsTransactionOpen || _redoStack.Last == null)
        {
            return;
        }

        var command = _redoStack.Last.Value;
        _redoStack.RemoveLast();
        command.Execute();
        _undoStack.AddLast(command);

        StackChanged?.Invoke();
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
        StackChanged?.Invoke();
    }

    private void PushExecutedCommand(IEditorCommand command)
    {
        _redoStack.Clear();
        _undoStack.AddLast(command);

        while (_undoStack.Count > _capacity)
        {
            _undoStack.RemoveFirst();
        }

        StackChanged?.Invoke();
    }
}