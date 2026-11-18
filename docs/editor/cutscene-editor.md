# Cutscene editor

Decisions: see ADR-0095, ADR-0096, ADR-0097 and ADR-0098.

## Summary

Opening a `.cutscene` asset from the Content Browser shows a document tab and fills two dock panels: the **Hierarchy** panel shows the action tree, and the **Inspector** panel edits the properties of the selected action in typed fields. Values and structure (add, duplicate, delete) can be edited, undone, redone and saved. The action tree (`CutsceneAsset.RootAction`) is the only source of truth: there is no separate editing model and no change in the file format.

The document itself keeps the transport bar, a status line and a preview of an isolated copy of a world to play the cutscene being edited (see "Preview"). A read-only **Cutscene Timeline** panel shows the tree in time (see "Timeline"). Moving actions and drag and drop are not done yet (see "Next").

## Usage

1. Open a `.cutscene` file from the Content Browser. Clicking its tab again makes it the active document, so undo and redo target its history.
2. **Hierarchy** shows one row per action: its type, then a short summary (`MoveSprite  orb-left -> (-60, 35) @ 120`, `Wait  1.5 s`, `Repeat  forever`). Select a row to edit that action.
3. **Inspector** shows the type and path of the selected action (`root_action.actions[2]`), then one row per property: a fixed-width label and a field of the right kind (see "Typed fields"). Without a selection it shows the cutscene view: title, file path, validation, load losses and the "Editor play mode" runtime state.
4. Commit a value with Enter, or by leaving the field. A numeric field also has `-` and `+` buttons that commit at once. Each commit is one history entry. A refused value puts the old value back and the reason is shown under the field.
5. Text typed in a field and not yet committed is committed when another action is selected, to the action it was typed for.
6. Undo and redo with the editor's global commands while the cutscene tab is active.
7. A modified document shows `*` in its tab title. Save it with the editor's save command, or with "save all".

## Action tree

- **Hierarchy bar.** A flat drop-down lists the creatable action types, each prefixed by its category (`Sprites / MoveSprite`); the categories are Flow, Timing, Movement, Sprites, Audio, Screen and World and input. Buttons: `Add`, `Duplicate`, `Delete`. A right click on a row opens a menu with Duplicate and Delete. The footer shows the number of actions.
- **Add.** If the selected action is a `Sequence`, `Parallel` or `Repeat`, the new action becomes its last child. Otherwise it is inserted right after the selection, in the same parent. With no selection it is appended to the root when the root is a container. The new action is selected.
- **Defaults.** A created action has the default values of its class, except `Repeat.count` = 1 and `WaitFrames.frames` = 1. It can therefore be invalid until its required fields are filled: the Inspector lists the validator messages. A save is still allowed, as for any cutscene the validator criticises.
- **Duplicate.** Inserts a deep copy right after the source and selects it. The copy goes through the serializer, so it is refused, with a message, for a subtree that holds a format error, a load loss or an unknown action type: the copy would lose those marks.
- **Delete.** Removes the action and its children. A load loss that belongs to the removed subtree is removed with it and comes back at its original place on undo. The root action can be neither deleted nor duplicated.
- **Selection and expansion.** The tree is rebuilt only when the document changes. The selection and the expanded nodes are kept by action; everything is expanded on first opening. A selection that is no longer in the tree after an undo, a redo or a reload is cleared.
- Add, duplicate and delete are history entries that mark the document modified. If the preview was playing, it restarts on the edited asset.

## Typed fields

| Property | Field |
|---|---|
| Number, integer, byte | Numeric field with `-` and `+`, limited by the bounds of the validator (`>= 0` for durations, distances and counters, `[0, 1]` for volumes, `> 0` for `MoveSprite.speed`). The step is 1, or 0.1 when the upper bound is 1. An empty field means "no value" for a nullable property and is refused otherwise. |
| Boolean | Check box |
| Enumeration | Drop-down list of the names, with `(none)` when the property can be empty |
| Colour | Colour editor |
| `FadeScreen.r`, `g`, `b` | One colour editor: one validation writes the three bytes in one history entry |
| Vector | Three numeric fields; the whole vector is validated together |
| Entity name | Drop-down list of the entity names of the preview world, plus the current value marked `(missing)` if it is not there. Text box when no preview world is loaded or it has no named entity |
| Sound, sprite, screen, world, cutscene, button mapping | Drop-down list of the assets of that type in the catalog, with `(none)` and the current value marked `(missing)` when absent. Text box when the catalog holds no asset of that type |
| `PlaySound.bus_name`, `ExitOnInput.inputs` | Text box |
| Other strings | Text box |

