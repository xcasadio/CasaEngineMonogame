namespace CasaEngine.Framework.Audio.Psx;

/// <summary>
/// Software PlayStation SPU core (task T4.1): 512 KB of SPU RAM, 24 ADPCM voices, pitch, fixed and sweeping volumes,
/// key on/off and ENDX, rendered at 44100 Hz into 16 bit stereo. Written from the psx-spx description of the SPU
/// (https://psx-spx.consoledev.net/ps1/spu/soundprocessingunitspu/ and, for the sample decoding formula the SPU shares
/// with CD-XA, https://psx-spx.consoledev.net/ps1/cdr/cdromformat/), decision P17: no hardware table is built in,
/// see <see cref="PsxSpuHardwareTables"/>.
///
/// Not yet implemented, left as extension points: ADSR and ENVX (T4.2, see <c>PlaceholderEnvx</c>), noise and pitch
/// modulation (T4.2, see <c>ComputeStep</c>), reverb (T4.3), hosting by the software mixer (T4.4).
///
/// Addresses of the voice registers are in 8 byte units like the hardware registers; <see cref="WriteRam"/> takes a
/// byte address. Not thread safe: the caller serialises register writes and <see cref="Render"/> (the ring between the
/// game and audio threads is T4.4). <see cref="Render"/> neither allocates nor locks.
/// </summary>
public sealed class PsxSpu
{
    /// <summary>Output rate of the SPU, in Hz.</summary>
    public const int SampleRate = 44100;

    /// <summary>Number of voices.</summary>
    public const int VoiceCount = 24;

    /// <summary>Size of the SPU RAM, in bytes.</summary>
    public const int RamSize = 512 * 1024;

    private const int RamMask = RamSize - 1;
    private const int BlockSamples = 28;
    private const int AddressUnitsPerBlock = 2; // a 16 byte block is two 8 byte units
    private const int AddressUnitMask = 0xFFFF;

    // Flag bits of the second byte of an ADPCM block.
    private const int FlagLoopEnd = 1;
    private const int FlagLoopRepeat = 2;
    private const int FlagLoopStart = 4;

    // Placeholder envelope until the ADSR generator (T4.2): a key on jumps straight to the maximum level.
    private const int PlaceholderEnvx = 0x7FFF;

    private readonly PsxSpuHardwareTables _tables;
    private readonly IPsxSpuInterpolator _interpolator;
    private readonly byte[] _ram = new byte[RamSize];

    // Per voice state, struct of arrays so that nothing is allocated while rendering.
    private readonly ushort[] _startAddress = new ushort[VoiceCount];
    private readonly ushort[] _repeatAddress = new ushort[VoiceCount];
    private readonly ushort[] _pitch = new ushort[VoiceCount];
    private readonly ushort[] _currentAddress = new ushort[VoiceCount];
    private readonly bool[] _on = new bool[VoiceCount];
    private readonly bool[] _blockLoaded = new bool[VoiceCount];
    private readonly byte[] _blockFlags = new byte[VoiceCount];
    private readonly int[] _nextSampleIndex = new int[VoiceCount];
    private readonly int[] _counter = new int[VoiceCount];
    private readonly int[] _envx = new int[VoiceCount];
    private readonly int[] _old = new int[VoiceCount];
    private readonly int[] _older = new int[VoiceCount];
    private readonly short[] _decoded = new short[VoiceCount * BlockSamples];
    private readonly int[] _history = new int[VoiceCount * 4]; // oldest, older, old, newest

    // Volumes: [voice * 2 + side], side 0 = left, 1 = right.
    private readonly ushort[] _volumeRegister = new ushort[VoiceCount * 2];
    private readonly int[] _volumeLevel = new int[VoiceCount * 2];
    private readonly int[] _sweepCounter = new int[VoiceCount * 2];

    private uint _endx;

    /// <param name="tables">Hardware tables supplied by the caller.</param>
    /// <param name="interpolator">
    /// Interpolation; when null, <see cref="PsxGaussianInterpolator"/> if <paramref name="tables"/> holds the Gaussian table,
    /// otherwise <see cref="PsxCubicInterpolator"/>.
    /// </param>
    public PsxSpu(PsxSpuHardwareTables tables, IPsxSpuInterpolator? interpolator = null)
    {
        ArgumentNullException.ThrowIfNull(tables);
        _tables = tables;
        _interpolator = interpolator
                        ?? (tables.HasGaussianTable ? new PsxGaussianInterpolator(tables) : PsxCubicInterpolator.Instance);
    }

