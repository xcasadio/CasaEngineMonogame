using System;
using System.Collections.Generic;
using CasaEngine.EditorServices.History;

namespace CasaEngine.Editor.History;

public sealed class EditorHistoryService
{
    public static EditorHistoryService Current { get; } = new();

    private readonly Dictionary<EditorHistoryContext, EditorHistoryStack> _stacks = new();
    private EditorHistoryContext _activeContext = EditorHistoryContext.Empty;
    private EditorHistoryStack? _activeStack;

    public event Action<EditorHistoryContext>? ActiveContextChanged;

    public event Action? ActiveHistoryChanged;

    public EditorHistoryContext ActiveContext => _activeContext;

    public bool HasActiveContext => !_activeContext.IsEmpty;

    public bool CanUndo => _activeStack?.CanUndo == true;

    public bool CanRedo => _activeStack?.CanRedo == true;

    public string? UndoDescription => _activeStack?.UndoDescription;

    public string? RedoDescription => _activeStack?.RedoDescription;

    private EditorHistoryService()
    {
    }

    public EditorHistoryStack GetOrCreate(EditorHistoryContext context)
    {
        if (context.IsEmpty)
        {
            throw new ArgumentException("History context cannot be empty.", nameof(context));
        }

        if (_stacks.TryGetValue(context, out var existingStack))
        {
            return existingStack;
        }

        var stack = new EditorHistoryStack();
        _stacks.Add(context, stack);
        return stack;
    }

    public bool TryGet(EditorHistoryContext context, out EditorHistoryStack? stack)
    {
        if (context.IsEmpty)
        {
            stack = null;
            return false;
        }

        return _stacks.TryGetValue(context, out stack);
    }

    public void SetActiveContext(EditorHistoryContext context)
    {
        if (context.IsEmpty)
        {
            Deactivate();
            return;
        }

        if (_activeContext.Equals(context) && _activeStack != null)
        {
            return;
        }

        DetachActiveStack();

        _activeContext = context;
        _activeStack = GetOrCreate(context);
        _activeStack.StackChanged += OnActiveStackChanged;

        ActiveContextChanged?.Invoke(_activeContext);
        ActiveHistoryChanged?.Invoke();
    }

    public void Deactivate()
    {
        if (_activeContext.IsEmpty && _activeStack == null)
        {
            return;
        }

        DetachActiveStack();
        _activeContext = EditorHistoryContext.Empty;

        ActiveContextChanged?.Invoke(_activeContext);
        ActiveHistoryChanged?.Invoke();
    }

    public void Execute(EditorHistoryContext context, IEditorCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        GetOrCreate(context).Execute(command);

        if (_activeContext.Equals(context))
        {
            ActiveHistoryChanged?.Invoke();
        }
    }

    public EditorHistoryTransactionScope OpenTransaction(EditorHistoryContext context, string description)
        => GetOrCreate(context).OpenTransaction(description);

    public void BeginTransaction(EditorHistoryContext context, string description)
        => GetOrCreate(context).BeginTransaction(description);

    public void CommitTransaction(EditorHistoryContext context, string? description = null)
    {
        if (TryGet(context, out var stack))
        {
            stack!.CommitTransaction(description);
        }
    }

    public void CancelTransaction(EditorHistoryContext context)
    {
        if (TryGet(context, out var stack))
        {
            stack!.CancelTransaction();
        }
    }

    public bool Undo()
    {
        if (_activeStack?.CanUndo != true)
        {
            return false;
        }

        _activeStack.Undo();
        return true;
    }

    public bool Redo()
    {
        if (_activeStack?.CanRedo != true)
        {
            return false;
        }

        _activeStack.Redo();
        return true;
    }

    public void Clear(EditorHistoryContext context)
    {
        if (TryGet(context, out var stack))
        {
            stack!.Clear();
        }
    }

    public void Remove(EditorHistoryContext context)
    {
        if (context.IsEmpty)
        {
            return;
        }

        if (!_stacks.Remove(context, out var removedStack))
        {
            return;
        }

        if (ReferenceEquals(removedStack, _activeStack))
        {
            DetachActiveStack();
            _activeContext = EditorHistoryContext.Empty;
            ActiveContextChanged?.Invoke(_activeContext);
            ActiveHistoryChanged?.Invoke();
        }
    }

    public void ClearAll()
    {
        DetachActiveStack();
        _stacks.Clear();
        _activeContext = EditorHistoryContext.Empty;

        ActiveContextChanged?.Invoke(_activeContext);
        ActiveHistoryChanged?.Invoke();
    }

    private void DetachActiveStack()
    {
        if (_activeStack == null)
        {
            return;
        }

        _activeStack.StackChanged -= OnActiveStackChanged;
        _activeStack = null;
    }

    private void OnActiveStackChanged()
    {
        ActiveHistoryChanged?.Invoke();
    }
}