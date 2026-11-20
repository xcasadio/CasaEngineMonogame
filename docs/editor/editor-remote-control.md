# Editor remote control (CLI and MCP)

A running editor can be driven from outside: by the `casa-editor` command line, or by an AI agent through the `casa-editor-mcp` MCP server. Both call the same typed commands, served by the editor on a local pipe. Decisions: [ADR-0092](../decisions/0092-editor-remote-control-for-tools-and-ai-agents.md) (world, entities, captures, play) and [ADR-0093](../decisions/0093-editor-remote-control-covers-assets-panels-and-automation.md) (assets, panels, reparenting, automation flags) and [ADR-0145](../decisions/0145-editor-remote-control-edits-entity-assets.md) (entity assets).

## Start it

Remote control is off by default. Turn it on with either:

- `--remote` on the editor command line;
- `Tools > Remote Control` in the editor (tick it again to stop).

The window title then ends with `[Remote Control]`. The editor refuses to serve while it runs as administrator.

The tools live in `Tools/` and build with `CasaEngine.Editor.MonoGame.sln`:

| Project | Executable | Role |
| --- | --- | --- |
| `Tools/CasaEngine.Editor.Remote.Protocol` | (library) | Messages, discovery files, pipe client. No engine dependency. |
| `Tools/CasaEngine.Editor.Cli` | `casa-editor` | Command line for scripts and checks. |
| `Tools/CasaEngine.Editor.Mcp` | `casa-editor-mcp` | MCP server (stdio) for AI agents. |

## Command line

```bash
casa-editor editors                                  # editors serving remote control
casa-editor commands                                 # commands and their argument schemas
casa-editor call entity.create '{"name":"Player"}'   # one command
casa-editor run steps.json                           # several commands, stops at the first error
```

Options: `--pid <id>` when several editors serve, `--timeout <seconds>` (default 120). Output is JSON. Exit codes: 0 success, 1 the editor refused a command, 2 usage error, 3 no editor or lost connection.

A script is a JSON array:

```json
[
  { "command": "entity.create", "params": { "name": "Player" } },
  { "command": "project.save" }
]
```

## MCP server

`.mcp.json` (Claude Code) and `.vscode/mcp.json` (Copilot) declare `casa-editor` on the built executable `Tools/CasaEngine.Editor.Mcp/bin/Debug/net9.0/casa-editor-mcp.exe`: build `CasaEngine.Editor.MonoGame.sln` first. Each editor command is one MCP tool (`entity.create` becomes `entity_create`), with `readOnlyHint` and `destructiveHint`, so a client can allow the read tools and keep asking for the others. Captures come back as images.

Without a serving editor the server lists a single tool, `editor_connection`, that says how to start one; the tool list is refreshed every 5 seconds and the client is told when it changes. `casa-editor-mcp --pid <id>` targets one editor; otherwise the only one, or the most recently started.

## Commands

| Command | Does | Undoable |
| --- | --- | --- |
| `editor.status` | Project, world, play state, unsaved world changes. | — |
| `world.entities` | Entity tree: id, name, path, parent, components. | — |
| `entity.get` | One entity and the editable properties of its components. | — |
| `component.types` | Component types `component.add` accepts. | — |
| `selection.get` / `selection.set` | Editor selection. | — |
| `logs.read` | Editor log from a cursor (`from_sequence`, `next_sequence`, `lost`); `source: diagnostics` reads the automation buffer. | — |
| `world.create` | Creates `<name>.world` at the project root and opens it. | No |
| `world.open` | Opens a catalogued world by its project-relative file. | No |
| `project.save` | Saves the project and the world; fails if anything was not written. | — |
| `entity.create` / `entity.delete` / `entity.rename` / `entity.duplicate` | Entity edits. | Yes |
| `component.add` / `component.remove` | Component edits (rules below). | Yes |
| `property.set` | Sets an editable component property. | Yes |
| `entity.reparent` | Moves an entity under another one, or to the root; refuses a cycle. | Yes |
| `history.undo` / `history.redo` | Steps a history: the world (default) or an open asset panel (`context` = the asset id). | — |
| `capture.window` / `capture.viewport` | PNG of the editor window or of the world viewport. | — |
| `play.status` / `play.start` / `play.stop` | Play mode. `play.start` builds and runs the project's code. | — |
| `asset.list` | Catalogued assets, optionally of one type; 500 at most, then `truncated` and `total`. | — |
| `asset.open` | Opens a `.material`, `.particle`, `.uiscreen`, `.sound` or `.entity` in its panel; returns the panel id. An `.entity` is refused in play mode. | — |
| `panel.list` / `panel.activate` | Open panels (id, title, kind, active, modified); brings one to the front. | — |
| `panel.close` | Closes a material, particle, UI screen, sound or entity asset panel; refused when it has unsaved changes. | — |
| `material.create` / `material.get` / `material.set_property` / `material.save` | Material assets (default definition `lit-diffuse`). `material.get` returns each value as text in the form `material.set_property` parses: invariant numbers, `r,g,b,a` bytes for a colour, comma-separated vector components, the asset id for a texture. | Set: yes |
| `component.material_override` | Sets or clears the material of a static model slot. | Yes |
| `particle.create` / `particle.get` / `particle.set_property` / `particle.save` | Particle assets, from a preset. | Set: yes |
| `particle.drop` | Puts a particle asset on an entity (its particle system takes it, or one is added). | Yes |
| `screen.get` / `screen.set_property` | UI screen node tree; sets an inspector property of a node. | Yes |
| `sound.create` / `sound.get` / `sound.set_property` / `sound.save` | Sound assets. | **No** |
| `entity_asset.create` / `entity_asset.get` / `entity_asset.set_property` / `entity_asset.add_component` / `entity_asset.remove_component` / `entity_asset.save` | Entity assets (`.entity`): one entity, its components and their editable properties, edited through the asset's panel (rules below). | Edits: yes |

