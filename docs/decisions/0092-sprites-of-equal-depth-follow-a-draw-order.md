# ADR-0092: Sprites of equal depth follow a draw order, then their submission

- **Status**: Accepted
- **Date**: 2026-11-10
- **Source**: this chantier: `ai-agent/tasks/sprite-draw-order-tasks.md` (decisions D1 to D6, settled with the author on 2026-11-10, and choices C1 to C7, locked by the approval of the plan on 2026-11-10), branch `chantier/sprite-draw-order`. Follows ADR-0051, ADR-0090 and ADR-0091.

## Context

- `SpriteRendererComponent` sorts its queue with `List<T>.Sort`, which is not stable. Two key-less entries of the same Z compare equal, so their draw order is left to the sort.
- Sorted sprites test and write depth with `LessEqual`, so sprites of the same Z blend over each other in the order of the sort. With blend modes that saturate (subtractive near zero, additive near the top), that order changes the picture.
- `World.Draw` submits the visible entities in the order of a query on the world's octree, which depends on cells and changes when a sprite moves. The submission order alone does not let data choose an order.
- Queue entries are structs taken from a pool and reused without reset; two paths enqueue them (the core `DrawSprite` and `QueuePsxQuad`).

## Decision

- **`draw_order`**: an optional integer on `StaticSpriteComponent` and `VideoSpriteComponent`, 0 by default, copied by the copy constructor. JSON `draw_order` is a JSON integer; any other shape fails the load with an `InvalidDataException` naming the component, the field and the value. The editor save path writes it only when it is not 0.
- **Comparator.** For two key-less entries, the larger Z is drawn first, as before; at an equal Z, the smaller `DrawOrder` first; at an equal `DrawOrder`, the smaller `Submission`, the rank of the entry in the frame's queue. Keyed entries keep their comparison; the `draw_order` of a keyed sprite has no effect.
- **Queue entries.** Both enqueue paths assign `DrawOrder` (0 when the call carries none) and `Submission` on every entry, so no value of an earlier frame survives in a pooled entry.
- **API.** No existing signature changes. Additive overloads carry a final `int drawOrder`: the default key-less sprite path and the key-less blended sprite path (public), the blended path with a scissor rectangle (internal, like its existing twin), and the key-less texture path with `ignoresDepth` (public).
- With `draw_order` 0, a component makes the same calls as before.
- **Additive.** Existing worlds and samples load and draw as before, except that sprites of equal Z now have a defined order instead of the sort's.

## Consequences

- Data can stack sprites of equal Z deterministically, which blended sprites need in order to blend over each other in a chosen order.
- At an equal Z and `draw_order`, the order is the submission of the frame: deterministic per frame, but it can change when a sprite moves, since it comes from the octree.
- The comparator gains two integer comparisons; nothing is allocated.
- Deferred: an order for keyed sprites, a cutscene action that changes `draw_order`, a change of the world's submission order.
- The ADR number may change if `origin/main` gains ADRs before this branch is merged.