    /// <summary>The interpolation in use.</summary>
    public IPsxSpuInterpolator Interpolator => _interpolator;

    /// <summary>
    /// Copies data into the SPU RAM at a byte address; the address and the copy wrap around the 512 KB (addresses masked).
    /// </summary>
    public void WriteRam(int byteAddress, ReadOnlySpan<byte> data)
    {
        var address = byteAddress & RamMask;
        while (!data.IsEmpty)
        {
            var chunk = Math.Min(data.Length, RamSize - address);
            data[..chunk].CopyTo(_ram.AsSpan(address));
            data = data[chunk..];
            address = 0;
        }
    }

    /// <summary>Sample start address (SSA) in 8 byte units; takes effect at the next key on.</summary>
    public void SetStartAddress(int voice, ushort units)
    {
        CheckVoice(voice);
        _startAddress[voice] = units;
    }

    /// <summary>Repeat address (LSAX) in 8 byte units; the hardware also rewrites it on a loop start flag.</summary>
    public void SetRepeatAddress(int voice, ushort units)
    {
        CheckVoice(voice);
        _repeatAddress[voice] = units;
    }

    /// <summary>Current repeat address (LSAX) in 8 byte units.</summary>
    public ushort GetRepeatAddress(int voice)
    {
        CheckVoice(voice);
        return _repeatAddress[voice];
    }

    /// <summary>Address of the ADPCM block being played, in 8 byte units.</summary>
    public ushort GetCurrentAddress(int voice)
    {
        CheckVoice(voice);
        return _currentAddress[voice];
    }

    /// <summary>Pitch register: 1000h is 44100 Hz, 0 stops the advance, values above 4000h act as 4000h.</summary>
    public void SetPitch(int voice, ushort pitch)
    {
        CheckVoice(voice);
        _pitch[voice] = pitch;
    }

    /// <summary>
    /// Writes a volume register. Bit 15 clear: fixed volume, bits 0-14 hold volume/2 as a signed 15 bit value
    /// (a negative value inverts the phase). Bit 15 set: sweep from the current level (bit 14 exponential, bit 13
    /// decrease, bit 12 negative phase, bits 2-6 shift, bits 0-1 step); the sweep advances once per output frame.
    /// </summary>
    public void SetVolume(int voice, bool right, ushort register)
    {
        CheckVoice(voice);
        var slot = voice * 2 + (right ? 1 : 0);
        _volumeRegister[slot] = register;
        if ((register & 0x8000) == 0)
        {
            var half = (register & 0x7FFF) << 17 >> 17; // sign extend 15 bits
            _volumeLevel[slot] = half * 2;
        }
    }

    /// <summary>Current volume level of a voice side (VOLXL / VOLXR), -8000h..+7FFFh.</summary>
    public int GetCurrentVolume(int voice, bool right)
    {
        CheckVoice(voice);
        return _volumeLevel[voice * 2 + (right ? 1 : 0)];
    }

    /// <summary>
    /// Current envelope level (ENVX) of a voice. Placeholder until T4.2: 7FFFh while the voice is on, 0 after a key
    /// off or a loop end without repeat flag.
    /// </summary>
    public int GetEnvx(int voice)
    {
        CheckVoice(voice);
        return _envx[voice];
    }

    /// <summary>ENDX: bit n set once voice n reached a loop end flag, cleared by the next key on of that voice.</summary>
    public uint ReadEndx() => _endx;

    /// <summary>
    /// Key on for the voices whose bit is set (bits 0-23): the current address takes the start address, ENDX is cleared,
    /// the decoder history and pitch counter are reset and the envelope goes to the placeholder level (T4.2: attack from 0).
    /// </summary>
    public void KeyOn(uint voiceMask)
    {
        for (var v = 0; v < VoiceCount; v++)
        {
            if ((voiceMask & (1u << v)) == 0)
            {
                continue;
            }

            _currentAddress[v] = _startAddress[v];
            _on[v] = true;
            _blockLoaded[v] = false;
            _nextSampleIndex[v] = BlockSamples;
            _counter[v] = 0;
            _endx &= ~(1u << v);
            // AMBIGUOUS (psx-spx): the initial decoder history and interpolation history at key on are not specified; zero.
            _old[v] = 0;
            _older[v] = 0;
            _history[v * 4] = _history[v * 4 + 1] = _history[v * 4 + 2] = _history[v * 4 + 3] = 0;
            _envx[v] = PlaceholderEnvx;
        }
    }

