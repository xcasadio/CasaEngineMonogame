using CasaEngine.Framework.Audio.Psx;

namespace CasaEngine.Framework.Audio.Software;

internal enum PsxSpuWriteKind
{
    None = 0,
    StartAddress,
    RepeatAddress,
    Pitch,
    Volume,
    Adsr,
    NoiseMode,
    PitchModulation,
    NoiseClock,
    ReverbVoices,
    ReverbEnabled,
    ReverbWorkAreaStart,
    ReverbOutputVolume,
    ReverbRegister,
    KeyOn,
    KeyOff,

    /// <summary>The upload whose number is in <see cref="PsxSpuWrite.Value"/> was submitted at this point of the order.</summary>
    UploadMarker,
}

/// <summary>One SPU register write, a plain value type (no allocation to enqueue one).</summary>
internal struct PsxSpuWrite
{
    public PsxSpuWriteKind Kind;
    public int Voice;
    public uint Value;
    public int Second;
}

/// <summary>A piece of an upload staged for the audio thread (at most <see cref="PsxSpuSource.MaxUploadPieceBytes"/>).</summary>
internal struct PsxSpuUploadPiece
{
    public int RamAddress;
    public int StagingPosition;
    public int Length;
    public int UploadNumber;
    public bool IsLast;
}

/// <summary>
/// One <see cref="PsxSpu"/> hosted by the software mixer (plan T4.4). The game thread (single producer)
/// feeds a register ring and an upload staging buffer; the audio thread (single consumer, called by
/// <see cref="SoftwareMixer"/>) applies them in order at the start of every block, renders the SPU at
/// 44100 Hz, resamples it to the output rate and adds it to the mix; then it publishes a snapshot of
/// ENDX and ENVX that the game thread reads without any lock.
/// </summary>
/// <remarks>
/// <para>
/// Order: each upload puts a marker carrying its number in the register ring. The audio thread applies the
/// ring in order and stops at a marker until that upload is entirely in SPU RAM, so a register write is never
/// applied before an upload submitted before it. Uploads are staged in pieces of at most 64 KB and applied
/// at the start of the following blocks, 256 KB per block at most.
/// </para>
/// <para>
/// Nothing allocates or waits after construction; everything is preallocated here, on the game thread.
/// </para>
/// </remarks>
internal sealed class PsxSpuSource
{
    public const int RegisterRingCapacity = 16384;
    public const int StagingCapacity = 1024 * 1024;
    public const int MaxUploadPieceBytes = 64 * 1024;
    public const int MaxUploadBytesPerBlock = 256 * 1024;
    public const int PieceRingCapacity = 4096;

    private const int MaxSpuFramesPerSlice = 4096;
    private const int MaxOutputFramesPerSlice = 512;
    private const float InverseShortRange = 1f / 32768f;

    private readonly PsxSpu _spu;
    private readonly SpscRingBuffer<PsxSpuWrite> _registers = new(RegisterRingCapacity);
    private readonly SpscRingBuffer<PsxSpuUploadPiece> _pieces = new(PieceRingCapacity);
    private readonly byte[] _staging = new byte[StagingCapacity];
    private readonly short[] _spuFrames = new short[MaxSpuFramesPerSlice * 2];

    // Staging byte positions grow without bound (wrapping int arithmetic); the offset is position & mask.
    private int _stagingTail;
    private int _stagingHead;

    // Producer side.
    private int _lastUploadNumber;
    private int _refusedWriteCount;
    private int _cachedEndx;
    private readonly int[] _cachedEnvx = new int[PsxSpu.VoiceCount];

    // Audio side: ordering state.
    private PsxSpuUploadPiece _heldPiece;
    private bool _hasHeldPiece;
    private int _completedUploads;
    private int _waitingForUpload;

    // Audio side: resampler state (4 point window, left/right, deterministic double position).
    private double _phase;
    private float _l0, _l1, _l2, _l3, _r0, _r1, _r2, _r3;
    private float _appliedGain;
    private bool _gainInitialized;

