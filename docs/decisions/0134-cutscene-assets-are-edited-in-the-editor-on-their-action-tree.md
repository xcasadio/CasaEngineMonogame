# ADR-0134: Cutscene assets are edited in the editor, on their action tree

- **Status**: Accepted
- **Date**: 2026-11-17
- **Source**: this chantier: `ai-agent/tasks/cutscene-property-editing-tasks.md` (decisions D1 to D7, settled with the author on 2026-11-17, and choices P1 to P11, approved with the plan on 2026-11-17 and open to correction at its acceptance, point O1), branch `chantier/cutscene-property-editing`. Follows ADR-0020, ADR-0088, ADR-0129 and ADR-0132.
- Renumbered from ADR-0095 on 2026-11-19 when origin/main was merged (its ADR-0095 is another decision)

## Context

- A `.cutscene` asset is a tree of actions under `CutsceneAsset.RootAction` (`Sequence`, `Parallel`, `Repeat` and leaf actions). `CutsceneAssetJsonSerializer` loads and saves it.
- The editor opens a `.cutscene` as a document, `CutsceneAssetInspectorPanel`, which only displays text built by `CutsceneReadOnlyDocumentBuilder`. That builder does not list the fields of `PlaySound`, `PlayMusic`, `StopMusic`, `FadeMusic` and `FadeScreen`.
- The editor cannot save a cutscene: `EditorAssetJsonSerializer.TrySerialize` has no case for `CutsceneAsset`, and `EditorAssetSaveSource` has no value for it.
- An action of an unknown type loads as `UnknownCutsceneActionData`, which keeps only its type name. Ten action classes carry a `FormatErrors` dictionary, filled by the loader and keyed by the JSON key; nothing clears it.
- The loader drops or rewrites some malformed values without recording a format error: a `target` that is not a string, an invalid `blend_mode` of `FadeScreen`, an entry of `actions` that is not an object, an `actions` value that is not an array.
- The loader refuses an empty string for several keys that the validator accepts, since the validator only tests for `null`.
- `EditorHistoryContext.FromDocument` returns an empty context for a cutscene document, so undo and redo do nothing while a cutscene tab is active.
- The "unsaved changes" prompt on closing exists only for UI screen documents.
- The archived timeline plan excludes a flat cutscene model and a `CutsceneTimelineAdapter`.

## Decision

- **The action tree stays the only source of truth.** The editor edits `CutsceneAsset.RootAction` in place. There is no flat model, no new file format, and no change in the runtime.
- **This first slice edits the properties of existing actions.** Each field that `SaveAction` writes for an action is editable under its JSON key, `type` and `actions` excepted. `Sequence`, `Parallel` and unknown actions have no editable property. Adding, removing and moving actions come later.
- **Fields are edited as text**, in the invariant culture: number, integer, `true`/`false`, GUID, colour `r, g, b, a`, vector `x, y, z`, enumeration name, comma-separated list. A number must be finite, an enumeration value must be a defined name, and a list has no empty entry. An empty string is accepted for a string property.
- **One undoable command per change.** The command keeps the old and the new typed value, and restores the old one without parsing text. A list value is copied when read and when written. Changing a field clears its `FormatErrors` entry and its load loss; undoing restores both.
- **Saving rewrites the file in canonical form**: explicit default values, the key order of the serializer, the name of the asset catalog, unknown keys dropped.
- **A save never destroys data silently.** It is refused, with the path and the key of the first case, when:
  - the tree holds an action of an unknown type or a remaining format error;
  - a load loss remains. On opening, the editor compares the JSON of the file with the JSON a save would write at once: each known key of an action present in the file must come back with the same kind and the same value, and each `actions` array with the same number of entries. Numbers compare as `float`, strings without case, arrays over the length found in the file, objects key by key;
  - reloading the JSON about to be written yields a format error, an unknown action or a load loss. The editor therefore never writes a file that its own loader would read back with an error.
- **Additive public API.** `EditorAssetSaveSource.CutsceneEditorPanel = 9`, `EditorHistoryContextKind.Cutscene` at the end of the enumeration, a `Cutscene` case in `EditorHistoryContext.FromDocument`, a `CutsceneAsset` case in `EditorAssetJsonSerializer.TrySerialize` that delegates to `CutsceneAssetJsonSerializer.Save`, and the editing services under `CasaEngine.EditorServices/Cutscenes`.
- **`CutsceneReadOnlyDocument` and its builder do not change.** They remain the display model of the tree and of the runtime state.

## Consequences

- A cutscene can be tuned in the editor, with undo, redo, a modified marker on its tab, and save.
- The first save of a file may change bytes that the user did not edit: defaults become explicit and the `name` becomes the catalog name.
- A file with an unknown action, a format error or a load loss opens and can be edited, but cannot be saved until the faulty field is edited. A faulty `actions` entry has no editable field: such a file is fixed outside the editor.
- **Closing a modified cutscene tab loses its changes without a prompt**, as for the other asset documents; a prompt is deferred.
- Deferred: editing the structure of the tree, pickers for entities and assets, checking that a named entity exists in a world, preview in an isolated world, a read-only timeline projection (which needs its own record, given the exclusion in the archived timeline plan).
- The ADR number may change if `origin/main` gains ADRs before this branch is merged.
