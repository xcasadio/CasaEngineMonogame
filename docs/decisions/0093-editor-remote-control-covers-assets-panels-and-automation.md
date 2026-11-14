# ADR-0093: Editor remote control covers assets, panels and the command-line automation

- **Status**: Accepted
- **Date**: 2026-11-14
- **Source**: this chantier: `ai-agent/tasks/editor-remote-control-2-tasks.md` (decisions D1 to D3 settled with the author on 2026-11-14, proposals P1 to P13), branch `chantier/editor-remote-control-2`. Extends ADR-0092.

## Context

- ADR-0092 gave the running editor a command registry served on a local pipe, used by the `casa-editor` CLI and the `casa-editor-mcp` MCP server. Its first slice covers the world, entities, components, captures and play mode.
- The editor's own panels already edit materials, particles, UI screens and sounds. Particle and screen edits are undoable; the material automation path and the sound inspector change their asset without the undo history. There is no material creation path and no entity reparenting.
- `EditorAutomationOptions` (24 command-line flags, replayed by `GameEditor.RunAutomation`) implements content edits a second time, beside the registry.
- The pipe serves one client at a time: a client that holds it (a long CLI call, a killed client waiting for its answer) hides the editor from the MCP server and from other clients.

## Decision

- The registry gains asset commands for materials, particles, UI screens and sounds, panel commands (list, open an asset, activate, close) and `entity.reparent`. Asset edits go through the code the asset's panel already uses, and through the undo history wherever that panel has one.
- The command-line automation flags keep their names and effects, but the content flags run the registry's commands in process; flags that only make sense for automation (dock resize, input drag, smokes, diagnostics, screenshots) stay as they are.
- The pipe accepts a few clients at once, each still with one request in flight; the first instance keeps the squatting protections of ADR-0092.

## Consequences

- An agent can author a level and its assets without touching the files directly, and the CLI, the MCP server and the automation flags share one implementation per edit.
- Sound edits are not undoable, as in the sound inspector today; giving sounds a history context is a separate change.
- More commands write project files: every new file is created by name inside the project, never over an existing file nor under a name the catalog already holds, and every asset reference, including the file the catalog returns, resolves inside the project with the expected asset type.
- Asset values are bounded like component properties (finite numbers, documented maxima, defined enum names, catalogued GUIDs of the right type); UI screen properties are limited to the names the screen property registry knows.
- `panel.close` never discards unsaved work and closes only asset document panels; `asset.open` does not open worlds, which keep `world.open` and its refusals.
- Automation flags change behaviour where they now share the commands' checks: a path outside the project is refused by `--open-asset`, `--content-folder`, `--create-particle-asset` and `--drop-particle-asset` (`--project`, `--diagnostics-out` and `--screenshot-out` are unchanged), `--set-screen-property` accepts only registered property names, and `--set-material-property` and `--set-particle-property` refuse non-finite numbers, uncatalogued or wrongly typed GUIDs, numeric enum values and values beyond the inspector's bounds.
