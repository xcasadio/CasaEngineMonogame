# Cutscene editor

Decisions: see ADR-0095, ADR-0096, ADR-0097, ADR-0098, ADR-0099, ADR-0100, ADR-0101, ADR-0102 and ADR-0103.

## Summary

Opening a `.cutscene` asset from the Content Browser shows a document tab and fills two dock panels: the **Hierarchy** panel shows the action tree, and the **Inspector** panel edits the properties of the selected action in typed fields. Values and structure (add, duplicate, delete, move, wrap, ungroup, cut, copy, paste) can be edited, undone, redone and saved. The action tree (`CutsceneAsset.RootAction`) is the only source of truth: there is no separate editing model. The file format gained one action type, `At`, which places a group of actions in time (see "Placing actions in time"); nothing else in it changed.

The document itself keeps the transport bar, a status line and a preview of an isolated copy of a world to play the cutscene being edited (see "Preview"). The preview draws the screen fades, can simulate the input of an `ExitOnInput` and can play the sound on demand (see "Drawn fades", "Simulated input" and "Sound"). A **Cutscene Timeline** panel shows the tree in time, and lets an action be dragged to another instant (see "Timeline" and "Placing actions in time"). The structure is edited by mouse in the Hierarchy tree and from a context menu shared with the timeline (see "Structure editing").

## Usage

1. Open a `.cutscene` file from the Content Browser. Clicking its tab again makes it the active document, so undo and redo target its history.
2. **Hierarchy** shows one row per action: its type, then a short summary (`MoveSprite  orb-left -> (-60, 35) @ 120`, `Wait  1.5 s`, `Repeat  forever`). Select a row to edit that action.
3. **Inspector** shows the type and path of the selected action (`root_action.actions[2]`), then one row per property: a fixed-width label and a field of the right kind (see "Typed fields"). Without a selection it shows the cutscene view: title, file path, validation, load losses and the "Editor play mode" runtime state.
4. Commit a value with Enter, or by leaving the field. A numeric field also has `-` and `+` buttons that commit at once. Each commit is one history entry. A refused value puts the old value back and the reason is shown under the field.
5. Text typed in a field and not yet committed is committed when another action is selected, to the action it was typed for.
6. Undo and redo with the editor's global commands while the cutscene tab is active.
7. A modified document shows `*` in its tab title. Save it with the editor's save command, or with "save all".

## Action tree

- **Hierarchy bar.** A flat drop-down lists the creatable action types, each prefixed by its category (`Sprites / MoveSprite`); the categories are Flow, Timing, Movement, Sprites, Audio, Screen and World and input. Buttons: `Add`, `Duplicate`, `Delete`. A right click on a row opens the structure menu (see "Structure editing"). The footer shows the number of actions.
- **Add.** If the selected action is a `Sequence`, `Parallel`, `Repeat` or `At`, the new action becomes its last child. Otherwise it is inserted right after the selection, in the same parent. With no selection it is appended to the root when the root is a container. The new action is selected.
- **Defaults.** A created action has the default values of its class, except `Repeat.count` = 1 and `WaitFrames.frames` = 1. It can therefore be invalid until its required fields are filled: the Inspector lists the validator messages. A save is still allowed, as for any cutscene the validator criticises.
- **Duplicate.** Inserts a deep copy right after the source and selects it. The copy goes through the serializer, so it is refused, with a message, for a subtree that holds a format error, a load loss or an unknown action type: the copy would lose those marks.
- **Delete.** Removes the action and its children. A load loss that belongs to the removed subtree is removed with it and comes back at its original place on undo. The root action can be neither deleted nor duplicated.
- **Selection and expansion.** The tree is rebuilt only when the document changes. The selection and the expanded nodes are kept by action; everything is expanded on first opening. A selection that is no longer in the tree after an undo, a redo or a reload is cleared.
- Add, duplicate and delete are history entries that mark the document modified. If the preview was playing, it restarts on the edited asset.

## Structure editing

The structure of the tree is edited one action at a time: there is no multiple selection. To group several actions, wrap one, then drag the others into the container. The file format does not change, and every operation below is one history entry that can be undone and redone, marks the document modified and replays the preview like any other edit. An operation that is refused (rules below) shows its reason on the status line under the tree and changes nothing.

### Drag and drop in the tree

Press the left button on a row and drag it; the selection does not change when the drag starts. The root and the expander arrow of a row cannot be dragged, and neither can a drag started with the right button. Releasing the button moves the action to the marked destination. The action keeps its identity (it is moved, not copied), and after a move it is the selected action, whether it was selected before or not.

The pointer position gives the destination. The band of a row runs from its top to the top of the next visible row, so the gap between two rows belongs to the row above, and the band of the last row ends at its bottom.

| Row under the pointer | Zone of its band | Destination | Marker |
|---|---|---|---|
| Leaf | Top half | Before the row, in its container | A line at the indentation of the row |
| Leaf | Bottom half | After the row, in its container | A line at the indentation of the row |
| Container (`Sequence`, `Parallel`, `Repeat`) | Top quarter | Before the row | A line at the indentation of the row |
| Container | Middle half | Inside it, as its last child (also when it is collapsed or empty) | A frame around the row |
| Container | Bottom quarter | After the row; for an expanded container that has children, as its first child | A line at the indentation of the destination |
| Root | Middle half | Inside it, as its last child | A frame around the row |
| Root | Top and bottom quarters | Refused: the root has no side | None |
| Under the last row, inside the visible area of the tree | | The end of the root | A line at the indentation of the children of the root |
| Above the first row, or outside the visible area of the tree (right of it, below it, over the status line) | | None | None |