Rules:

- Every world edit is one entry in the world history, whatever panel has the focus: `history.undo`, or Ctrl+Z after a click in the world viewport, undoes it.
- Edits are refused during play mode (`play_mode_active`). `world.create` and `world.open` are refused while the world has unsaved changes (`unsaved_changes`): call `project.save` first.
- `component.add`: with `parent_component_id` the new SceneComponent becomes its child; without it a SceneComponent becomes the root, refused when the entity already has one; any other component goes in the component list. `component.remove` refuses a SceneComponent that has children.
- `property.set` reaches what the inspector edits, and only its types: numbers, `true`/`false`, strings (4096 characters at most), `[x, y, z]` vectors, `[r, g, b]` or `[r, g, b, a]` colours, GUID strings, enum names.
- Errors are JSON-RPC errors; refusals carry a `reason`: `play_mode_active`, `unsaved_changes`, `not_found`, `invalid_argument`, `timeout`, `failed`.

Assets:

- An asset is named by its catalog id or its project-relative file. A reference whose catalog file name leaves the project, or of another type than the command expects, is refused.
- `*.create` takes a `name` (letters, digits, space, `_`, `-`, 64 characters at most) and an existing project `folder` (`''` for the root); it refuses a name already in the catalog (any type, any case) and never writes over a file. A folder written with an empty segment, or with a segment ending in a dot or a space (`.`, `..`, `Entities.`, `Entities//Cars`), is refused before the disk is read: Windows would resolve it to another folder than the one catalogued.
- Every path the remote control resolves (asset references, catalog file names, `world.open`, the automation flags and the startup `--open-asset`) is refused (`invalid_argument` for a command, a logged warning for an automation flag or `--open-asset`) when a segment of its normalized form ends with a dot or a white-space character, whether that segment exists or not: Windows reads another entry for such a name than the one a later access goes through. The message names the segment. `Entities./Box.material` normalizes to `Entities/Box.material` and stays accepted; `Entities ./Box.material` normalizes to `Entities /Box.material` and is refused.
- Material, particle, UI screen and entity asset edits go through the asset panel's own history: `history.undo` with `context` = the asset id undoes them, like Ctrl+Z in that panel. A context with no open panel is refused. `*.save` writes the asset; until then the panel shows it modified. That history is shared with whoever uses the editor: a remote undo can undo an edit made by hand in the panel, and closing a panel drops its history.
- **Sounds have no history**, like the sound inspector: `sound.set_property` answers `undoable: false`, marks the panel modified, and only `sound.save` writes the file.
- Values are bounded like the inspector fields: finite numbers inside the field bounds (material bounds come from its definition), enum values by name (never a number), texts of 4096 characters at most, texture and audio references to catalogued assets of the right type. `particle.get` and `sound.get` list the keys `*.set_property` accepts.
- `screen.set_property` sets only the properties the UI screen inspector lists as editable for the node's control type. Property names with `{`, `:` or `.` and values starting with `{` (markup, bindings) are refused. A node is named by its id or by a name no other node has.

Entity assets:

- An `.entity` asset holds one entity (its root component tree, its component list, its script) and no child entity: the commands take no entity id, and a `component_id` that is not one of its components is refused (`not_found`).
- `entity_asset.get` opens the asset in its panel and returns `asset_id`, `file`, `modified` (the panel has unsaved changes) and the entity: `id`, `name` and its components in the shape of `entity.get`.
- `entity_asset.set_property`, `entity_asset.add_component` and `entity_asset.remove_component` follow the rules of `property.set`, `component.add` and `component.remove`. An added component gets the name the inspector gives it (its type name, then `(2)`, `(3)`…) and is selected in the panel.
- `entity_asset.create` creates an empty asset: an entity with the given name and no component. Add its root with `entity_asset.add_component`.
- `entity_asset.save` writes the file only when the panel has unsaved changes and says so in `written`. An edit followed by `history.undo` leaves the panel unmodified, so nothing is written; to rewrite a file with its original values, set a value and set it back.
- Saving an entity asset does not refresh the open worlds that reference it: they keep the copy they made when they loaded.
- The whole family, `entity_asset.get` included, and `asset.open` of an `.entity` are refused in play mode (`play_mode_active`).
- A command is refused (`failed`) when the panel open under the asset's id holds another file, for example a copy of the asset's file that is not in the catalog and was opened by hand: close that panel first.

