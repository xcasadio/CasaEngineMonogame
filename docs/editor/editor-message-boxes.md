# Editor message boxes

Every question and message of the editor is an MGUI message box (`MGMessageBox`), shown over the whole editor desktop,
in the editor theme, while the game loop keeps running. Decisions: [ADR-0041](../decisions/0041-editor-message-boxes-are-mgui.md)
(editor) and MGUI ADR-0018 (`MGUI/Docs/decisions/0018-modal-message-box-in-the-desktop-overlay.md`).

## What the user sees

- One box at a time, in the order the questions were asked; the next one opens when the previous one is answered.
- While a box is open, nothing else of the editor takes input: docked and floating panels, menus, the viewports and the
  editor shortcuts (Ctrl+S, Ctrl+Z, F5...) are blocked.
- Keyboard: Enter (or Space) activates the focused button, which is the default one when the box opens; Tab moves
  between buttons; Escape chooses the cancel button.
- Labels: plain messages have "OK"; delete confirmations "Yes" / "No"; save questions "Save" / "Don't Save" / "Cancel".
  No icons.

## Usage (code)

`GameEditor` owns one `EditorMessageBoxes` (`CasaEngine.Editor/Controls/EditorMessageBoxes.cs`) and passes it to the
panels that need it (`ContentBrowserPanel`, `ProjectLauncherWindow`, through their internal constructors). The answer
arrives in a callback, never on the line after the call:

```csharp
_messageBoxes.ShowMessage("Content Browser", errorMessage);

_messageBoxes.AskYesNo("Content Browser", $"Delete '{item.Name}'?", confirmed =>
{
    if (confirmed)
    {
        DeleteItem(item);
    }
});

_messageBoxes.AskSave("Close Screen", $"Save changes to '{title}' before closing?", answer =>
{
    // EditorSaveAnswer.Save, DontSave or Cancel
});
```

The ordering is `EditorMessageBoxQueue` (pure, tested in `CasaEngine.Tests/Editor/EditorMessageBoxQueueTests.cs`). A
question asked from an answer's callback comes after the questions already waiting.

## The three "save before losing changes?" questions

A native box answered on the next line; an MGUI box answers later. Each flow therefore refuses or stops at once, asks,
and redoes the action by code once the answer lets it go. The decision itself is `ModifiedScreenCloseDecision.ApplyAnswer`:
Save saves then proceeds (or stops if the save failed), Don't Save proceeds without saving, Cancel stops.

| Flow | Where | Once the answer lets it go |
|---|---|---|
| Closing a modified screen (tab, Close Others, Close All, floating tab, auto-hide drawer, whole floating window) | `ModifiedScreenCloseCoordinator` (`CasaEngine.Editor/History`) | `MGDockHost.ClosePanel(panel)`; for a whole floating window, `TryCloseWindow()` on it again, which asks about its next modified screen |
| Quitting with modified screens (File > Exit, window close button) | `ModifiedScreensExitCoordinator` (`CasaEngine.Editor/History`) | `Game.Exit()` again; that one exit is let through |
| Opening a world while the current one is modified | `GameEditor.AskSaveBeforeOpeningWorld` | `OpenWorldAsset(fullPath, unsavedChangesHandled: true)`, which redoes every other check |

"Close All" and "Close Others" ask each modified screen in turn; Cancel keeps that screen only. A whole floating window
stops at its first modified screen and retries after each screen it closed; Cancel or a failed save stops the chain.

Under editor automation, nothing is asked when a screen is closed or the editor exits: the abandoned screens are logged,
as before.

## Limits

- The file and folder pickers stay native Windows dialogs (Content Browser import, Export PNG, the project launcher's
  Browse and project folder), as do "Open in Explorer" and the WinForms clipboard. `UseWindowsForms` therefore stays in
  `CasaEngine.Editor.csproj`.
- No icons in the boxes.
- The question lists and texts are fixed when the question is queued: a quit question queued behind another question
  lists the screens modified at the time the user asked to quit.
- To be checked by hand in the running editor (automated tests cover the logic, not the clicks): each close gesture,
  File > Exit and the window close button with each answer - only a real run shows that `Exit()` requested again after
  a cancelled exit closes the editor under MonoGame DesktopGL.
