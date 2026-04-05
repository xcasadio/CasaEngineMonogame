using System;
using System.Collections.Generic;
using CasaEngine.EditorServices.History;

namespace CasaEngine.Editor.History;

public sealed class EditorDirtyStateService
{
    public static EditorDirtyStateService Current { get; } = new(EditorHistoryService.Current);

    private readonly Dictionary<EditorHistoryContext, DirtyStateEntry> _states = new();

    public event EventHandler<EditorDirtyStateChangedEventArgs>? DirtyStateChanged;

    private EditorDirtyStateService(EditorHistoryService historyService)
    {
        historyService.HistoryChanged += OnHistoryChanged;
    }

    public bool IsDirty(EditorHistoryContext context)
    {
        if (context.IsEmpty)
        {
            return false;
        }

        return GetOrCreateState(context).IsDirty;
    }

    public void MarkSaved(EditorHistoryContext context)
    {
        if (context.IsEmpty)
        {
            return;
        }

        var state = GetOrCreateState(context);
        state.SavedRevision = state.CurrentRevision;
        UpdateDirtyState(context, state, EditorDirtyStateChangeKind.Save, forceNotification: true);
    }

    public void Remove(EditorHistoryContext context)
    {
        if (context.IsEmpty || !_states.Remove(context, out _))
        {
            return;
        }

        DirtyStateChanged?.Invoke(this, new EditorDirtyStateChangedEventArgs(context, false, EditorDirtyStateChangeKind.Reset));
    }

    public void ClearAll()
    {
        if (_states.Count == 0)
        {
            return;
        }

        var contexts = new List<EditorHistoryContext>(_states.Keys);
        _states.Clear();

        foreach (var context in contexts)
        {
            DirtyStateChanged?.Invoke(this, new EditorDirtyStateChangedEventArgs(context, false, EditorDirtyStateChangeKind.Reset));
        }
    }

    private void OnHistoryChanged(object? sender, EditorHistoryChangedEventArgs e)
    {
        var state = GetOrCreateState(e.Context);
        state.CurrentRevision = e.CurrentRevision;
        UpdateDirtyState(e.Context, state, MapChangeKind(e.ChangeKind), forceNotification: true);
    }

    private DirtyStateEntry GetOrCreateState(EditorHistoryContext context)
    {
        if (_states.TryGetValue(context, out var state))
        {
            return state;
        }

        state = new DirtyStateEntry();
        _states.Add(context, state);
        return state;
    }

    private void UpdateDirtyState(EditorHistoryContext context, DirtyStateEntry state, EditorDirtyStateChangeKind changeKind, bool forceNotification)
    {
        bool isDirty = state.CurrentRevision != state.SavedRevision;
        bool changed = isDirty != state.IsDirty;
        state.IsDirty = isDirty;

        if (!changed && !forceNotification)
        {
            return;
        }

        DirtyStateChanged?.Invoke(this, new EditorDirtyStateChangedEventArgs(context, isDirty, changeKind));
    }

    private static EditorDirtyStateChangeKind MapChangeKind(EditorHistoryStackChangeKind changeKind)
    {
        return changeKind switch
        {
            EditorHistoryStackChangeKind.Executed => EditorDirtyStateChangeKind.Execute,
            EditorHistoryStackChangeKind.Undone => EditorDirtyStateChangeKind.Undo,
            EditorHistoryStackChangeKind.Redone => EditorDirtyStateChangeKind.Redo,
            EditorHistoryStackChangeKind.Cleared => EditorDirtyStateChangeKind.Clear,
            _ => EditorDirtyStateChangeKind.Reset,
        };
    }

    private sealed class DirtyStateEntry
    {
        public long CurrentRevision { get; set; }

        public long SavedRevision { get; set; }

        public bool IsDirty { get; set; }
    }
}