    /// <summary>
    /// Key off for the voices whose bit is set. Placeholder until T4.2 (release phase): the envelope drops to 0
    /// immediately; the voice keeps advancing silently.
    /// </summary>
    public void KeyOff(uint voiceMask)
    {
        for (var v = 0; v < VoiceCount; v++)
        {
            if ((voiceMask & (1u << v)) != 0)
            {
                _envx[v] = 0;
            }
        }
    }

    /// <summary>
    /// Renders <paramref name="frames"/> frames at 44100 Hz into <paramref name="interleavedStereo"/> (left, right),
    /// overwriting it; voices are summed and saturated to 16 bit. No allocation, no lock.
    /// </summary>
    public void Render(Span<short> interleavedStereo, int frames)
    {
        if (frames < 0 || interleavedStereo.Length < frames * 2)
        {
            throw new ArgumentException("The destination is smaller than the requested frame count.", nameof(frames));
        }

        for (var f = 0; f < frames; f++)
        {
            var left = 0;
            var right = 0;

            for (var v = 0; v < VoiceCount; v++)
            {
                if (!_on[v])
                {
                    continue;
                }

                var h = v * 4;
                var sample = _interpolator.Interpolate(_history[h], _history[h + 1], _history[h + 2], _history[h + 3], (_counter[v] >> 4) & 0xFF);

                // Lvol = (ENVX * VOLXL) >> 15 (psx-spx), the sample is then scaled by Lvol the same way.
                var env = _envx[v];
                var lvol = (env * _volumeLevel[v * 2]) >> 15;
                var rvol = (env * _volumeLevel[v * 2 + 1]) >> 15;
                left += SaturateToShort((sample * lvol) >> 15);
                right += SaturateToShort((sample * rvol) >> 15);

                _counter[v] += ComputeStep(v);
                var advance = _counter[v] >> 12;
                _counter[v] &= 0xFFF;
                for (var k = 0; k < advance; k++)
                {
                    PushNextSample(v);
                }
            }

            for (var s = 0; s < VoiceCount * 2; s++)
            {
                StepSweep(s);
            }

            interleavedStereo[f * 2] = (short)SaturateToShort(left);
            interleavedStereo[f * 2 + 1] = (short)SaturateToShort(right);
        }
    }

    // Pitch counter step of psx-spx: values above 3FFFh give 4000h. Extension point: PMON (T4.2) modifies the step first.
    private int ComputeStep(int voice)
    {
        int step = _pitch[voice];
        return step > 0x3FFF ? 0x4000 : step;
    }

    private void PushNextSample(int v)
    {
        if (_nextSampleIndex[v] >= BlockSamples)
        {
            if (_blockLoaded[v])
            {
                EndBlock(v);
            }

            DecodeBlock(v);
            _nextSampleIndex[v] = 0;
        }

        var h = v * 4;
        _history[h] = _history[h + 1];
        _history[h + 1] = _history[h + 2];
        _history[h + 2] = _history[h + 3];
        _history[h + 3] = _decoded[v * BlockSamples + _nextSampleIndex[v]++];
    }

    // Decodes the block at the current address. Header flags: loop start copies the current address into the repeat
    // address when the block is reached; loop end sets ENDX when the block is reached (AMBIGUOUS, psx-spx says "when
    // reaching a LOOP-END flag in ADPCM header"), the jump itself happens after the block, see EndBlock.
    private void DecodeBlock(int v)
    {
        var address = (_currentAddress[v] * 8) & RamMask;
        int header = _ram[address];
        var flags = _ram[address + 1] & 7;
        _blockFlags[v] = (byte)flags;
        _blockLoaded[v] = true;

        if ((flags & FlagLoopStart) != 0)
        {
            _repeatAddress[v] = _currentAddress[v];
        }

        if ((flags & FlagLoopEnd) != 0)
        {
            _endx |= 1u << v;
        }

        var shiftField = header & 0xF;
        // AMBIGUOUS (psx-spx): shift values 13..15 are documented for XA ("same as 9") and the SPU decoder is said to be
        // the same algorithm; applied here as 9.
        if (shiftField > 12)
        {
            shiftField = 9;
        }

        var shift = 12 - shiftField;
        // AMBIGUOUS (psx-spx): the SPU header holds a filter index 0..4 (3 bits, bits 4-6); indices 5..7 are not
        // described, treated as filter 0 (no prediction).
        var filter = (header >> 4) & 7;
        if (filter >= PsxSpuHardwareTables.AdpcmFilterCount)
        {
            filter = 0;
        }

        var f0 = _tables.AdpcmPositive(filter);
        var f1 = _tables.AdpcmNegative(filter);
        var old = _old[v];
        var older = _older[v];
        var baseIndex = v * BlockSamples;

        for (var j = 0; j < BlockSamples; j++)
        {
            int packed = _ram[(address + 2 + (j >> 1)) & RamMask];
            var nibble = (j & 1) == 0 ? packed & 0xF : packed >> 4; // low nibble is the 1st sample
            var t = (nibble << 28) >> 28; // signed 4 bit

            // s = (t << shift) + ((old*f0 + older*f1 + 32) / 64), clamped to 16 bit.
            // AMBIGUOUS (psx-spx): written "/64" in the CD-XA pseudo code; read literally as an integer division
            // truncating toward zero, not an arithmetic shift.
            var s = (t << shift) + ((old * f0 + older * f1 + 32) / 64);
            s = Math.Clamp(s, short.MinValue, short.MaxValue);
            _decoded[baseIndex + j] = (short)s;
            older = old;
            old = s;
        }

        // The history is kept for the next block, including across a loop jump.
        _old[v] = old;
        _older[v] = older;
    }

