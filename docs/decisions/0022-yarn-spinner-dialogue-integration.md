# ADR-0022: Yarn Spinner dialogue integration

- **Status**: Accepted
- **Date**: 2026-09-06 (backfill)
- **Source**: `docs/engine/yarn_spinner_integration.md:1238-1315`

## Context

The document poses five open questions ("Risques et décisions à valider") about integrating Yarn Spinner dialogue into CasaEngine and records a recommendation for each.

## Decision

- `DialogueService` is attached to the `World` or the active game context, not a global game service and not a scene component, to keep dialogues tied to scene/world state and ease save-by-world (source: `docs/engine/yarn_spinner_integration.md:1238-1258`).
- V1 blocks gameplay input during dialogue but does not yet freeze the whole `World`; a `PauseGameplay` option is added afterwards (source: `docs/engine/yarn_spinner_integration.md:1260-1279`).
- Yarn compilation happens at import/editor time; runtime compilation is allowed only for prototypes or tests (source: `docs/engine/yarn_spinner_integration.md:1282-1294`).
- V1 uses the existing CasaEngine UI; a clean MGUI implementation is provided later if MGUI becomes the engine's main UI (source: `docs/engine/yarn_spinner_integration.md:1296-1304`).
- `DialogueRunner` is built independently first; `CutsceneAction StartDialogue` is added afterwards, because dialogue has its own choices, variables, conditions, and commands and should not be reduced to a cutscene action (source: `docs/engine/yarn_spinner_integration.md:1306-1315`).

## Consequences

- Dialogue does not yet fully pause the `World`; systems other than gameplay input keep running while a dialogue box is open in V1.
- A future MGUI-based dialogue presentation remains an open follow-up conditioned on MGUI becoming the main UI.
- Implementation status observed in code: `DialogueService` (`CasaEngine/Framework/Dialogue/Runtime/DialogueService.cs`) and `YarnDialogueRunner` (`CasaEngine/Framework/Dialogue/Yarn/YarnDialogueRunner.cs`) exist, along with `DialogueAsset`, `DialogueAssetJsonSerializer`, and `DialogueScreen` under `CasaEngine/Framework/Dialogue/`. The runtime consumes a precompiled program: `DialogueAsset.FromCompiledProgram(...)` takes the program bytes and `YarnDialogueRunner` refuses an asset without `HasCompiledProgram` (`CasaEngine/Framework/Dialogue/Assets/DialogueAsset.cs:20-22`, `CasaEngine/Framework/Dialogue/Yarn/YarnDialogueRunner.cs:29`), which is consistent with import-time compilation. No compiler invocation was found under `CasaEngine/`, `CasaEngine.EditorServices/` or `CasaEngine.Editor/`, so the editor-side compilation step itself is unverified in code.
