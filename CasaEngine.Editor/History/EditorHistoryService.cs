using System;
using System.Collections.Generic;
using CasaEngine.Core.Logging;
using CasaEngine.EditorServices.History;

namespace CasaEngine.Editor.History;

public sealed class EditorHistoryService
{
    public static EditorHistoryService Current { get; } = new();

    private readonly Dictionary<EditorHistoryContext, EditorHistoryStack> _stacks = new();
    private readonly Dictionary<EditorHistoryStack, EditorHistoryContext> _stackContexts = new();
    private EditorHistoryContext _activeContext = EditorHistoryContext.Empty;
    private EditorHistoryStack _activeStack;

    public event Action<EditorHistoryContext> ActiveContextChanged;

    public event Action ActiveHistoryChanged;

    public event EventHandler<EditorHistoryChangedEventArgs> HistoryChanged;

    public EditorHistoryContext ActiveContext => _activeContext;

    public bool HasActiveContext => !_activeContext.IsEmpty;

    public bool CanUndo => _activeStack?.CanUndo == true;

    public bool CanRedo => _activeStack?.CanRedo == true;

    public string UndoDescription => _activeStack?.UndoDescription;

    public string RedoDescription => _activeStack?.RedoDescription;

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
        _stackContexts.Add(stack, context);
        stack.StackChanged += OnStackChanged;
        return stack;
    }

    public bool TryGet(EditorHistoryContext context, out EditorHistoryStack stack)
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

    /// <summary>
    /// While suspended (play-in-editor session), commands are refused instead of being
    /// executed: runtime state must not enter the undo stacks nor mutate the documents.
    /// </summary>
    public bool IsSuspended { get; set; }

    public void Execute(EditorHistoryContext context, IEditorCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (IsSuspended)
        {
            Logs.WriteWarning($"Edit command '{command.Description}' ignored: the editor history is suspended during a play session.");
            return;
        }

        GetOrCreate(context).Execute(command);
    }

    public EditorHistoryTransactionScope OpenTransaction(EditorHistoryContext context, string description)
        => GetOrCreate(context).OpenTransaction(description);

    public void BeginTransaction(EditorHistoryContext context, string description)
        => GetOrCreate(context).BeginTransaction(description);

    public void CommitTransaction(EditorHistoryContext context, string description = null)
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
        if (IsSuspended || _activeStack?.CanUndo != true)
        {
            return false;
        }

        _activeStack.Undo();
        return true;
    }

    public bool Redo()
    {
        if (IsSuspended || _activeStack?.CanRedo != true)
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

        removedStack.StackChanged -= OnStackChanged;
        _stackContexts.Remove(removedStack);

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

        foreach (var stack in _stackContexts.Keys)
        {
            stack.StackChanged -= OnStackChanged;
        }

        _stacks.Clear();
        _stackContexts.Clear();
        _activeContext = EditorHistoryContext.Empty;

        ActiveContextChanged?.Invoke(_activeContext);
        ActiveHistoryChanged?.Invoke();
    }

    private void DetachActiveStack()
    {
        _activeStack = null;
    }

    private void OnStackChanged(object sender, EditorHistoryStackChangedEventArgs e)
    {
        if (sender is not EditorHistoryStack stack || !_stackContexts.TryGetValue(stack, out var context))
        {
            return;
        }

        if (_activeContext.Equals(context))
        {
            ActiveHistoryChanged?.Invoke();
        }

        HistoryChanged?.Invoke(this, new EditorHistoryChangedEventArgs(context, e.ChangeKind, e.CurrentRevision));
    }
}