# ADR-0015: Animation2D editor V1

- **Status**: Accepted
- **Date**: 2026-09-06 (backfill)
- **Source**: `docs/editor/animation2d_editor_casaengine.md:1-25`

## Context

`docs/editor/animation2d_editor_casaengine.md` targets a generic editor for `.anim2d` assets already used by the engine and editor (docs/editor/animation2d_editor_casaengine.md:5). The document states that a CasaEngine 2D animation should not be seen only as a sequence of complete images, but can be composed of several sprites, each with its own position, visibility, draw order and time-based changes (docs/editor/animation2d_editor_casaengine.md:7). It records "verrouillées" (locked) decisions at its head, and explicitly states that older mentions of a mono-sprite/event-only V1 subset found elsewhere in the document must be read as design history, not as a still-supported target (docs/editor/animation2d_editor_casaengine.md:9).

## Decision

- Keep the `.anim2d` extension and the contract loaded by `Animation2dData` (source: docs/editor/animation2d_editor_casaengine.md:13).
- Keep the existing time-based model (`time_seconds`) (source: docs/editor/animation2d_editor_casaengine.md:14).
- Target directly a composed 2D animation with `parts`, `tracks` and `events` (source: docs/editor/animation2d_editor_casaengine.md:15).
- Keep a read-only timeline, graduated in seconds, horizontally scrollable, zoomable and centered on events (source: docs/editor/animation2d_editor_casaengine.md:16).
- Stay centered on the generic CasaEngine editor (source: docs/editor/animation2d_editor_casaengine.md:17).
- Strictly separate runtime and editor (source: docs/editor/animation2d_editor_casaengine.md:18).

## Consequences

- The document's own body contradicts the locked decision above: later sections describe a V1 that displays only a single current sprite, changed only through supported events, with composition/sprite-library/part-hierarchy/property-track surfaces explicitly out of V1 scope (e.g. "En V1, il n'affiche qu'un seul sprite courant" at docs/editor/animation2d_editor_casaengine.md:846, and "Les surfaces de composition ... sont hors V1" at docs/editor/animation2d_editor_casaengine.md:836). Per `ai-agent/audits/analysis-decisions-inventory.md:89`, this contradiction (labelled `[dec-27]`) is tracked and treated by a separate task of the plan (T5.3), not by this ADR.
- Implementation status observed in code: `ai-agent/audits/analysis-decisions-inventory.md:89` reports the composed structures (`parts`, `tracks`, `events`) as present but the V1 editor as mono-sprite — i.e. "Partielle" — which was not independently re-verified against the editor UI code beyond the timeline types confirmed in ADR-0014; treat the precise current editor scope as unverified pending T5.3.