A refused destination shows no marker, and releasing there does nothing. The refusals are: the root as the dragged action, a container dropped into itself or into one of its own descendants, a destination that is not a container, and a move that would change nothing (the action would stay where it is).

Escape cancels the drag. A rebuild of the tree, which any change of the document causes (an undo in the middle of a drag, for example), or a change of the document shown, also cancels it: releasing the button afterwards does nothing. The marker is drawn by the tree itself and bounded to its visible area.

### Context menu

A right click on a row of the Hierarchy tree selects it and opens the menu; a right click on an item of the Cutscene Timeline selects its action and opens the same menu. A right click on a track header opens nothing; a right click on the empty part of a track opens `Add here` when a `Parallel` can receive the action (see the placement in time), and nothing otherwise. A right click on the empty part of the tree clears the selection, and the menu then only offers `Add` and `Paste`, which insert at the end of the root.

| Entry | Effect |
|---|---|
| `Add` | Sub-menu per category, then per type; inserts with the rule of the `Add` button |
| `Wrap in` | `Sequence`, `Parallel`, `Repeat` or `At`: the action is replaced by a new container that holds it. The root can be wrapped: the container becomes the root. A new `Repeat` runs once; a new `At` waits 0 s |
| `Ungroup` | Replaces a container that is not the root by its children, in order, at its place; an empty container is simply removed. The count of a `Repeat` and the delay of an `At` are lost, and come back on undo |
| `Move up`, `Move down` | Swap the action with its neighbour in the same container; an action never leaves its container this way. Absent for the first child (up) and the last child (down) |
| `Cut`, `Copy`, `Paste` | See "Clipboard" |
| `Duplicate` | Inserts a deep copy right after the action |
| `Delete` | Removes the action and its children |

The menu is built when it opens, and an entry whose operation would be refused is not shown: no `Ungroup` on a leaf, no `Paste` with an empty clipboard, no `Cut` or `Delete` on the root, no `Cut`, `Copy` or `Duplicate` on an action that holds a load loss, a format error or an unknown type. Separators never follow each other and never end the menu.

After an operation the selection follows: the moved action stays selected; a wrap selects the new container; an ungroup selects the first freed child (nothing when the container was empty); a paste selects the copy; a cut selects nothing. A container that appears in the tree, by a wrap, a paste or an undo, is shown expanded; selecting a container that is already there and collapsed does not expand it.

### Clipboard

The clipboard is an object of the editor, shared by every cutscene document: copy in one document, paste in another. It holds one deep copy of one action, with its whole subtree. It is not the clipboard of the system.

- Every paste inserts a new copy, at the place the `Add` rule gives for the selection: the end of the selected container, otherwise right after the selected action, otherwise the end of the root.
- Cut is a copy followed by a delete, in one history entry. The root can be copied but not cut.
- Copy and cut are refused for a subtree that holds an unknown type, a format error or a load loss: the copy would lose them silently.
- An action that names an entity absent from the world of the preview is pasted as it is, and the validator reports it.

### Keyboard

- Ctrl+C, Ctrl+X, Ctrl+V and Ctrl+D go through the global shortcuts of the editor. When the active document is a cutscene they act on that document and nothing else; otherwise they keep their behaviour for screen documents. A text field that has the focus still receives these keys itself, as before.
- Delete removes the selected action only while the tree has the keyboard focus. It is not a global shortcut. The arrow keys, Home, End, Space and Enter belong to the tree. There is no keyboard move.

### Limits of the structure editing

- One action at a time; no multiple selection.
- The timeline items cannot be resized, and only some of them can be moved: see "Placing actions in time".
- No automatic scroll near the edges and no automatic expansion of a collapsed container during a drag: dropping on a collapsed container adds to its end, and a row out of view cannot be reached until the tree is scrolled by hand (scroll before the drag, or use the menu).
- No drag of a type from a palette, and no drag from one document to another.
- A load loss records the path of its action as read from the file. That path is not updated when an action changes place, as it was already not updated by an insertion or a deletion: the path shown in a save refusal can be out of date.
- The keys themselves are checked only by hand; the automation reaches the same methods without the keyboard (see `shortcut:` below).

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

The **Cutscene Timeline** panel is a contextual tool panel: it appears in the bottom tab group only while a cutscene document is active, like the Animation2D timeline, and it comes to the front when a cutscene is opened. It is a **projection of the action tree**: the tree stays the only model and the timeline has no data of its own. The only edit it offers is to place an action at another instant (see "Placing actions in time"); items cannot be resized, and tracks cannot be edited. It can be docked, resized and maximised like any panel.

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

`Sequence`, `Parallel` and `At` have no item of their own. Two bars or ranges of a row that overlap are spread over sub-rows (only the first sub-row carries the row label); a marker stays on the first sub-row.

### Reading the shapes

| Shape | Meaning |
|---|---|
| Solid bar | A known duration: `Wait`, `WaitFrames`, `FadeScreen`, `FadeScreenFrames`, `FadeMusic`, `TintSprite`, the fade of `PlayMusic` and `StopMusic` |
| Dashed outline, lighter fill | An **estimated** duration: one that only a play can measure (`MoveSprite` effect, `MoveTo`, `NavigateTo`, `PlayVideo`, `PlayVideoSprite`, shown as 1 s), or a duration given in frames and converted at 60 frames per second |
| Bar open on the right | `ExitOnInput`: it lasts until the end of the cutscene |
| Range | The span of a `Repeat`, on the Loops row |
| Diamond with its label | An instant: an action with no duration |

After a play, the measured durations replace the estimates (the bar becomes solid). Any change of the document clears them, and a play in progress restarts and measures again.

