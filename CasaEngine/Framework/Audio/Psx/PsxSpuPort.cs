using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Software;

namespace CasaEngine.Framework.Audio.Psx;

/// <summary>
/// Game thread access to the <see cref="PsxSpu"/> hosted by the software audio backend (see
/// <see cref="IPsxSpuHost"/>). Every <c>Try*</c> method mirrors the setter of <see cref="PsxSpu"/> with the same
/// parameters and returns false, without waiting, when the register ring is full (16384 entries; at least
/// 4096 writes per 10 ms block are accepted); the refused call is counted in <see cref="RefusedWriteCount"/>.
/// The writes reach the SPU in call order at the start of the next audio blocks.
/// </summary>
/// <remarks>
/// Game thread only (a single producer). Nothing allocates after creation. An invalid voice, noise clock or
/// reverb register index throws like <see cref="PsxSpu"/> does, on the calling thread. After
/// <see cref="Dispose"/> every write returns false without being counted.
/// </remarks>
public sealed class PsxSpuPort : IDisposable
{
    private readonly PsxSpuSource _source;
    private readonly SoftwareAudioBackend _backend;
    private float _gain = 1f;
    private float _busGain = 1f;

    internal PsxSpuPort(PsxSpuSource source, SoftwareAudioBackend backend)
    {
        _source = source;
        _backend = backend;
    }

    /// <summary>The SPU behind the port, for tests (read it only while the audio thread is idle).</summary>
    internal PsxSpuSource Source => _source;

    /// <summary>True once <see cref="Dispose"/> was called.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>Writes and uploads refused because a ring or the staging buffer was full.</summary>
    public int RefusedWriteCount => _source.RefusedWriteCount;

    /// <summary>Gain of the whole SPU output the game set, 0..1.</summary>
    public float Gain => _gain;

    /// <summary>Sets the gain of the whole SPU output (a last value, never lost); the bus gain applies on top of it.</summary>
    public void SetGain(float gain)
    {
        _gain = Math.Clamp(float.IsNaN(gain) ? 1f : gain, AudioVoiceParameters.MinVolume, AudioVoiceParameters.MaxVolume);
        PublishGain();
    }

    /// <summary>Sets the effective gain of the bus the SPU is routed to (set by <see cref="AudioService"/>).</summary>
    internal void SetBusGain(float busGain)
    {
        _busGain = busGain;
        PublishGain();
    }

    /// <summary>Sample start address (SSA) in 8 byte units; see <see cref="PsxSpu.SetStartAddress"/>.</summary>
    public bool TrySetStartAddress(int voice, ushort units) => Write(PsxSpuWriteKind.StartAddress, voice, units);

    /// <summary>Repeat address (LSA) in 8 byte units; see <see cref="PsxSpu.SetRepeatAddress"/>.</summary>
    public bool TrySetRepeatAddress(int voice, ushort units) => Write(PsxSpuWriteKind.RepeatAddress, voice, units);

    /// <summary>Pitch register; see <see cref="PsxSpu.SetPitch"/>.</summary>
    public bool TrySetPitch(int voice, ushort pitch) => Write(PsxSpuWriteKind.Pitch, voice, pitch);

    /// <summary>Volume register of a voice side; see <see cref="PsxSpu.SetVolume"/>.</summary>
    public bool TrySetVolume(int voice, bool right, ushort register) => Write(PsxSpuWriteKind.Volume, voice, register, right ? 1 : 0);

    /// <summary>32 bit ADSR register; see <see cref="PsxSpu.SetAdsr"/>.</summary>
    public bool TrySetAdsr(int voice, uint register) => Write(PsxSpuWriteKind.Adsr, voice, register);

    /// <summary>Noise mode flags (NON); see <see cref="PsxSpu.SetNoiseMode"/>.</summary>
    public bool TrySetNoiseMode(uint voiceMask) => Write(PsxSpuWriteKind.NoiseMode, 0, voiceMask);

    /// <summary>Pitch modulation flags (PMON); see <see cref="PsxSpu.SetPitchModulation"/>.</summary>
    public bool TrySetPitchModulation(uint voiceMask) => Write(PsxSpuWriteKind.PitchModulation, 0, voiceMask);

