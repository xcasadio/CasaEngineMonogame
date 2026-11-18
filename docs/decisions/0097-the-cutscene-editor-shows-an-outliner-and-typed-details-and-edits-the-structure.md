# ADR-0097: The cutscene editor shows an action outliner and typed details, and edits the tree structure

- **Status**: Accepted
- **Date**: 2026-11-18
- **Source**: this chantier: `ai-agent/tasks/cutscene-editor-outliner-details-tasks.md` (decision D2 settled with the author on 2026-11-18, and choices P1 to P10, approved with the plan and open to correction at its acceptance), branch `chantier/cutscene-property-editing`. Completes ADR-0095 and ADR-0096.

## Context

- ADR-0095 made the properties of existing actions editable and deferred adding, removing and moving actions, and pickers for entities and assets. ADR-0096 added the world preview.
- The cutscene document shows the properties as a column of text boxes beside the preview. The author rejected it as text with a few controls, not usable.
- The Hierarchy and Inspector panels are contextual dock hosts: a document kind registers its content with a `ContextualPanelDefinition`. None is registered for cutscenes, so both panels report that nothing is available.
- Validation bounds live in `CutsceneValidator` (`>= 0` for durations, distances and counters, `[0, 1]` for volumes, `> 0` for the `MoveSprite` speed). Nothing in `CutsceneActionProperty` says that a string names an entity or an asset.
- The runtime reads references in different forms: a sound as a GUID, a sprite by catalogue name, a screen or video target by name or GUID, a world target by file name, a button mapping and a next cutscene by name or GUID, an entity by ordinal name.
- `CutsceneSaveGuard` refuses a save for an unknown type, a format error or a loading loss, and does not consult `CutsceneValidator`.

## Decision

- **Layout (P1).** The action tree is shown in the Hierarchy panel and the typed details of the selected action in the Inspector panel, through contextual panel definitions. The document keeps the transport bar, the status line and the preview, which takes the whole area.
- **Property metadata.** Each editable property declares what it designates (plain value, entity, sound, sprite, screen, world, cutscene, button mapping, input list) and its optional bounds, with an exclusive minimum for the `MoveSprite` speed. The bounds follow `CutsceneValidator`.
- **Reference forms (P5, P6).** References are chosen from a list of the assets of the right type, and the value written is the one the runtime reads: a GUID for a sound, a file name for a world, the catalogue name for the others. A value missing from the catalogue is kept and displayed as "(missing)". An entity is chosen from the entity names of the preview world; without a preview world the field is a text box.
- **Structure editing (D2, P2, P3).** The tree can add, remove and duplicate actions, undoably, through the document. Moving, reordering by mouse, wrapping, copy and paste, and multiple selection remain deferred. Insertion rule: if the selected action is `Sequence`, `Parallel` or `Repeat`, the new action becomes its last child; otherwise it is inserted right after the selection in the same parent; with no selection it is appended to the root when the root is a container. The root action cannot be removed or duplicated. Removing a container removes its children. Duplicating an action whose subtree carries a format error, a loading loss or an unknown type is refused with a message, because the clone would lose those marks.
- **Defaults of a created action (P4).** A created action takes the defaults of its class, except `Repeat.count` = 1 and `WaitFrames.frames` = 1. It may therefore be invalid while its required fields are empty: the Inspector shows the validator messages. Saving an incomplete action stays permitted, as it is today for any cutscene the validator criticises, because the save guard does not consult the validator. The same holds for a container emptied of its children.
- **Field semantics (P7, P8, P9).** A numeric field is an input with minus and plus buttons, bounded by the validator limits; typing commits on Enter or focus loss as one history entry, the buttons commit at once, and an empty field means "no value" for a nullable property and is refused otherwise. `FadeScreen.r`, `g` and `b` are one colour field whose validation writes the three properties in one history entry. `PlaySound.bus_name` and `ExitOnInput.inputs` stay text boxes.
- **Play mode lines (P10).** The runtime-state lines describe the editor world in Play mode. They move to the Inspector, under the title "Editor play mode", shown when no action is selected, with the file path, the validation and the loading losses.
- The `.cutscene` format does not change. This ADR realises the "adding, removing actions" point deferred by ADR-0095; moving actions stays deferred.

## Consequences

- The preview gets the whole document area, and the tree and the details follow the layout of the other asset editors.
- The editing services gain additive public types (property meaning and bounds, reference options, action catalogue, structure commands); the runtime is untouched.
- A created or duplicated action may fail validation until filled in; the author sees the messages but is not blocked from saving.
- A reference to an asset absent from the catalogue survives editing and is shown as missing, not silently replaced.
- The action type list is flat, prefixed by category, because the combo box cannot group. Clicking an entity in the preview to fill a field is deferred.
- The rewrite of the document tests onto the new structure is part of the implementation plan.
