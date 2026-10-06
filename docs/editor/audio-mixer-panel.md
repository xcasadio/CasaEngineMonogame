# Audio mixer panel

An editable mixer for a project's `.audioMixer` asset: bus faders, mute, solo, meters, the output level over time,
and each bus's effects, sends and ducking, with undo/redo and save. The asset is the single source of truth: the panel
edits the asset and applies it to the live mixer, never the reverse. Decision:
[ADR-0067](../decisions/0067-a-project-mixer-asset-applied-to-the-live-mixer.md). Format and runtime application:
[audio-system.md §2 ter](../engine/audio-system.md). Code: `CasaEngine.Editor/Controls/AudioMixerPanel.cs`,
`AudioMixerBusDetailView.cs`, `AudioMixerGestureTracker.cs`, `AudioEnvelopeControl.cs`, `AudioLevelHistory.cs`;
`CasaEngine.EditorServices/Audio/AudioMixerDocument.cs`, `AudioMixerSolo.cs`, `AudioMixerEffectCatalog.cs`.

## Opening it

- **Create**: right-click a folder in the Content Browser, **Create Audio Mixer** (`NewAudioMixer.audioMixer`, the four
  engine buses under Master).
- **Open**: double-click a `.audioMixer`; one document tab per asset.
- **Make it the project mixer**: set `"AudioMixerAsset": "<asset id>"` in the project file (there is no settings UI).
  The panel says which: "Live: changes are applied to the mixer", or "This asset is not the project mixer: changes
  are saved but not heard".

## What it shows

- A header, the banners (live or not heard; "Effects, sends and ducking: software backend only" when the backend has no
  bus capability; "Stop play mode to edit the mixer" during a play session), Save, Reload, Apply to live mixer, and the
  validation problems of the asset.
- **Output level (last 10 s)**: peak and RMS of the output over the last ten seconds, newest on the right.
- **Bus strips**, indented by depth: editable rows for the asset's buses (fader 0..1 with a dB readout, M, S, meter,
  delete for custom buses); read-only rows for live buses the asset does not list (for example buses a game creates);
  Master and Editor read-only with their live volume. An "Add bus" row (name, parent).
- **Effects and sends of the selected bus** (click a bus name): its effects in order (up, down, delete, one field per
  parameter, filter type, ducking source, add effect) and its sends (target, level; level 0 removes).

## Behaviour

- **One history entry per user intent**: a fader drag is one entry; a burst of typing in a field is one entry, written
  after 0.5 s without a change or when the field loses focus (the live mixer follows at that moment); up, down, delete,
  add and combo changes are one entry each. Undo and redo re-apply the asset to the live mixer.
- **Mute and solo** are transient: not saved, not undoable, released when the tab closes. Solo keeps the soloed buses,
  their ancestors and descendants, the return buses (targets of sends, from the asset and the live mixer) and the Editor
  bus audible; a live bus outside the asset is never muted.
- **Play sessions**: faders, add and delete, Save, Apply, Reload and every effect or send control are disabled; mute,
  solo and meters keep working.
- **Closing** a modified document puts the saved asset back on the live mixer; **Reload** (only when the document has
  no unsaved change) re-reads the file and re-applies it when the document is live.
- **Project change**: when another project opens, every open mixer tab detaches from the live mixer; it becomes live
  again only if the new project's setting names its asset.
- **Structure**: buses can be added and custom buses without children, incoming sends or ducking role deleted; a deleted
  bus stays in the live mixer (greyed) until the next start, since the engine cannot remove a bus.

## Limits

- The faders allocate on each change (`MGSlider`), accepted until the MGUI fix.
- The target of an existing send cannot be changed in place: remove it (level 0) and add a new one.
- Numeric fields react to the mouse wheel (shared control).
- Closing a modified tab asks no confirmation (like the other document panels except UI screens); "save all" saves the
  modified mixers.
- A layout restored with a mixer tab whose panel is not open shows "Panel unavailable", like particle documents.
