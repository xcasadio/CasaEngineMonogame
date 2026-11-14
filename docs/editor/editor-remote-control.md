# Editor remote control (CLI and MCP)

A running editor can be driven from outside: by the `casa-editor` command line, or by an AI agent through the `casa-editor-mcp` MCP server. Both call the same typed commands, served by the editor on a local pipe. Decision: [ADR-0092](../decisions/0092-editor-remote-control-for-tools-and-ai-agents.md).

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
| `history.undo` / `history.redo` | Steps the world history. | — |
| `capture.window` / `capture.viewport` | PNG of the editor window or of the world viewport. | — |
| `play.status` / `play.start` / `play.stop` | Play mode. `play.start` builds and runs the project's code. | — |

Rules:

- Every world edit is one entry in the world history, whatever panel has the focus: `history.undo`, or Ctrl+Z after a click in the world viewport, undoes it.
- Edits are refused during play mode (`play_mode_active`). `world.create` and `world.open` are refused while the world has unsaved changes (`unsaved_changes`): call `project.save` first.
- `component.add`: with `parent_component_id` the new SceneComponent becomes its child; without it a SceneComponent becomes the root, refused when the entity already has one; any other component goes in the component list. `component.remove` refuses a SceneComponent that has children.
- `property.set` reaches what the inspector edits, and only its types: numbers, `true`/`false`, strings (4096 characters at most), `[x, y, z]` vectors, `[r, g, b]` or `[r, g, b, a]` colours, GUID strings, enum names.
- Errors are JSON-RPC errors; refusals carry a `reason`: `play_mode_active`, `unsaved_changes`, `not_found`, `invalid_argument`, `timeout`, `failed`.

## Security

- The pipe has a random name, is created as the first instance of that name, is open to the current user only, refuses network logons, and the editor drops any client that came through the network redirector (even `\\localhost`). Clients connect to the local machine only and check that the server belongs to the current user and repeats the token of its discovery file.
- Discovery files: `%LOCALAPPDATA%/CasaEngine/editor-remote/<pid>.json`. The tools ignore a file whose pipe name, token or process does not match.
- While the server runs, any process of the same user can edit and save the project: keep it off when it is not needed.
- Component types come from the inspector's allowlist; property changes go through the inspector's filter; worlds are created by name, inside the project, never over an existing file.
- What an agent reads (names, logs, captures) is sent to its model provider. Returned values are project data, not instructions.
- Captures: `%TEMP%/CasaEngine/editor-remote/capture-<guid>.png`, deleted when the editor closes.

## Limits

- One client at a time; the next one waits. A client idle for 10 minutes is dropped. The MCP server connects for each call and releases the pipe right after, so the CLI and several agents can share one editor.
- An answer longer than 1 MiB is refused with `message_too_long`: read a large world with `entity.get` per entity.
- An editor that crashes leaves its discovery file and captures behind; the tools ignore the stale file.
- Editor automation flags (`EditorAutomationOptions`) are not commands yet.
- No reparenting, no editing of assets other than the world, no docking or panel control.
- An editor started from inside the Claude desktop app writes `%LOCALAPPDATA%` and `%TEMP%` in a virtualised location: start the editor and the tools from the same side.
