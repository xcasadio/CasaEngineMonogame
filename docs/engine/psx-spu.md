# PlayStation SPU (software)

A software PlayStation sound processing unit in the engine (`CasaEngine.Framework.Audio.Psx`, ADR-0058). A game
drives it with register writes from the game thread, as a PlayStation sound driver would, and hears it mixed with the
rest of the engine audio by the software backend (ADR-0055). It is written from the psx-spx description of the SPU
(<https://psx-spx.consoledev.net/ps1/spu/soundprocessingunitspu/>); no third-party code and no hardware table ship
with it.

## What it emulates

- 512 KB of SPU RAM (addresses masked), 24 voices, output at 44,100 Hz, 16-bit stereo.
- ADPCM decoding per 16-byte block: shift and filter from the block header, decoder history kept across blocks and
  loop jumps; loop start, loop end and repeat flags; ENDX.
- Pitch counter (1000h = 44,100 Hz, values above 3FFFh act as 4000h, 0 holds), pitch modulation (PMON) by the
  previous voice, noise generator and noise mode (NON).
- ADSR envelope (attack, decay, sustain, release; linear and exponential; ENVX), fixed and sweeping voice volumes
  (negative values invert the phase).
- Reverb unit driven by its registers (work area from ESA to 7FFFEh, comb, all-pass and reflection stages, input and
  output resampling), per-voice send (EON), master enable, output volume.
- Interpolation through `IPsxSpuInterpolator`: the engine's cubic by default, the Gaussian formula of psx-spx when a
  table is supplied.

Not emulated: CD audio and XA input, SPU interrupts, capture buffers, sound RAM reads by DMA, the volume sweep of the
reverb output, the libsnd sequencer (separate slice X3, paused).

## Hardware tables

`PsxSpuHardwareTables` holds the three constant tables of the chip: the 5 ADPCM filter coefficient pairs (required),
the 39 reverb FIR coefficients and the 512-entry Gaussian table (both optional). The engine ships none of them
(open question O12 of the audio plan): the caller supplies them, and they are validated and copied at construction.
Without FIR coefficients the reverb is bypassed (no output, no work-area write); without a Gaussian table the cubic
interpolation is used.

## Usage

```csharp
var tables = new PsxSpuHardwareTables(adpcmPositive, adpcmNegative, reverbFir, gaussian);
if (audioService.TryCreatePsxSpu(tables, AudioBusNames.Music, out var spu))
{
    spu.TryUpload(0x1000, soundBank);          // bytes into SPU RAM
    spu.TrySetStartAddress(0, 0x1000 / 8);     // voice registers use 8-byte units
    spu.TrySetPitch(0, 0x1000);
    spu.TrySetAdsr(0, adsrWord);               // ADSR1 in the low half, ADSR2 in the high half
    spu.TrySetVolume(0, right: false, 0x3FFF);
    spu.TrySetVolume(0, right: true, 0x3FFF);
    spu.TryKeyOn(1u << 0);
    // ...
    var envelope = spu.GetEnvx(0);             // as of the last audio block
    spu.Dispose();                             // detaches the SPU from the mixer
}
```

- `AudioService.TryCreatePsxSpu` works only on a backend that implements `IPsxSpuHost` (the software backend).
  Elsewhere (MonoGame backend, test fakes) it returns false and logs "SPU unavailable" once.
- One SPU is alive per backend, like the console; a second creation fails until the first is disposed.
- `PsxSpuPort` mirrors every setter of `PsxSpu` as a `Try*` method. Call it from the game thread only.
- `SetGain` sets the gain of the whole SPU; it is multiplied by the effective gain of the bus given at creation and
  re-applied when bus gains change. Until the bus graph of the audio plan's slice S4, the bus is not a real mix stage.
- `PsxSpu` itself can also be used directly, without the mixer (offline rendering, tests): it renders with
  `Render(Span<short>, frames)` and is not thread safe.

## Threading, bounds and order

- Register writes go into a preallocated ring of 16,384 entries; at least 4,096 writes per 10 ms block are accepted
  without loss. A full ring makes the call return false without waiting and increments `RefusedWriteCount`.
- `TryUpload` copies the data at once into a 1 MB staging buffer, in pieces of at most 64 KB. The audio thread
  applies at most 256 KB per block, so a 512 KB bank reaches SPU RAM over two blocks. An upload that does not fit is
  refused whole (false, counted).
- Order is kept: a register write made after an upload is never applied before that upload is complete in SPU RAM
  (each upload places a numbered marker in the register ring, and the audio thread stops at it until the upload is
  done).
- Writes take effect at the start of the next audio block (10 ms by default): register timing is quantised to a
  block. `ReadEndx` and `GetEnvx` return a snapshot the audio thread publishes once per block.
- Nothing allocates after creation, on either thread; the audio thread never waits or locks.
- The SPU output is resampled from 44,100 Hz to the output rate with the mixer's cubic interpolation and added before
  the final clip. `AudioService.StopAll` does not affect the SPU.

## Accuracy

psx-spx leaves several points open (rounding of the ADPCM division, timing of the envelope against the output,
noise generator start, reverb resampling layout and gain, and others). Each choice is marked
`AMBIGUOUS (psx-spx)` in the code and listed in open question O19 of `ai-agent/tasks/audio-modern-tasks.md`. The
tests recompute the psx-spx formulas with synthetic tables, so a misreading would be reproduced by them: the choices
are to be confirmed against hardware references. Known consequence: the reverb may come out about half as loud as on
the console (output FIR gain not compensated).

## Demo and tests

- The audio demo (`CasaEngine.Demos`, "Audio demo") starts an SPU with synthetic tables and a square wave on four
  voices when its stress mode runs (key G or `CASAENGINE_AUDIO_STRESS_SECONDS`), and shows its state and refused
  writes.
- Tests: `CasaEngine.Tests/Audio/Psx/` (decoding, pitch, loops, volumes, sweep, ADSR, noise, PMON, reverb, zero
  allocation) and `CasaEngine.Tests/Audio/Software/SoftwareAudioBackendPsxSpuTests.cs` (hosting, bounds, order,
  gains, detach).