A nullable property has a small `x` button that sets it back to "no value".

### Written form of references

The value written is the one the runtime reads:

| Property | Written value |
|---|---|
| `sound_asset_id` | the GUID of the sound asset |
| `sprite` | the name of the sprite in the catalog |
| `OpenScreen.target`, `PlayVideo.target` | the name of the screen asset |
| `ChangeWorld.target` | the **file name** of the world |
| `mapping`, `next_cutscene` | the name of the asset |
| `entity` | the name of an entity of the world (compared by ordinal) |

A stored value that is not in the catalog is kept as it is, shown as `(missing)`, and never rewritten unless the user picks another one. Where the runtime accepts a name or a GUID, a stored GUID is found in the list.

## Timeline

The **Cutscene Timeline** panel is a contextual tool panel: it appears in the bottom tab group only while a cutscene document is active, like the Animation2D timeline, and it comes to the front when a cutscene is opened. It is a **read-only projection of the action tree**: the tree stays the only model, nothing is edited on the timeline and the file format does not change. It can be docked, resized and maximised like any panel.

The panel is attached to the active cutscene document and detached when that document closes. The model is rebuilt when the document changes or when a new duration is measured, never per frame; per frame only the playhead moves.

### Rows

Rows are created only when something uses them, in this order:

| Row | Content |
|---|---|
| Loops | The ranges of `Repeat` |
| Flow | `Wait`, `WaitFrames` |
| One row per entity | `ShowSprite`, `HideSprite`, `MoveSprite`, `TintSprite`, `MoveTo`, `NavigateTo`, `PlayVideoSprite`, `StartVideoSprite`, in order of first appearance; `(no entity)` when the name is empty |
| Screen | `FadeScreen`, `FadeScreenFrames`, `OpenScreen`, `PlayVideo` |
| Audio | `PlaySound`, `PlayMusic`, `StopMusic`, `FadeMusic` |
| World and input | `ChangeWorld`, `ExitOnInput` |
| Other | Actions of an unknown type, as markers |

`Sequence` and `Parallel` have no item of their own. Two bars or ranges of a row that overlap are spread over sub-rows (only the first sub-row carries the row label); a marker stays on the first sub-row.

### Reading the shapes

| Shape | Meaning |
|---|---|
| Solid bar | A known duration: `Wait`, `WaitFrames`, `FadeScreen`, `FadeScreenFrames`, `FadeMusic`, `TintSprite`, the fade of `PlayMusic` and `StopMusic` |
| Dashed outline, lighter fill | An **estimated** duration: one that only a play can measure (`MoveSprite` effect, `MoveTo`, `NavigateTo`, `PlayVideo`, `PlayVideoSprite`, shown as 1 s), or a duration given in frames and converted at 60 frames per second |
| Bar open on the right | `ExitOnInput`: it lasts until the end of the cutscene |
| Range | The span of a `Repeat`, on the Loops row |
| Diamond with its label | An instant: an action with no duration |

After a play, the measured durations replace the estimates (the bar becomes solid). Any change of the document clears them, and a play in progress restarts and measures again.

Time on the ruler is in seconds. The bar above the timeline reads `Playing` or `Stopped`, the play time in seconds and the number of frames, for example `Playing   2.00 s  ·  120 frames`; it is refreshed at most ten times per second.

### How time is laid out

- A `Parallel` starts all its children at the same instant and lasts as long as its longest branch. The one-update shift the runtime adds to each branch is not drawn.
- A `Repeat` is drawn as **one lap** under a range `Repeat ×N` or `Repeat forever`. For a count above 1, a second range `×(N-1) more` covers the other laps and what follows is placed after them. A `Repeat` with no count is drawn as `Repeat ×?`, one lap.
- The durations that move the sequence on are those of `Wait`, `WaitFrames`, `FadeScreen`, `FadeScreenFrames` and `FadeMusic` (their value), and `MoveTo`, `NavigateTo`, `PlayVideo` and `PlayVideoSprite` (measured, else 1 s estimated). Every other action takes no time in the sequence; a `MoveSprite` or a `TintSprite` does not block it.