    // Gain published as a last value by the game thread (float bits).
    private int _gainBits;

    // Snapshot written by the audio thread under a sequence counter (odd while writing).
    private int _snapshotSequence;
    private int _snapshotEndx;
    private readonly int[] _snapshotEnvx = new int[PsxSpu.VoiceCount];

    public PsxSpuSource(PsxSpuHardwareTables tables)
    {
        _spu = new PsxSpu(tables);
        _gainBits = BitConverter.SingleToInt32Bits(1f);
    }

    /// <summary>The hosted SPU. Audio thread only while attached; tests may read it once the render thread is idle.</summary>
    internal PsxSpu Spu => _spu;

    /// <summary>Game thread: writes and uploads refused because a ring or the staging buffer was full.</summary>
    public int RefusedWriteCount => _refusedWriteCount;

    #region Producer side (game thread)

    public bool TryWrite(PsxSpuWriteKind kind, int voice = 0, uint value = 0, int second = 0)
    {
        var write = new PsxSpuWrite { Kind = kind, Voice = voice, Value = value, Second = second };

        if (_registers.TryEnqueue(in write))
        {
            return true;
        }

        _refusedWriteCount++;
        return false;
    }

    public bool TryUpload(int byteAddress, ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return true;
        }

        var pieceCount = (data.Length + MaxUploadPieceBytes - 1) / MaxUploadPieceBytes;

        // Single producer: these checks guarantee that every enqueue below succeeds.
        if (data.Length > StagingCapacity
            || StagingCapacity - (_stagingTail - Volatile.Read(ref _stagingHead)) < data.Length
            || _pieces.FreeCount < pieceCount
            || _registers.FreeCount < 1)
        {
            _refusedWriteCount++;
            return false;
        }

        var number = ++_lastUploadNumber;
        var offset = 0;

        for (var i = 0; i < pieceCount; i++)
        {
            var length = Math.Min(MaxUploadPieceBytes, data.Length - offset);
            var position = _stagingTail;
            CopyToStaging(position, data.Slice(offset, length));

            var piece = new PsxSpuUploadPiece
            {
                RamAddress = byteAddress + offset,
                StagingPosition = position,
                Length = length,
                UploadNumber = number,
                IsLast = i == pieceCount - 1,
            };

            _stagingTail = unchecked(position + length);
            _pieces.TryEnqueue(in piece);
            offset += length;
        }

