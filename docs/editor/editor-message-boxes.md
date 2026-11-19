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

## The "save before losing changes?" questions

Every open document that can be modified asks before its changes are lost. Decisions: [ADR-0104](../decisions/0104-every-modified-document-asks-before-its-changes-are-lost.md).

A native box answered on the next line; an MGUI box answers later. Each flow therefore refuses or stops at once, asks,
and redoes the action by code once the answer lets it go. The decision itself is `ModifiedScreenCloseDecision.ApplyAnswer`:
Save saves then proceeds (or stops if the save failed), Don't Save proceeds without saving, Cancel stops.

Documents that ask: Screen, Cutscene, Material, Sprite, Particle, Animation2D, Entity, Audio Mixer and Sound. A document
is "modified" when its tab shows ` *`; the question, the list and the save all read that same condition. The kind names
above are the `<Kind>` labels of the texts below. Tile map and animation clip are not modifiable and never ask.

| Flow | Title and message | Where | Once the answer lets it go |
|---|---|---|---|
| Closing a modified document (tab, Close Others, Close All, floating tab, auto-hide drawer, whole floating window) | `Close <Kind>`, "Save changes to '<title>' before closing?" | `ModifiedScreenCloseCoordinator` (`CasaEngine.Editor/History`) | `MGDockHost.ClosePanel(panel)`; for a whole floating window, `TryCloseWindow()` on it again, which asks about its next modified document |
| Quitting with modified documents or a modified world (File > Exit, window close button) | `Quit`, "Save changes to the modified documents before quitting?" followed by the list `- <Kind>: <title>` (the world is `World: <name>`) | `ModifiedScreensExitCoordinator` (`CasaEngine.Editor/History`) | `Game.Exit()` again; that one exit is let through |
| Opening or creating another project while documents or the world are modified | `Open Project`, "Save changes to the modified documents before opening another project?" followed by the same list | `ModifiedDocumentsGate` (`CasaEngine.Editor/History`), asked once the project is chosen in the launcher, before loading | The queued open or create action runs; Cancel keeps the current project and does not reopen the launcher |
| Opening a world while the current one is modified | `GameEditor.AskSaveBeforeOpeningWorld` | `GameEditor` | `OpenWorldAsset(fullPath, unsavedChangesHandled: true)`, which redoes every other check |

"Close All" and "Close Others" ask each modified document in turn; Cancel keeps that document only. A whole floating
window stops at its first modified document and retries after each document it closed; Cancel or a failed save stops
the chain.

On quit and on project switch, Save writes each modified document one by one, then the world through the project
save. The action goes on only if nothing of the list is still modified; otherwise it is cancelled and the reason is in
the log. If a play session is running when Save is answered on quit, it is stopped first (the project save is refused
during a session). Closing a tab during a session saves that document directly.

When another project is loaded, every open document of the previous project is closed by code first (saved or
abandoned according to the answer), so none stays open with the paths of the old project.

Under editor automation, nothing is asked on any of the three paths: the exit logs `[Automation] Exiting with N modified
document(s) abandoned`, as before for screens.

## Limits

- The file and folder pickers stay native Windows dialogs (Content Browser import, Export PNG, the project launcher's
  Browse and project folder), as do "Open in Explorer" and the WinForms clipboard. `UseWindowsForms` therefore stays in
  `CasaEngine.Editor.csproj`.
- No icons in the boxes.
- The question lists and texts are fixed when the question is queued: a quit question queued behind another question
  lists the documents modified at the time the user asked to quit.
- Text typed in a field and not yet validated is not counted as a change, except for the cutscene and the audio mixer.
- After `Reset Layout` or `Load Layout`, open documents lose their tab but stay open: they are saved by "Save project"
  and listed when quitting.
- Closing a project's documents on a project switch happens before loading: if the load then fails, the documents of
  the previous project are already closed.
- To be checked by hand in the running editor (automated tests cover the logic, not the clicks): each close gesture,
  File > Exit and the window close button with each answer - only a real run shows that `Exit()` requested again after
  a cancelled exit closes the editor under MonoGame DesktopGL.
