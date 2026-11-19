# ADR-0128: The engine plays MDEC videos through a Video element

- **Status**: Accepted
- **Date**: 2026-11-08
- **Source**: this chantier: `ai-agent/tasks/video-playback-tasks.md` (decisions D1 to D8, settled with the author on 2026-11-08, and choices C1 to C10, locked by the approval of the plan on 2026-11-08), branch `chantier/video-playback`. Follows ADR-0016 of MGUI (host image sources), ADR-0037, ADR-0058, ADR-0078, ADR-0080 and ADR-0088; MGUI's ADR-0039 records the element side.
- Renumbered from ADR-0089 on 2026-11-19 when origin/main was merged (its ADR-0089 is another decision)

## Context

- A project needs to play videos stored as PSX MDEC frames, the format the console's macroblock decoder reads, and to show them in a data-driven MGUI screen.
- The engine has no video, MDEC, IDCT or YCbCr code. Its audio voices expose no playback position, so syncing on the sound needs a new audio capability; the first video this serves has no sound.
- MGUI already lets the host provide animated image sources (`IUIAnimatedImage`, `IUIAssetProvider.TryCreateAnimatedImage`) that an `MGImage` advances in `UpdateSelf` with no allocation and no re-layout while the size does not change. `IUIImageResource` is an opaque handle the engine implements as `CasaMonoGameImageResource` over a `Texture2D`.
- In the UI input path of ADR-0080, `UINavigationInput` sends every action to the MGUI desktop and only reports an unhandled Cancel, to the top data-driven screen's `cancel_command`. Submit never reaches a screen.
- The public documentation of the format is psx-spx (https://psx-spx.consoledev.net/macroblockdecodermdec/ and the "BS Compression" sections of https://psx-spx.consoledev.net/cdromfileformats/). It states that the hardware's arithmetic is not reproduced bit for bit.

## Decision

- **Container `.mdecvideo`**, little-endian, asset type `mdecvideo`: magic `MDVC`, format version 1, reserved flags (0), displayed width and height (1 to 4096), frame count (at least 1), frame rate as a numerator and a denominator (both non-zero), offset and length of an optional audio section ((0, 0) when absent), a table of frame count + 1 offsets from the start of the file, strictly increasing, then the frames. Each frame is a BS frame as the console reads it: an 8-byte header (count, 0x3800, quantisation scale, version 1, 2 or 3) and the bitstream. The loader reads the header and the table only and validates every field with an error naming the file and the field; frames are read on demand.
- **No sound in this decision.** The audio section, a WAV file, is reserved and validated as bounds only. The player paces on the frame rate. Syncing on the sound comes with the first video that has one.
- **Decoder written anew from psx-spx and MPEG-1**, on the CPU: bitstream to run-length words for versions 1, 2 and 3, dequantisation, an integer IDCT, YCbCr to RGB, then RGBA output (always 24-bit colour, alpha 255), cropped to the displayed size when it is not a multiple of 16. The exact rules, and the engine's own fixed-point choices where psx-spx leaves the result open, are written in `docs/engine/video.md` before the code and proven by tests on synthetic frames.
- **Player `MdecVideoPlayer`**, which implements MGUI's `IUIVideo`: it owns a `Texture2D` of the displayed size, reads frames through a file stream into a reused buffer, decodes on the game thread into a reused RGBA buffer and makes one `SetData` per new frame. Its clock counts integer ticks of the time given to `Advance`: frame k is shown while the elapsed time is within [k, k+1) frame periods; a late frame is not decoded, only the last one due. The video ends when the time covers every frame; the last frame stays shown. A stream error ends the video and is logged once. Nothing is allocated after the start.
- **The Video element plays the video, through the engine.** `<Video SourceName="..."/>` asks the host for an `IUIVideo` (`IUIAssetProvider.TryCreateVideo`, MGUI's ADR-0039); `CasaUIAssetProvider` creates a player for a catalogued `mdecvideo` asset found by name or id. The element advances it, shows it, and raises its end and skip events, which also run `EndedCommandName` and `SkippedCommandName` through the screen's commands. A data-driven screen refuses a Video that names an undeclared command.
- **Skip.** A video declared skippable is skipped by a Submit or Cancel press of the project's UI mapping. `UINavigationInput` calls a hook for each such press, not for repeats, before the desktop sees it; `UIRoot` answers by asking the top data-driven screen to skip its playing skippable videos. When a video is skipped, the press is consumed: no button is clicked and no `cancel_command` runs. Otherwise the path of ADR-0080 is unchanged. Without a mapping, no key skips a video.
- **Cutscene action `PlayVideo`** (`{"type": "PlayVideo", "target": "<screen id or name>"}`): it opens the screen like `OpenScreen`, with the same failures and the same rule under external view management, waits until every Video of the screen has ended or been skipped, then removes the screen. A screen with no Video fails the play. A cutscene stopped before removes the screen, except when the world is cleared.
- **Additive.** Existing projects, screens and cutscenes load and behave as before.

## Consequences

- A data-only project can play a console video in a screen, chain screens on its end, and let the player skip it.
- Decoding runs on the game thread. Its speed is measured and written in `docs/engine/video.md`; a thread would only be added on a later decision if the measure requires it.
- The output is not bit-exact with the console's decoder, which psx-spx does not document exactly; the engine's arithmetic is its own, documented and tested.
- A collapsed Video does not advance, like an animated image.
- Deferred: sound and audio-clock sync, interleaved STR streams and XA audio, 15-bit output, a decoding thread, an editor for the asset.
- The ADR number may change if `origin/main` gains ADRs before this branch is merged.