### Playhead

- During a play the playhead follows the real time of the cutscene, read from the director clock of the preview of the attached document (`CutsceneDirector.PlayElapsedSeconds` and `PlayElapsedFrames`, advanced after the coroutines are updated, so the first step of a play always sees time 0). The view scrolls to keep it visible.
- When the cutscene ends with an endless `Repeat`, the playhead loops back on the drawn lap: it shows the play time minus the length of the laps already played. With an `ExitOnInput` before the loop, the time before the loop is not folded.
- At rest the playhead is at 0, and it goes back to 0 when a play ends.
- A click in the ruler moves nothing: the playhead goes back to the play time. Moving the playhead, pause and step are for a later slice.

### Selection

A click on an item selects its action in the tree and in the Inspector; selecting an action in the tree highlights its item and scrolls its row into view. A click in empty space does not change the document selection. A `Repeat` is selected through its drawn lap. When items overlap under the cursor, the most precise one is picked: a marker before a bar, and the shorter of two bars or ranges.

### Scrolling

The ruler stays fixed while the row headers and the rows scroll together vertically; a vertical bar appears only when the rows do not fit. The mouse wheel over the time area zooms, and over the row headers it scrolls. The bottom tab group is about 250 px high in the default layout, which shows only the first rows of a long cutscene: scroll, resize the group or maximise the tab to see the others. Labels never overlap: when a label does not fit, the type of the action is shown, then a truncated type; a marker has no label when it sits on a bar or too close to the next item of its row.

### Timeline limits

- Read-only: no move, resize, add or delete on the timeline.
- Measured durations are those of the **first occurrence** of each action during the last play. The duration of a `MoveSprite` effect is wrong when another move, `ShowSprite` or `HideSprite` interrupts it.
- In the preview a `FadeMusic` does not wait (sound is muted): its bar shows the declared duration while the play goes faster.
- For an endless loop whose lap holds an action of unknown duration that is not yet measured, the loop-back of the playhead becomes exact only after a first lap.
- A `MoveSprite` or a `TintSprite` notifies its end right after its start: the length of its bar is not the length of its action in the director.

## Preview

The document shows a transport bar (a world choice, `Play`, `Stop`, `Restart`, then the play time in seconds and frames), a status line and the preview status line, then the world preview on the whole remaining area. When no preview world can be loaded, a message replaces the preview.

- **Isolated copy.** The preview loads a copy of a world of the project. The world open in the editor is never touched.
- **What is played.** `Play` runs the real cutscene director on a copy of the asset as it currently is in the editor, unsaved edits included. Nothing is written to disk.
- **Rebuilt on every play.** `Stop` does not restore the world, so `Play` and `Restart` always rebuild the preview world from its asset before playing.
- **Preview world choice.** The first world offered is found automatically, in this order: the world remembered for this cutscene, the world whose startup cutscene it is, the project's first loaded world, then the first world by file name. The choice made in the list is remembered per cutscene in `<project>/.casaeditor/cutscene-preview.editor.json`; it is an editor setting and never part of the `.cutscene` file. A project without any world shows a message instead of a preview.
- **Camera.** The preview uses the editor's front 2D camera, framed on all entities of the world. It does not use the camera of the world itself.

### Reported effects

Effects that would leave the preview world are neutralised and reported in the status line:

- A world change (`ChangeWorld`), a screen (`OpenScreen`), a video (`PlayVideo`) and the chaining of `ExitOnInput` are skipped and listed as skipped actions (type and target); the playback continues. When a `ChangeWorld` is skipped, the playback ends on the next step, as the game would replace the world.
- Sound is muted: sound and music actions do nothing, and a music fade does not wait for its duration.
- Screen fades run for their duration, but are shown as text in the status line; they are not drawn over the preview.
- A `MoveTo` that cannot run (missing entity, entity without a character controller, timeout) ends the play as `Failed` with a reason, in the preview as in the game.