Time on the ruler is in seconds. The text under the transport buttons reads a state word (`Seeking`, `Paused`, `Playing`, `Ended` or `Stopped`), the play time in seconds and the number of frames, for example `Playing   2.00 s  ·  120 frames` (see "Transport"). It is rewritten only when the word, the tenth of a second or, while paused, the number of frames changes.

### How time is laid out

- A `Parallel` starts all its children at the same instant and lasts as long as its longest branch. The one-update shift the runtime adds to each branch is not drawn.
- An `At` holds its sequence for `seconds`, then its children follow one after the other from that instant: the children are drawn at the start of the `At` plus `seconds`, and what follows the `At` is placed after the children.
- A `Repeat` is drawn as **one lap** under a range `Repeat ×N` or `Repeat forever`. For a count above 1, a second range `×(N-1) more` covers the other laps and what follows is placed after them. A `Repeat` with no count is drawn as `Repeat ×?`, one lap.
- The durations that move the sequence on are those of `Wait`, `WaitFrames`, `FadeScreen`, `FadeScreenFrames` and `FadeMusic` (their value), and `MoveTo`, `NavigateTo`, `PlayVideo` and `PlayVideoSprite` (measured, else 1 s estimated). Every other action takes no time in the sequence; a `MoveSprite` or a `TintSprite` does not block it.

### Playhead

- The playhead always shows the play time of the preview session, whether a play runs or not (`CutsceneDirector.PlayElapsedSeconds` and `PlayElapsedFrames`, advanced after the coroutines are updated, so the first step of a play always sees time 0). The view scrolls to keep it visible while the preview plays or seeks, not while it is paused.
- When the cutscene ends with an endless `Repeat`, the playhead loops back on the drawn lap: it shows the play time minus the length of the laps already played. With an `ExitOnInput` before the loop, the time before the loop is not folded.
- At rest, before any play and after `Stop`, the playhead is at 0. When a play ends by itself, the playhead stays at the end time.
- A click in the ruler, or in the empty space of the track area, moves the preview to that time and leaves it paused (see "Transport"). The time is limited to `[0, duration of the projection]`. Holding the left button down in the ruler and moving the mouse moves the playhead continuously (the `rulerDragScrub` option of the generic timeline, enabled for this panel only; see [timeline-generic.md](timeline-generic.md)). If the document has no preview, the click moves nothing and the playhead goes back to the play time.

### Selection

A click on an item selects its action in the tree and in the Inspector; selecting an action in the tree highlights its item and scrolls its row into view. A click in empty space does not change the document selection. A `Repeat` is selected through its drawn lap. When items overlap under the cursor, the most precise one is picked: a marker before a bar, and the shorter of two bars or ranges.

### Scrolling

The ruler stays fixed while the row headers and the rows scroll together vertically; a vertical bar appears only when the rows do not fit. The mouse wheel over the time area zooms, and over the row headers it scrolls. The bottom tab group is about 250 px high in the default layout, which shows only the first rows of a long cutscene: scroll, resize the group or maximise the tab to see the others. Labels never overlap: when a label does not fit, the type of the action is shown, then a truncated type; a marker has no label when it sits on a bar or too close to the next item of its row.

### Timeline limits

- Items cannot be resized, deleted or moved from a row to another; the keyboard shortcuts of the generic control (Delete, Backspace, Ctrl+D, Ctrl+C) do nothing here. The structure is edited in the tree and from the context menu.
- Measured durations are those of the **first occurrence** of each action during the last play. The duration of a `MoveSprite` effect is wrong when another move, `ShowSprite` or `HideSprite` interrupts it.
- In the preview a `FadeMusic` waits for its duration, as in the game, whether the sound is on or off (see "Sound"): its bar and the play agree.
- For an endless loop whose lap holds an action of unknown duration that is not yet measured, the loop-back of the playhead becomes exact only after a first lap.
- A `MoveSprite` or a `TintSprite` notifies its end right after its start: the length of its bar is not the length of its action in the director.

### Placing actions in time

Free placement is carried by one action of the format, **`At`**, and by nothing else: the tree stays the saved model, and the timeline reads and changes it.

```json
{ "type": "At", "seconds": 1.5, "actions": [ { "type": "MoveSprite", "...": "..." } ] }
```

- **Meaning.** An `At` waits `seconds`, counted from the instant it starts, then plays its children one after the other, as a `Sequence` does. It is exactly `Sequence[Wait(seconds), ...]`, with the delay as an explicit value that the timeline can read and change. In a `Parallel`, all the `At` start together, so `seconds` is the instant of each clip inside that block.
- **Values.** A missing `seconds` is 0. A negative value, or one that is not a number, is a validation error; an `At` without children gives the warning that every empty container gives. The schema version did not change: an older engine loads `At` as an unknown action type and refuses the cutscene with an error, instead of playing it wrongly.
- **In the editor.** `At` is a container: it is in the `Flow` category of the add list (created at 0 s), in `Wrap in`, `Ungroup`, drag and drop in the tree, copy and paste. The tree shows `At  1.5 s`, and the `Seconds` field of the Inspector sets an exact instant. An `At` has no item of its own on the timeline.
- **What can be dragged.** Only the main item of an action can be dragged: not the "more" range of a `Repeat`. The rule starts from the action of the item and goes up, and the first parent met decides:
  1. the parent is an `At` and the node is its first child: the drag changes the `seconds` of that `At`; if the node is not the first child, nothing can be dragged;
  2. the parent is a `Parallel`: the drag wraps the node in a new `At`, in one history entry;
  3. the parent is a `Sequence` or a `Repeat` and the node is its first child: the rule starts again with that parent as the node; if it is not the first child, nothing can be dragged;
  4. at the root, nothing can be dragged.

  Why only these: in a `Sequence` the start of an action is the end of the previous one, so moving it alone would mean changing the previous action; the delay in front of a first child, on the other hand, belongs to its group. For example, in `Parallel > Sequence > [X, Y]`, dragging X wraps the `Sequence` in an `At`; after that, dragging X changes that `At`; Y can never be dragged.
