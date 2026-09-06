# ADR-0020: Cutscenes V1 on the World coroutine manager

- **Status**: Accepted
- **Date**: 2026-09-06 (backfill)
- **Source**: `docs/engine/cutscene_commandes_sequentielles_async_coroutine.md:36-60`

## Context

The document lays out CasaEngine V1 decisions for cutscenes, stated to prime over the conceptual examples described elsewhere in the same file (`docs/engine/cutscene_commandes_sequentielles_async_coroutine.md:40`).

## Decision

- Use `World.CoroutineManager` for cutscenes; do not create a new `CutsceneRunner` (source: `docs/engine/cutscene_commandes_sequentielles_async_coroutine.md:44-45`).
- Add `CutsceneDirector` directly in `World`, kept as a facade without a separate `Update` (source: `docs/engine/cutscene_commandes_sequentielles_async_coroutine.md:46-47`).
- Limit V1 to `Wait`, `Sequence`, `Parallel`, `Stop`, debug, validation, CasaEngine asset serialization, and read-only editor display (source: `docs/engine/cutscene_commandes_sequentielles_async_coroutine.md:48`).
- Exclude gameplay commands from V1 (source: `docs/engine/cutscene_commandes_sequentielles_async_coroutine.md:49`).
- Do not invent `InputManager`, `DialogueSystem`, `CameraManager`, `QuestSystem`, `AudioSystem`, `CharacterController`, or `Animator` (source: `docs/engine/cutscene_commandes_sequentielles_async_coroutine.md:50`).
- Load `CutsceneAsset` through the CasaEngine asset system (source: `docs/engine/cutscene_commandes_sequentielles_async_coroutine.md:51`).
- Use typed actions instead of `Dictionary<string, string>` (source: `docs/engine/cutscene_commandes_sequentielles_async_coroutine.md:52`).
- Do not implement `CompleteImmediately` in V1 (source: `docs/engine/cutscene_commandes_sequentielles_async_coroutine.md:53`).

## Consequences

- Movement, animation, dialogue, camera, input, quest, and audio examples in the document remain future tracks and must not be turned into V1 tasks until the corresponding CasaEngine systems are identified (source: `docs/engine/cutscene_commandes_sequentielles_async_coroutine.md:55`).
- Implementation status observed in code: the exclusion of gameplay commands from V1 has been superseded. A `MoveTo` gameplay command exists as a typed cutscene action (`CutsceneActionTypes.MoveTo`), implemented end to end in `CasaEngine/Framework/Cutscenes/MoveToCutsceneActionData.cs` and `CasaEngine/Framework/Cutscenes/CutsceneActionCoroutineFactory.cs` (`StartMoveTo`, driving `CharacterControllerComponent` through `world.RuntimeSystems.CharacterMotion.MoveTo`), with matching validation in `CasaEngine/Framework/Cutscenes/CutsceneValidator.cs` and serialization in `CasaEngine/Framework/Cutscenes/Serialization/CutsceneAssetJsonSerializer.cs`. This overtakes the "gameplay commands excluded from V1" decision above.
- Other observed elements match the decision: `CutsceneDirector`, `CutsceneAsset`, `CutsceneValidator`, and typed action data classes (`WaitCutsceneActionData`, `SequenceCutsceneActionData`, `ParallelCutsceneActionData`, etc.) exist under `CasaEngine/Framework/Cutscenes/`.
