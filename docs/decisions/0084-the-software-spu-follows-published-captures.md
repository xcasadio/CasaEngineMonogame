# ADR-0084: The software SPU follows published captures

- **Status**: Accepted
- **Date**: 2026-11-01
- **Source**: this chantier: `ai-agent/tasks/psx-spu-capture-conformance-tasks.md` (decisions D1 to D4, points P1 to P6, approved by the author on 2026-11-01), branch `chantier/psx-spu-capture-conformance`. Amends ADR-0058 and ADR-0061.

## Context

- ADR-0058 records that the tests of `PsxSpu` recompute the code's own reading of psx-spx, with no hardware reference. ADR-0061 planned to judge those readings by ear.
- The nugget project publishes SPU captures and ENVX tables under the MIT license, in `nugget/tests/spu`. Their comments describe them as taken from hardware.
- Compared with them, `PsxSpu` differs on the arithmetic of the ADPCM predictor, on shifts 13 to 15, on the start of a voice, on the rounding of the Gaussian interpolation and on the envelope. Only the Gaussian rounding can be replaced from outside the class.

## Decision

- **The reference is the nugget captures and ENVX tables, with psx-spx.** Each behaviour below is verified by a named capture or table. The captures did not derive every rule: other implementations were read, none of their code is copied, and the code is written anew.
- **The engine ships no capture.** Its tests use synthetic blocks with values worked out by hand. The comparison with the captures runs outside this repository.
- **The predictor** adds each term shifted right by 5, then shifts the sum right by 1.
- **Shifts 13 to 15** give -128 for a negative nibble and 0 otherwise. The captures only play the nibbles -8 and 7: the other nibbles are an extension decided by the author.
- **A voice starts with four samples already in its interpolation window:** key on decodes them.
- **The Gaussian interpolation** adds its four terms shifted right by 14, then shifts the sum right by 1.
- **An exponential decrease of the ADSR rounds toward minus infinity.**
- **The decay leaves for the sustain only after a step was applied,** when the level is strictly below `(N+1)*800h`.
- **The first envelope step comes one frame later.**
- **The cubic interpolation stays the default,** since the engine ships no Gaussian table (ADR-0062).

## Consequences

- The tests of the SPU now rest on measurements for these points. The other readings keep their `AMBIGUOUS (psx-spx)` mark.
- A voice with the ADSR word `1FC0000F` falls from 7FFFh to 3FFFh after its attack, which is 6 dB lower than before. This is what the captures show.
- Limits, all written in the code or the plan:
  - The decoder history stays at zero at key on. The start of `predictor_1` to `_4` contradicts it, but it depends on what played before, and replaying that does not reproduce it.
  - The volume sweeps keep the rounding toward zero: no capture covers them.
  - A voice muted by a block with the end flag stops 8 frames after `flag_end_mute`. One capture shows it.
  - Key on now decodes a block, so ENDX and the repeat address move earlier. No capture fixes that moment.
- Reserves on the reference:
  - The hardware origin of the captures rests on their comments.
  - `spu-adsr.c` and `spu.c` disagree on whether `adsr_decay_shift` and `adsr_sustain_down_linear` hold on hardware: `adsr_sustain_level`, an unconditional test, is the main proof of the envelope.
  - The first envelope step rests on the start of the captures: the tables do not show it.
  - The `adpcm_edge` golden files are only compared on an option of those tests.
  - The reverb has no capture.
