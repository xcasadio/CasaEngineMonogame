# ADR-0062: Default SPU tables and a project switch for the Master limiter

- **Status**: Accepted
- **Date**: 2026-10-06
- **Source**: `ai-agent/tasks/audio-modern-tasks.md`, slice T7 (tasks T7.1-T7.3), implementing the author's decisions
  D25 and D32 recorded in ADR-0061. Supersedes in part ADR-0058 (hardware tables) and completes ADR-0059 (Master
  limiter on by default).

## Context

- The software SPU (ADR-0058) received all its hardware tables from the caller, so no game could decode real
  PlayStation ADPCM or use the SPU reverb without supplying measured values itself.
- The Master limiter (ADR-0059) is on by default under the software backend; the author wants it to stay on, with a
  way for a project such as Alundra to switch it off without code.

## Decision

- `PsxSpuHardwareTables.CreateDefault()` (additive) returns the tables the engine ships:
  - the 5 ADPCM filter coefficient pairs copied from psx-spx ("CDROM XA Audio ADPCM Compression", "Pos/neg
    Tables", https://psx-spx.consoledev.net/ps1/cdr/cdromformat/; the SPU page refers to the CD-XA filters), the
    author accepting that psx-spx declares no licence;
  - a 39-tap reverb FIR computed once by formula, with no hardware value: windowed-sinc low-pass of S. W. Smith,
    "The Scientist and Engineer's Guide to Digital Signal Processing", ch. 16 (eq. 16-4 kernel, eq. 16-2 Blackman
    window), M = 38, cut-off 11025 Hz, rounded to Q15 with the centre tap adjusted so the sum is exactly 32768;
  - no Gaussian table: it stays caller-supplied, and the engine's cubic interpolation is used without it.
- The existing constructor, its validation and the reverb's use of the FIR are unchanged.
- An additive project setting, `bool? IsMasterLimiterEnabled` (absent = on, written only when set), switches the
  Master limiter off. `ProjectAudioSettings.Apply(AudioService, ProjectSettings)` applies it with the Master mute at
  startup, and `EditorProjectAudioMuteSync(AudioService)` re-applies both on every project open in the editor; the
  limiter is touched only under a backend with the bus capability. No existing public signature changes.

## Consequences

- A game creates a working SPU with `PsxSpuHardwareTables.CreateDefault()`; its ADPCM decoding follows the documented
  filters, while its reverb tone is an approximation (formula FIR) and its level may still be about half the console's
  (open question O19, reading 20).
- Projects without the setting keep the limiter; a project file without it is unchanged when saved; opening a
  project without it after one that switched it off turns it back on.
