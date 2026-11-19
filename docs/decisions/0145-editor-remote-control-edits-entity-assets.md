# ADR-0145: Editor remote control edits entity assets through their panel

- **Status**: Proposed
- **Date**: 2026-11-19
- **Source**: this chantier: `ai-agent/tasks/entity-asset-remote-tasks.md` (decisions D1 to D4 settled with the author on 2026-11-19, proposals P1 to P15, security review findings S3-1 to S3-10), branch `chantier/entity-asset-remote`; requested by the RacingGame plan `ai-agent/tasks/rgce-worlds-as-assets-tasks.md` (open point O2, the author's choice of 2026-11-19: extend the engine). Extends ADR-0093.
- Renumbered from ADR-0129 on 2026-11-19 when origin/main was merged (its ADR-0129 is another decision)

## Context

Facts verified on 2026-11-19, at `1ed32d862`:

- ADR-0093 gave the remote control asset commands for materials, particles, UI screens and sounds. An `.entity` asset is not among them: `asset.open` refuses it, and no command reads or edits it.
- The editor already has a panel for an `.entity` asset (`EntityAssetEditorPanel`). It holds the loaded entity in a preview world that updates continuously, has its own history context, and its inspector edits the entity through `EditorWorldOperations`, the code the world edit commands of ADR-0092 already call.
- The panel is found by an id built from the asset id. For a file that is not in the asset catalog the id is the one written in the file, so a copy of a catalogued `.entity`, opened by hand, gets the panel id of the catalogued asset.
- The panel writes nothing when it has no unsaved change. "Unsaved" means that the history revision differs from the saved one: an edit followed by an undo leaves the panel unmodified, an edit followed by a second edit that puts the value back leaves it modified.
- An `.entity` file holds one entity: its root component tree, its component list and its script. It holds no child entity.
- No editor gesture creates an `.entity` asset.
- `EditorRemoteAssetTypes.IsOpenable` is used by `asset.open` and by the routing of the automation flag `--open-asset`.

## Decision

- The registry gains the family `entity_asset.*`: `create`, `get`, `set_property`, `add_component`, `remove_component` and `save`. `asset.open` accepts an `.entity`; `panel.list` reports an entity panel with the kind `entity`; `panel.close` closes an entity panel that has no unsaved change; `history.undo` and `history.redo` accept the id of an `.entity` asset whose panel is open.
- Every edit goes through the code the panel's inspector uses and is one entry of the panel's history. The component type allowlist, the property filter and the value bounds are those of the world edit commands. An added component gets the name the inspector would give it, and the panel's selection follows the edit.
- An asset holds one entity: the commands act on that entity and take no entity argument.
- `entity_asset.create` creates an empty asset (an entity with the given name and no component). The file is reserved without overwriting, written, then added to the catalog; any failure after the reservation removes the catalog entry and the reserved file.
- `entity_asset.save` saves through the panel and keeps its rule: it answers `written: true` only when the panel had unsaved changes; otherwise no file is touched.
- A command acts on a panel only when that panel holds the file the command resolved: the loaded path is relative, has no `..`, and resolves to the resolved file. The rule applies to every `entity_asset.*` command, to `asset.open` of an `.entity` and to the history context of an entity panel, which must also belong to an `.entity` entry of the catalog. An entity panel without a dock tab is not treated as open.
- The whole family, `entity_asset.get` included, and `asset.open` of an `.entity` are refused during play mode.
- For an existing command, "contract" means its name, the property names, types, `required` and `additionalProperties` of its schema, its `readOnly` and `destructive` flags, the fields of its answer and its refusal reasons. Description texts may be completed. The only behaviour changes of existing commands are the four listed in the first bullet, plus the refusal reason of `asset.open` for an `.entity` during play mode (`play_mode_active`).
- The automation flag `--open-asset` keeps the editor's own route for an `.entity`: its routing uses its own list of the four earlier asset types.

## Consequences

- An agent can author an entity asset without touching its file, and the author can undo each remote edit from the panel.
- Opening an `.entity` loads its components and its script into a preview world that updates continuously: project code runs, as with `play.start` or with opening the same asset by hand. `entity_asset.get` is flagged read-only because it does not change the project, not because it runs nothing.
- The panel's history is shared with the author: a remote undo can undo a manual edit made in that panel, and closing an unmodified panel drops its undo history, as for the other asset panels.
- Saving an `.entity` does not refresh the open worlds that reference it; that gap exists today and is left as it is.
- Out of scope: child entities, renaming or deleting an asset, the entity's script and policies, the other asset types.
- The rule "the panel holds the resolved file" is applied to entity panels only. The material, particle and sound panels are also found by an id that another file can carry; they keep their behaviour.
- Accepted residuals, under the same-user threat model of ADR-0092 and ADR-0093: the save and the write that follows the reservation go through `File.CreateText`, which follows links; hard links are not detected; a link created between the check and the write is not seen; `project.save` writes modified entity panels without these checks. If the editor stops between two steps of a creation, a reserved file can remain without a catalog entry. If saving the catalog fails after the entry was added, the content browser can show the entry until its next refresh, and an interrupted catalog save can leave `AssetInfos.json` truncated, as for every creation command.
- A manual edit that the inspector would make without a history entry would leave the panel unmodified, and `panel.close` would close it; this was not checked.
