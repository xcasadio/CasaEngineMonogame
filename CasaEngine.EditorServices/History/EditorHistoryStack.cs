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

    public void Execute(IEditorCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        command.Execute();
        _redoStack.Clear();
        _undoStack.AddLast(command);

        while (_undoStack.Count > _capacity)
        {
            _undoStack.RemoveFirst();
        }

        StackChanged?.Invoke();
    }

    public void Undo()
    {
        if (_undoStack.Last == null)
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
        if (_redoStack.Last == null)
        {
            return;
        }

        var command = _redoStack.Last.Value;
        _redoStack.RemoveLast();
        command.Execute();
        _undoStack.AddLast(command);

        StackChanged?.Invoke();
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StackChanged?.Invoke();
    }
}