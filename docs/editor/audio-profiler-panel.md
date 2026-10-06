# Audio profiler panel

A read-only "Audio" tool panel that shows the audio backend's statistics and one level meter per mixing bus, while
editing and in play-in-editor. Decision: [ADR-0060](../decisions/0060-lock-free-bus-metering-and-a-read-only-audio-profiler-panel.md).
Code: `CasaEngine.Editor/Controls/AudioProfilerPanel.cs`, `AudioMeterControl.cs`, `AudioProfilerModel.cs`.

## Opening it

**Windows > Audio** docks the panel as a tab in the group of the Content Browser and the Logs, or brings its tab to the
front when it is already docked or floating. The default layout does not contain it, so a layout saved without it opens
without it.

## What it shows

- The backend in use (and whether its output device is running), active and refused voices, whether the backend can
  host a PlayStation SPU ([psx-spu.md](../engine/psx-spu.md)).
- Under the software backend: voice capacity, output rate, lead, underruns, dropped streaming chunks and events.
- One meter per bus of the mixer, indented by depth, then the output:
  - peak bar (green, yellow above −12 dBFS, red above −3 dBFS, on a −60..0 dBFS scale), RMS bar and a peak-hold marker;
  - peak, hold and RMS in dBFS ("-inf" below −90 dBFS);
  - for the output, the number of samples that went beyond full scale before the Master limiter (the "overs"), with a
    lamp that stays lit for the hold time.
- A bus the backend cannot hold (beyond its 32 buses) shows "n/a".
- On a backend that does not measure levels (MonoGame backend): "Metering unavailable" and the common statistics only.

The panel changes nothing in the mix: it does not edit volumes, mutes or effects.

## Behaviour

- Levels are read on every editor update while the panel is docked, so every audio block is seen (none is skipped
  between two reads, ADR-0060); reading allocates nothing.
- Peak hold lasts 1.5 s, then the marker falls at 20 dB/s; the bars rise at once and fall at 20 dB/s. These are panel
  choices, not a metering standard.
- Texts are rebuilt at most four times per second, and only when a displayed value changes by at least 0.1 dB.

## Limits

- Meters exist only under the software backend.
- The SPU line says whether the backend can host an SPU, not the state of a running one.
- An editable mixer panel, solo, a mixer asset and waveforms are not part of this panel (open question O24 of the
  audio plan).