- **How a drop is converted.** By difference: the new `seconds` is the current one (0 for a wrap) plus the drop instant minus the current start of the item. The result is aligned on frames (1/60 s) and cannot go below 0: during the drag, the item stops at the instant where `seconds` would be 0. A bar keeps the place where it was grabbed (the `keepGrabOffset` option of the generic timeline, enabled for this panel; see [timeline-generic.md](timeline-generic.md)): the gap between the cursor and the start of the bar, measured when the bar is grabbed and limited to the length of the bar, stays the same during the drag, so a bar grabbed in its middle moves by the distance of the cursor and does not jump. A marker has no length: its instant goes under the cursor. A drop that leaves the value unchanged does nothing.
- **Recognising a draggable item.** Its left edge is drawn lighter and its tooltip says what the drag does (`Drag in time: changes the delay of its At (now 0.4 s).` or `Drag in time: wraps it in an At.`). Other items keep their usual drawing.
- **Room after the end.** The timeline is longer than the cutscene by the larger of a quarter of its duration and 2 s, so that a clip can be dragged later than the current end. The playhead, the seeks and the open bars keep the real duration.
- **Ctrl and the keyboard.** Ctrl + drag, which the generic control turns into a duplication, does nothing; duplicate from the menu or with Ctrl+D. The generic control reserves Delete, Backspace, Ctrl+D and Ctrl+C while an item that can be dragged has the focus, and does nothing with them; the global shortcuts of the editor still act.
- **Add here.** A right click on the empty space of a track row, with a `Parallel` selected (or an action inside one), offers `Add here`, then a category, then a type. The action is created inside a new `At` placed at the end of that `Parallel`, at the instant of the cursor counted from the start of the `Parallel` (aligned on frames, not below 0). It is selected. With no target `Parallel`, the entry is not offered. The menu of an item is the one of the structure editing, unchanged.
- **Preview.** Placing an action is a change of the document: the measured durations are cleared and the preview replays under the usual rules. Until the next play, items whose duration is measured use their estimated duration again and can move by as much. A branch of a `Parallel` starts one update after it, as before, so a clip at 0.5 s starts one frame later in the preview than its bar says.
- **Refusals.** A drag that cannot be applied changes nothing and the item returns to its place; the reason is kept for the diagnostics and is not shown on the status line.
- **Sample.** `Projects/SpriteCutsceneSample/Cutscenes/stagger.cutscene`: four sprites are shown, then a `Parallel` of four `At` (0, 0.4, 0.8 and 1.2 s) each move one sprite, then a `Wait` of 1 s. No world starts it: open it in the editor.

#### Limits of the placement

- A bar cannot be stretched: a duration stays a property of the action, set in the Inspector.
- No snapping to other items; the drag only snaps to frames.
- An action cannot be moved from one row to another: the row comes from the entity of the action.
- One action at a time; no multiple selection.
- An existing cutscene is not converted to placed clips: only the `At` actions it holds can be dragged, plus the first child of a `Parallel`, which a drag wraps.
- `Add here` is offered only on a track row: the control does not ask for a menu below the last row.
- The real drag with the mouse can only be checked by hand: the automation reaches the same operation without the mouse (`place` and `addat` below), and the unit tests simulate the drag.

## Preview

The document shows a transport bar (a world choice, `Play` / `Pause`, `Stop`, `Restart`, `-1 frame`, `+1 frame` and the `Sound: off` / `Sound: on` button, with the state word and the play time under them), a row of input buttons that is collapsed until an input is armed, a status line and the preview status line, then the world preview on the whole remaining area. When no preview world can be loaded, a message replaces the preview.

- **Isolated copy.** The preview loads a copy of a world of the project. The world open in the editor is never touched.
- **What is played.** `Play` runs the real cutscene director on a copy of the asset as it currently is in the editor, unsaved edits included. Nothing is written to disk.
- **Rebuilt when needed.** `Play` from the start, `Restart`, `Stop` and every move backwards rebuild the preview world from its asset (see "Transport"). The world a play leaves behind is kept until one of these.
- **Preview world choice.** The first world offered is found automatically, in this order: the world remembered for this cutscene, the world whose startup cutscene it is, the project's first loaded world, then the first world by file name. The choice made in the list is remembered per cutscene in `<project>/.casaeditor/cutscene-preview.editor.json`; it is an editor setting and never part of the `.cutscene` file. A project without any world shows a message instead of a preview.
- **Camera.** The preview uses the editor's front 2D camera, framed on all entities of the world. It does not use the camera of the world itself. `Play` from the start and `Restart` frame the entities again; going to a time, `-1 frame`, `Stop`, and a `Play` or `+1 frame` that leaves the first instant (see "Time 0") rebuild the world but keep the camera as you left it.
- **Status line.** While a play runs, the preview status line starts with the word of the transport bar (`Preview: Playing.`, `Preview: Paused.` or `Preview: Seeking.`); when no play runs it starts with the state of the last play (`Completed`, `Failed`, `Stopped`...). It is rewritten only when what it shows changes.

### Transport

The state word and the label of the `Play` / `Pause` button follow this table, read from top to bottom, first true line:

| Condition | Word | Button | What the button does |
|---|---|---|---|
| A play runs and the preview is seeking | `Seeking` | `Pause` | Abandons the seek and stays paused |
| A play runs and the preview is paused | `Paused` | `Play` | Resumes |
| A play runs | `Playing` | `Pause` | Pauses |
| No play runs, and the state is `Completed` or `Failed`, or `Stopped` after a world change | `Ended` | `Play` | Plays again from the start |
| Otherwise (before any play, after `Stop`, invalid asset, no world) | `Stopped` | `Play` | Plays from the start |

