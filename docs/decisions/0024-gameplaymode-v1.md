# ADR-0024: GameplayMode V1

- **Status**: Accepted
- **Date**: 2026-09-06 (backfill)
- **Source**: `docs/engine/gameplay-mode.md:53-125, 251-291, 427-435`

## Context

The document proposes a target V1 architecture to replace the Unreal-style `GameMode` machine, which today lacks an explicit runtime state comparable to `GameplayState` and an explicit unique result comparable to `GameplayResult` (source: `docs/engine/gameplay-mode.md:50-51`).

## Decision

- V1 replaces `GameMode` with three runtime elements: `GameplayMode` (gameplay rules logic), `GameplayModeRunner` (active mode lifecycle), `GameplayState` (minimal inspectable runtime state), plus two enums: `GameplayResult` (current mode result) and `GameplayPhase` (runtime phase) (source: `docs/engine/gameplay-mode.md:53-64`).
- V1 must not yet include: a full objective system, a gameplay event bus, polymorphic data-driven assets, an advanced editor panel, dialogue/cutscene/checkpoint integration, or complex scene transitions (source: `docs/engine/gameplay-mode.md:66-73`).
- `GameplayContext` must only expose contracts that actually exist; in V1, `World` is enough to reach existing services such as `RuntimeSystems.CoroutineManager`. `Scene`, `Audio`, `Input`, `UI`, `Dialogue`, or `Cutscenes` must not be added to `GameplayContext` until the exact contract to expose is chosen (source: `docs/engine/gameplay-mode.md:113-115`).
- The `GameplayModeRunner` must not call rendering, physics, UI, or entity scripts directly; it only orchestrates the active mode (source: `docs/engine/gameplay-mode.md:250-251`).
- Minimal `World` integration: add a `GameplayModeRunner` to the `World` runtime (or to `World` itself); add an explicit `SetGameplayMode(GameplayMode mode)`-style entry point; build `GameplayContext` from the current `World`; replace `GameMode?.Tick(elapsedTime)` with the runner update at the same place in `World.Update(FrameTime)` to preserve the current runtime update order; stop the active mode in `World.Clear()`; expose current state for debug/tests via the runner rather than a string-based state machine (source: `docs/engine/gameplay-mode.md:257-266`). Moving the runner into `WorldRuntimeSystems.Update()` instead would change the current order (`GameMode.Tick()` today runs before `InternalAddEntities()`, `RuntimeSystems.Update(frameTime)`, and entity update) and must be an explicit decision, not a side effect (source: `docs/engine/gameplay-mode.md:268`).
- `AssetLoader<T>` requires `where T : ISerializable, new()` and constructs `new T()` directly, so it cannot load an abstract `GameplayMode` class without a specialized loader or a concrete asset; V1 runtime may ship without a complete data-driven asset, as long as a mode can be started by code, with the asset migration coming afterwards (source: `docs/engine/gameplay-mode.md:284-291`).
- Decisions explicitly not to be taken in V1: adding a `Scene` class for this feature; introducing global services in `GameplayContext` without an existing contract; building a full objective system before the runner; making `GameplayMode` depend on UI or rendering; renaming file extensions without a migration plan; removing player spawn fields without compatibility (source: `docs/engine/gameplay-mode.md:429-435`).

## Consequences

- The legacy `.gameMode` asset's player/controller startup fields (`default_pawn_asset_id`, `player_controller_class`, `hud_classClass`, and the currently commented `ai_controller_class`) must be handled before `GameMode` is removed; V1 must not hide these fields inside `GameplayMode`, since they do not describe mode result, objectives, pause, or rules — mixing them back in would recreate the current `GameMode` problem (source: `docs/engine/gameplay-mode.md:278-283`).
- A full data-driven gameplay-mode asset, an objective system, and dialogue/cutscene/checkpoint integration remain deferred past V1.
- Implementation status observed in code: `GameplayMode`, `GameplayModeRunner`, `GameplayState`, `GameplayContext`, `GameplayResult`, `GameplayPhase`, `GameplayModeAsset`, and `ObjectiveGameplayMode` all exist under `CasaEngine/Framework/Gameplay/`.
