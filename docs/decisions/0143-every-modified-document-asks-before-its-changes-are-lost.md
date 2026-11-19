# ADR-0143: Every modified document asks before its changes are lost

- **Status**: Accepted
- **Date**: 2026-11-19
- **Source**: this chantier: `ai-agent/tasks/editor-unsaved-documents-tasks.md` (decisions D1 to D5, settled with the author on 2026-11-19, and choices P1 to P14, approved with the plan and open to correction at its acceptance), branch `chantier/cutscene-property-editing`. The author, after testing the cutscene editor on 2026-11-19: the question must be asked for every opened and edited document, as a general behaviour of every open view.
- Renumbered from ADR-0104 on 2026-11-19 when origin/main was merged (its ADR-0104 is another decision)

## Context

- ADR-0041 turned the three save prompts of the editor into asynchronous MGUI message boxes: closing a modified screen, quitting with modified screens, and opening a world while the current one is modified. Two of the three cover UI screen documents only.
- ADR-0134 states that the "unsaved changes" prompt on closing exists only for UI screen documents, and defers the prompt for cutscenes and the other asset documents.
- Commit `b5b4ae05` added the question on closing a modified cutscene tab. Other modified documents (material, sprite, particle, Animation2D, entity, audio mixer, sound) are still lost without a question on closing, on quitting, and when another project is opened. The world is asked only when another world is opened.
- The sound document does not show ` *` in its tab when modified and is not saved by "Save project"; reopening an already opened sound reloads it and drops its unsaved changes.
- Opening another project calls `_editorDirtyState.ClearAll()` and `_editorHistory.ClearAll()` without asking; the document tables are emptied only when a panel is removed, and replacing the dock layout raises no removal, so documents of the left project stay in the tables.
- Nine kinds of document are modifiable and know whether they are modified: UI screen, cutscene, material, sprite, particle, Animation2D, entity, audio mixer, sound. Tile map and animation clip are not modifiable.

## Decision

This record completes ADR-0041 and replaces the deferral written in ADR-0134. Neither file is modified.

- D1: the save question is asked for every open and modified document, not only UI screens and cutscenes.
- D2: when quitting the editor, the question covers the documents and the world.
- D3: before opening or creating another project, the question is asked too.
- D4: the sound document is aligned with the others: ` *` in its title when modified, and saved by "Save project".
- D5: the work runs in autonomous mode, with the plan reviewed beforehand and the author's acceptance at the end.
- P3: the nine modifiable kinds ask; tile map and animation clip do not, since nothing in them is modifiable.
- P4: texts. Closing: title `Close <Kind>`, message `Save changes to '<title>' before closing?`. Quitting: title `Quit`, message `Save changes to the modified documents before quitting?` followed by the list `- <Kind>: <title>`. Another project: title `Open Project`, message `Save changes to the modified documents before opening another project?` followed by the same list. `<Kind>` is one of `Screen`, `Cutscene`, `Material`, `Sprite`, `Particle`, `Animation2D`, `Entity`, `Audio Mixer`, `Sound`, `World`.
- P5: when quitting or changing project, `Save` saves each modified document one by one, then the world through the project save. The action proceeds only if nothing of the list is still modified; otherwise it is cancelled and the reason is logged.
- P6: if a play session runs when `Save` is answered on quitting, it is stopped first, as opening another world does, because saving the project is refused during a session. Closing a tab during a session saves that document directly, as screens already do.
- P7: under editor automation nothing is asked on the three paths. Quitting logs the abandoned documents; the log text changes from "modified screen(s)" to "modified document(s)".
- P8: on a project change the question is asked once the project is chosen in the launcher, before loading. `Cancel` keeps the current project and does not reopen the launcher. When the load starts, every open document of the left project is closed by code, whether it still has a tab or not and whether a question was asked or not.
- P9: reopening an already opened and modified sound keeps its changes, as material, particle and Animation2D documents do ("Unsaved changes kept").
- P11: after `Reset Layout` or `Load Layout`, documents lose their tab but stay in the tables (existing behaviour, unchanged). They are still saved by "Save project" and are now listed on quitting.
- P12: when quitting, the world is listed as `World: <name>`; its `Save` is the project save.
- P14: a document is "modified" when its tab shows ` *`; the question, the list and the save all read that one condition. When it is true, `Save` really writes the file even if the panel's internal flag is clean, and a successful save always makes it false. A document is never marked saved without a write. Consequences for "Save project": a material or particle whose value was changed then restored is rewritten with the same content and loses its ` *`; an Animation2D saved then undone is really saved, which it was not before.

## Consequences

- The question is asked on three paths for the nine modifiable kinds: closing a tab, quitting (documents and world), and opening or creating another project. The existing coordinators keep their names (P2) and serve every document; renaming them is a separate chantier.
- Under automation no question is shown, so the automated runs are unchanged apart from the log text.
- The click-through behaviour on the three paths is not exercised by an automated test, since no input is sent to the editor and no test builds the editor; it is checked by the author.
- Out of scope, recorded as open points of the plan: documents removed by `Reset Layout` or `Load Layout` are not closed; deleting or renaming in the Content Browser an asset whose document is open does not close it; text typed and not yet validated is not counted as a modification beyond what the cutscene and the audio mixer already do (P10); if loading the other project fails after the question, the documents of the current project are already closed; the sound document gets no undo history; tile map and animation clip stay non-modifiable; MGUI is not modified, including a middle click that would close a tab.
- Reopening an already opened and modified document of a kind other than sound is not corrected by this decision.