    /// <summary>Noise clock; see <see cref="PsxSpu.SetNoiseClock"/>.</summary>
    public bool TrySetNoiseClock(int shift, int step)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shift);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(shift, 15);
        ArgumentOutOfRangeException.ThrowIfNegative(step);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(step, 3);
        return Write(PsxSpuWriteKind.NoiseClock, shift, 0, step, checkVoice: false);
    }

    /// <summary>Reverb voice flags (EON); see <see cref="PsxSpu.SetReverbVoices"/>.</summary>
    public bool TrySetReverbVoices(uint voiceMask) => Write(PsxSpuWriteKind.ReverbVoices, 0, voiceMask);

    /// <summary>Reverb enable (SPUCNT bit 7); see <see cref="PsxSpu.SetReverbEnabled"/>.</summary>
    public bool TrySetReverbEnabled(bool enabled) => Write(PsxSpuWriteKind.ReverbEnabled, 0, enabled ? 1u : 0u);

    /// <summary>Reverb work area start (ESA); see <see cref="PsxSpu.SetReverbWorkAreaStart"/>.</summary>
    public bool TrySetReverbWorkAreaStart(ushort units) => Write(PsxSpuWriteKind.ReverbWorkAreaStart, 0, units);

    /// <summary>Reverb output volume; see <see cref="PsxSpu.SetReverbOutputVolume"/>.</summary>
    public bool TrySetReverbOutputVolume(short left, short right)
    {
        return Write(PsxSpuWriteKind.ReverbOutputVolume, left, 0, right, checkVoice: false);
    }

    /// <summary>Reverb register by index (0..31); see <see cref="PsxSpu.SetReverbRegister"/>.</summary>
    public bool TrySetReverbRegister(int index, ushort value)
    {
        if ((uint)index >= PsxSpuReverb.RegisterCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return Write(PsxSpuWriteKind.ReverbRegister, index, value, checkVoice: false);
    }

    /// <summary>Key on for the voices whose bit is set; see <see cref="PsxSpu.KeyOn"/>.</summary>
    public bool TryKeyOn(uint voiceMask) => Write(PsxSpuWriteKind.KeyOn, 0, voiceMask);

    /// <summary>Key off for the voices whose bit is set; see <see cref="PsxSpu.KeyOff"/>.</summary>
    public bool TryKeyOff(uint voiceMask) => Write(PsxSpuWriteKind.KeyOff, 0, voiceMask);

    /// <summary>
    /// Copies <paramref name="data"/> into the SPU RAM at <paramref name="byteAddress"/> (wrapping like
    /// <see cref="PsxSpu.WriteRam"/>). The data is copied at once into a preallocated staging buffer (1 MB)
    /// and reaches the SPU RAM over the next blocks (at most 256 KB per block, so a 512 KB bank takes two);
    /// a register write made after this call is never applied before the upload is complete. Returns false,
    /// counted in <see cref="RefusedWriteCount"/>, when the staging buffer or a ring has no room for all of it.
    /// </summary>
    public bool TryUpload(int byteAddress, ReadOnlySpan<byte> data)
    {
        return !IsDisposed && _source.TryUpload(byteAddress, data);
    }

    /// <summary>ENDX as of the last audio block (bit n set once voice n reached a loop end flag).</summary>
    public uint ReadEndx() => _source.ReadEndx();

    /// <summary>ENVX (envelope level, 0..7FFFh) of a voice as of the last audio block.</summary>
    public int GetEnvx(int voice)
    {
        CheckVoice(voice);
        return _source.GetEnvx(voice);
    }

    /// <summary>Detaches the SPU from the mixer (the order is retried until the mixer accepted it) and stops all writes.</summary>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        _backend.ReleasePsxSpu(_source);
    }

    private void PublishGain()
    {
        _source.PublishGain(_gain * _busGain);
    }

    private bool Write(PsxSpuWriteKind kind, int voice, uint value, int second = 0, bool checkVoice = true)
    {
        if (checkVoice)
        {
            CheckVoice(voice);
        }

        return !IsDisposed && _source.TryWrite(kind, voice, value, second);
    }

    private static void CheckVoice(int voice)
    {
        if ((uint)voice >= PsxSpu.VoiceCount)
        {
            throw new ArgumentOutOfRangeException(nameof(voice));
        }
    }
}
