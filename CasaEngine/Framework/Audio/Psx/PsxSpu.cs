namespace CasaEngine.Framework.Audio.Psx;

/// <summary>
/// Software PlayStation SPU core (tasks T4.1 and T4.2): 512 KB of SPU RAM, 24 ADPCM voices, pitch, pitch modulation,
/// fixed and sweeping volumes, ADSR envelopes (ENVX), the noise generator, key on/off and ENDX, rendered at 44100 Hz
/// into 16 bit stereo. Written from the psx-spx description of the SPU
/// (https://psx-spx.consoledev.net/ps1/spu/soundprocessingunitspu/ and, for the sample decoding formula the SPU shares
/// with CD-XA, https://psx-spx.consoledev.net/ps1/cdr/cdromformat/), decision P17: no hardware table is built in,
/// see <see cref="PsxSpuHardwareTables"/>.
///
/// The ADSR, the volume sweep and the noise generator are formula only (psx-spx sections "Volume and ADSR Generator",
/// "ADSR Register", "SPU Noise Generator"): no table is needed. The reverb unit (T4.3, see <c>PsxSpuReverb</c>) is
/// bypassed unless the tables hold the 39 FIR coefficients. Not yet implemented: hosting by the software mixer (T4.4).
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

    // ADSR phases.
    private const byte PhaseAttack = 0;
    private const byte PhaseDecay = 1;
    private const byte PhaseSustain = 2;
    private const byte PhaseRelease = 3;

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
    private readonly uint[] _adsr = new uint[VoiceCount]; // ADSR1 in the low 16 bits, ADSR2 in the high 16 bits
    private readonly byte[] _phase = new byte[VoiceCount];
    private readonly int[] _adsrCounter = new int[VoiceCount];
    private readonly int[] _outx = new int[VoiceCount]; // voice output after the envelope, before the volume
    private readonly int[] _old = new int[VoiceCount];
    private readonly int[] _older = new int[VoiceCount];
    private readonly short[] _decoded = new short[VoiceCount * BlockSamples];
    private readonly int[] _history = new int[VoiceCount * 4]; // oldest, older, old, newest

    // Volumes: [voice * 2 + side], side 0 = left, 1 = right.
    private readonly ushort[] _volumeRegister = new ushort[VoiceCount * 2];
    private readonly int[] _volumeLevel = new int[VoiceCount * 2];
    private readonly int[] _sweepCounter = new int[VoiceCount * 2];

    private readonly PsxSpuReverb _reverb;
    private uint _endx;
    private uint _noiseVoices;
    private uint _pitchModulation;

    // Noise generator state (psx-spx "SPU Noise Generator").
    private int _noiseShift;
    private int _noiseStep = 4;
    private int _noiseTimer;
    private int _noiseLevel;

    /// <param name="tables">Hardware tables supplied by the caller.</param>
    /// <param name="interpolator">
    /// Interpolation; when null, <see cref="PsxGaussianInterpolator"/> if <paramref name="tables"/> holds the Gaussian table,
    /// otherwise <see cref="PsxCubicInterpolator"/>.
    /// </param>
    public PsxSpu(PsxSpuHardwareTables tables, IPsxSpuInterpolator interpolator = null)
    {
        ArgumentNullException.ThrowIfNull(tables);
        _tables = tables;
        _reverb = new PsxSpuReverb(_ram, tables.ReverbFir);
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

    /// <summary>Copies SPU RAM at a byte address into <paramref name="destination"/>, wrapping like <see cref="WriteRam"/>.</summary>
    internal void ReadRam(int byteAddress, Span<byte> destination)
    {
        var address = byteAddress & RamMask;
        var done = 0;
        while (done < destination.Length)
        {
            var chunk = Math.Min(destination.Length - done, RamSize - address);
            _ram.AsSpan(address, chunk).CopyTo(destination.Slice(done));
            done += chunk;
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
    /// Writes the 32 bit ADSR register of a voice: the low half is ADSR1 (bit 15 attack mode, bits 14-10 attack shift,
    /// bits 9-8 attack step, bits 7-4 decay shift, bits 3-0 sustain level), the high half is ADSR2 (bit 31 sustain mode,
    /// bit 30 sustain direction, bits 28-24 sustain shift, bits 23-22 sustain step, bit 21 release mode, bits 20-16
    /// release shift). It is read on every envelope step, so a write also applies to a voice that is playing.
    /// </summary>
    public void SetAdsr(int voice, uint register)
    {
        CheckVoice(voice);
        _adsr[voice] = register;
    }

    /// <summary>
    /// Noise mode flags (NON): for the voices whose bit is set (bits 0-23), the noise level replaces the ADPCM sample;
    /// the ADPCM decoding, the address advance and the loop flags go on.
    /// </summary>
    public void SetNoiseMode(uint voiceMask) => _noiseVoices = voiceMask & 0xFFFFFF;

    /// <summary>
    /// Pitch modulation flags (PMON): the pitch of a voice whose bit is set is modulated by the output of the previous
    /// voice. Bit 0 is ignored (voice 0 is never modulated).
    /// </summary>
    public void SetPitchModulation(uint voiceMask) => _pitchModulation = voiceMask & 0xFFFFFE;

    /// <summary>
    /// Noise clock from the SPUCNT register: <paramref name="shift"/> is bits 13-10 (0..15, low to high frequency),
    /// <paramref name="step"/> is bits 9-8 (0..3, noise step 4..7).
    /// </summary>
    public void SetNoiseClock(int shift, int step)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shift);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(shift, 15);
        ArgumentOutOfRangeException.ThrowIfNegative(step);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(step, 3);
        _noiseShift = shift;
        _noiseStep = 4 + step;
    }

    /// <summary>
    /// Reverb mode flags (EON, 1F801D98h/1F801D9Ah): the voices whose bit is set (bits 0-23) are sent to the reverb in
    /// addition to the mixer.
    /// </summary>
    public void SetReverbVoices(uint voiceMask) => _reverb.VoiceMask = voiceMask & 0xFFFFFF;

    /// <summary>
    /// Reverb master enable (SPUCNT bit 7). When clear the reverb unit still reads its work area and outputs, but writes
    /// nothing to it (psx-spx "Reverb Bits in ATTR Register", "Reverb Disable"). Without FIR coefficients in the hardware
    /// tables the whole reverb unit is bypassed: no output and no work area write.
    /// </summary>
    public void SetReverbEnabled(bool enabled) => _reverb.Enabled = enabled;

    /// <summary>
    /// Reverb work area start (ESA, 1F801DA2h) in 8 byte units; the area ends at 7FFFEh. Writing it also sets the current
    /// buffer address to the start, like the hardware.
    /// </summary>
    public void SetReverbWorkAreaStart(ushort units) => _reverb.SetWorkAreaStart(units);

    /// <summary>Reverb output volume left and right (EVOLL/EVOLR, 1F801D84h/1F801D86h), signed 16 bit, fixed volume.</summary>
    public void SetReverbOutputVolume(short left, short right) => _reverb.SetOutputVolume(left, right);

    /// <summary>
    /// Writes a reverb preset register. <paramref name="index"/> is (address - 1F801DC0h) / 2, 0 to 31: 0 dAPF1,
    /// 1 dAPF2, 2 vIIR, 3-6 vCOMB1-4, 7 vWALL, 8 vAPF1, 9 vAPF2, 10 mLSAME, 11 mRSAME, 12 mLCOMB1, 13 mRCOMB1,
    /// 14 mLCOMB2, 15 mRCOMB2, 16 dLSAME, 17 dRSAME, 18 mLDIFF, 19 mRDIFF, 20 mLCOMB3, 21 mRCOMB3, 22 mLCOMB4,
    /// 23 mRCOMB4, 24 dLDIFF, 25 dRDIFF, 26 mLAPF1, 27 mRAPF1, 28 mLAPF2, 29 mRAPF2, 30 vLIN, 31 vRIN.
    /// </summary>
    public void SetReverbRegister(int index, ushort value) => _reverb.SetRegister(index, value);

    /// <summary>Current noise level of the generator, a signed 16 bit value.</summary>
    public int GetNoiseLevel() => (short)_noiseLevel;

    /// <summary>Current envelope level (ENVX) of a voice, 0..7FFFh.</summary>
    public int GetEnvx(int voice)
    {
        CheckVoice(voice);
        return _envx[voice];
    }

    /// <summary>ENDX: bit n set once voice n reached a loop end flag, cleared by the next key on of that voice.</summary>
    public uint ReadEndx() => _endx;

    /// <summary>
    /// Key on for the voices whose bit is set (bits 0-23): the current address takes the start address, ENDX is cleared,
    /// the decoder history and pitch counter are reset and the envelope restarts its attack from 0.
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
            // psx-spx: key on "automatically initializes ADSR Volume to zero".
            _envx[v] = 0;
            _phase[v] = PhaseAttack;
            // AMBIGUOUS (psx-spx): the envelope counter at key on is not specified; zero.
            _adsrCounter[v] = 0;
        }
    }

    /// <summary>
    /// Key off for the voices whose bit is set: the envelope enters its release phase from the current level.
    /// </summary>
    public void KeyOff(uint voiceMask)
    {
        for (var v = 0; v < VoiceCount; v++)
        {
            if ((voiceMask & (1u << v)) != 0)
            {
                _phase[v] = PhaseRelease;
            }
        }
    }

    /// <summary>
    /// Renders <paramref name="frames"/> frames at 44100 Hz into <paramref name="interleavedStereo"/> (left, right),
    /// overwriting it; voices are summed, the reverb output is added, and the sum is saturated to 16 bit. No allocation, no lock.
    /// </summary>
    public void Render(Span<short> interleavedStereo, int frames)
    {
        if (frames < 0 || interleavedStereo.Length < frames * 2)
        {
            throw new ArgumentException("The destination is smaller than the requested frame count.", nameof(frames));
        }

        for (var f = 0; f < frames; f++)
        {
            StepNoise();
            var left = 0;
            var right = 0;
            var reverbLeft = 0;
            var reverbRight = 0;
            var reverbMask = _reverb.VoiceMask;

            for (var v = 0; v < VoiceCount; v++)
            {
                if (!_on[v])
                {
                    continue;
                }

                var h = v * 4;
                int sample;
                if ((_noiseVoices & (1u << v)) != 0)
                {
                    sample = (short)_noiseLevel;
                }
                else
                {
                    sample = _interpolator.Interpolate(_history[h], _history[h + 1], _history[h + 2], _history[h + 3], (_counter[v] >> 4) & 0xFF);
                }

                // Lvol = (ENVX * VOLXL) >> 15 (psx-spx), the sample is then scaled by Lvol the same way.
                var env = _envx[v];
                // AMBIGUOUS (psx-spx): VxOUTX is "the output after ADSR"; taken as the sample scaled by ENVX, before the
                // voice volume, and the factor of PMON uses the value of the previous voice computed in this same frame.
                _outx[v] = (sample * env) >> 15;
                var lvol = (env * _volumeLevel[v * 2]) >> 15;
                var rvol = (env * _volumeLevel[v * 2 + 1]) >> 15;
                var voiceLeft = SaturateToShort((sample * lvol) >> 15);
                var voiceRight = SaturateToShort((sample * rvol) >> 15);
                left += voiceLeft;
                right += voiceRight;
                if ((reverbMask & (1u << v)) != 0)
                {
                    reverbLeft += voiceLeft;
                    reverbRight += voiceRight;
                }

                _counter[v] += ComputeStep(v);
                var advance = _counter[v] >> 12;
                _counter[v] &= 0xFFF;
                for (var k = 0; k < advance; k++)
                {
                    PushNextSample(v);
                }

                StepAdsr(v);
            }

            for (var s = 0; s < VoiceCount * 2; s++)
            {
                StepSweep(s);
            }

            // Order: dry voices summed, reverb output added, then one saturation to 16 bit.
            _reverb.Process(SaturateToShort(reverbLeft), SaturateToShort(reverbRight), out var wetLeft, out var wetRight);
            interleavedStereo[f * 2] = (short)SaturateToShort(left + wetLeft);
            interleavedStereo[f * 2 + 1] = (short)SaturateToShort(right + wetRight);
        }
    }

    // Pitch counter step of psx-spx "Pitch Counter": PMON modulates the step with the output of the previous voice, then
    // values above 3FFFh give 4000h.
    private int ComputeStep(int voice)
    {
        int step = _pitch[voice];
        if (voice > 0 && (_pitchModulation & (1u << voice)) != 0)
        {
            var factor = _outx[voice - 1] + 0x8000;
            step = (short)step; // SignExpand16to32
            step = (step * factor) >> 15; // SAR 15
            step &= 0xFFFF;
        }

        return step > 0x3FFF ? 0x4000 : step;
    }

    // Noise generator of psx-spx "SPU Noise Generator", once per output frame, before the voices.
    // AMBIGUOUS (psx-spx): the initial timer and noise level are not specified (both zero), and the three "IF Timer<0"
    // lines are run in the written order: the level update and the first reload see the same timer, the second reload
    // sees the timer after the first.
    private void StepNoise()
    {
        _noiseTimer -= _noiseStep;
        var parity = ((_noiseLevel >> 15) ^ (_noiseLevel >> 12) ^ (_noiseLevel >> 11) ^ (_noiseLevel >> 10) ^ 1) & 1;
        if (_noiseTimer < 0)
        {
            _noiseLevel = ((_noiseLevel << 1) + parity) & 0xFFFF;
        }

        if (_noiseTimer < 0)
        {
            _noiseTimer += 0x20000 >> _noiseShift;
        }

        if (_noiseTimer < 0)
        {
            _noiseTimer += 0x20000 >> _noiseShift;
        }
    }

    // One envelope step of a voice (psx-spx "ADSR Register" and "Envelope Operation"), once per output frame after the
    // voice output was produced. AMBIGUOUS (psx-spx): the tick order against the output is not specified; the output of a
    // frame uses the level before that frame's step. The phase switches are evaluated at the start of the step from the
    // current level: attack to decay once the level is 7FFFh, decay to sustain once the level is at or below
    // (N+1)*800h (a sustain level of Fh, 8000h, thus ends the decay at once).
    private void StepAdsr(int v)
    {
        var register = _adsr[v];
        var level = _envx[v];
        var phase = _phase[v];

        if (phase == PhaseAttack && level >= 0x7FFF)
        {
            phase = PhaseDecay;
        }

        if (phase == PhaseDecay && level <= (((int)register & 0xF) + 1) * 0x800)
        {
            phase = PhaseSustain;
        }

        _phase[v] = phase;

        // AMBIGUOUS (psx-spx): the counter carries over from one phase to the next (it is only reset at key on).
        switch (phase)
        {
            case PhaseAttack:
                level = StepEnvelope(level, ref _adsrCounter[v], (register & 0x8000) != 0, false, false, (int)(register >> 10) & 0x1F, (int)(register >> 8) & 3);
                break;
            case PhaseDecay:
                // Decay: fixed exponential decrease, step value 0 ("-8").
                level = StepEnvelope(level, ref _adsrCounter[v], true, true, false, (int)(register >> 4) & 0xF, 0);
                break;
            case PhaseSustain:
                level = StepEnvelope(level, ref _adsrCounter[v], (register & 0x80000000) != 0, (register & 0x40000000) != 0, false, (int)(register >> 24) & 0x1F, (int)(register >> 22) & 3);
                break;
            default:
                // Release: decrease, step value 0 ("-8"), mode and shift from the register.
                level = StepEnvelope(level, ref _adsrCounter[v], (register & 0x200000) != 0, true, false, (int)(register >> 16) & 0x1F, 0);
                break;
        }

        _envx[v] = level;
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
                // Code 1, End+Mute: jump to the loop address, ENDX, "Release, Env=0000h". The voice keeps playing the
                // loop silently, there is no way to stop the output (psx-spx).
                _phase[v] = PhaseRelease;
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

        _volumeLevel[slot] = StepEnvelope(
            _volumeLevel[slot],
            ref _sweepCounter[slot],
            (register & 0x4000) != 0,
            (register & 0x2000) != 0,
            (register & 0x1000) != 0,
            (register >> 2) & 0x1F,
            register & 3);
    }

    // The psx-spx "Envelope Operation depending on Shift/Step/Mode/Direction", shared by the volume sweep and the ADSR:
    // one step per output frame, the counter is carried by the caller; returns the new level.
    private static int StepEnvelope(int level, ref int counterRef, bool exponential, bool decreasing, bool phaseNegative, int shift, int stepValue)
    {
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

        var counter = counterRef + counterIncrement;
        if ((counter & 0x8000) == 0)
        {
            counterRef = counter;
            return level;
        }

        // AMBIGUOUS (psx-spx): the pseudo code does not say what happens to the counter once a step is taken; reset to 0.
        counterRef = 0;

        level += adsrStep;
        if (!decreasing)
        {
            return Math.Clamp(level, -0x8000, 0x7FFF);
        }

        return phaseNegative ? Math.Clamp(level, -0x8000, 0) : Math.Max(level, 0);
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
