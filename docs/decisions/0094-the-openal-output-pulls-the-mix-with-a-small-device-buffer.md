# ADR-0094: The OpenAL output lets OpenAL Soft pull the mix, with a small device buffer

- **Status**: Accepted
- **Date**: 2026-11-17
- **Source**: this chantier: `ai-agent/tasks/low-latency-audio-output-tasks.md` (decision D1 given by the author on 2026-11-17, points P1 to P9 approved with the plan on 2026-11-17), branch `chantier/low-latency-audio-output`. Replaces, for the OpenAL output, the 40 ms lead of ADR-0055.

## Context

- With the software backend, a UI sound is heard about 100 ms after the gesture that starts it, after the focus marker has already moved.
- The software mixer keeps 4 buffers of 10 ms queued on the OpenAL source (`OpenAlAudioOutput.cs:20`, `:230`). A new voice is picked up at the next `SoftwareMixer.Render`, so behind up to 40 ms of audio already queued (`SoftwareMixer.cs:1060-1083`). ADR-0055 already named this lead plus the device buffering as the latency floor.
- OpenAL Soft 1.24.3, the library MonoGame ships (`OpenAlNative.cs:7`), read at tag `1.24.3` (https://github.com/kcat/openal-soft/tree/1.24.3):
  - its default device buffer is 3 periods of 960 frames, 60 ms at 48 kHz (`alc/alc.cpp:1111-1133`, `core/device.h:45-46`);
  - only a configuration file changes it: `ALC_REFRESH` is not read when a context is created (`alc/alc.cpp:1259-1329`), and no attribute sets the period. On Windows the files are `%AppData%\alsoft.ini`, then `alsoft.ini` in the executable's folder, then the file named by `ALSOFT_CONF`, each overriding the keys of the one before (`alc/alconfig.cpp:333-378`);
  - in WASAPI shared mode, `period_size × periods` is only the requested duration; Windows grants the buffer (`alc/backends/wasapi.cpp:2044`, `2267-2279`, `2315-2318`);
  - `AL_SOFT_callback_buffer` lets the mixing thread ask the application for samples when it needs them (`include/AL/alext.h:583-591`, `core/voice.cpp:916-945`);
  - on a device loss, the WASAPI thread leaves its mixing loop (`wasapi.cpp:1306-1312`, `1358-1364`): no callback arrives any more.
- Off-repository probes on the author's PC, through the same library, at 48 kHz: 60 ms of device latency by default; 22 ms with an `alsoft.ini` beside the executable (`period_size = 480`, `periods = 2`; requests of 240 × 2 and 128 × 2 give the same 22 ms); with a callback buffer, about 480 frames per call every 10 ms, and a longest interval of 15.5 ms over 30 s under garbage collector stress.
- The engine does not enable `AllowUnsafeBlocks`, and the native bindings of this subsystem are written without `unsafe` code (`ai-agent/tasks/audio-modern-tasks.md:900-916`).

## Decision

- **Device buffer.** The engine ships an `alsoft.ini` beside each of its executables, with `period_size = 480` and `periods = 2` under `[general]`. It overrides these two keys of a user's `%AppData%\alsoft.ini`; `ALSOFT_CONF` is still read after it. It also applies to the MonoGame backend, which loads the same library.
- **No lead.** When `AL_SOFT_callback_buffer` is present, the OpenAL output sets one callback buffer on its source and no longer queues buffers. The binding has no `unsafe` code: marshalled delegates, created once and kept in fields. The callback renders the request in blocks of `BufferFrames` frames into an array allocated at open, and copies each block into OpenAL Soft's memory with `Marshal.Copy`. It never throws, allocates, blocks or calls AL. Only one render runs at a time; a callback that finds it taken returns silence. Without the extension, the output keeps the queue of ADR-0055 and logs a warning.
- **Underrun estimate.** In callback mode an underrun is counted when two calls are further apart than a threshold set after a 500 ms window: the largest of the largest `ALC_DEVICE_LATENCY_SOFT` reading and twice the duration of the largest request seen in the window. Nothing is counted during the window. At its end, the output logs its mode, rate, device latency, lead and threshold.
- **Device loss.** In callback mode the audio thread reads `ALC_CONNECTED` on each pass. Once the device is lost, it logs one line and calls the mixer render itself at the device rate into a discarded buffer, as the queue mode does: game commands are drained, voices advance and end, and the output stays available. There is no reopening.
- **Stop order.** Stop the source, delete the source and the buffer, then the context and the device; the callback delegate stays referenced until after `alcCloseDevice`.

## Consequences

- The delay between a sound request and its output falls from about 100 ms to the device buffer Windows grants plus the wait for the next callback (about 22 to 32 ms on the author's PC). The latency of Windows and the hardware after OpenAL Soft is not measured.
- `LeadMilliseconds` is 0 in callback mode. `UnderrunCount` is an estimate there, not a count reported by the device.
- A garbage collection pause now has to fit in the device buffer: the GC stress mode of the audio demo is the check (0 underrun over 60 s).
- A user who wants another device buffer sets it in a file named by `ALSOFT_CONF`.
- Not done: an output of the engine's own on WASAPI (`IAudioClient3`, short shared periods, which OpenAL Soft 1.24.3 does not request), the exclusive mode, a project setting for the latency, and reopening a lost device.
