# ADR-0041: Editor message boxes are MGUI message boxes, answered asynchronously

- **Status**: Accepted
- **Date**: 2026-09-25
- **Source**: this chantier: `ai-agent/tasks/mgui-message-boxes-tasks.md` (decisions D1, D2, D4, D5, D6, D8, D9),
  branch `chantier/mgui-message-boxes`. Decided with the author on 2026-09-25, after the "Save changes to
  'DialogueScreen' before closing?" prompt showed up as a native Windows box. MGUI counterpart: MGUI ADR-0018
  (`MGUI/Docs/decisions/0018-modal-message-box-in-the-desktop-overlay.md`).

## Context

- The editor shows 13 native WinForms message boxes: 8 OK messages (project open and create errors in
  `CasaEngine.Editor/GameEditor.cs`, operation errors, warnings and "Properties" in
  `CasaEngine.Editor/Controls/ContentBrowserPanel.cs`, three launcher messages in
  `CasaEngine.Editor/ProjectLauncher/ProjectLauncherWindow.cs`), 2 delete confirmations in the Content Browser, and
  3 Yes/No/Cancel save prompts: closing a modified screen (`GameEditor.OnDockHostPanelClosing`), quitting with
  modified screens (`GameEditor.OnExiting`) and opening a world while the current one is modified
  (`GameEditor.ConfirmSaveBeforeOpeningWorld`).
- A native box ignores the editor theme, labels its buttons in the operating system's language, and is called
  synchronously on the game thread, so the MonoGame loop neither updates nor draws while it is open.
- The three save prompts rely on a synchronous answer: `CancelEventArgs.Cancel` of `MGDockHost.PanelClosing`,
  `ExitingEventArgs.Cancel` of `Game.OnExiting`, and the `bool` that stops `TryOpenWorldAsset`.
- The editor also uses four native file and folder pickers (Export PNG, Content Browser import, the launcher's
  Browse and project folder), "Open in Explorer" and the WinForms clipboard.

## Decision

- Every native message box of the editor becomes an MGUI message box (`MGMessageBox`, MGUI ADR-0018). The four
  native file and folder pickers stay native; "Open in Explorer" and the WinForms clipboard are not dialogs and
  stay as they are.
- The editor keeps a queue of message boxes: only one is open at a time, the next opens when the previous closes.
  The queue lives in the editor, not in MGUI.
- The three save prompts become asynchronous. Closing a modified screen: the close is refused, the question is
  asked, then the panel is closed by code (`MGDockHost.ClosePanel`, added by MGUI ADR-0018; first written here as
  `RemovePanel`, corrected during the same chantier when a test showed `RemovePanel` does not see floating panels)
  when the answer lets it close. Quitting: the
  exit is cancelled, the question is asked, then `Exit()` is called again once the answer lets it proceed.
  Opening a world: the open resumes in the answer's callback.
- Several modified screens closed at once ("Close Others", "Close All"): one box per screen, one after the other;
  Cancel keeps that screen open and the questions go on for the next ones, as before.
- Closing a whole floating window that holds modified screens: after a successful Save or a Don't Save, the window
  retries its close by itself, which asks about the next modified screen; Cancel, or a failed save, stops.
- The save prompts read "Save", "Don't Save", "Cancel". Delete confirmations keep "Yes" / "No"; plain messages
  have a single "OK". No icon in this version.
- Under editor automation, nothing is asked when a screen is closed or the editor exits, as before.

## Consequences

- The editor keeps running and drawing while a question is open, and the question looks like the rest of the
  editor. While an MGUI message box is open, the editor's shortcuts and the viewport pointer are blocked by the
  desktop overlay (MGUI ADR-0018).
- Code that asks a question can no longer read the answer on the next line: it continues in a callback. Any future
  question in the editor goes through the queue.
- `UseWindowsForms` stays in `CasaEngine.Editor.csproj` for the native pickers and the clipboard.
- A behaviour change: a whole floating window closed with several modified screens now asks them one per retry of
  the window close instead of in one synchronous pass; the result for the user is the same sequence of questions.
- Known risk, checked by the author on a real run: calling `Exit()` again after a cancelled `OnExiting` must close
  the editor under MonoGame DesktopGL.
