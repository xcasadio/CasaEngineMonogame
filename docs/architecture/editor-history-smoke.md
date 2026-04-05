# Editor Undo/Redo Smoke Checklist

This checklist is a bounded manual smoke pass for the global editor history system.

## Preconditions

- Build the editor solution:
  - `dotnet build CasaEngine.Editor.MonoGame.sln -clp:"ErrorsOnly;Summary"`
- Open a project with:
  - at least one world
  - at least one UI screen asset
  - at least one material asset
  - at least one importable external file for the content browser scenario

## 1. Active Context Routing

1. Open the world viewport, a UI screen document, a material document, and the content browser.
2. Perform one edit in each context.
3. Switch between panels and use `Ctrl+Z`, `Ctrl+Y`, and `Ctrl+Shift+Z`.
4. Verify undo/redo always targets the active authoring context only.
5. Verify the `Edit` menu labels change with the active context.

## 2. World Editing

1. Create, rename, duplicate, and delete an entity from the hierarchy.
2. Change transform or component values in the inspector.
3. Move the selected entity with the viewport gizmo.
4. Undo and redo each action.
5. Verify selection, hierarchy, inspector values, and viewport state stay coherent.

## 3. UI Screen Editing

1. Open a UI screen asset.
2. Add a control from the toolbox.
3. Rename a node from the inspector.
4. Edit text and numeric properties from the inspector, then commit focus.
5. Drag and resize the node in the preview.
6. Undo and redo each action.
7. Verify one history entry is produced per user intent, not per keystroke or per drag frame.

## 4. Material Editing

1. Open a material asset.
2. Edit at least one float, one color-like value, one enum, and one texture reference when available.
3. Reset one overridden property.
4. Undo and redo the edits.
5. Save the material, then undo and redo again.
6. Reopen the material panel.
7. Verify the preview, inspector, dirty flag, and saved state remain coherent.

## 5. Content Browser Editing

1. Create a new folder.
2. Rename it.
3. Duplicate an asset or folder.
4. Move an item with drag/drop or cut/paste.
5. Delete one item, then delete multiple items.
6. Import at least one external file.
7. Undo and redo each action.
8. Verify current folder, selection, and tree/list refresh stay coherent after every step.

## 6. Dirty State and Save

1. Make one unsaved edit in world, UI screen, material, and content browser contexts.
2. Verify the active document or panel title shows the dirty indicator.
3. Save the active material document.
4. Save the project from the world context.
5. Verify the dirty indicator clears only for the contexts that were saved.

## Expected Outcome

- Undo/redo is routed by active context.
- Every editable surface uses reversible commands.
- Drag and text-edit interactions are grouped at the user-intent level.
- Material save and hot reload stay coherent with history.
- Content browser file operations restore both filesystem state and editor state.