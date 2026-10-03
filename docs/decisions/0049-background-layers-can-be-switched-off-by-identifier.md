# ADR-0049: Background layers can be switched off by identifier, frozen and not drawn

- **Status**: Accepted
- **Date**: 2026-10-03 (plan E19.k2 read until READY on 2026-10-03; work in the engine authorized by the author on 2026-10-03)
- **Source**: this chantier: `ai-agent/tasks/e19k2-layer-mask-tasks.md` (rule K2-R1). Parent repository:
  `docs/plan-e19-opcodes.md`, step E19.k2 (the background-layer mask opcode of the original game). Complements the scrolling
  and cellular layer mechanisms (`docs/engine/scrolling-layers.md`, `docs/engine/cellular-layers.md`); replaces no ADR.

## Context

- `ScrollingLayerService` and `CellularLayerService` run every layer they were given: each tick advances every layer's state
  (animation cadence, auto-scroll, waves, draws from the shared random stream) and the components submit every layer. Neither
  service had a notion of an active or inactive layer.
- The original game can switch its two background layers off and on at run time (a mask of two bits tested by the drawing
  routine). The state of a layer advances inside the guarded call, so a masked layer is frozen as well as not drawn, whatever its
  kind (scrolling or cellular). The mask applies to both kinds.
- A layer's place in the service is not its identifier: the converter's export drops some layers, so a game cannot address a
  layer by index. The definitions already carry the identifier the game gave them (`ScrollingLayerDefinition.StableId`,
  `CellularLayerDefinition.LayerId`).

## Decision

- Each service gets `SetLayerActive(id, active)`: every layer whose identifier equals `id` takes the state; an identifier no
  layer carries is ignored; `SetLayers` and `Clear` make every layer active again. `IsLayerActive(index)` reads the state by
  position.
- An inactive layer is frozen: `Advance` skips it entirely (no cadence, auto-scroll, offset recompute, wave tick, cell movement
  or random draw), so reactivating it resumes from the state it had. Its state is kept as is, including values a later
  `SetFrame` would otherwise change.
- The components skip inactive layers when submitting (`ScrollingLayerComponent.Submit`, `CellularLayerComponent.Submit`),
  including cells left drawable by the last advance.
- The mechanism is generic (identifier and boolean). The meaning of the mask bits, and which identifier a bit stands for,
  stays in the game.

## Consequences

- Additive public API; layers that are never masked behave exactly as before (the default state is active).
- `ResetLayerRuntimeState` (called when a component re-resolves textures) does not change the active state: only
  `SetLayers` and `Clear` do.
- Not covered: the palette cycling the original ties to the same opcode (a palette-offset program touching the whole map); it
  is outside this decision.
