using System;
using System.Collections.Generic;

namespace CasaEngine.Editor.ContentBrowser.Services;

public sealed class ReversibleFileOperation
{
    private readonly Func<FileOperationService, bool> _redoAction;
    private readonly Func<FileOperationService, bool> _undoAction;

    public ReversibleFileOperation(
        Func<FileOperationService, bool> redoAction,
        Func<FileOperationService, bool> undoAction,
        IReadOnlyList<string> selectionAfterExecute = null,
        IReadOnlyList<string> selectionAfterUndo = null)
    {
        ArgumentNullException.ThrowIfNull(redoAction);
        ArgumentNullException.ThrowIfNull(undoAction);

        _redoAction = redoAction;
        _undoAction = undoAction;
        SelectionAfterExecute = selectionAfterExecute ?? Array.Empty<string>();
        SelectionAfterUndo = selectionAfterUndo ?? Array.Empty<string>();
    }

    public IReadOnlyList<string> SelectionAfterExecute { get; }

    public IReadOnlyList<string> SelectionAfterUndo { get; }

    public bool Redo(FileOperationService fileOperationService)
    {
        ArgumentNullException.ThrowIfNull(fileOperationService);
        return _redoAction(fileOperationService);
    }

    public bool Undo(FileOperationService fileOperationService)
    {
        ArgumentNullException.ThrowIfNull(fileOperationService);
        return _undoAction(fileOperationService);
    }
}