- **Pause.** A pause freezes the preview world: it stops advancing and nothing else changes. It is done in the editor; the runtime has no pause.
- **`+1 frame`.** Pauses, then advances the world by one step of 1/60 s. It does nothing when no play runs. `-1 frame` goes to the current time minus 1/60 s (not below 0), even after a play has ended.
- **Time 0.** Going to time 0 (or back from the first frame) shows the first instant of the cutscene: after the rebuild the world receives one update of zero duration, so the actions placed at time 0 (a `ShowSprite`, for example) have played and their sprites are visible. That update is not counted in the frames shown (`0 frames`). This start state is only for display: a play continued from it would be one frame ahead of the clock for everything that follows. So any action that leaves it (a seek to another time, `Play` during the pause, `+1 frame`) launches the play again, without the zero-duration update, and then does what it usually does. The state shown at a time therefore never depends on the path followed. `Stop` is unchanged: it shows the state before the cutscene. Limit: actions at time 0 inside a `Parallel` are not shown, because its branches only start at the next update.
- **After the end.** The pause and the seek exist only while a play runs. As soon as no play runs, however its end was reached (by itself, or during a seek), the transport goes back to `Running`: the preview world advances with the editor frames, like the game world after a cutscene.
- **Going to a time.** A click or a drag in the timeline ruler, or the `--cutscene-preview-seek` switch, replays the cutscene to that time and leaves the preview paused. The play time and the target agree to within one step. If the play ends before the target (a `ChangeWorld` skipped, a failure, a shorter cutscene), the seek stops there, the state is `Ended` and the transport goes back to `Running`.
- **How a seek runs.** The preview world advances by fixed steps of 1/60 s until the play time reaches the target. When the target is after the current time, the play still runs and the document has not changed since it started, the world goes on from its current state. Otherwise the world is rebuilt from its asset and the cutscene starts again from the beginning. A seek spends about 8 ms of steps per editor frame at most (and always at least one step), so the editor stays responsive; the state is `Seeking` meanwhile and the viewport shows the advance.
- **`Stop`.** Rebuilds the preview world from its asset and puts the playhead back at 0: the state is `Stopped`, and the screen veil and the skipped actions of the previous play are forgotten. This replaces the earlier behaviour, where the world kept the state the play had left.
- **End of a play.** When a play ends by itself, the world keeps its final state, the playhead stays at the end time and the state is `Ended`.
- **Editing while paused.** Any change of the document replays up to the same time and stays paused (the target time during a seek, the play time during a pause). During a play, a change restarts the play from the beginning. When no play runs, nothing is replayed. Saving counts as a change, so a save during a pause replays to the same time.
- **Measured durations.** Seeking and `Stop` keep the durations measured so far, as long as the document does not change, so the bars of the timeline do not go back to "estimated" when you go back in time. `Play` from the start and `Restart` measure again from an empty record.

### Reported effects

Effects that would leave the preview world are neutralised and reported in the status line:

- A world change (`ChangeWorld`), a screen (`OpenScreen`), a video (`PlayVideo`) and the chaining of `ExitOnInput` are skipped and listed as skipped actions (type and target); the playback continues. When a `ChangeWorld` is skipped, the playback ends on the next step, as the game would replace the world. The `ExitOnInput` that is skipped is also remembered as the armed input (see "Simulated input").
- Sound is off when the document opens: the sound actions run, with their durations, but nothing is heard until the `Sound` button is turned on (see "Sound").
- Screen fades are drawn over the preview image (see "Drawn fades"), and the status line also says so in text.
- A `MoveTo` that cannot run (missing entity, entity without a character controller, timeout) ends the play as `Failed` with a reason, in the preview as in the game.

### Drawn fades

A `FadeScreen` or a `FadeScreenFrames` darkens, lightens or tints the image of the preview, at the instant of the playhead, with the same blend as the game: opaque, alpha, additive or subtractive.

- **How it is drawn.** An overlay registered on the render view of the preview viewport draws one white pixel stretched over the whole current viewport of the graphics device, coloured with the `rgba` of the screen effect of the play, after the interface of the view. It is drawn in screen coordinates because the preview camera is not a 2D camera, so the placement formula of the game does not apply. Every graphics device state it changes is restored.
- **Blend states.** The four blend states are copies of the private ones of the engine sprite renderer, which are not reachable from the editor; a test compares them field by field with the engine ones, so a divergence shows.
- **Nothing is drawn** when the effect is inactive or when its alpha is 2 or less, as the sprite shader of the engine discards those texels.
- **Same effect, same instance.** The screen effect of the preview is one object for the whole life of the document; the start of a play, `Stop`, `Restart` and a change of preview world put it back in its first state (black, alpha 255, default blend, inactive), so a fade of a previous play never shows in the next one.
- **Status line.** It keeps the text mention (`Screen fade in progress.` or `Screen overlay rgba(r,g,b,a).`), which says what the image shows even when the veil is faint.
- An `OpenScreen` does not open a screen in the preview, so the order between the veil and a screen does not arise.

### Simulated input

In the game, an `ExitOnInput` arms a watch: the next press of one of its inputs plays another cutscene. The preview cannot press a key, so it simulates the press with a button.