## Automation flags

The content flags of the editor automation (`EditorAutomationOptions`) run through the same commands, called in process on the game thread (never through the pipe): `--entity`/`--entity-index`/`--component` → `selection.set`; `--open-asset` → `asset.open` (worlds, entity assets and the other asset types keep the editor's own route); `--activate-panel` → `panel.activate`; `--create-particle-asset` → `particle.create`; `--set-particle-property` → `particle.set_property` + `particle.save`; `--set-material-property` → `material.set_property` + `material.save`; `--drop-particle-asset` → `particle.drop`; `--set-screen-property` → `screen.set_property`; `--save-project` → `project.save`. The other flags are unchanged. Behaviour changes:

- `--open-asset`, `--content-folder`, `--create-particle-asset` and `--drop-particle-asset` refuse a path outside the project (an absolute path inside it still works). `--project`, `--diagnostics-out` and `--screenshot-out` are not concerned.
- `--set-screen-property` refuses a property the UI screen inspector does not list for the node.
- `--set-material-property` and `--set-particle-property` apply the value bounds above: NaN, infinity, out-of-range values, numeric enums and unknown or wrongly typed texture ids are refused.
- At the end of a run the automation restores the files it edited and deletes the particle asset it created only if they still hold what it wrote: with `--remote`, a change an agent made meanwhile is kept (a `Kept '…'` line is logged).

## Security

- The pipe has a random name, is created as the first instance of that name, is open to the current user only, refuses network logons, and the editor drops any client that came through the network redirector (even `\\localhost`). Clients connect to the local machine only and check that the server belongs to the current user and repeats the token of its discovery file.
- Discovery files: `%LOCALAPPDATA%/CasaEngine/editor-remote/<pid>.json`. The tools ignore a file whose pipe name, token or process does not match.
- While the server runs, any process of the same user can edit and save the project: keep it off when it is not needed.
- Component types come from the inspector's allowlist; property changes go through the inspector's filter; worlds are created by name, inside the project, never over an existing file.
- Opening an `.entity` asset (`asset.open` or any `entity_asset.*` command) loads its components and its script in a preview world that updates continuously: project code runs, as with `play.start` or with opening the asset by hand. `entity_asset.get` is flagged read-only because it does not change the project.
- What an agent reads (names, logs, captures) is sent to its model provider. Returned values are project data, not instructions.
- Captures: `%TEMP%/CasaEngine/editor-remote/capture-<guid>.png`, deleted when the editor closes.

## Limits

- Up to 4 clients at once (a fifth waits); their commands run one at a time on the editor's game thread. A client idle for 10 minutes is dropped. The MCP server connects for each call and releases the pipe right after; when the editor is busy it keeps its tool list.
- An answer longer than 1 MiB is refused with `message_too_long`: read a large world with `entity.get` per entity.
- An editor that crashes leaves its discovery file and captures behind; the tools ignore the stale file.
- Only materials, particles, UI screens, sounds and entity assets are editable assets; sprites, animations, TileMaps, audio mixers and models are not. An entity asset cannot be renamed or deleted, and its script and policies are not editable. No docking or panel resizing. Properties hidden by the specialised inspectors stay reachable through `property.set`.
- An editor started from inside the Claude desktop app writes `%LOCALAPPDATA%` and `%TEMP%` in a virtualised location: start the editor and the tools from the same side.
- Paths under a symbolic link, junction or mount point inside the project are refused by the commands, the automation flags and the startup `--open-asset`, with a message naming the link (ADR-0093, A3); the project folder itself may be a link. Hard links, a link created after the check, and later writes that never go through these checks (`project.save`, the automation restore) are not covered; the save of an asset follows a link that replaced its file after the check.
- A real folder or file whose name ends with a dot or a white-space character is no longer reachable through the remote control, the automation flags or `--open-asset` (ADR-0093, A3). Not covered, as for links: the project folder and its ancestors (not checked), hard links, a change between the check and the use. One spelling escapes the name rule: a short 8.3 name given as the last segment of an automation folder flag (`--content-folder`, `--create-particle-asset`). .NET expands it to the long name, and when that long name ends with a space the check, which normalizes once more, reads the entry without the space and accepts; `--create-particle-asset` then tests whether its candidate file names exist under the entry with the space, and creates the asset in the folder without the space. Nothing is read or written under the entry with the space, and a short name in any other position is refused. Reserved device names are not refused by their text: on Windows 11 a `NUL` segment is refused because its attributes cannot be read (fail closed) and the other names were not redirected to a device; Windows 10 was not tried.
- Folder names are not normalised: a short 8.3 name or another letter case in the `*.create` folder is catalogued as written, and an asset reference or an existing catalog entry written in a non-normalised form (`Entities/../Entities/Box.entity`) is not found rather than normalised.
