# ADR-0137: The cutscene timeline is a read-only projection of the action tree, fed by a play clock

- **Status**: Accepted
- **Date**: 2026-11-18
- **Source**: this chantier: `ai-agent/tasks/cutscene-editor-timeline-tasks.md` (decisions D1 to D5, settled with the author, and choices P1 to P14, approved with the plan and open to correction at its acceptance), branch `chantier/cutscene-property-editing`. Refines ADR-0135.
- Renumbered from ADR-0098 on 2026-11-19 when origin/main was merged (its ADR-0098 is another decision)

## Context

- ADR-0135 decided that the timeline of the cutscene editor is a projection of the action tree, and listed it among the zones of the document. It deferred the timeline itself.
- `CutsceneDirector` exposes neither elapsed time nor per-action notifications; `Ended` is its only event. Actions run as coroutines: an action starts when its iterator is entered and ends after its last `yield`. `WaitFrames` and `FadeScreenFrames` count updates, and nothing converts frames to seconds.
- The preview plays a serialized copy of the asset, so executed actions are not the edited instances, though the tree has the same shape.
- The generic timeline control has no vertical scrolling and its hit test returns the first bar or range under the cursor, so a marker laid over a bar cannot be clicked. Its only consumer today is the Animation2D timeline, hosted as a contextual tool panel.

## Decision

- **Read-only projection (D1, D2).** The action tree stays the saved model and the `.cutscene` format does not change. The timeline is computed from the tree and edits nothing. Moving the playhead, reordering and free placement are deferred.
- **Display (D3, P10).** Time is shown in seconds and in frames. Frames are converted at 60 frames per second and the item is marked as estimated.
- **Hosting (P1, P14).** The timeline is a contextual tool panel "Cutscene Timeline", dockable and present only while a cutscene document is active, brought to the front when a cutscene is opened. It is bound to the active cutscene document and detached when that document closes. This refines ADR-0135, which placed the timeline among the zones of the document.
- **Play clock (P2, P3).** `CutsceneDirector` gains, additively, `PlayElapsedSeconds`, `PlayElapsedFrames` and the events `ActionStarted` and `ActionEnded`, allocation free, carrying the action, the time and the frame. The clock is reset by each `Play`, and advances after the coroutine update, only if the play running after the update is the one running before it, so the first step of a play always sees time 0.
- **Notification semantics (P4).** `ActionEnded` is raised when the code of an action ends without failure: normal end, an action that never returns, a request refused by the host, an empty `Parallel`. It is not raised for a failing action, nor for the running actions of a stopped, failed or replaced play, which only raise `Ended` as before. No notification comes from a `finally` block. For `MoveSprite` and `TintSprite`, `ActionEnded` follows `ActionStarted` immediately: the duration of their effect is not the duration of the action.
- **Rows (P5).** In this order, created only when used: Loops, Flow, one row per entity in order of first appearance, Screen, Audio, World and input, Other. `Sequence` and `Parallel` have no item. Overlapping bars or ranges of a row go on sub-rows; a marker stays on the row of its bar.
- **Durations (P6, P7, P8).** Waits, frame waits, fades and `FadeMusic` advance the sequence by their value; `MoveTo`, `NavigateTo`, `PlayVideo` and `PlayVideoSprite` by their measured duration, otherwise 1 s shown as estimated; other actions advance it by zero. A non-blocking action draws a bar by its duration (`TintSprite`), by the measured duration of its effect or an estimated 1 s (`MoveSprite`), or by its fade (`PlayMusic`, `StopMusic`); `ExitOnInput` is a bar open to the end; other instant actions are markers. `Parallel` starts its children at the same instant and lasts as long as its longest branch; the one-update runtime offset is not drawn.
- **Repeat (P9).** One round is drawn under a "Repeat xN" or "Repeat forever" range on the Loops row. For N greater than 1 a second range covers the following rounds and what follows is placed after them. For an endless loop the playhead wraps onto the drawn round.
- **Measured durations (P11).** Measured durations are those of the first occurrence of each action during the last play. Any document change clears them, and a play in progress restarts and measures again. The effect duration of a `MoveSprite` is wrong when another move, `ShowSprite` or `HideSprite` interrupts it: a documented limit.
- **Scrubbing (P12).** A click in the ruler moves nothing in this slice: the playhead returns to the play time. A click in the empty area keeps the document selection.
- **Generic control options (P13).** `TimelineControl` gets two options, off by default so that Animation2D is unchanged: vertical scrolling of the tracks under a fixed ruler, and a "most precise wins" hit test (a marker before a bar, the shortest bar before a longer one).

## Consequences

- A cutscene can be read in time at a glance, with a playhead following the preview, and its selection is shared with the tree and the Inspector.
- The runtime gains additive public members; without subscribers the behaviour is the one before. The clock and notifications are an observation tool, not a scripting hook.
- Some durations are only known after a play; before it they are drawn as estimates. For `FadeMusic`, the preview has no sound service, so its bar shows the declared duration while the play goes faster. An endless loop containing an unmeasured action wraps exactly only after a first round.
- Deferred: moving the playhead, pause and stepping, drag, resize, add and delete on the timeline, free placement, curves, highlighting the running action in the tree, vertical zoom and row folding.