- **Armed input.** While a play runs, the last `ExitOnInput` the play went through is the armed input; a new one replaces the previous one, and the same one met again in a loop does not change it. The armed input is dropped at the end of every play, by `Stop`, `Restart` and a change of preview world.
- **Buttons.** A row under the transport bar shows one `Press "<name>"` button per input of the armed action. It is collapsed while nothing is armed. It is rebuilt, on the following frame, only when the armed action changes, never from the click itself.
- **What a press does.** The play ends, the state is `Ended`, the world keeps its state and the playhead stays where it was. The status line says `Input "<name>": the game would play <cutscene>. The play ends here.` (`no cutscene` when the action has none). The next cutscene is not played in the preview.
- **When it acts.** Only while a play runs or is paused, not while it seeks, and only for an armed name. It goes through no device: it is a button, not a key.
- **Moves in progress.** A `MoveSprite` started before the press goes on to its end after the play has stopped, as in the game, because the move does not belong to the cutscene director; only the clock of the play is frozen.

### Sound

The `Sound: off` / `Sound: on` button is the last button of the transport bar. It is off whenever a document opens and its state is not remembered, so no preview makes a noise unless asked.

- **Durations are those of the game.** The sound actions always have an audio service: the preview owns a silent one, built on the silent backend of the engine and recreated at every start of a play. So `FadeMusic` waits for its duration whether the sound is on or off, and a seek replays exactly the course of a play.
- **When it is heard.** The audio service of the editor (the one of the sound inspector, shared with the play mode of the editor) is given to the cutscene only when the sound is on **and** the step is a normal play step: a step made while the transport is `Running`. It is not given for the steps of a seek, for `+1 frame`, nor for the zero-duration step of the first instant (see "Time 0"), which use the silent service: nothing is heard during a seek, or every sound of the path would start at once. A music started before the instant reached by a seek is not heard afterwards: use `Restart`.
- **Stop, not pause.** The preview gives the audio service of the editor back clean at every stopping point: `Pause`, `Stop`, a seek that goes on from the current state, `+1 frame`, `Restart`, the end of a play, turning the button off, a change of preview world, deactivating the tab and closing the document. It stops the voices owned by the preview world, stops the music, and puts the mixer back in the state captured at the start of the play. Resuming after a pause does not restart a music.
- **Mixer state.** The state of the editor mixer is captured once per play, just after the previous play is cleaned, whether the sound is on or not; turning the button on never captures. A music fade writes the volume of the music bus, which is the one of the whole editor: that is why the state is put back.
- **Tab in the background.** When the tab of the document is no longer the active document, the sound is turned off, and the button goes back to `Sound: off`.
- **Master bus.** If the project mutes the `Master` bus of its mixer, the sound stays inaudible even when the button is on. The diagnostics say whether the master bus is muted.

Limits of the sound:

- A music fade started during an audible play goes on acting on the music volume of the editor while the button is turned off, or during a seek or frame steps that follow, until the next stopping point, where the state is put back.
- Putting the mixer state back also cancels a mixer setting made elsewhere between the start of the play and that stopping point.
- The silent audio service writes a warning in the log for each sound it cannot play.
- Stopping the music acts on the music player shared by the editor; `StopMusic` and a `PlayMusic` with a crossfade played by a cutscene act on it too. The voices are stopped only for the preview world.
- No sample plays a sound: the sound is checked by tests with a fake audio backend, and by ear.

### Limits

- Sprite animations, physics and gameplay scripts are frozen in the preview, because the editor execution policy is shared by all the worlds of the editor; changing it for the preview world alone is a separate decision. The status line says so. A video played on a sprite does play.
- No keyboard shortcut for the transport (space, arrows): the buttons and the timeline are the only way.
- Going back in time replays from the start, so each move backwards rebuilds the world from its asset. On a large world a drag backwards in the ruler rebuilds it at every mouse move; this cost has not been measured on a large world.
- A seek advances by fixed steps of 1/60 s, a play by the editor frames. An action counted in frames (`WaitFrames`, `FadeScreenFrames`) can therefore fall at a slightly different time in a play and in a seek: the same cutscene can end one step later when reached by a seek (for example 61 frames instead of 60 for a one-second fade).
- The first instant (time 0) does not show the actions placed at time 0 inside a `Parallel`.
- An effect that outlives the last action (a sprite move still running) goes on after the end in the preview whatever the way the end was reached, since the world is live again.
- The cutscene that an `ExitOnInput` or a `ChangeWorld` leads to is announced, never played.
- Pausing the sound and resuming it is not offered: a pause stops it (see "Sound").
- The preview applies the editor's execution policy, not the world's virtual resolution, so the framing can differ from the game.
- Editing the cutscene is disabled while a play session of the editor is running.
- The state of the editor's own world in play mode is shown in the Inspector cutscene view under "Editor play mode"; it is not the state of the preview.

### Automation

For remote validation the editor can play the preview without any input: `--project <project.json> --open-asset <Cutscenes/name.cutscene> --cutscene-preview-play --screenshot-out <file.png> --capture-delay <seconds>`. With this switch the capture delay counts from the start of the playback, and the diagnostics file gets the preview state, the skipped actions, the name and position of each preview entity and the name of the editor's current world.

Three more switches drive the transport, after the play and the edits above, one step per frame; the capture delay counts from the end of the last step:

- `--cutscene-preview-seek <seconds[,seconds...]>` goes to each time in turn, the next one starting when the previous seek has finished. Numbers use the invariant culture. A list holding an unreadable or negative value is ignored as a whole.
- `--cutscene-preview-step <n>` then advances by `n` frames, or goes back `|n|` times by one frame when `n` is negative, each step waiting for the end of the previous seek (a non-zero integer; zero, a decimal or text is ignored).
- `--cutscene-preview-stop` then stops the preview, as the `Stop` button does.

Two more switches change the order or add a step:

- `--cutscene-edit-after-transport` applies the edits (`--cutscene-select-action`, `--cutscene-add-action`, see below) after the seeks, steps and stop instead of before. The automation then waits for the replay that the edit triggers to finish before the capture delay starts counting.
- `--cutscene-preview-resume` resumes the play after all other steps, as `Play` does during a pause. It acts only when the state is `Paused` at that moment; otherwise the diagnostics note it as refused. When `--cutscene-preview-stop` is also given, only the stop is applied and the diagnostics say so.

Two more switches drive the simulated input and the sound:

- `--cutscene-preview-sound` turns the sound of the document on before any play, seek, step or edit; those steps, and the capture, wait for it.
- `--cutscene-preview-input <name>` is the last step of the transport, after the resume and the edits; the capture delay counts from it. Without a play it is refused at once. With a play it waits for the preview to have made a step and for an input to be armed, for 120 editor frames at most (a seek in progress is waited for without counting), then presses the input, or is refused and noted. A name that starts with `--` is not taken for a name. With `--cutscene-preview-stop` it is ignored and the transport options line says so, as for the resume.

The diagnostics also give the screen veil (active, `rgba`, blend, fading), the armed inputs and their cutscene, the simulated input and its cutscene, the sound (button label, on or off, presence of the editor audio service, master bus muted or not, editor audio service lent since the launch) and the step count since the launch: normal play steps, other steps, and the nature of the last step. The transport options line also gives the sound and input switches and their result (`done` or `refused (...)`).

The diagnostics add the transport options and the last transport step, the state, the transport mode, the seek target, the pending frame steps, the play time, the label of the `Play` / `Pause` button, the last seek requested from the timeline ruler and the number of play launches since the document opened (each successful world rebuild followed by a launch of the cutscene; `Stop` and a world that fails to load are not counted).

`--cutscene-edit-ops "<operation>;<operation>;..."` applies the structure operations, one per frame, after `--cutscene-select-action` and `--cutscene-add-action` (or after the transport steps with `--cutscene-edit-after-transport`), with the same wait. Operations are separated by `;`. A name runs to the first `:`, except for `move`, `draghover` and `addat`, whose last `:` separates the final argument; a path never contains `:` or `;`. Seconds are a finite number in the invariant culture with a decimal point (`1.6`, not `1,6` or `1.6 s`). Nothing is saved.

| Operation | Effect |
|---|---|
| `select:<path>` | Selects the action at the path |
| `add:<Type>` | Adds an action, as the `Add` button does |
| `move:<container path>:<index>` | Moves the selected action to the container; the index counts among the children **before** the move |
| `up`, `down` | Moves the selected action up or down |
| `wrap:<Type>` | Wraps the selected action in `Sequence`, `Parallel` or `Repeat` |
| `ungroup`, `duplicate`, `delete` | As the menu entries |
| `copy`, `cut`, `paste` | Clipboard operations on the selection |
| `undo`, `redo` | The global undo and redo |
| `shortcut:<copy\|cut\|paste\|duplicate>` | Calls the method of the global shortcut; refused when the active document is not a cutscene |
| `menu` | Opens the context menu of the selection, at its row; the menu is not closed when the real mouse is far away, so a capture can show it |
| `draghover:<target path>:<before\|after\|into>` | Puts the tree in the state of a drag of the selected action over that zone of the target row, without the mouse; the marker stays for the capture |
| `place:<seconds>` | Places the selected action at that instant of the timeline, as a drag released there would; refused for an action with no main item on the timeline, such as a container, and when the value does not change |
| `addat:<Type>:<seconds>` | As `Add here` at that instant: adds a new action of that type, in an `At`, at the end of the target `Parallel` of the selection |

An unknown, malformed or refused operation is written in the diagnostics, and the following operations still run. The diagnostics give the result of each operation (`done` or `refused: <reason>`), the type held by the clipboard, the tree plan (one line per action: path, type, summary), the drag marker (dragged action, zone, reference row, container, index, marker depth, accepted or refusal) and the status line of the tree.

