# ADR-0074: A streamed music track loops back to its loop-start sample

- **Status**: Accepted (numbered ADR-0072 until 2026-10-10: renumbered when the engine merged origin/main, whose ADR-0072 records the demos main screen; the decision is unchanged)
- **Date**: 2026-10-08 (plan E19.L-a, task T1 read until READY twice; decision D-E19-102 of the parent repository)
- **Source**: this chantier: `ai-agent/tasks/e19la-music-loop-start-tasks.md` (tasks T1.1 to T1.3). Parent repository:
  `docs/plan-e19-opcodes.md` section 1.2w.1 (rules LA-R1 to LA-R7), values and counter-check in
  `docs/plan-e19-loop-annexe/la-1008/` (`notes.md`, `sim.out`, `verify/verify.md`). Extends ADR-0056 (streaming worker) and ADR-0063
  (additive `.sound` keys); replaces no ADR.

## Context

- A streamed track loops by seeking its reader back to the start of the file; two sites do it, `MusicPlayer.FillQueue` (inline mode and the
  synchronous first fill of `Play` in every mode) and the streaming worker (`StreamingWorker.Channel.Service`, the production path of the
  software and MonoGame backends). `Acquire` and `Channel.Reset` carried no loop point.
- The game being ported plays music whose loop jumps back to a point inside the track, not to its first sample. The engine could not
  express that: `SoundAsset` had no loop point and `WavStreamReader` could only rewind to the first audio byte.
- `SoundAsset.ReadNumber` reads through a float: an odd integer above 2^24 (16 777 217) would come back as 16 777 216.
- The data of a wav file starts at the offset read from its header, which is 44 only for a 16-byte `fmt ` chunk and no other chunk (46 with an
  18-byte `fmt `, 60 with an 18-byte `fmt ` and a 6-byte `LIST`): a seek to `44 + frame * BlockAlign` lands on the wrong frame.
- A read stops at the end of the data, so a seam always ends one buffer.

## Decision

- `SoundAsset.LoopStartSample` (`long`, in sample frames, one sample per channel counted from the first audio byte; default 0; a negative
  value is brought back to 0). `Load` reads the optional key `loop_start_sample` as an exact integer; any other token type warns, naming the
  asset and the key, and keeps 0. `SaveSoundAsset` writes the key only when it is not 0, right after `is_streaming`, so every existing
  `.sound` saves with exactly its keys. The inspector has no row for it (its numeric field is a float); `CreatePreviewCopy` (now `internal`)
  copies it.
- `WavStreamReader.SeekToSample(long frame)` seeks to the data offset read from the header plus `frame * BlockAlign`, and throws unless
  `0 <= frame < WavFormatInfo.SampleFrameCount`; it allocates nothing. `Rewind()` is `SeekToSample(0)`.
- `MusicPlayer.Play` checks the loop point once, for a looped track only: a value at or past the end of the data gives one warning (through
  the single throttle the player already uses for all its warnings) and 0 is used. The value is kept on the track and the asset is not read
  again. `StreamingWorker.Acquire(reader, isLooped, loopStartSample, name)` and `Channel.Reset(reader, isLooped, loopStartSample, name)` carry
  it; `Reset` writes it before `State` becomes active, like the reader and the flag.
- Both loop sites seek to the loop start instead of rewinding. The reads still stop at the end of the data: each seam submits one buffer
  shorter than the others, and nothing is filled across the seam (it would gain 90 ms at a queue of 24 buffers, for a second code path).
- Out of scope, unchanged: a loop end (the loop runs from the end of the data), an engine crossfade at the seam (baked into the file by its
  producer), a loop point for resident sounds (a sound that is not streamed ignores the key), the `IAudioBackend` contract.

## Consequences

- A game can loop its music from any frame by authoring one integer in the `.sound`; a project that does not use the key sees the same bytes
  as before (`loop_start_sample` 0 is the old behaviour).
- At a seam the queue holds one short buffer, so the margin against a late `Update` shrinks to the duration of the buffers behind it. With the
  default depth of 3 buffers this margin is small at the worst seam; the depth of the default queue is a separate decision (plan task T2) and
  is not changed here.
- A loop region shorter than one buffer submits short buffers on every pass (a loop start on the last frame submits buffers of one frame);
  the player does not merge them.
- Only a streamed, looped sound uses the key; setting it on a resident sound is silently ignored.
- Editing the key is done in the `.sound` file; the inspector neither shows nor changes it, but keeps it through a save and a preview.
- Because the key is additive and written only when not 0, going back to a version of the editor without it loses the loop point at the next
  save of such a `.sound`.
