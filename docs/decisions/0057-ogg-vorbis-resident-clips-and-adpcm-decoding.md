# ADR-0057: Ogg Vorbis resident clips and ADPCM decoding in one clip loader

- **Status**: Accepted
- **Date**: 2026-10-05 (decided by the agent in AUTO mode after the author's "fini tout", within the approved
  program envelope; to be confirmed by the author on return)
- **Source**: this chantier: `ai-agent/tasks/audio-modern-tasks.md`, slice S3 (tasks T3.1-T3.3, decisions P15 and
  P16, open questions O11 and O13). Builds on ADR-0055 (engine-owned software mixer), which is not replaced.

## Context

- After slice S1 the engine read only WAV files (`WavDecoder`: integer PCM 8/16/24/32-bit and 32-bit float). The
  content browser already classified `.ogg` as a sound, but no loader accepted it, and ADPCM WAV files, which
  MonoGame used to load, were refused.
- NVorbis 0.10.4 (MIT, fully managed) is already shipped with the engine as a transitive dependency of
  `MonoGame.Framework.DesktopGL` 3.8.5.1. Measured during discovery, its decoding allocates about 41 KB per read of
  4,096 frames (about 444 KB/s for 44.1 kHz stereo).
- `AssetContentManager` keeps one loader per asset type: a second `IAudioClip` loader cannot be registered.
- AGENTS.md §9.3 forbids allocations in hot paths, which include asset streaming.

## Decision

- One clip loader, `AudioClipLoader`, registered for `IAudioClip`, picks the decoder by extension: `.wav` →
  `WavDecoder`, `.ogg` → `OggDecoder`. It replaces `WavAudioClipLoader`, which existed only on this unmerged branch.
- **Ogg Vorbis is resident only** (P15): the whole file is decoded at load time through NVorbis 0.10.4, now
  referenced directly at the exact version MonoGame ships (central package management), into a `PcmAudioClip`.
  Float samples go through the same conversion as WAV float (`PcmConversion`). A streamed Ogg track is refused by
  `MusicPlayer` with a clear message, because streaming decode would allocate on every packet (open question O11).
- **MS ADPCM (tag 2) and IMA/DVI ADPCM (tag 0x11)** are decoded by `WavDecoder` for resident clips (P16), mono and
  stereo, from the documentation cited in `AdpcmDecoder.cs`. The `fact` chunk trims the last block; a short final
  block that holds its full header is decoded. IMA uses the closed-form difference `((2·(nibble&7)+1)·step)>>3`,
  which matches ffmpeg's decoder sample for sample; the IMA reference document's add-and-shift form differs by at
  most 2 per sample. `WavStreamReader` still reads 16-bit PCM only.
- MP3, FLAC and Opus are not supported: they would be new dependencies (open question O13).

## Consequences

- Games can ship `.ogg` sound effects and short music as resident clips on both backends (the MonoGame backend
  receives the decoded `PcmAudioClip`).
- A long Ogg music track costs its full decoded size in memory (16-bit PCM), since it cannot be streamed yet.
- Decoders are covered by committed synthetic fixtures generated with ffmpeg (commands in
  `CasaEngine.Tests/Audio/Fixtures/README.md`); the ADPCM ones match ffmpeg's reference PCM exactly.
- The editor's `.sound` inspector accepts `.wav` and `.ogg` files.