### Limits

- Sprite animations, physics and gameplay scripts are frozen in the preview. The status line says so.
- No pause and no seeking: only play, stop and restart. The play time is shown on the timeline (see "Timeline").
- Sound cannot be enabled, fades are not drawn and the input of `ExitOnInput` cannot be simulated.
- The preview applies the editor's execution policy, not the world's virtual resolution, so the framing can differ from the game.
- Editing the cutscene is disabled while a play session of the editor is running.
- The state of the editor's own world in play mode is shown in the Inspector cutscene view under "Editor play mode"; it is not the state of the preview.

### Automation

For remote validation the editor can play the preview without any input: `--project <project.json> --open-asset <Cutscenes/name.cutscene> --cutscene-preview-play --screenshot-out <file.png> --capture-delay <seconds>`. With this switch the capture delay counts from the start of the playback, and the diagnostics file gets the preview state, the skipped actions, the name and position of each preview entity and the name of the editor's current world.

`--activate-panel cutscenetimeline` brings the timeline to the front. The diagnostics also give the timeline: play time in seconds and frames, playhead, total duration, transport text, number of tracks and items, one line per item (track, label, start, duration, shape, exact, estimated or measured), the selected item, and the visible and content heights of the track area with the state of the vertical bar.

Three more switches act on the document after it opens, one step per frame, and save nothing: `--cutscene-reactivate` activates the world viewport panel then the cutscene tab, as a click on the tab does; `--cutscene-select-action <path>` selects the action at a path such as `root_action.actions[1].actions[2]`; `--cutscene-add-action <Type>` then adds an action of that type. The diagnostics also give the selected action, the number of actions, the modified state, the active document and history context, and the document the tree and the Inspector are linked to.

## Text formats

Text boxes, and the text of a numeric field, use the invariant culture.

| Kind | Text | Example |
|---|---|---|
| Number | finite decimal number | `1.5` |
| Integer | whole number | `30` |
| Byte | `0` to `255` | `255` |
| Boolean | `true` or `false` | `true` |
| GUID | standard GUID text | `0f8fad5b-d9cb-469f-a165-70867728950e` |
| Colour | `r, g, b, a`, or `r, g, b` for an opaque colour | `255, 255, 255, 255` |
| Vector | `x, y, z` | `5, 0.5, 0` |
| Enumeration | a defined name of the enumeration | `Additive` |
| String | any text; the empty string is accepted by the field | `orb-right` |
| List | entries separated by commas, none empty | `Submit, Cancel` |

`NaN`, `Infinity` and an overflow such as `1e40` are refused. A number is not accepted for an enumeration. A list entry cannot contain a comma.

An empty text sets a nullable property that is not a string back to "no value".

## Save

- Saving rewrites the file in canonical form: default values are written explicitly, keys follow the order of the serializer, `name` takes the name of the asset catalog, and keys the engine does not know are dropped. The first save of a file may therefore change lines that were not edited.
- A save never destroys data silently. It is refused, and the message gives the path and the key, when:
  - the tree holds an action of an unknown type;
  - an action still carries a format error found by the loader;
  - a *load loss* remains: a value of the file that the loader dropped or rewrote without an error (a `target` that is not a string, an invalid `blend_mode` of `FadeScreen`, an entry of `actions` that is not an object, an `actions` value that is not an array);
  - the file about to be written would not load back cleanly (for example a required `entity` left empty).
- Editing the faulty field clears its error; undoing brings the error back. A faulty `actions` entry has no field: fix such a file outside the editor.
- Format errors and load losses are listed in the Validation section of the Inspector cutscene view, and under the fields of the selected action.

## Code

