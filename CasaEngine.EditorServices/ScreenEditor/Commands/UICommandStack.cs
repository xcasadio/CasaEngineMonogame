using System;
using CasaEngine.EditorServices.History;

namespace CasaEngine.EditorServices.ScreenEditor.Commands;

/// <summary>
/// Maintains a stack of reversible <see cref="IUIScreenCommand"/> operations.
/// Exposes <see cref="CanUndo"/>/<see cref="CanRedo"/> and fires <see cref="StackChanged"/>
/// whenever the history changes.
/// </summary>
public sealed class UICommandStack
{
    private readonly EditorHistoryStack _inner;

    // ─────────────────────────────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Fired after any push, undo, redo, or clear.</summary>
    public event Action? StackChanged
    {
        add => _inner.StackChanged += value;
        remove => _inner.StackChanged -= value;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Constructor
    // ─────────────────────────────────────────────────────────────────────

    public UICommandStack(int capacity = DefaultCapacity)
    {
        _inner = new EditorHistoryStack(capacity);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  State
    // ─────────────────────────────────────────────────────────────────────

    private const int DefaultCapacity = 100;

    public bool CanUndo => _inner.CanUndo;
    public bool CanRedo => _inner.CanRedo;

    public string? UndoDescription => _inner.UndoDescription;
    public string? RedoDescription => _inner.RedoDescription;

    // ─────────────────────────────────────────────────────────────────────
    //  Operations
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes <paramref name="command"/> and pushes it onto the undo stack.
    /// Clears the redo stack.
    /// </summary>
    public void Execute(IUIScreenCommand command)
    {
        _inner.Execute(command);
    }

    /// <summary>Undoes the most recent command.</summary>
    public void Undo()
    {
        _inner.Undo();
    }

    /// <summary>Re-executes the most recently undone command.</summary>
    public void Redo()
    {
        _inner.Redo();
    }

    /// <summary>Clears both undo and redo stacks.</summary>
    public void Clear()
    {
        _inner.Clear();
    }
}
