# ADR-0067: A project mixer asset applied to the live mixer, and an editable mixer panel

- **Status**: Accepted (agent arbitrations P45 to P58 to be confirmed by the author)
- **Date**: 2026-10-06
- **Source**: `ai-agent/tasks/audio-modern-tasks.md`, slice S6b (tasks T10.1-T10.11, arbitrations P45-P58, open points
  O34-O39), implementing the author's decisions D15 to D21 recorded in ADR-0061. Completes ADR-0059 (bus graph built
  by code) and ADR-0060 (read-only audio panel).

## Context

- The bus graph of the software mixer (ADR-0059) was built by code only; the editor had a read-only Audio panel
  (ADR-0060) and a sound inspector with a hard-coded bus list.
- The engine can add a bus but cannot remove, rename or reparent one; effects are engine types with self-clamping
  parameters; a ducking's source is fixed at construction; the software backend holds 32 buses, Master included.
- The author decided (ADR-0061): one mixer asset per project named by an optional project setting, extension
  `.audioMixer` (D15); the panel edits the asset and applies it to the live mixer, never the reverse (D16); solo built on
  the existing mutes, keeping return buses and the Editor bus audible (D17); waveforms are the output level over time
  and a drawing of a sound file in the inspector (D18); Master stays out of the asset (D19); the `MGSlider` allocation
  is accepted here (D20); the sound inspector lists the asset's buses (D21).

## Decision

- **Asset** (P45): `AudioMixerAsset` (`.audioMixer`): buses (name, parent, volume), inserted effects (biquad,
  compressor, reverb, ducking) and sends; no Master, Editor, mutes, limiter or snapshots. Versioned like the other
  versioned assets (`type`, `version`, `schema_version`, a `MigrateToCurrent` hook); keys and loader in the runtime, the
  writer in `CasaEngine.EditorServices`. Effect defaults are named constants of the model (biquad LowPass, 1000 Hz,
  Butterworth Q: agent choices).
- **Validation and application** (P46, P47): tolerant per entry (ignored entries are logged with asset and bus names;
  only an unreadable file or a newer version is refused); `AudioMixerAssetApplier` applies idempotently in two passes
  (buses and volumes, then effects and sends), owns what it set (original volumes, effects, sends) and releases it,
  never removes, renames or reparents a bus, and never touches Master, Editor, mutes, the limiter or what the game set.
  A changed effect parameter is edited in place; a changed type or ducking source replaces the effect.
- **Project setting** (P48): `AudioMixerAsset` (id recommended, or name; empty = default mixer), written only when set;
  applied right after the asset loaders are registered at startup and on every editor project load, released on project
  close; `ProjectAudioMixer.Apply` never lets an exception reach the startup path.
- **Editor document and panel** (P49 to P54, P57): one document tab per asset with its own history context; snapshot
  undo/redo; one entry per gesture (fader drag, field typing burst); only the project's asset drives the live mixer, and
  open documents detach on project close and re-attach only when the new project names them; mute and solo transient;
  history operations disabled during a play session; closing a modified document re-applies the saved asset; read-only
  rows for live buses outside the asset, Master and Editor; the sound inspector lists the engine buses then the asset's
  others, marking an unknown bus without selecting it.
- **Waveforms** (P55, P56): the output level over the last 10 s from the existing metering (240 columns, dBFS scale),
  in the mixer panel only; the main audio file of a `.sound` drawn in its inspector (one transient decode, 512 columns,
  64 MB file cap).
- **Example** (P58): `demo_mixer.audioMixer` ships in the demo content, not activated.

Alternatives set aside: several mixer assets per project; editing the live mixer and saving it back (two sources of
truth); storing mutes in the asset; reconstructing a fresh mixer per project (buses cannot be removed); a single runtime
`Save` (saving belongs to the tooling); a per-bus level history in the Audio panel.

## Consequences

- A project without the setting behaves exactly as before; the startup cannot fail because of the asset.
- After a project change in the editor, the previous project's buses stay live (original volume, no effect, no send)
  until the editor restarts; a deleted bus stays live until the next start.
- `AudioService`, `IAudioBackend`, `AudioMixer`, `AudioBus` and the software mixer are unchanged; the parent repository
  is unaffected.
- The panel's faders allocate on each change until the MGUI fix (D20); the target of an existing send cannot be changed
  in place; drawing a large Ogg decodes the whole file, as playing it does.
- Open for the author: O34 (mutes, snapshots or limiter settings in the asset), O35 (editing during play), O36 (a "use
  as project mixer" button; activating the demo example), O37 (rename, reparent, live removal), O38 (waveform cost:
  measured 0.04-0.3 s for a 5-minute WAV, a file-size cap does not bound a large Ogg), O39 (confirmation when closing a
  modified mixer tab); confirmation of P45 to P58.
