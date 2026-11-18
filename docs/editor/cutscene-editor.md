# Cutscene editor

Decisions: see ADR-0095 and ADR-0096.

## Summary

Opening a `.cutscene` asset from the Content Browser shows its action tree as a document tab. The properties of the existing actions can be edited, undone, redone and saved. The action tree (`CutsceneAsset.RootAction`) is the only source of truth: there is no separate editing model and no change in the file format.

The document also shows a preview of an isolated copy of a world, with transport controls, to play the cutscene being edited (see "Preview"). Adding, removing and moving actions, pickers and a timeline are not done yet (see "Next").

## Usage

1. Open a `.cutscene` file from the Content Browser.
2. Each action is listed with its path (`root_action.actions[2]`) and one text field per property.
3. Type a value and press Enter, or leave the field, to commit it. An invalid text is refused: the field takes its old value back and the status line says why.
4. Undo and redo with the editor's global commands while the cutscene tab is active. Each committed field is one history entry.
5. A modified document shows `*` in its tab title. Save it with the editor's save command, or with "save all".

## Preview

The document shows a world preview on the left and the property fields in a column on the right. A transport bar sits above the preview: a world choice, `Play`, `Stop`, `Restart` and a status line.

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
- No pause, no current time and no seeking: only play, stop and restart.
- Sound cannot be enabled, fades are not drawn and the input of `ExitOnInput` cannot be simulated.
- The preview applies the editor's execution policy, not the world's virtual resolution, so the framing can differ from the game.
- Editing the cutscene is disabled while a play session of the editor is running.

### Automation

For remote validation the editor can play the preview without any input: `--project <project.json> --open-asset <Cutscenes/name.cutscene> --cutscene-preview-play --screenshot-out <file.png> --capture-delay <seconds>`. With this switch the capture delay counts from the start of the playback, and the diagnostics file gets the preview state, the skipped actions, the name and position of each preview entity and the name of the editor's current world.

## Field formats

All texts use the invariant culture.

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
- Format errors and load losses are listed in the Validation section of the document.

## Code

- `CasaEngine.EditorServices/Cutscenes/`: `CutsceneActionPropertyEditor` and `CutsceneActionProperty` (editable properties, typed access, text), `CutsceneEditSession` (asset and load losses), `SetCutsceneActionPropertyCommand` (undoable change), `CutsceneLoadFidelity` (load losses), `CutsceneSaveGuard` (save refusal).
- `CasaEngine.EditorServices/Cutscenes/`: also `CutscenePreviewWorldResolver`, `CutscenePreviewSettings` and `CutscenePreviewEffectHost` (preview world choice, remembered choice, reported effects).
- `CasaEngine/Framework/Cutscenes/ICutsceneEffectHost.cs`: the runtime injection point, set through `CutsceneDirector.EffectHost`.
- `CasaEngine.Editor/Runtime/CutscenePreviewSession.cs`: loads the world copy and plays the cutscene in it.
- `CasaEngine.Editor/Controls/CutsceneAssetInspectorPanel.cs`: the document panel.
- History context: `EditorHistoryContextKind.Cutscene`. Save source: `EditorAssetSaveSource.CutsceneEditorPanel`.

## Limits

- **Closing a modified cutscene tab loses its changes without a prompt.** Save first.
- A string longer than 256 characters, or one that holds a line break, is shown as an empty field. Do not give the focus to such a field: leaving it replaces the value with an empty string (the change can be undone).
- The structure of the tree cannot be edited: no add, remove, duplicate, move or wrap.
- Entities, sprites, sounds, screens and worlds are typed as text; the editor does not check that a named entity exists in a world.
- An action of an unknown type cannot be edited, and blocks the save.
- No timeline, and no pause or seeking in the preview (see "Preview").

## Next

- Editing the structure of the tree.
- Pickers and world-aware validation.
- A sandbox for the preview: drawn fades, sound toggle, simulated input, per-world execution policy.
- A read-only timeline projected from the tree, after its own decision record.
