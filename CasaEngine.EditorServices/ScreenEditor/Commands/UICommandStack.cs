using System;
using System.Collections.Generic;

namespace CasaEngine.EditorServices.ScreenEditor.Commands;

/// <summary>
/// Maintains a stack of reversible <see cref="IUIScreenCommand"/> operations.
/// Exposes <see cref="CanUndo"/>/<see cref="CanRedo"/> and fires <see cref="StackChanged"/>
/// whenever the history changes.
/// </summary>
public sealed class UICommandStack
{
    private const int DefaultCapacity = 100;

    private readonly LinkedList<IUIScreenCommand> _undoStack = new();
    private readonly LinkedList<IUIScreenCommand> _redoStack = new();
    private readonly int _capacity;

    // ─────────────────────────────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Fired after any push, undo, redo, or clear.</summary>
    public event Action? StackChanged;

    // ─────────────────────────────────────────────────────────────────────
    //  Constructor
    // ─────────────────────────────────────────────────────────────────────

    public UICommandStack(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _capacity = capacity;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  State
    // ─────────────────────────────────────────────────────────────────────

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public string? UndoDescription => _undoStack.Last?.Value.Description;
    public string? RedoDescription => _redoStack.Last?.Value.Description;

    // ─────────────────────────────────────────────────────────────────────
    //  Operations
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes <paramref name="command"/> and pushes it onto the undo stack.
    /// Clears the redo stack.
    /// </summary>
    public void Execute(IUIScreenCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        command.Execute();
        _redoStack.Clear();
        _undoStack.AddLast(command);

        // Trim if over capacity (remove oldest)
        while (_undoStack.Count > _capacity)
        {
            _undoStack.RemoveFirst();
        }

        StackChanged?.Invoke();
    }

    /// <summary>Undoes the most recent command.</summary>
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

    /// <summary>Re-executes the most recently undone command.</summary>
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

    /// <summary>Clears both undo and redo stacks.</summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StackChanged?.Invoke();
    }
}
