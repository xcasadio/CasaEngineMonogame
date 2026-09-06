# ADR-0002: Audio asset format and editor scope V1

- **Status**: Accepted
- **Date**: 2026-08-26
- **Source**: `ai-agent/audits/analysis-audio-system.md:194-203` (section 3, D3, D4, D6, D8, D9, D12, arbitrated with the author on 2026-08-26); `docs/engine/audio-system.md:185-200` (known limits). Backfilled on 2026-09-06.

## Context

The V1 audio system (ADR-0001) needed an asset model, a file format policy and an editor scope. The engine already referenced binary content through small JSON assets (a `.texture` referencing a `.png`), the Content Browser listed `.mp3` files as "Sound" although they were not playable, and three legacy audio source files had no caller (source: `analysis-audio-system.md:194, 197, 200`).

## Decision

- Sounds are **JSON `.sound` assets** referencing the audio file plus metadata, like `.texture` references a `.png` (source: `analysis-audio-system.md:194`, D3).
- V1 metadata of a `.sound`: file reference (Guid), **volume**, **pitch**, **loop**, **target bus**, explicit **streaming mode**; no random variations in V1 (source: `analysis-audio-system.md:195`, D4).
- **WAV** for sound effects and, since the streaming decision, for music; Ogg Vorbis is a later extension on the same API; `.mp3` is **removed** from the Content Browser mapping (source: `analysis-audio-system.md:197`, D6).
- Editor scope V1: a **`.sound` asset inspector** (document opened on double-click, with preview) and a **"Create Sound" context menu** on folders; no direct preview in the Content Browser, no mixer panel, no waveform (source: `analysis-audio-system.md:199`, D8).
- **Removal** of the dead code `Sound.cs`, `IAudioEmitter.cs`, `AudioComponent.cs` (source: `analysis-audio-system.md:200`, D9).
- A separate **"Editor" bus**, with its own volume and mute: the inspector preview never goes through the game buses and survives `Stop` in play-in-editor (source: `analysis-audio-system.md:203`, D12).

## Consequences

- A `.mp3` must be converted to `.wav` to be played (source: `audio-system.md:187-189`).
- The mixer panel, the waveform display, the Content Browser preview and random variations are explicitly out of V1 and remain open for a later version (source: `analysis-audio-system.md:195, 199`).
- Implementation observed on 2026-09-06: `SoundAsset.cs`, `SoundAssetLoader.cs` (asset and loader), `SoundAssetInspectorPanel.cs` (editor inspector), the Editor bus constant in `AudioBusNames.cs`; none of `Sound.cs`, `IAudioEmitter.cs`, `AudioComponent.cs` exists under `CasaEngine/`, `CasaEngine.Editor/` or `CasaEngine.EditorServices/`.