    // After the 28 samples of a block: apply the loop end jump.
    private void EndBlock(int v)
    {
        var flags = _blockFlags[v];
        if ((flags & FlagLoopEnd) != 0)
        {
            _currentAddress[v] = _repeatAddress[v];
            if ((flags & FlagLoopRepeat) == 0)
            {
                // Code 1, End+Mute: jump to the loop address, ENDX, release, envelope 0. The voice keeps playing the
                // loop silently, there is no way to stop the output (psx-spx).
                _envx[v] = 0;
            }
        }
        else
        {
            _currentAddress[v] = (ushort)((_currentAddress[v] + AddressUnitsPerBlock) & AddressUnitMask);
        }
    }

    // One step of a volume sweep, from the psx-spx "Envelope Operation depending on Shift/Step/Mode/Direction".
    private void StepSweep(int slot)
    {
        int register = _volumeRegister[slot];
        if ((register & 0x8000) == 0)
        {
            return;
        }

        var exponential = (register & 0x4000) != 0;
        var decreasing = (register & 0x2000) != 0;
        var phaseNegative = (register & 0x1000) != 0;
        var shift = (register >> 2) & 0x1F;
        var stepValue = register & 3;
        var level = _volumeLevel[slot];

        var adsrStep = 7 - stepValue;
        if (decreasing ^ phaseNegative)
        {
            adsrStep = ~adsrStep; // +7,+6,+5,+4 become -8,-7,-6,-5
        }

        adsrStep <<= Math.Max(0, 11 - shift);
        var counterIncrement = 0x8000 >> Math.Max(0, shift - 11);

        if (exponential && !decreasing && level > 0x6000)
        {
            if (shift < 10)
            {
                adsrStep /= 4;
            }
            else if (shift >= 11)
            {
                counterIncrement /= 4;
            }
            else
            {
                adsrStep /= 2;
                counterIncrement /= 2;
            }
        }
        else if (exponential && decreasing)
        {
            // AMBIGUOUS (psx-spx): "/" on a possibly negative product, read as truncation toward zero.
            adsrStep = adsrStep * level / 0x8000;
        }

        if ((stepValue | (shift << 2)) != 0x7F)
        {
            counterIncrement = Math.Max(counterIncrement, 1);
        }

        var counter = _sweepCounter[slot] + counterIncrement;
        if ((counter & 0x8000) == 0)
        {
            _sweepCounter[slot] = counter;
            return;
        }

        // AMBIGUOUS (psx-spx): the pseudo code does not say what happens to the counter once a step is taken; reset to 0.
        _sweepCounter[slot] = 0;

        level += adsrStep;
        if (!decreasing)
        {
            level = Math.Clamp(level, -0x8000, 0x7FFF);
        }
        else if (phaseNegative)
        {
            level = Math.Clamp(level, -0x8000, 0);
        }
        else
        {
            level = Math.Max(level, 0);
        }

        _volumeLevel[slot] = level;
    }

    private static int SaturateToShort(int value) => value > short.MaxValue ? short.MaxValue : value < short.MinValue ? short.MinValue : value;

    private static void CheckVoice(int voice)
    {
        if ((uint)voice >= VoiceCount)
        {
            throw new ArgumentOutOfRangeException(nameof(voice));
        }
    }
}
