# ADR-0139: The cutscene preview shows the first instant at time 0 and keeps its world live once no play runs

- **Status**: Accepted
- **Date**: 2026-11-18
- **Source**: this chantier: `ai-agent/tasks/cutscene-editor-transport-tasks.md` (phase 4, choices P16 to P18, taken after the independent verification of the transport and the author's request of 2026-11-18 to correct its reported points), branch `chantier/cutscene-property-editing`. Refines ADR-0138 without rewriting it; makes precise its point on the end of a play during a seek.
- Renumbered from ADR-0100 on 2026-11-19 when origin/main was merged (its ADR-0100 is another decision)

## Context

- ADR-0138 made the preview pause, step and seek by replay. Going to time 0 rebuilds the world, launches the play and pauses before any world update. The first step of a play runs at the first update of the world that follows `Play`, so the actions placed at time 0 have not played and the viewport is empty.
- `WorldRuntimeSystems.Update` advances the play clock of the director (`CutsceneDirector.AdvancePlayClock`) after the coroutines, at every update: an update of zero duration during a play adds 0 seconds and one frame to the clock.
- An action scheduled at time t runs during the update that leaves t. A wait instruction returned during an update is checked only at the next one, so a wait created during an update of zero duration ends one step earlier than the same wait created in a normal play. A play continued after an update of zero duration is therefore one frame ahead of the clock for all that follows.
- The branches of a `Parallel` are queued by `StartCoroutine` and start only at the next update.
- The transport of ADR-0138 keeps its `Paused` or `Seeking` mode after the play has ended, and in `Paused` or `Seeking` it makes no step of the frame duration. After the end of a play reached during a seek, the preview world is no longer updated, while it is updated after an end reached during a normal play. A visible effect that outlives the last action (a sprite move still running) can then finish in a different state for the same end time.
- The status line of the preview writes the state of the play, which stays `Playing` during a pause or a seek, next to the word of the transport row ("Paused", "Seeking").

## Decision

- **Time 0 shows the first instant (P17).** After the play is relaunched, going to time 0 gives the world one update of zero duration: the actions of time 0 play without time advancing. This update is not counted in the displayed frames.
- **That start state serves display only (P17).** It is marked, and a play continued from it would be one frame ahead of the clock (see Context). Every action that starts from it (a seek to another instant, a resume, `Play` during the pause, a next frame) relaunches the play without the update of zero duration, by rebuilding the world, then plays or steps as usual. The state shown at an instant therefore never depends on the path followed. `Stop` keeps its meaning: the state before the cutscene. A relaunch clears the mark, so a play coming from a relaunch is never one frame ahead.
- **Accepted limit of the first instant (P17).** The actions of time 0 placed in a `Parallel` are not shown, because its branches start only at the next update.
- **The world stays live when no play runs (P18).** Pause and seek exist only during a play. As soon as no play runs, the transport returns to `Running`: the preview world advances with the editor frames, as the world of the game does after the cutscene, however the end was reached (normal end, failure, refused change of world, end during a seek). Requested frame steps are forgotten. This makes precise the point of ADR-0138 where a play that ends during a seek stops the advance: the seek stops stepping, and the next update applies the rule above. The displayed state is unchanged (the pure state function does not depend on the mode when no play runs).
- **The status line follows the transport (P16).** While a play runs, the status line of the preview writes the word of the transport row ("Playing", "Paused", "Seeking"); otherwise it writes the end state as before.

## Consequences

- The first instant of a cutscene is visible when the playhead is at 0, and a seek to 0 and a seek to any other instant give the same state whatever the path.
- Going to time 0 and resuming costs a rebuild of the world in addition to the one that showed the first instant. This is the price of a state that does not depend on the path, and falls under the cost of replays already accepted in ADR-0138.
- After the end of a play, the effects that outlive the last action finish the same way for the same end time, whether the end came during a play or a seek.
- The session keeps the asset of the last relaunch, to be able to relaunch from the marked start state.
- The runtime `CasaEngine/` is unchanged.
- The cases the samples cannot reproduce (a play that ends in `Stopped` through a refused change of world) are covered by unit tests of the transport and by reading the code.
