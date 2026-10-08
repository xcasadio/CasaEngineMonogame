# ADR-0073: The default music queue is 24 buffers deep

- **Status**: Accepted
- **Date**: 2026-10-08 (decision of the author, D-E19-104 of the parent repository, plan E19.L-a task T2)
- **Source**: this chantier: `ai-agent/tasks/e19la-music-loop-start-tasks.md` (task T2). Parent repository: `docs/plan-e19-opcodes.md`
  section 1.2w.1 (LA-T2), margins and costs in `docs/plan-e19-loop-annexe/la-1008/` (`notes.md` section 4, `verify/verify.md` items 9, 10 and 14,
  `verify/margins.out`, `verify/seams.out`) and the stall measurement O-E19-63 (`docs/plan-e19-backlog-1006-annexe/o63/`). Follows ADR-0072,
  which left the depth of the default queue to a separate decision; replaces no ADR (ADR-0056 did not fix the number of buffers).

## Context

- `MusicPlayer.DefaultQueuedBufferTarget` was 3 buffers of 16 KiB. That is about 186 ms per buffer at 22.05 kHz, but about 93 ms at 44.1 kHz,
  the rate of the music the engine now plays.
- A loop seam submits one buffer shorter than the others (ADR-0072). With 3 buffers the audio queued behind the seam was 96 ms at the worst
  seam of the extracted game tracks (81 frames, track 13); the worst measured stall of the game thread is 351.5 ms (O-E19-63). A late `Update`
  at a seam would starve the voice.
- With 24 buffers the same margins are 2045 ms at the worst seam and about 2.2 s in steady state.

## Decision

- `MusicPlayer.DefaultQueuedBufferTarget` is 24, for the whole engine (every caller of the default constructor, the inline and the worker modes).
  A caller that needs another depth still passes it to the constructor.
- The stereo software voice (`StereoVoiceMixer`) keeps its own queue of 3 buffers of about 20 ms: it is fed from memory by the mixer and
  its documentation no longer says it follows the music constant.
- `MusicPlayerTests.SampleCount` is 106 496 frames (26 buffers of 16 KiB) so the file is long enough for the queue to fill, and
  `Play_StartsAStreamAndQueuesAhead` and `Update_TopsTheQueueBackUpAsBuffersAreConsumed` expect 24, then 22 after two consumed buffers, then 24.

## Consequences

- Cost per played track: 384 KiB of queued audio, a 768 KiB ring in the streaming worker (channels are pooled and never freed), and a 384 KiB
  read, synchronous, in `Play`. The software mixer accepts 64 buffers per voice, so 24 fit.
- `GetPosition` can run ahead of what is heard by up to about 2.2 s.
- A seam can still starve a queue that holds only short buffers (a loop region shorter than one buffer, ADR-0072); the depth does not change that.
- A game that played music under the engine default of 3 now keeps more audio queued; nothing in the file formats changes.
- Going back to 3 is one constant and the two test values; the task is a commit of its own so it can be reverted alone.
