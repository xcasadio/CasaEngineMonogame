# ADR-0092: The running editor exposes typed commands to a CLI and an MCP server over a local pipe

- **Status**: Accepted
- **Date**: 2026-11-14
- **Source**: this chantier: `ai-agent/tasks/archive/editor-remote-control-tasks.md` (decisions D1 to D4 settled with the author on 2026-11-14, proposals P1 to P11), branch `chantier/editor-remote-control`.

## Context

- The only way to drive the editor from outside is `CasaEngine.Editor/EditorAutomationOptions.cs`: command-line flags read once at start-up and replayed by a per-frame state machine (`GameEditor.RunAutomation`). It cannot create a world, an entity or a component, and a caller cannot see the result of one step before choosing the next.
- The author wants an AI agent to be able to interact fully with the editor in the future, through a CLI and an MCP server, both kept as separate tool projects outside the engine and the editor.
- Every editor change runs on the game thread inside `GameEditor.Update`; world edits go through `EditorHistoryService` (`Execute`, `OpenTransaction`), which refuses changes while play mode suspends it.

## Decision

- The editor holds one registry of typed commands (name, description, JSON schema of the arguments, handler). Both tools call these commands; neither re-implements editor logic.
- The editor serves the commands on a local named pipe with a random name, created as the first instance, open to the current user only and closed to network logons; clients connect only to the local machine and check that the server belongs to the current user. The editor refuses to serve while it runs elevated. Requests are queued and executed on the game thread from `GameEditor.Update`; a command may complete on a later frame (screenshot, play mode).
- The wire format is JSON-RPC 2.0 shaped messages, one per line, defined in a small protocol project without engine or MonoGame dependency.
- The CLI (`casa-editor`) and the MCP server are separate executables under `Tools/`. The MCP server uses the official `ModelContextProtocol` NuGet package and exposes the editor commands as MCP tools.
- Every command that changes the world is one undoable entry in the world's history context, whatever panel has the focus, and is refused during play mode. Creating or opening a world is not undoable, as in the editor today: opening a world clears its history.
- The remote server is off by default; it starts only when the author asks for it.

## Consequences

- An agent can read the editor state, change it step by step, see the result (including screenshots) and undo it from the editor.
- New editor features become scriptable by registering a command; the tools pick it up through the command list.
- A local process of the same user can drive the editor while the server runs: it can create, modify and save project files. The server is opt-in for that reason, and commands accept only what the editor's own panels allow: component types from the inspector's allowlist, properties the inspector edits, world names (not paths) inside the project.
- What an agent reads (logs, screenshots, names) leaves the machine with the model's requests; returned values are project data, never instructions.
- `EditorAutomationOptions` stays as it is; moving its flags onto the command registry is deferred.
- One new package dependency (`ModelContextProtocol`, Apache-2.0), limited to the MCP tool project.