`--activate-panel cutscenetimeline` brings the timeline to the front. The diagnostics also give the timeline: play time in seconds and frames, playhead, total duration, transport text, number of tracks and items, one line per item (track, label, start, duration, shape, exact, estimated or measured, then its drag rule: `drag: none`, `drag: changes the At <path> (seconds ...); lowest ... s` or `drag: wraps <path> in an At (in the Parallel <path>); lowest ... s`), one line per `Parallel` with its start, the last drop and its refusal, the selected item, and the visible and content heights of the track area with the state of the vertical bar.

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
- `CasaEngine.Editor/Runtime/CutscenePreviewSession.cs`: loads the world copy and plays the cutscene in it; it implements `ICutscenePreviewStepper` and exposes `Pause`, `Resume`, `StepFrames`, `Seek` and `Rewind`.
- `CasaEngine.Editor/Controls/CutsceneAssetInspectorPanel.cs`: the document panel. It owns the selection, the edit operations (`SetProperty`, `SetProperties`, `AddAction`, `DuplicateSelectedAction`, `RemoveSelectedAction`) and the `SelectionChanged` and `DocumentChanged` events.
- `CasaEngine.EditorServices/Cutscenes/`: also `CutscenePreviewTransport`, with `ICutscenePreviewStepper`, `CutscenePreviewTransportMode` and `CutscenePreviewStatus` (the pause, step and seek logic, testable without the editor, and `GetStatus`, which gives the state word), and `CutscenePlayTimings.Continue` (keeps the measured durations across replays).
- `CasaEngine.EditorServices/Cutscenes/`: also `CutsceneTimelineProjection` and `CutsceneTimelineLayout` (the pure projection of the tree on rows and times, and the playhead mapping `MapPlayTime`), `CutscenePlayTimings` (measured durations, mapped from the played copy back to the edited actions by `CutsceneActionTree.MapByShape`).
- `CasaEngine/Framework/Cutscenes/CutsceneDirector.cs`: `PlayElapsedSeconds`, `PlayElapsedFrames`, and the events `ActionStarted` and `ActionEnded` (`CutsceneActionNotification`). Additions only; without a subscriber the behaviour is unchanged.
- `CasaEngine.Editor/Controls/CutsceneTimelinePanel.cs` and `Controls/Cutscenes/CutsceneTimelineItemRenderer.cs`: the Cutscene Timeline panel (`EditorPanelIds.CutsceneTimeline`) and the drawing of its items. The preview session exposes `ElapsedSeconds`, `ElapsedFrames`, `Timings` and `TimingsVersion`.
- `CasaEngine.Editor/Controls/CutsceneOutlinerPanel.cs`: the action tree in the Hierarchy panel. `CasaEngine.Editor/Controls/CutsceneDetailsPanel.cs`, with `Controls/Cutscenes/CutsceneNumberField.cs` and `CutsceneTextCommitBinding.cs`: the typed fields in the Inspector panel. Both are registered as contextual panels for the cutscene document kind in `GameEditor`.
- `CasaEngine.EditorServices/Cutscenes/`: also `MoveCutsceneActionCommand`, `WrapCutsceneActionCommand` and `UngroupCutsceneActionCommand` (undoable structure edits, each with a static `GetRefusal`), `CutsceneClipboard` (the clipboard) and `CutsceneDropTargetResolver` (the pure calculation of the destination of a drop from the visible rows and the pointer).
- `CasaEngine.Editor/Controls/CutsceneAssetInspectorPanel.cs`: also `MoveAction`, `MoveSelectedActionUp`, `MoveSelectedActionDown`, `WrapSelectedAction`, `UngroupSelectedAction`, `CopySelectedAction`, `CutSelectedAction` and `PasteAction`, each with a `Get...Refusal` query that changes nothing, and the `Clipboard` property that `GameEditor` shares between documents.
- `CasaEngine/Framework/Cutscenes/AtCutsceneActionData.cs`: the `At` action (`Seconds`, `Actions`); its serialization, validation and playing are with those of the other containers in the same folder.
- `CasaEngine.EditorServices/Cutscenes/CutscenePlacement.cs`: the pure rule of what can be dragged (`Resolve`), the conversion of a drop (`ConvertDrop`, `LowestInstant`), the undoable placement and add commands (`TryCreatePlace`, `TryCreateAdd`) and the target `Parallel` of a selection (`FindTargetParallel`). `CutsceneTimelineLayout.TryGetContainerStart` gives the start of a container.
- `CasaEngine.Editor/Controls/CutsceneAssetInspectorPanel.cs`: also `PlaceActionAt` and `AddActionAt`, with their `GetPlaceActionRefusal` and `GetAddActionAtRefusal` queries. `CasaEngine.Editor/Controls/Cutscenes/CutsceneTimelineEditPolicy.cs`: the edit policy of the timeline (frame snapping and lowest instant, no resize, no insertion).
- `CasaEngine.Editor/Controls/CutsceneOutlinerPanel.Drag.cs` (drag and drop, Delete and Escape keys) and `CutsceneOutlinerPanel.Automation.cs` (`menu` and `draghover`); `Controls/Cutscenes/CutsceneActionMenu.cs` (the context menu, used by the tree and by `CutsceneTimelinePanel`); `CasaEngine.Editor/CutsceneEditOperation.cs` (the parsing of `--cutscene-edit-ops`).
- `CasaEngine.Editor/Runtime/Overlays/CutsceneScreenFadeOverlay.cs`: the overlay that draws the screen veil (`IPostUIOverlay`), set on the preview viewport by `WorldViewportPanel.SetPostUiOverlay`. `CutscenePreviewEffectHost` also holds the armed input, the silent audio service, the audio service lent by the editor and the captured mixer state; `CutscenePreviewSession` exposes `SimulateInput`, `SetSound`, `EndedByInput` and the armed inputs; the document exposes `SimulatePreviewInput` and `SetPreviewSound`.
- History context: `EditorHistoryContextKind.Cutscene`. Save source: `EditorAssetSaveSource.CutsceneEditorPanel`.

## Limits

- **Closing a modified cutscene tab loses its changes without a prompt.** Save first.
- A string longer than 256 characters, or one that holds a line break, is shown as an empty field. Do not give the focus to such a field: leaving it replaces the value with an empty string (the change can be undone).
- The structure is edited one action at a time, with no multiple selection and no automatic scroll or expansion during a drag (see "Limits of the structure editing").
- A container emptied of its children, or a new action with empty required fields, is invalid but can still be saved: the Inspector shows the validator messages.
- The editor checks that a named entity exists only in the preview world list, not in every world. Clicking an entity in the preview to fill a field is not done.
- `ExitOnInput.inputs` and `PlaySound.bus_name` stay text boxes.
- The categories of the add list are a prefix, not a menu: the drop-down list cannot group.
- Long summaries in the Hierarchy panel are cut at the right edge; a horizontal scroll bar reads them.
- An action of an unknown type cannot be edited, and blocks the save.
- The timeline cannot resize a bar, snap to other items or move an action to another row (see "Limits of the placement"), and the transport has no keyboard shortcut and no speed control (see "Timeline" and "Preview").

## Next

- Multiple selection; automatic scroll and expansion during a drag; keyboard moves; dragging a type from a palette.
- Picking an entity by clicking it in the preview.
- A per-world execution policy for the preview (sprite animations, physics and scripts); playing the cutscene an input or a world change leads to.
- Keyboard shortcuts for the transport, a playback speed, and going back in time without a replay.
- Editing on the timeline: resizing a bar, snapping to other items, moving a clip from a row to another, multiple selection.
