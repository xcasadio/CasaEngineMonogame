# ADR-0046: A logical tick clock for the ends of 2D animations

- **Status**: Accepted
- **Date**: 2026-10-01 (decisions D1-D4 taken with the author on 2026-10-01; plan approved on 2026-10-01)
- **Source**: this chantier: `ai-agent/tasks/animation-logical-end-clock-tasks.md` (decisions D1-D4), branch
  `chantier/animation-logical-end-clock`. Parent repository: `docs/plan-e19-opcodes.md` (D-E19-16, D-E19-17, D-E19-19,
  step E19.c2, open point O-E19-10). Refines the runtime sampling described in
  `docs/engine/animation2d-composed-format-v1.md`; replaces no ADR.

## Context

- `AnimatedSpriteComponent.Update` advances an `Animation2dCompositionSampler` by a real `float` elapsed time and
  raises `AnimationFinished` on the transition to the finished state (`CurrentTime > DurationSeconds`, strict). A game
  that simulates on a fixed integer tick (the Alundra port, 50 Hz) sees the end of an animation one tick late or early
  with respect to its own logic: the float32 sum of 0.02 s steps lands on, or just under, the key times.
- Defect of the real-time path: `UpdateLooping` accepts `previousTime + remainingTime` as inside the cycle even when it
  equals the duration exactly, then replaces a non-positive `timeUntilRestart` by a whole duration without resetting
  `previousTime`. The time then exceeds the duration for ever, the loop never wraps again and the sprite samples the
  hidden end key. Example: animation 53 of the hero (0.32 s) at 0.02 s per step: 0.29999998 after 15 updates, 0.32 at
  the 16th, then 0.34, 0.36... `Seek(Duration)` on a loop enters the same state. Over the 9623 exported `.anim2d`, a
  float32 model gives 197 of 5205 positive-duration loops that never wrap at 0.02 s, none at 1/60, 1/144 or 1/30 s.
  `docs/engine/animation2d-composed-format-v1.md` already states that time wraps modulo the duration.
- Data: every exported key time is within 1.03e-4 tick of the 50 Hz grid; the duration always comes from the last key of
  a track; no exported Once has a zero duration, 5 loops have one; no exported `.anim2d` has a drawn event.
- The author refused to advance the rendering of sprites by ticks.

## Decision

- **D1** - `AnimatedSpriteComponent` gains an optional logical clock in whole ticks, driven by the game layer. When it is
  active it alone raises `AnimationFinished` (Once) and the new `AnimationLooped` (one per loop turn), at the exact tick.
- **D2** - Rendering stays in real time: images, collision timeline and drawn events are unchanged. Only the logical ends
  are exact. There is no re-alignment of the rendering at the logical end.
- **D3** - The loop freeze of the real-time path is fixed in this chantier: a loop whose time lands on its duration
  wraps (strict `<` test, and an immediate wrap when the time already is at or past the duration). In the default mode,
  `AnimationLooped` is also raised once per turn so that the event has the same contract in both modes.
- **API** (additive): `SetLogicalTickRate(int)` (0 = off, default; negative throws `ArgumentOutOfRangeException`; the
  same value does nothing; another value resets the clock), `int AdvanceLogicalTicks(int)` (synchronous events, returns
  the ticks applied; negative throws `ArgumentOutOfRangeException`; with the rate at 0 throws `InvalidOperationException`),
  read-only `LogicalTickRate`, `LogicalTick`, `LogicalDurationTicks`, `IsLogicalEndReached`, `CompletedLoopCount`, and
  `event EventHandler<Animation2d> AnimationLooped`. The counter lives on the component in value fields; the sampler
  stays pure and only gains `DurationSeconds` and `AnimationType` getters and the loop turn count of its last update.
- **Rules**: the duration in ticks D is 0 if `DurationSeconds` <= 0, else max(1, round-half-away-from-zero of
  `DurationSeconds * rate`). A Once finishes on its D-th advance after a reset (one `AnimationFinished`, then nothing); a
  Loop turns on every D-th advance (`AnimationLooped`, count + 1, tick back to 0). With D = 0 a Once finishes on the first
  advance and a Loop never turns. **These are rules of the engine**, not a copy of the Alundra binary, where an image with
  a zero delay freezes the animation.
- **No animation**: with no current animation or sampler, `SetLogicalTickRate` keeps the rate and sets D to 0 with no
  pending end; `AdvanceLogicalTicks` does nothing, raises nothing, returns 0 and never throws. The "a Once of D = 0
  finishes on the first advance" rule only applies to a real animation.
- **Resets** of the clock: a `SetCurrentAnimation` that resets the sampler, `InitializeWithWorld`, a change of rate; D is
  then recomputed from the current sampler. `SeekCurrentAnimation` re-aligns the tick without any event
  (k = round-half-away-from-zero of max(0, t) * rate; Once: tick = min(k, D), end reached if D > 0 and k >= D; Loop:
  tick = k mod D, 0 if D = 0; the loop count is unchanged).
- Neither `IsPlaybackPaused` nor `ExecutionPolicy` blocks the logical advance (the game layer decides when to advance).
  The copy constructor does not copy the rate; `InitializeWithWorld` keeps it. `ShouldUpdateWhenConditional` is unchanged.
- A handler that changes the animation during an advance stops it through a reset version; the remaining ticks are
  dropped and the returned value says so. `AdvanceLogicalTicks` allocates nothing.

## Consequences

- An animation has two clocks: rendering (real time) and the logical end (ticks). They can differ by about one tick at
  the end of an animation; the game layer reads the logical one for its rules.
- A component with the clock active and no driver never raises `AnimationFinished` nor `AnimationLooped` again.
- Key times off the tick grid are rounded to the nearest tick for the logical end.
- Loops that were frozen on the hidden end key now keep playing in real time; `CasaUIAnimatedImage`, which owns its
  sampler, benefits from the fix. No existing engine test changes.
- Drawn events and the collision timeline stay on the real-time clock; making them logical is out of scope.