- `CasaEngine.EditorServices/Cutscenes/`: `CutsceneActionPropertyEditor` and `CutsceneActionProperty` (editable properties, typed access, text, meaning and bounds), `CutsceneEditSession` (asset and load losses), `SetCutsceneActionPropertyCommand` (undoable change), `CutsceneLoadFidelity` (load losses), `CutsceneSaveGuard` (save refusal).
- `CasaEngine.EditorServices/Cutscenes/`: also `CutsceneActionCatalog` (creatable types, categories, defaults), `CutsceneActionSummary` (one-line text), `CutsceneActionTree` (parent, index, path, insertion target), `CutsceneReferenceOptions` (choices and written value of a reference), `InsertCutsceneActionCommand` and `RemoveCutsceneActionCommand` (undoable structure edits).
- `CasaEngine.EditorServices/Cutscenes/`: also `CutscenePreviewWorldResolver`, `CutscenePreviewSettings` and `CutscenePreviewEffectHost` (preview world choice, remembered choice, reported effects).
- `CasaEngine/Framework/Cutscenes/ICutsceneEffectHost.cs`: the runtime injection point, set through `CutsceneDirector.EffectHost`.
- `CasaEngine.Editor/Runtime/CutscenePreviewSession.cs`: loads the world copy and plays the cutscene in it.
- `CasaEngine.Editor/Controls/CutsceneAssetInspectorPanel.cs`: the document panel. It owns the selection, the edit operations (`SetProperty`, `SetProperties`, `AddAction`, `DuplicateSelectedAction`, `RemoveSelectedAction`) and the `SelectionChanged` and `DocumentChanged` events.
- `CasaEngine.EditorServices/Cutscenes/`: also `CutsceneTimelineProjection` and `CutsceneTimelineLayout` (the pure projection of the tree on rows and times, and the playhead mapping `MapPlayTime`), `CutscenePlayTimings` (measured durations, mapped from the played copy back to the edited actions by `CutsceneActionTree.MapByShape`).
- `CasaEngine/Framework/Cutscenes/CutsceneDirector.cs`: `PlayElapsedSeconds`, `PlayElapsedFrames`, and the events `ActionStarted` and `ActionEnded` (`CutsceneActionNotification`). Additions only; without a subscriber the behaviour is unchanged.
- `CasaEngine.Editor/Controls/CutsceneTimelinePanel.cs` and `Controls/Cutscenes/CutsceneTimelineItemRenderer.cs`: the Cutscene Timeline panel (`EditorPanelIds.CutsceneTimeline`) and the drawing of its items. The preview session exposes `ElapsedSeconds`, `ElapsedFrames`, `Timings` and `TimingsVersion`.
- `CasaEngine.Editor/Controls/CutsceneOutlinerPanel.cs`: the action tree in the Hierarchy panel. `CasaEngine.Editor/Controls/CutsceneDetailsPanel.cs`, with `Controls/Cutscenes/CutsceneNumberField.cs` and `CutsceneTextCommitBinding.cs`: the typed fields in the Inspector panel. Both are registered as contextual panels for the cutscene document kind in `GameEditor`.
- History context: `EditorHistoryContextKind.Cutscene`. Save source: `EditorAssetSaveSource.CutsceneEditorPanel`.

## Limits

- **Closing a modified cutscene tab loses its changes without a prompt.** Save first.
- A string longer than 256 characters, or one that holds a line break, is shown as an empty field. Do not give the focus to such a field: leaving it replaces the value with an empty string (the change can be undone).
- The structure can be edited only by add, duplicate and delete. Actions cannot be moved, reordered by mouse, wrapped, copied and pasted, or selected several at a time.
- A container emptied of its children, or a new action with empty required fields, is invalid but can still be saved: the Inspector shows the validator messages.
- The editor checks that a named entity exists only in the preview world list, not in every world. Clicking an entity in the preview to fill a field is not done.
- `ExitOnInput.inputs` and `PlaySound.bus_name` stay text boxes.
- The categories of the add list are a prefix, not a menu: the drop-down list cannot group.
- Long summaries in the Hierarchy panel are cut at the right edge; a horizontal scroll bar reads them.
- An action of an unknown type cannot be edited, and blocks the save.
- The timeline is read-only and the preview has no pause or seeking (see "Timeline" and "Preview").

## Next

- Moving, wrapping, copying and pasting actions; drag and drop.
- Picking an entity by clicking it in the preview.
- A sandbox for the preview: drawn fades, sound toggle, simulated input, per-world execution policy.
- Moving the playhead, pause and step.
- Editing on the timeline: reorder, then free placement.