        var marker = new PsxSpuWrite { Kind = PsxSpuWriteKind.UploadMarker, Value = (uint)number };
        _registers.TryEnqueue(in marker);
        return true;
    }

    /// <summary>Publishes the gain applied to the whole SPU output; a last value, never lost.</summary>
    public void PublishGain(float gain)
    {
        Volatile.Write(ref _gainBits, BitConverter.SingleToInt32Bits(gain));
    }

    /// <summary>Latest published ENDX (a block old at most).</summary>
    public uint ReadEndx()
    {
        TryReadSnapshot(-1);
        return (uint)_cachedEndx;
    }

    /// <summary>Latest published ENVX of a voice (a block old at most).</summary>
    public int GetEnvx(int voice)
    {
        TryReadSnapshot(voice);
        return _cachedEnvx[voice];
    }

    // Sequence lock, reader side: a copy is valid only if the sequence was even and unchanged around it.
    // If no stable copy is obtained after a few tries, the previous value stays.
    private void TryReadSnapshot(int voice)
    {
        for (var attempt = 0; attempt < 16; attempt++)
        {
            var before = Volatile.Read(ref _snapshotSequence);
            if ((before & 1) != 0)
            {
                continue;
            }

            var endx = _snapshotEndx;
            var envx = voice >= 0 ? _snapshotEnvx[voice] : 0;
            Interlocked.MemoryBarrier();

            if (Volatile.Read(ref _snapshotSequence) != before)
            {
                continue;
            }

            if (voice < 0)
            {
                _cachedEndx = endx;
            }
            else
            {
                _cachedEnvx[voice] = envx;
            }

            return;
        }
    }

    private void CopyToStaging(int position, ReadOnlySpan<byte> data)
    {
        var offset = position & (StagingCapacity - 1);
        var first = Math.Min(data.Length, StagingCapacity - offset);
        data[..first].CopyTo(_staging.AsSpan(offset));
        data[first..].CopyTo(_staging.AsSpan(0));
    }

    #endregion

    #region Audio side

    /// <summary>
    /// Applies the pending uploads and register writes, renders <paramref name="frameCount"/> output frames of
    /// the SPU, adds them to <paramref name="output"/> (interleaved stereo float) and publishes the snapshot.
    /// Audio thread only. No allocation, no lock, no wait.
    /// </summary>
    public void MixInto(Span<float> output, int frameCount, int outputRate)
    {
        ApplyUploads();
        ApplyRegisters();

        var target = BitConverter.Int32BitsToSingle(Volatile.Read(ref _gainBits));
        if (!_gainInitialized)
        {
            _gainInitialized = true;
            _appliedGain = target;
        }

        // The gain ramps linearly over the block, so a change does not click.
        var gain = _appliedGain;
        var gainStep = (target - gain) / frameCount;
        var step = (double)PsxSpu.SampleRate / outputRate;
        var maxSliceFrames = Math.Max(1, Math.Min(MaxOutputFramesPerSlice, (int)((MaxSpuFramesPerSlice - 2) / step)));
        var done = 0;

        while (done < frameCount)
        {
            var slice = Math.Min(maxSliceFrames, frameCount - done);

            // Count the SPU frames this slice consumes with the very arithmetic used below, so both agree exactly.
            var phase = _phase;
            var needed = 0;
            for (var i = 0; i < slice; i++)
            {
                phase += step;
                while (phase >= 1.0)
                {
                    phase -= 1.0;
                    needed++;
                }
            }

            _spu.Render(_spuFrames.AsSpan(0, needed * 2), needed);

            var next = 0;
            for (var i = 0; i < slice; i++)
            {
                var t = (float)_phase;
                var l = Hermite(_l0, _l1, _l2, _l3, t);
                var r = Hermite(_r0, _r1, _r2, _r3, t);
                gain += gainStep;
                var index = (done + i) * 2;
                output[index] += l * gain;
                output[index + 1] += r * gain;

                _phase += step;
                while (_phase >= 1.0)
                {
                    _phase -= 1.0;
                    _l0 = _l1;
                    _l1 = _l2;
                    _l2 = _l3;
                    _l3 = _spuFrames[next * 2] * InverseShortRange;
                    _r0 = _r1;
                    _r1 = _r2;
                    _r2 = _r3;
                    _r3 = _spuFrames[next * 2 + 1] * InverseShortRange;
                    next++;
                }
            }

            done += slice;
        }

        _appliedGain = target;
        PublishSnapshot();
    }

    private void ApplyUploads()
    {
        var budget = MaxUploadBytesPerBlock;

        while (true)
        {
            if (!_hasHeldPiece)
            {
                if (!_pieces.TryDequeue(out _heldPiece))
                {
                    return;
                }

                _hasHeldPiece = true;
            }

            if (_heldPiece.Length > budget)
            {
                return;
            }

            budget -= _heldPiece.Length;

            var offset = _heldPiece.StagingPosition & (StagingCapacity - 1);
            var first = Math.Min(_heldPiece.Length, StagingCapacity - offset);
            _spu.WriteRam(_heldPiece.RamAddress, _staging.AsSpan(offset, first));

            if (first < _heldPiece.Length)
            {
                _spu.WriteRam(_heldPiece.RamAddress + first, _staging.AsSpan(0, _heldPiece.Length - first));
            }

            Volatile.Write(ref _stagingHead, unchecked(_heldPiece.StagingPosition + _heldPiece.Length));

            if (_heldPiece.IsLast)
            {
                _completedUploads = _heldPiece.UploadNumber;
            }

            _hasHeldPiece = false;
        }
    }

    private void ApplyRegisters()
    {
        while (true)
        {
            if (_waitingForUpload != 0)
            {
                if (_completedUploads < _waitingForUpload)
                {
                    return;
                }

                _waitingForUpload = 0;
            }

            if (!_registers.TryDequeue(out var write))
            {
                return;
            }

            if (write.Kind == PsxSpuWriteKind.UploadMarker)
            {
                _waitingForUpload = (int)write.Value;
                continue;
            }

            Apply(in write);
        }
    }

    // Values were validated by the port on the game thread, so nothing here can throw.
    private void Apply(in PsxSpuWrite write)
    {
        switch (write.Kind)
        {
            case PsxSpuWriteKind.StartAddress: _spu.SetStartAddress(write.Voice, (ushort)write.Value); break;
            case PsxSpuWriteKind.RepeatAddress: _spu.SetRepeatAddress(write.Voice, (ushort)write.Value); break;
            case PsxSpuWriteKind.Pitch: _spu.SetPitch(write.Voice, (ushort)write.Value); break;
            case PsxSpuWriteKind.Volume: _spu.SetVolume(write.Voice, write.Second != 0, (ushort)write.Value); break;
            case PsxSpuWriteKind.Adsr: _spu.SetAdsr(write.Voice, write.Value); break;
            case PsxSpuWriteKind.NoiseMode: _spu.SetNoiseMode(write.Value); break;
            case PsxSpuWriteKind.PitchModulation: _spu.SetPitchModulation(write.Value); break;
            case PsxSpuWriteKind.NoiseClock: _spu.SetNoiseClock(write.Voice, write.Second); break;
            case PsxSpuWriteKind.ReverbVoices: _spu.SetReverbVoices(write.Value); break;
            case PsxSpuWriteKind.ReverbEnabled: _spu.SetReverbEnabled(write.Value != 0); break;
            case PsxSpuWriteKind.ReverbWorkAreaStart: _spu.SetReverbWorkAreaStart((ushort)write.Value); break;
            case PsxSpuWriteKind.ReverbOutputVolume: _spu.SetReverbOutputVolume((short)write.Voice, (short)write.Second); break;
            case PsxSpuWriteKind.ReverbRegister: _spu.SetReverbRegister(write.Voice, (ushort)write.Value); break;
            case PsxSpuWriteKind.KeyOn: _spu.KeyOn(write.Value); break;
            case PsxSpuWriteKind.KeyOff: _spu.KeyOff(write.Value); break;
        }
    }

    // Sequence lock, writer side (single writer: the audio thread).
    private void PublishSnapshot()
    {
        var sequence = _snapshotSequence;
        Volatile.Write(ref _snapshotSequence, unchecked(sequence + 1));
        Interlocked.MemoryBarrier();

        _snapshotEndx = (int)_spu.ReadEndx();
        for (var v = 0; v < PsxSpu.VoiceCount; v++)
        {
            _snapshotEnvx[v] = _spu.GetEnvx(v);
        }

        Volatile.Write(ref _snapshotSequence, unchecked(sequence + 2));
    }

    // Catmull-Rom, the same cubic as the voices (see SoftwareMixer): exactly y1 at t = 0.
    private static float Hermite(float y0, float y1, float y2, float y3, float t)
    {
        var c1 = 0.5f * (y2 - y0);
        var c2 = y0 - 2.5f * y1 + 2f * y2 - 0.5f * y3;
        var c3 = 0.5f * (y3 - y0) + 1.5f * (y1 - y2);
        return ((c3 * t + c2) * t + c1) * t + y1;
    }

    #endregion
}
