# ADR-0014: Generic timeline base for the editor

- **Status**: Accepted
- **Date**: 2026-06-13
- **Source**: `docs/editor/timeline-generic.md:1-35`

## Context

`docs/editor/timeline-generic.md` records a design review that confronted an earlier conception document with the actual repository code, and states that the corrections in its "Décisions verrouillées" section take precedence over the rest of the document in case of conflict (docs/editor/timeline-generic.md:2-6). The review found that the real types were `internal sealed`: `TimelineModel { DurationSeconds, Lanes, Events }`, `TimelineLane { Id, Label, IsEditable }`, `TimelineEvent { Id, LaneId, ... }` (docs/editor/timeline-generic.md:33-35), and that a real cutscene model exists as an action tree (`Sequence`/`Parallel`, `CutsceneAsset.RootAction`) played by `CutsceneDirector`, with no `StartTime`/`Duration`, no per-actor track, and no `Seek`/`Pause`/`Update(dt)` on the director (docs/editor/timeline-generic.md:14-20).

## Decision

- Build a generic timeline base (`Track` / `Item` / `Duration` / `Kind`), validated only by the existing Animation2D editor (source: docs/editor/timeline-generic.md:12-13).
- The cutscene model is an action tree (`Sequence`/`Parallel`) played by `CutsceneDirector`, not a per-actor track; there is no `CutsceneTimelineAdapter` that would turn a flat model into the source of truth for cutscenes, and the cutscene editor stays a tree control, not a flat timeline (source: docs/editor/timeline-generic.md:14-23).
- The generic model moves from `internal sealed` to `public sealed`, staying in `CasaEngine.Editor.Controls.Timeline` (source: docs/editor/timeline-generic.md:24-25).
- The `Lane`/`Event` naming is renamed to `Track`/`Item`, propagated up to the Animation2D API (`Animation2dTimelineControl`, its public data records and events, and `Animation2dAssetInspectorPanel`) (source: docs/editor/timeline-generic.md:26-28).
- The work is phased: the core first (renaming, then `Duration`/`Kind`), then the abstractions (adapter, policy, renderer, menu provider, playback) (source: docs/editor/timeline-generic.md:29-30).

## Consequences

- A cutscene timeline, if built later, may at most show a read-only preview projection computed from the action tree; it must never become the source of truth for cutscene data (docs/editor/timeline-generic.md:18-21). All cutscene-related sections of the design document beyond the locked scope are marked out of scope / aspirational (docs/editor/timeline-generic.md:21-23).
- Implementation status observed in code: `TimelineModel`, `ITimelineAdapter` exist under `CasaEngine.Editor/Controls/Timeline/` (`CasaEngine.Editor/Controls/Timeline/TimelineModel.cs`, `CasaEngine.Editor/Controls/Timeline/Editing/ITimelineAdapter.cs`), and `Animation2dTimelineControl` / `Animation2dAssetInspectorPanel` consume the timeline types (`CasaEngine.Editor/Controls/Animation2dTimelineControl.cs`, `CasaEngine.Editor/Controls/Animation2dAssetInspectorPanel.cs`). The phased approach (decision 5) is reported as only partially completed in `ai-agent/audits/analysis-decisions-inventory.md:87`; this was not independently re-verified beyond confirming the core types exist.
