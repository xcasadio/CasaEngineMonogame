# Global Editor History

This document describes the global undo/redo system used by the editor shell.

## Goals

- Route undo/redo by active authoring context instead of by UI widget.
- Keep one history entry per user intent.
- Preserve dirty state independently from save operations.
- Make new editable panels plug into the same model without inventing a parallel stack.

## History Contexts

The editor uses one history stack per authoring context:

- `World`
- one stack per open `UIScreen` document
- one stack per open material document
- one shared `ContentBrowser` tooling context

The shell resolves the active stack from the active dock panel and routes `Ctrl+Z`, `Ctrl+Y`, `Ctrl+Shift+Z`, and the `Edit` menu to that context.

## Core Types

The shared history abstractions live in the editor services layer:

- `CasaEngine.EditorServices.History.IEditorCommand`
- `CasaEngine.EditorServices.History.EditorHistoryStack`
- `CasaEngine.EditorServices.History.EditorCompositeCommand`
- `CasaEngine.EditorServices.History.EditorDelegateCommand`
- `CasaEngine.EditorServices.History.EditorHistoryTransactionScope`

The shell-level routing lives in:

- `CasaEngine.Editor.History.EditorHistoryService`
- `CasaEngine.Editor.History.EditorHistoryContext`
- `CasaEngine.Editor.History.EditorDirtyStateService`

## Grouping Rules

The editor records one command per user intent.

Examples:

- A gizmo drag becomes one reversible transform command.
- A committed text edit becomes one history entry, not one entry per keystroke.
- Multi-property UI resize and drag interactions are grouped.
- Content browser operations batch all impacted filesystem and asset-catalog changes into one command.

## Dirty State and Save

Dirty tracking is revision-based at the history layer.

- Saving a context marks the current revision as saved.
- Saving does not clear the undo stack.
- Closing a document or unloading a project may clear its stack.

Material documents also keep a serialized authoring snapshot so local preview edits remain decoupled from disk writes until save time.

## Content Browser Specifics

The content browser now uses reversible file operations backed by:

- `CasaEngine.Editor.ContentBrowser.Services.FileOperationService`
- `CasaEngine.Editor.ContentBrowser.Services.ContentBrowserTrashService`
- `CasaEngine.Editor.ContentBrowser.Services.ReversibleFileOperation`

Key rules:

- Delete is staged through editor trash instead of immediate irreversible removal.
- Move and rename update the asset catalog together with filesystem paths.
- Copy and import restore created files and catalog entries on redo.
- Undo/redo restores current folder and selection through panel view-state snapshots.

## Integrating a New Editable Panel

When adding a new editable panel:

1. Choose the correct history context.
2. Wrap every authoring mutation in an `IEditorCommand` or `EditorDelegateCommand`.
3. Use transactions when an interaction spans multiple low-level changes.
4. Restore panel-local selection or focus after execute and undo when needed.
5. Mark the context saved only from the explicit save flow.
6. Do not route transient UI state such as search text through the global history.

## Validation

Use the bounded smoke checklist in `ai-agent/audits/editor-history-smoke.md` together with a targeted solution build:

- `dotnet build CasaEngine.Editor.MonoGame.sln -clp:"ErrorsOnly;Summary"`