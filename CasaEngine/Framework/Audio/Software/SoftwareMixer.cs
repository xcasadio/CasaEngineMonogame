using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Audio.Effects;

namespace CasaEngine.Framework.Audio.Software;

/// <summary>
/// Engine-owned software mixer core (ADR-0055): no thread, no device, deterministic. Mixes resident
/// clips and streamed 16 bit PCM into interleaved stereo float at a fixed output rate.
/// </summary>
/// <remarks>
/// <para>
/// Threading: exactly one producer thread (game thread) calls the <c>Try*</c> methods and
/// <see cref="TryDequeueEvent"/>; exactly one consumer thread (audio thread) calls
/// <see cref="Render"/>. They communicate only through lock-free single-producer/single-consumer
/// rings; there is no lock. In tests both roles may be the same thread.
/// </para>
/// <para>
/// Hot path: <see cref="Render"/> and command application allocate nothing, use no LINQ, closure
/// or lock. Per-voice state, the rings and the initial chunks are preallocated; only the producer
/// side may allocate (growing the chunk pool, up to the maximum).
/// </para>
/// <para>
/// Slots and generations: the caller owns slot allocation. A start/create command establishes a
/// voice in a slot with the generation it carries (replacing any voice there); any other command
/// whose generation does not match the voice in the slot is ignored. A queued stream chunk for a
/// stale voice is returned to the pool.
/// </para>
/// <para>
/// Semantics (plan P5): volume is linear; pitch is in octaves, step = sourceRate * 2^pitch /
/// outputRate; a mono source uses a constant power pan (left = v cos t, right = v sin t,
/// t = (pan + 1) pi / 4); a stereo source uses balance (pan below 0 scales the right channel by
/// 1 + pan, above 0 scales the left by 1 - pan). Resampling is 4 point cubic Hermite (Catmull-Rom).
/// Source frames beyond the edges are the edge frame (clamped) for a non-looped voice and wrap for
/// a looped resident voice. Positions are <see cref="double"/>, so the output is bit-reproducible.
/// A change of volume/pan ramps the gains linearly across the next rendered block.
/// </para>
/// <para>
/// Streaming: 16 bit little endian PCM buffers are copied into pooled chunks of
/// <see cref="ChunkSamples"/> samples (default 4096 = 8 KB; MusicPlayer's 16384-byte buffer takes 2,
/// a 20 ms stereo buffer at 48 kHz takes 1). The default pool holds 256 chunks (2 MB) and grows
/// on the producer side up to 4096 (32 MB). A voice keeps at most
/// <see cref="VoiceChunkQueueCapacity"/> chunks queued; beyond that a chunk is dropped and counted
/// in <see cref="DroppedChunkCount"/>. A buffer is reported consumed when its last frame is read
/// into the interpolation window, that is two frames ahead of what is audible. A voice whose queue
/// runs dry outputs silence and keeps playing.
/// </para>
/// </remarks>
internal sealed class SoftwareMixer
{
    public const int DefaultVoiceCapacity = 64;
    public const int DefaultCommandCapacity = 4096;
    public const int DefaultEventCapacity = 4096;
    public const int DefaultChunkSamples = 4096;
    public const int DefaultInitialChunkCount = 256;
    public const int DefaultMaxChunkCount = 4096;

    /// <summary>Largest block <see cref="Render"/> mixes at once; a larger request is split.</summary>
    public const int DefaultMaxBlockFrames = 1024;

    /// <summary>Fixed number of buses, Master (index 0) included.</summary>
    public const int BusCapacity = 32;

    /// <summary>Insert effects held by one bus.</summary>
    public const int EffectSlotsPerBus = 4;

    /// <summary>Index of the Master bus: the root every other bus ends in, and the default route.</summary>
    public const int MasterBus = 0;

    /// <summary>Maximum number of chunks a single streaming voice can have queued.</summary>
    public const int VoiceChunkQueueCapacity = 128;

    private const float InverseShortRange = 1f / 32768f;
    private const double QuarterPi = Math.PI / 4.0;

    private readonly MixerVoice[] _voices;
    private readonly SpscRingBuffer<MixerCommand> _commands;
    private readonly SpscRingBuffer<MixerEvent> _events;
    private readonly SampleChunkPool _pool;
    private readonly int[] _producerChannels;

    // Per slot, (generation << 32) | consumed buffer count; written by the render thread only.
    private readonly long[] _consumedBuffers;
    private int _droppedEventCount;
    private int _droppedChunkCount;
    private readonly AudioLogThrottle _invalidRegionLog = new();

    // Render thread only: the pulled PlayStation SPU source, null while none is attached, and its bus.
    private PsxSpuSource _spu;
    private int _spuBus;

    // Bus graph. A parent always has a lower index than its child (it must exist to be given as a
    // parent), so mixing from the highest index down to 0 is "children, then parents, then Master".
    private readonly int _maxBlockFrames;
    private readonly float[] _busBuffers;
    private readonly int[] _busParent = new int[BusCapacity];
    private readonly float[] _busAppliedGain = new float[BusCapacity];

    // Last value published per bus by the producer (float bits); never queued, so never lost.
    private readonly int[] _busGainBits = new int[BusCapacity];

    // Number of times the producer published a gain for each bus: lets a ramp notice a publish even of the same value.
    private readonly int[] _busGainPublishCount = new int[BusCapacity];

    // Render thread: explicit duration gain ramp of each bus (see MixBuses).
    private readonly BusRamp[] _busRamps = new BusRamp[BusCapacity];

    // Render thread: insert effects of each bus (EffectSlotsPerBus slots, in insertion order) and the DSP state of
    // each, preallocated so that adding an effect allocates nothing on the audio thread.
    private readonly AudioEffect[] _busEffects = new AudioEffect[BusCapacity * EffectSlotsPerBus];
    private readonly EffectDspState[] _busEffectStates = new EffectDspState[BusCapacity * EffectSlotsPerBus];
    private readonly int[] _busEffectCounts = new int[BusCapacity];

    // Render thread: buses created so far (Master always exists). Producer: buses handed out so far.
    private int _busCount = 1;
    private int _producerBusCount = 1;

    public SoftwareMixer(
        int outputSampleRate,
        int voiceCapacity = DefaultVoiceCapacity,
        int commandCapacity = DefaultCommandCapacity,
        int eventCapacity = DefaultEventCapacity,
        int chunkSamples = DefaultChunkSamples,
        int initialChunkCount = DefaultInitialChunkCount,
        int maxChunkCount = DefaultMaxChunkCount,
        int maxBlockFrames = DefaultMaxBlockFrames)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outputSampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(voiceCapacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBlockFrames);

        if (chunkSamples < 2 || chunkSamples % 2 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSamples), chunkSamples, "The chunk size must be an even number of samples.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(initialChunkCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxChunkCount, Math.Max(initialChunkCount, 1));

        OutputSampleRate = outputSampleRate;
        ChunkSamples = chunkSamples;
        _voices = new MixerVoice[voiceCapacity];
        _producerChannels = new int[voiceCapacity];
        _consumedBuffers = new long[voiceCapacity];
        _commands = new SpscRingBuffer<MixerCommand>(commandCapacity);
        _events = new SpscRingBuffer<MixerEvent>(eventCapacity);
        _pool = new SampleChunkPool(chunkSamples, initialChunkCount, maxChunkCount);
        _maxBlockFrames = maxBlockFrames;
        _busBuffers = new float[BusCapacity * maxBlockFrames * 2];
        var unity = BitConverter.SingleToInt32Bits(1f);

        for (var i = 0; i < BusCapacity; i++)
        {
            _busGainBits[i] = unity;
            _busAppliedGain[i] = 1f;
        }

        for (var i = 0; i < _voices.Length; i++)
        {
            _voices[i].ChunkQueue = new SampleChunk[VoiceChunkQueueCapacity];
        }
    }

    public int OutputSampleRate { get; }

    public int VoiceCapacity => _voices.Length;

    /// <summary>Largest block mixed at once (frames); <see cref="Render"/> splits a larger request.</summary>
    public int MaxBlockFrames => _maxBlockFrames;

    /// <summary>Producer side: buses handed out so far, Master included (at most <see cref="BusCapacity"/>).</summary>
    public int BusCount => _producerBusCount;

    /// <summary>Samples (all channels) per streaming chunk.</summary>
    public int ChunkSamples { get; }

    /// <summary>Capacity of the command ring (a power of two).</summary>
    public int CommandCapacity => _commands.Capacity;

    /// <summary>Capacity of the event ring (a power of two).</summary>
    public int EventCapacity => _events.Capacity;

    /// <summary>Events lost because the event ring was full (buffer consumed reports only).</summary>
    public int DroppedEventCount => Volatile.Read(ref _droppedEventCount);

    /// <summary>Stream chunks lost because a voice queue was full.</summary>
    public int DroppedChunkCount => Volatile.Read(ref _droppedChunkCount);

    /// <summary>Chunks currently allocated by the pool.</summary>
    public int ChunkPoolSize => _pool.CreatedCount;

    /// <summary>Render thread only: number of voices currently occupying a slot.</summary>
    public int ActiveVoiceCount
    {
        get
        {
            var count = 0;

            for (var i = 0; i < _voices.Length; i++)
            {
                if (_voices[i].Active)
                {
                    count++;
                }
            }

            return count;
        }
    }

    #region Producer side

    /// <summary>
    /// Enqueues a raw command. Returns false, and enqueues nothing, when the command ring is full;
    /// the caller decides whether to retry on a later frame or to drop the command.
    /// </summary>
    public bool TryEnqueueCommand(in MixerCommand command)
    {
        return _commands.TryEnqueue(in command);
    }

    public bool TryStartResidentVoice(int slot, int generation, PcmAudioClip clip, AudioVoiceParameters parameters, int bus = MasterBus)
    {
        var command = new MixerCommand
        {
            Kind = MixerCommandKind.StartResident,
            Slot = slot,
            Generation = generation,
            Clip = clip,
            Parameters = ValidateLoopRegion(parameters, clip),
            Bus = bus,
        };

        return _commands.TryEnqueue(in command);
    }

    // Producer side. A loop region that does not fit the clip falls back to the whole clip.
    private AudioVoiceParameters ValidateLoopRegion(AudioVoiceParameters parameters, PcmAudioClip clip)
    {
        if (!parameters.IsLooped || !parameters.HasLoopRegion || clip == null)
        {
            return parameters;
        }

        if (IsValidRegion(parameters.LoopStartFrame, parameters.LoopEndFrame, clip.FrameCount))
        {
            return parameters;
        }

        _invalidRegionLog.WriteWarning("Audio: a loop region does not fit its clip (start below 0, end past the clip or start not below end), the whole clip loops instead.");
        return parameters.WithoutLoopRegion();
    }

    private static bool IsValidRegion(int start, int end, int frames)
    {
        return start >= 0 && end <= frames && start < end;
    }

    /// <summary>
    /// Starts a resident mono voice whose channel gains are explicit: left = volume * leftGain, right =
    /// volume * rightGain, no pan law. A clip that is not mono starts as an ordinary voice.
    /// </summary>
    public bool TryStartResidentStereoVoice(int slot, int generation, PcmAudioClip clip, AudioVoiceParameters parameters, float leftGain, float rightGain, int bus = MasterBus)
    {
        var command = new MixerCommand
        {
            Kind = MixerCommandKind.StartResident,
            Slot = slot,
            Generation = generation,
            Clip = clip,
            Parameters = ValidateLoopRegion(parameters, clip),
            ExplicitGains = true,
            LeftGain = leftGain,
            RightGain = rightGain,
            Bus = bus,
        };

        return _commands.TryEnqueue(in command);
    }

    /// <summary>Changes the explicit gains of a voice started by <see cref="TryStartResidentStereoVoice"/>; ramped over the next block.</summary>
    public bool TrySetStereoGains(int slot, int generation, float leftGain, float rightGain)
    {
        var command = new MixerCommand
        {
            Kind = MixerCommandKind.SetStereoGains,
            Slot = slot,
            Generation = generation,
            LeftGain = leftGain,
            RightGain = rightGain,
        };

        return _commands.TryEnqueue(in command);
    }

    public bool TryCreateStreamingVoice(int slot, int generation, int channels, int sampleRate, AudioVoiceParameters parameters, int bus = MasterBus)
    {
        if ((uint)slot >= (uint)_voices.Length || channels is not (1 or 2) || sampleRate <= 0)
        {
            return false;
        }

        var command = new MixerCommand
        {
            Kind = MixerCommandKind.CreateStreaming,
            Slot = slot,
            Generation = generation,
            Channels = channels,
            SampleRate = sampleRate,
            Parameters = parameters,
            Bus = bus,
        };

        if (!_commands.TryEnqueue(in command))
        {
            return false;
        }

        _producerChannels[slot] = channels;
        return true;
    }

    /// <summary>
    /// Creates the next bus as a child of <paramref name="parentBus"/> and returns its index. False, with
    /// nothing created, when the parent was not handed out, when the <see cref="BusCapacity"/> buses exist
    /// (the caller routes to Master instead) or when the command ring is full. Producer thread only.
    /// </summary>
    public bool TryCreateBus(int parentBus, out int bus)
    {
        bus = -1;

        if ((uint)parentBus >= (uint)_producerBusCount || _producerBusCount >= BusCapacity)
        {
            return false;
        }

        var index = _producerBusCount;
        var command = new MixerCommand { Kind = MixerCommandKind.CreateBus, Bus = index, ParentBus = parentBus };

        if (!_commands.TryEnqueue(in command))
        {
            return false;
        }

        _producerBusCount++;
        bus = index;
        return true;
    }

    /// <summary>
    /// Publishes the own gain of a bus, in [0, 1]: a last value read at the next block, never queued so never
    /// lost (NaN is ignored). The audio thread ramps to it across the block. A bus not handed out is ignored.
    /// </summary>
    public void SetBusGain(int bus, float gain)
    {
        if ((uint)bus >= (uint)_producerBusCount || float.IsNaN(gain))
        {
            return;
        }

        var clamped = Math.Clamp(gain, AudioVoiceParameters.MinVolume, AudioVoiceParameters.MaxVolume);
        Volatile.Write(ref _busGainBits[bus], BitConverter.SingleToInt32Bits(clamped));
        Volatile.Write(ref _busGainPublishCount[bus], _busGainPublishCount[bus] + 1);
    }

    /// <summary>
    /// Ramps the own gain of a bus to <paramref name="gain"/> over <paramref name="frames"/> output frames, linearly per
    /// sample, from its current value. A command, so it is never lost silently: false when the ring is full (the caller
    /// retries) or the bus was not handed out. The ramp starts at the start of the next block the audio thread renders
    /// (at most one block, about 10 ms, late: commands carry no timestamp). The ramp owns the gain of the bus while it
    /// runs and after it ends (the gain stays at the target) until the next <see cref="SetBusGain"/> call, which wins.
    /// </summary>
    public bool TryRampBusGain(int bus, float gain, int frames)
    {
        if ((uint)bus >= (uint)_producerBusCount)
        {
            return false;
        }

        var command = new MixerCommand
        {
            Kind = MixerCommandKind.RampBus,
            Bus = bus,
            Volume = Math.Clamp(float.IsNaN(gain) ? 1f : gain, AudioVoiceParameters.MinVolume, AudioVoiceParameters.MaxVolume),
            Frames = frames,
        };

        return _commands.TryEnqueue(in command);
    }

    /// <summary>Stops the gain ramp of a bus at its current value. False when the ring is full or the bus was not handed out.</summary>
    public bool TryFreezeBusGain(int bus)
    {
        if ((uint)bus >= (uint)_producerBusCount)
        {
            return false;
        }

        var command = new MixerCommand { Kind = MixerCommandKind.FreezeBus, Bus = bus };
        return _commands.TryEnqueue(in command);
    }

    /// <summary>Routes the attached SPU to a bus, applied in order with the other commands.</summary>
    /// <summary>
    /// Appends an insert effect to a bus (at most <see cref="EffectSlotsPerBus"/>; a further one is ignored by the audio
    /// thread). A command, so it is never lost silently: false when the ring is full (the caller retries), the bus was not
    /// handed out or the effect is null. Producer thread only.
    /// </summary>
    public bool TryAddEffect(int bus, AudioEffect effect)
    {
        if ((uint)bus >= (uint)_producerBusCount || effect == null)
        {
            return false;
        }

        var command = new MixerCommand { Kind = MixerCommandKind.AddEffect, Bus = bus, Effect = effect };
        return _commands.TryEnqueue(in command);
    }

    /// <summary>Removes an insert effect from a bus; the ones after it move up. False when the command could not be sent.</summary>
    public bool TryRemoveEffect(int bus, AudioEffect effect)
    {
        if ((uint)bus >= (uint)_producerBusCount || effect == null)
        {
            return false;
        }

        var command = new MixerCommand { Kind = MixerCommandKind.RemoveEffect, Bus = bus, Effect = effect };
        return _commands.TryEnqueue(in command);
    }

    public bool TryRoutePsxSpu(PsxSpuSource spu, int bus)
    {
        if ((uint)bus >= (uint)_producerBusCount)
        {
            return false;
        }

        var command = new MixerCommand { Kind = MixerCommandKind.RoutePsxSpu, Spu = spu, Bus = bus };
        return _commands.TryEnqueue(in command);
    }

    public bool TryStartStreamingVoice(int slot, int generation)
    {
        return TrySimple(MixerCommandKind.StartStreaming, slot, generation);
    }

    public bool TrySetParameters(int slot, int generation, AudioVoiceParameters parameters)
    {
        var command = new MixerCommand
        {
            Kind = MixerCommandKind.SetParameters,
            Slot = slot,
            Generation = generation,
            Parameters = parameters,
        };

        return _commands.TryEnqueue(in command);
    }

    public bool TrySetVolume(int slot, int generation, float volume)
    {
        var command = new MixerCommand
        {
            Kind = MixerCommandKind.SetVolume,
            Slot = slot,
            Generation = generation,
            Volume = Math.Clamp(float.IsNaN(volume) ? 1f : volume, AudioVoiceParameters.MinVolume, AudioVoiceParameters.MaxVolume),
        };

        return _commands.TryEnqueue(in command);
    }

    /// <summary>
    /// Ramps the volume of a voice to <paramref name="volume"/> over <paramref name="frames"/> output frames, linearly per
    /// sample, from its current value (the value a previous ramp reached when it is interrupted). The ramp starts at the
    /// start of the next block the audio thread renders, at most one block (about 10 ms) after the command is sent. It
    /// advances while the voice is paused. While it runs, a <see cref="TrySetVolume"/> ends it and the volume of a
    /// <see cref="TrySetParameters"/> is ignored (pan and pitch still apply).
    /// </summary>
    public bool TryRampVoiceVolume(int slot, int generation, float volume, int frames)
    {
        var command = new MixerCommand
        {
            Kind = MixerCommandKind.RampVoice,
            Slot = slot,
            Generation = generation,
            Volume = Math.Clamp(float.IsNaN(volume) ? 1f : volume, AudioVoiceParameters.MinVolume, AudioVoiceParameters.MaxVolume),
            Frames = frames,
        };

        return _commands.TryEnqueue(in command);
    }

    /// <summary>Stops the volume ramp of a voice at the value it has reached.</summary>
    public bool TryFreezeVoiceVolume(int slot, int generation)
    {
        return TrySimple(MixerCommandKind.FreezeVoice, slot, generation);
    }

    public bool TryPause(int slot, int generation)
    {
        return TrySimple(MixerCommandKind.Pause, slot, generation);
    }

    public bool TryResume(int slot, int generation)
    {
        return TrySimple(MixerCommandKind.Resume, slot, generation);
    }

    /// <summary>Silences the voice at the next render and frees the slot; queued stream chunks go back to the pool.</summary>
    public bool TryStop(int slot, int generation)
    {
        return TrySimple(MixerCommandKind.Stop, slot, generation);
    }

    public bool TryStopAll()
    {
        return TrySimple(MixerCommandKind.StopAll, 0, 0);
    }

    /// <summary>
    /// Copies 16 bit little endian interleaved PCM into pooled chunks and queues them for the voice.
    /// The caller may reuse <paramref name="pcm16"/> immediately. A trailing partial frame is
    /// ignored. All-or-nothing: returns false (nothing queued) when the voice was not created
    /// through <see cref="TryCreateStreamingVoice"/>, when the command ring has too little room or
    /// when the pool is exhausted. <paramref name="sequence"/> is echoed by the matching
    /// <see cref="MixerEventKind.BufferConsumed"/> event.
    /// </summary>
    public bool TrySubmitStreamingBuffer(int slot, int generation, ReadOnlySpan<byte> pcm16, int sequence)
    {
        if ((uint)slot >= (uint)_voices.Length)
        {
            return false;
        }

        var channels = _producerChannels[slot];

        if (channels == 0)
        {
            return false;
        }

        var totalSamples = pcm16.Length / 2 / channels * channels;
        var chunkCount = Math.Max(1, (totalSamples + ChunkSamples - 1) / ChunkSamples);

        // Both checks guarantee that every rent and every enqueue below succeeds: this thread is
        // the only producer of commands and of rents.
        if (_commands.FreeCount < chunkCount || !_pool.CanRent(chunkCount))
        {
            return false;
        }

        var written = 0;

        for (var i = 0; i < chunkCount; i++)
        {
            var chunk = _pool.Rent();
            var count = Math.Min(ChunkSamples, totalSamples - written);
            FillChunk(chunk, pcm16, written, count, sequence, i == chunkCount - 1);
            written += count;

            var command = new MixerCommand
            {
                Kind = MixerCommandKind.SubmitChunk,
                Slot = slot,
                Generation = generation,
                Chunk = chunk,
            };

            _commands.TryEnqueue(in command);
        }

        return true;
    }

    /// <summary>Dequeues the next render-to-producer event. Producer thread only.</summary>
    public bool TryDequeueEvent(out MixerEvent mixerEvent)
    {
        return _events.TryDequeue(out mixerEvent);
    }

    /// <summary>
    /// Streaming buffers the render thread consumed (or dropped on queue overflow) for the voice of
    /// <paramref name="generation"/> in <paramref name="slot"/>. 0 while the render thread has not yet
    /// applied the creation of that generation (the published value then belongs to an older voice).
    /// Producer thread only. Allocation free.
    /// </summary>
    public int GetConsumedBufferCount(int slot, int generation)
    {
        if ((uint)slot >= (uint)_consumedBuffers.Length)
        {
            return 0;
        }

        var published = Volatile.Read(ref _consumedBuffers[slot]);
        return (int)(published >> 32) == generation ? (int)(published & 0xFFFFFFFFL) : 0;
    }

    private bool TrySimple(MixerCommandKind kind, int slot, int generation)
    {
        var command = new MixerCommand { Kind = kind, Slot = slot, Generation = generation };
        return _commands.TryEnqueue(in command);
    }

    private static void FillChunk(SampleChunk chunk, ReadOnlySpan<byte> pcm16, int firstSample, int count, int sequence, bool endsBuffer)
    {
        var target = chunk.Samples;
        var byteOffset = firstSample * 2;

        for (var i = 0; i < count; i++)
        {
            target[i] = BinaryPrimitives.ReadInt16LittleEndian(pcm16.Slice(byteOffset + i * 2, 2));
        }

        chunk.Count = count;
        chunk.EndsBuffer = endsBuffer;
        chunk.Sequence = sequence;
    }

    #endregion

    #region Render side

    /// <summary>
    /// Applies the pending commands, then writes <paramref name="frameCount"/> interleaved stereo
    /// frames (2 floats each) into <paramref name="interleavedStereo"/>, hard clipped to [-1, 1].
    /// Render thread only.
    /// </summary>
    public void Render(Span<float> interleavedStereo, int frameCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frameCount);

        if (interleavedStereo.Length < frameCount * 2)
        {
            throw new ArgumentException("The output span is too small for the requested frame count.", nameof(interleavedStereo));
        }

        while (_commands.TryDequeue(out var command))
        {
            ApplyCommand(in command);
        }

        // A request larger than the block the bus buffers were sized for is mixed in several blocks.
        var done = 0;

        while (done < frameCount)
        {
            var blockFrames = Math.Min(_maxBlockFrames, frameCount - done);
            RenderBlock(interleavedStereo.Slice(done * 2, blockFrames * 2), blockFrames);
            done += blockFrames;
        }
    }

    private void RenderBlock(Span<float> output, int frameCount)
    {
        var sampleCount = frameCount * 2;

        for (var b = 0; b < _busCount; b++)
        {
            BusBuffer(b, sampleCount).Clear();
        }

        for (var v = 0; v < _voices.Length; v++)
        {
            ref var voice = ref _voices[v];

            if (!voice.Active)
            {
                continue;
            }

            if (voice.EndPending)
            {
                TryEmitEnded(v, ref voice);
                continue;
            }

            if (!voice.Started || voice.Paused)
            {
                // A ramp keeps running on a silent voice, like the game thread chronology it follows.
                FinishVoiceRamp(ref voice, frameCount);
                continue;
            }

            var busBuffer = BusBuffer(voice.Bus, sampleCount);

            if (voice.IsStreaming)
            {
                RenderStreaming(v, ref voice, busBuffer, frameCount);
            }
            else
            {
                RenderResident(v, ref voice, busBuffer, frameCount);
            }

            if (voice.Active)
            {
                FinishVoiceRamp(ref voice, frameCount);
            }
        }

        // Like a voice, on its own bus.
        _spu?.MixInto(BusBuffer(_spuBus, sampleCount), frameCount, OutputSampleRate);

        MixBuses(output, frameCount);

        // The final hard clip, after Master.
        for (var i = 0; i < output.Length; i++)
        {
            var sample = output[i];
            output[i] = sample > 1f ? 1f : sample < -1f ? -1f : sample;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Span<float> BusBuffer(int bus, int sampleCount)
    {
        return _busBuffers.AsSpan(bus * _maxBlockFrames * 2, sampleCount);
    }

    // Children before parents: a child always has a higher index than its parent, so going down from the
    // last bus reaches Master last. Each bus is scaled by its own gain, ramped across the block, while it is
    // added to its parent buffer; Master is scaled into the output. The insert effects of a bus run on its
    // buffer first, between the voices and this scaling.
    private void MixBuses(Span<float> output, int frameCount)
    {
        var sampleCount = frameCount * 2;

        for (var b = _busCount - 1; b >= 0; b--)
        {
            var source = BusBuffer(b, sampleCount);
            ProcessEffects(b, source, frameCount);
            ref var ramp = ref _busRamps[b];
            var publishCount = Volatile.Read(ref _busGainPublishCount[b]);
            var publishedBits = Volatile.Read(ref _busGainBits[b]);

            // A gain published after the ramp started is an explicit change: it wins.
            if ((ramp.Active || ramp.Holding) && publishCount != ramp.SeenPublishCount)
            {
                ramp.Active = false;
                ramp.Holding = false;
            }

            var ramping = ramp.Active;
            var rampStart = ramp.Value;
            var rampIncrement = ramp.Increment;
            var rampTarget = ramp.Target;
            var rampLeft = ramp.FramesLeft;
            var target = ramp.Holding ? (float)ramp.Value : BitConverter.Int32BitsToSingle(publishedBits);
            var gain = _busAppliedGain[b];
            var increment = (target - gain) / frameCount;
            var destination = b == MasterBus ? output : BusBuffer(_busParent[b], sampleCount);
            var add = b != MasterBus;

            for (var f = 0; f < frameCount; f++)
            {
                if (ramping)
                {
                    gain = (float)(f + 1 >= rampLeft ? rampTarget : rampStart + rampIncrement * (f + 1));
                }
                else
                {
                    gain += increment;
                }

                var i = f * 2;

                if (add)
                {
                    destination[i] += source[i] * gain;
                    destination[i + 1] += source[i + 1] * gain;
                }
                else
                {
                    destination[i] = source[i] * gain;
                    destination[i + 1] = source[i + 1] * gain;
                }
            }

            if (ramping)
            {
                if (frameCount >= rampLeft)
                {
                    ramp.Active = false;
                    ramp.Holding = true;
                    ramp.Value = rampTarget;
                    ramp.FramesLeft = 0;
                    _busAppliedGain[b] = (float)rampTarget;
                }
                else
                {
                    ramp.Value = rampStart + rampIncrement * frameCount;
                    ramp.FramesLeft = rampLeft - frameCount;
                    _busAppliedGain[b] = (float)ramp.Value;
                }
            }
            else
            {
                _busAppliedGain[b] = target;
            }
        }
    }

    // Insert effects of a bus, in insertion order, on its buffer: after the voices and the children were mixed into
    // it, before its gain. A silent bus is processed too, so a tail (filter ring, compressor release) keeps going.
    private void ProcessEffects(int bus, Span<float> buffer, int frameCount)
    {
        var count = _busEffectCounts[bus];
        var first = bus * EffectSlotsPerBus;

        for (var e = 0; e < count; e++)
        {
            _busEffects[first + e].Process(ref _busEffectStates[first + e], buffer, frameCount, OutputSampleRate);
        }
    }

    private void AddBusEffect(int bus, AudioEffect effect)
    {
        var count = _busEffectCounts[bus];

        if (count >= EffectSlotsPerBus)
        {
            return;
        }

        var slot = (bus * EffectSlotsPerBus) + count;
        _busEffects[slot] = effect;
        _busEffectStates[slot] = default;
        _busEffectCounts[bus] = count + 1;
    }

    private void RemoveBusEffect(int bus, AudioEffect effect)
    {
        var count = _busEffectCounts[bus];
        var first = bus * EffectSlotsPerBus;

        for (var e = 0; e < count; e++)
        {
            if (!ReferenceEquals(_busEffects[first + e], effect))
            {
                continue;
            }

            for (var next = e + 1; next < count; next++)
            {
                _busEffects[first + next - 1] = _busEffects[first + next];
                _busEffectStates[first + next - 1] = _busEffectStates[first + next];
            }

            _busEffects[first + count - 1] = null;
            _busEffectStates[first + count - 1] = default;
            _busEffectCounts[bus] = count - 1;
            return;
        }
    }

    // Render thread. The ramp starts from the gain the bus has now; see TryRampBusGain.
    private void StartBusRamp(int bus, float target, int frames)
    {
        frames = Math.Max(1, frames);
        ref var ramp = ref _busRamps[bus];
        ramp.Value = _busAppliedGain[bus];
        ramp.Target = target;
        ramp.Increment = (target - ramp.Value) / frames;
        ramp.FramesLeft = frames;
        ramp.Active = true;
        ramp.Holding = false;
        ramp.SeenPublishCount = Volatile.Read(ref _busGainPublishCount[bus]);
    }

    // Render thread. A frozen bus keeps the gain it reached until another value is published.
    private void FreezeBusRamp(int bus)
    {
        ref var ramp = ref _busRamps[bus];

        if (ramp.Active)
        {
            ramp.Active = false;
            ramp.Holding = true;
            ramp.Value = _busAppliedGain[bus];
        }
    }

    // Render thread. Starts a voice ramp from the volume the voice has now (a running ramp keeps Volume at its
    // value at the start of the block, which is where a command is applied).
    private static void StartVoiceRamp(ref MixerVoice voice, float target, int frames)
    {
        frames = Math.Max(1, frames);
        voice.RampValue = voice.Volume;
        voice.RampTarget = target;
        voice.RampIncrement = (target - voice.RampValue) / frames;
        voice.RampFramesLeft = frames;
        voice.RampActive = true;
    }

    // Render thread, after a block: moves a running ramp forward by the block, and keeps Volume and the channel
    // gains at the value reached, so the next command (or the end of the ramp) starts from it.
    private static void FinishVoiceRamp(ref MixerVoice voice, int frameCount)
    {
        if (!voice.RampActive)
        {
            return;
        }

        double value;

        if (frameCount >= voice.RampFramesLeft)
        {
            value = voice.RampTarget;
            voice.RampActive = false;
            voice.RampFramesLeft = 0;
        }
        else
        {
            value = voice.RampValue + voice.RampIncrement * frameCount;
            voice.RampFramesLeft -= frameCount;
        }

        voice.RampValue = value;
        voice.Volume = (float)value;
        voice.CurrentLeftGain = voice.TargetLeftGain = voice.PanLeftFactor * voice.Volume;
        voice.CurrentRightGain = voice.TargetRightGain = voice.PanRightFactor * voice.Volume;
    }

    private void ApplyCommand(in MixerCommand command)
    {
        if (command.Kind == MixerCommandKind.AttachPsxSpu)
        {
            _spu = command.Spu;
            _spuBus = MasterBus;
            return;
        }

        if (command.Kind == MixerCommandKind.DetachPsxSpu)
        {
            if (ReferenceEquals(_spu, command.Spu))
            {
                _spu = null;
                _spuBus = MasterBus;
            }

            return;
        }

        if (command.Kind == MixerCommandKind.RoutePsxSpu)
        {
            if (ReferenceEquals(_spu, command.Spu) && (uint)command.Bus < (uint)_busCount)
            {
                _spuBus = command.Bus;
            }

            return;
        }

        if (command.Kind == MixerCommandKind.CreateBus)
        {
            // The producer hands indices out in order; anything else is ignored.
            if (command.Bus == _busCount && command.Bus < BusCapacity && (uint)command.ParentBus < (uint)_busCount)
            {
                _busParent[command.Bus] = command.ParentBus;
                // A new bus starts at its published gain: no ramp from a stale value.
                _busAppliedGain[command.Bus] = BitConverter.Int32BitsToSingle(Volatile.Read(ref _busGainBits[command.Bus]));
                _busCount++;
            }

            return;
        }

        if (command.Kind == MixerCommandKind.RampBus)
        {
            if ((uint)command.Bus < (uint)_busCount)
            {
                StartBusRamp(command.Bus, command.Volume, command.Frames);
            }

            return;
        }

        if (command.Kind == MixerCommandKind.FreezeBus)
        {
            if ((uint)command.Bus < (uint)_busCount)
            {
                FreezeBusRamp(command.Bus);
            }

            return;
        }

        if (command.Kind == MixerCommandKind.AddEffect)
        {
            if ((uint)command.Bus < (uint)_busCount)
            {
                AddBusEffect(command.Bus, command.Effect);
            }

            return;
        }

        if (command.Kind == MixerCommandKind.RemoveEffect)
        {
            if ((uint)command.Bus < (uint)_busCount)
            {
                RemoveBusEffect(command.Bus, command.Effect);
            }

            return;
        }

        if (command.Kind == MixerCommandKind.StopAll)
        {
            for (var i = 0; i < _voices.Length; i++)
            {
                ReleaseVoice(ref _voices[i]);
            }

            return;
        }

        if ((uint)command.Slot >= (uint)_voices.Length)
        {
            if (command.Kind == MixerCommandKind.SubmitChunk)
            {
                _pool.ReturnFromRender(command.Chunk);
            }

            return;
        }

        ref var voice = ref _voices[command.Slot];

        if (command.Kind == MixerCommandKind.StartResident)
        {
            StartResident(ref voice, in command);
            return;
        }

        if (command.Kind == MixerCommandKind.CreateStreaming)
        {
            CreateStreaming(ref voice, in command);
            return;
        }

        var matches = voice.Active && voice.Generation == command.Generation;

        if (command.Kind == MixerCommandKind.SubmitChunk)
        {
            QueueChunk(command.Slot, ref voice, matches, command.Chunk);
            return;
        }

        if (!matches)
        {
            return;
        }

        switch (command.Kind)
        {
            case MixerCommandKind.StartStreaming:
                voice.Started = true;
                break;
            case MixerCommandKind.SetParameters:
                voice.Looped = !voice.IsStreaming && command.Parameters.IsLooped;
                voice.RateMultiplier = command.Parameters.RateMultiplier;

                if (!voice.IsStreaming)
                {
                    ApplyLoopRegion(ref voice, command.Parameters);
                }

                // A running ramp owns the volume.
                SetParameters(ref voice, voice.RampActive ? voice.Volume : command.Parameters.Volume, command.Parameters.Pan, command.Parameters.Pitch, false);
                break;
            case MixerCommandKind.SetVolume:
                voice.RampActive = false;
                SetParameters(ref voice, command.Volume, voice.Pan, voice.Pitch, false);
                break;
            case MixerCommandKind.RampVoice:
                StartVoiceRamp(ref voice, command.Volume, command.Frames);
                break;
            case MixerCommandKind.FreezeVoice:
                voice.RampActive = false;
                break;
            case MixerCommandKind.SetStereoGains:
                if (voice.ExplicitGains)
                {
                    voice.ExplicitLeft = command.LeftGain;
                    voice.ExplicitRight = command.RightGain;
                    SetParameters(ref voice, voice.Volume, voice.Pan, voice.Pitch, false);
                }

                break;
            case MixerCommandKind.Pause:
                voice.Paused = true;
                break;
            case MixerCommandKind.Resume:
                voice.Paused = false;
                break;
            case MixerCommandKind.Stop:
                ReleaseVoice(ref voice);
                break;
        }
    }

    private void QueueChunk(int slot, ref MixerVoice voice, bool matches, SampleChunk chunk)
    {
        if (matches && voice.IsStreaming)
        {
            if (voice.QueueCount < voice.ChunkQueue.Length)
            {
                voice.ChunkQueue[(voice.QueueHead + voice.QueueCount) % voice.ChunkQueue.Length] = chunk;
                voice.QueueCount++;
                return;
            }

            Interlocked.Increment(ref _droppedChunkCount);

            // The dropped chunk will never be played: the buffer it ends still counts as consumed.
            if (chunk.EndsBuffer)
            {
                voice.ConsumedBuffers++;
                PublishConsumed(slot, ref voice);
            }
        }

        _pool.ReturnFromRender(chunk);
    }

    private void StartResident(ref MixerVoice voice, in MixerCommand command)
    {
        ReleaseVoice(ref voice);

        var clip = command.Clip;

        if (clip == null)
        {
            return;
        }

        voice.Active = true;
        voice.Bus = (uint)command.Bus < (uint)_busCount ? command.Bus : MasterBus;
        voice.Generation = command.Generation;
        voice.IsStreaming = false;
        voice.Started = true;
        voice.Paused = false;
        voice.EndPending = false;
        voice.Samples = clip.SampleArray;
        voice.SourceChannels = clip.ChannelCount;
        voice.FrameCount = clip.FrameCount;
        voice.Position = 0.0;
        voice.ConsumedBuffers = 0;
        PublishConsumed(command.Slot, ref voice);
        voice.SourceRatio = (double)clip.SampleRate / OutputSampleRate;
        voice.Looped = command.Parameters.IsLooped;
        voice.RateMultiplier = command.Parameters.RateMultiplier;
        ApplyLoopRegion(ref voice, command.Parameters);
        voice.ExplicitGains = command.ExplicitGains && clip.ChannelCount == 1;
        voice.ExplicitLeft = command.LeftGain;
        voice.ExplicitRight = command.RightGain;
        SetParameters(ref voice, command.Parameters.Volume, command.Parameters.Pan, command.Parameters.Pitch, true);
    }

    private void CreateStreaming(ref MixerVoice voice, in MixerCommand command)
    {
        ReleaseVoice(ref voice);

        if (command.Channels is not (1 or 2) || command.SampleRate <= 0)
        {
            return;
        }

        voice.Active = true;
        voice.Bus = (uint)command.Bus < (uint)_busCount ? command.Bus : MasterBus;
        voice.Generation = command.Generation;
        voice.IsStreaming = true;
        voice.Started = false;
        voice.Paused = false;
        voice.EndPending = false;
        voice.Looped = false;
        voice.RateMultiplier = command.Parameters.RateMultiplier;
        voice.SourceChannels = command.Channels;
        voice.SourceRatio = (double)command.SampleRate / OutputSampleRate;
        voice.StreamFraction = 3.0;
        voice.WindowStarted = false;
        voice.QueueHead = 0;
        voice.QueueCount = 0;
        voice.CurrentChunk = null;
        voice.CurrentIndex = 0;
        voice.ConsumedBuffers = 0;
        voice.ExplicitGains = false;
        PublishConsumed(command.Slot, ref voice);
        SetParameters(ref voice, command.Parameters.Volume, command.Parameters.Pan, command.Parameters.Pitch, true);
    }

    // Render thread. One 64-bit store carries the generation and the count together, so the producer
    // can never pair the count of one voice with the generation of another.
    private void PublishConsumed(int slot, ref MixerVoice voice)
    {
        Volatile.Write(ref _consumedBuffers[slot], ((long)(uint)voice.Generation << 32) | (uint)voice.ConsumedBuffers);
    }

    // Render thread. A region invalid against the clip (already warned about on the producer side) is the whole clip.
    private static void ApplyLoopRegion(ref MixerVoice voice, in AudioVoiceParameters parameters)
    {
        if (parameters.HasLoopRegion && IsValidRegion(parameters.LoopStartFrame, parameters.LoopEndFrame, voice.FrameCount))
        {
            voice.LoopStart = parameters.LoopStartFrame;
            voice.LoopEnd = parameters.LoopEndFrame;
        }
        else
        {
            voice.LoopStart = 0;
            voice.LoopEnd = voice.FrameCount;
        }
    }

    private static void SetParameters(ref MixerVoice voice, float volume, float pan, float pitch, bool immediate)
    {
        voice.Volume = volume;
        voice.Pan = pan;
        voice.Pitch = pitch;
        voice.Step = voice.SourceRatio * Math.Pow(2.0, pitch) * voice.RateMultiplier;

        float left;
        float right;

        if (voice.ExplicitGains)
        {
            voice.PanLeftFactor = voice.ExplicitLeft;
            voice.PanRightFactor = voice.ExplicitRight;
            left = volume * voice.ExplicitLeft;
            right = volume * voice.ExplicitRight;
        }
        else if (voice.SourceChannels == 1)
        {
            var angle = (pan + 1.0) * QuarterPi;
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);
            voice.PanLeftFactor = (float)cos;
            voice.PanRightFactor = (float)sin;
            left = (float)(volume * cos);
            right = (float)(volume * sin);
        }
        else
        {
            voice.PanLeftFactor = pan > 0f ? 1f - pan : 1f;
            voice.PanRightFactor = pan < 0f ? 1f + pan : 1f;
            left = pan > 0f ? volume * (1f - pan) : volume;
            right = pan < 0f ? volume * (1f + pan) : volume;
        }

        voice.TargetLeftGain = left;
        voice.TargetRightGain = right;

        if (immediate)
        {
            voice.CurrentLeftGain = left;
            voice.CurrentRightGain = right;
        }
    }

    private void ReleaseVoice(ref MixerVoice voice)
    {
        if (voice.IsStreaming)
        {
            if (voice.CurrentChunk != null)
            {
                _pool.ReturnFromRender(voice.CurrentChunk);
                voice.CurrentChunk = null;
            }

            var queue = voice.ChunkQueue;

            for (var i = 0; i < voice.QueueCount; i++)
            {
                var index = (voice.QueueHead + i) % queue.Length;
                _pool.ReturnFromRender(queue[index]);
                queue[index] = null;
            }

            voice.QueueCount = 0;
            voice.QueueHead = 0;
        }

        voice.Active = false;
        voice.RampActive = false;
        voice.ExplicitGains = false;
        voice.EndPending = false;
        voice.Started = false;
        voice.Paused = false;
        voice.IsStreaming = false;
        voice.Samples = null;
    }

    private void TryEmitEnded(int slot, ref MixerVoice voice)
    {
        var mixerEvent = new MixerEvent { Kind = MixerEventKind.VoiceEnded, Slot = slot, Generation = voice.Generation };

        if (_events.TryEnqueue(in mixerEvent))
        {
            voice.EndPending = false;
            voice.Active = false;
            voice.Samples = null;
        }
        else
        {
            // Event ring full: stay silent and retry at the next block rather than lose the end.
            voice.EndPending = true;
        }
    }

    private void RenderResident(int slot, ref MixerVoice voice, Span<float> output, int frameCount)
    {
        var data = voice.Samples;
        var channels = voice.SourceChannels;
        var frames = voice.FrameCount;
        var looped = voice.Looped;
        var loopStart = looped ? voice.LoopStart : 0;
        var loopEnd = looped ? voice.LoopEnd : frames;
        var loopLength = loopEnd - loopStart;
        var position = voice.Position;
        var step = voice.Step;

        var leftGain = voice.CurrentLeftGain;
        var rightGain = voice.CurrentRightGain;
        var leftIncrement = (voice.TargetLeftGain - leftGain) / frameCount;
        var rightIncrement = (voice.TargetRightGain - rightGain) / frameCount;
        var ramping = voice.RampActive;
        var rampStart = voice.RampValue;
        var rampIncrement = voice.RampIncrement;
        var rampTarget = voice.RampTarget;
        var rampLeft = voice.RampFramesLeft;
        var panLeft = voice.PanLeftFactor;
        var panRight = voice.PanRightFactor;
        var ended = frames == 0;

        for (var i = 0; i < frameCount && !ended; i++)
        {
            if (position >= loopEnd)
            {
                if (!looped)
                {
                    ended = true;
                    break;
                }

                position -= loopEnd;

                while (position >= loopLength)
                {
                    position -= loopLength;
                }

                position += loopStart;
            }

            var index = (int)position;
            var t = (float)(position - index);

            float l;
            float r;

            if (channels == 1)
            {
                var y0 = data[Neighbor(index - 1, index, frames, looped, loopStart, loopEnd)] * InverseShortRange;
                var y1 = data[index] * InverseShortRange;
                var y2 = data[Neighbor(index + 1, index, frames, looped, loopStart, loopEnd)] * InverseShortRange;
                var y3 = data[Neighbor(index + 2, index, frames, looped, loopStart, loopEnd)] * InverseShortRange;
                l = Hermite(y0, y1, y2, y3, t);
                r = l;
            }
            else
            {
                var i0 = Neighbor(index - 1, index, frames, looped, loopStart, loopEnd) * 2;
                var i1 = index * 2;
                var i2 = Neighbor(index + 1, index, frames, looped, loopStart, loopEnd) * 2;
                var i3 = Neighbor(index + 2, index, frames, looped, loopStart, loopEnd) * 2;
                l = Hermite(data[i0] * InverseShortRange, data[i1] * InverseShortRange, data[i2] * InverseShortRange, data[i3] * InverseShortRange, t);
                r = Hermite(data[i0 + 1] * InverseShortRange, data[i1 + 1] * InverseShortRange, data[i2 + 1] * InverseShortRange, data[i3 + 1] * InverseShortRange, t);
            }

            if (ramping)
            {
                var rampGain = (float)(i + 1 >= rampLeft ? rampTarget : rampStart + rampIncrement * (i + 1));
                leftGain = panLeft * rampGain;
                rightGain = panRight * rampGain;
            }
            else
            {
                leftGain += leftIncrement;
                rightGain += rightIncrement;
            }

            output[i * 2] += l * leftGain;
            output[i * 2 + 1] += r * rightGain;
            position += step;
        }

        voice.Position = position;
        voice.CurrentLeftGain = voice.TargetLeftGain;
        voice.CurrentRightGain = voice.TargetRightGain;

        if (ended)
        {
            TryEmitEnded(slot, ref voice);
        }
    }

    private void RenderStreaming(int slot, ref MixerVoice voice, Span<float> output, int frameCount)
    {
        var fraction = voice.StreamFraction;
        var step = voice.Step;
        var stereo = voice.SourceChannels == 2;

        var leftGain = voice.CurrentLeftGain;
        var rightGain = voice.CurrentRightGain;
        var leftIncrement = (voice.TargetLeftGain - leftGain) / frameCount;
        var rightIncrement = (voice.TargetRightGain - rightGain) / frameCount;
        var ramping = voice.RampActive;
        var rampStart = voice.RampValue;
        var rampIncrement = voice.RampIncrement;
        var rampTarget = voice.RampTarget;
        var rampLeft = voice.RampFramesLeft;
        var panLeft = voice.PanLeftFactor;
        var panRight = voice.PanRightFactor;

        for (var i = 0; i < frameCount; i++)
        {
            var underrun = false;

            while (fraction >= 1.0)
            {
                if (!PullFrame(slot, ref voice, out var pulledLeft, out var pulledRight))
                {
                    underrun = true;
                    break;
                }

                if (!voice.WindowStarted)
                {
                    voice.WindowStarted = true;
                    voice.L0 = voice.L1 = voice.L2 = voice.L3 = pulledLeft;
                    voice.R0 = voice.R1 = voice.R2 = voice.R3 = pulledRight;
                }
                else
                {
                    voice.L0 = voice.L1;
                    voice.L1 = voice.L2;
                    voice.L2 = voice.L3;
                    voice.L3 = pulledLeft;
                    voice.R0 = voice.R1;
                    voice.R1 = voice.R2;
                    voice.R2 = voice.R3;
                    voice.R3 = pulledRight;
                }

                fraction -= 1.0;
            }

            if (underrun)
            {
                break;
            }

            var t = (float)fraction;
            var l = Hermite(voice.L0, voice.L1, voice.L2, voice.L3, t);
            var r = stereo ? Hermite(voice.R0, voice.R1, voice.R2, voice.R3, t) : l;

            if (ramping)
            {
                var rampGain = (float)(i + 1 >= rampLeft ? rampTarget : rampStart + rampIncrement * (i + 1));
                leftGain = panLeft * rampGain;
                rightGain = panRight * rampGain;
            }
            else
            {
                leftGain += leftIncrement;
                rightGain += rightIncrement;
            }

            output[i * 2] += l * leftGain;
            output[i * 2 + 1] += r * rightGain;
            fraction += step;
        }

        voice.StreamFraction = fraction;
        voice.CurrentLeftGain = voice.TargetLeftGain;
        voice.CurrentRightGain = voice.TargetRightGain;
    }

    /// <summary>Reads the next source frame of a streaming voice; false when its queue ran dry.</summary>
    private bool PullFrame(int slot, ref MixerVoice voice, out float left, out float right)
    {
        while (true)
        {
            var chunk = voice.CurrentChunk;

            if (chunk == null)
            {
                if (voice.QueueCount == 0)
                {
                    left = 0f;
                    right = 0f;
                    return false;
                }

                var queue = voice.ChunkQueue;
                chunk = queue[voice.QueueHead];
                queue[voice.QueueHead] = null;
                voice.QueueHead = (voice.QueueHead + 1) % queue.Length;
                voice.QueueCount--;
                voice.CurrentChunk = chunk;
                voice.CurrentIndex = 0;
            }

            if (voice.CurrentIndex >= chunk.Count)
            {
                FinishChunk(slot, ref voice, chunk);
                continue;
            }

            var samples = chunk.Samples;
            var index = voice.CurrentIndex;
            left = samples[index] * InverseShortRange;

            if (voice.SourceChannels == 2)
            {
                right = samples[index + 1] * InverseShortRange;
                voice.CurrentIndex = index + 2;
            }
            else
            {
                right = left;
                voice.CurrentIndex = index + 1;
            }

            if (voice.CurrentIndex >= chunk.Count)
            {
                FinishChunk(slot, ref voice, chunk);
            }

            return true;
        }
    }

    private void FinishChunk(int slot, ref MixerVoice voice, SampleChunk chunk)
    {
        if (chunk.EndsBuffer)
        {
            voice.ConsumedBuffers++;
            PublishConsumed(slot, ref voice);

            var mixerEvent = new MixerEvent
            {
                Kind = MixerEventKind.BufferConsumed,
                Slot = slot,
                Generation = voice.Generation,
                Sequence = chunk.Sequence,
            };

            if (!_events.TryEnqueue(in mixerEvent))
            {
                Interlocked.Increment(ref _droppedEventCount);
            }
        }

        voice.CurrentChunk = null;
        voice.CurrentIndex = 0;
        _pool.ReturnFromRender(chunk);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // A looped voice wraps neighbours inside [loopStart, loopEnd[ once its base frame is inside it (the
    // whole clip without a region); before the region (the intro) a neighbour below 0 is clamped.
    private static int Neighbor(int index, int baseIndex, int frames, bool looped, int loopStart, int loopEnd)
    {
        if (looped)
        {
            if (index >= loopEnd)
            {
                return loopStart + ((index - loopEnd) % (loopEnd - loopStart));
            }

            if (index < loopStart && baseIndex >= loopStart)
            {
                var length = loopEnd - loopStart;
                var wrapped = (index - loopStart) % length;
                return loopStart + (wrapped < 0 ? wrapped + length : wrapped);
            }
        }

        return index < 0 ? 0 : index >= frames ? frames - 1 : index;
    }

    /// <summary>Catmull-Rom cubic through y1 (t = 0) and y2 (t = 1); exactly y1 at t = 0.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Hermite(float y0, float y1, float y2, float y3, float t)
    {
        var c1 = 0.5f * (y2 - y0);
        var c2 = y0 - 2.5f * y1 + 2f * y2 - 0.5f * y3;
        var c3 = 0.5f * (y3 - y0) + 1.5f * (y1 - y2);
        return ((c3 * t + c2) * t + c1) * t + y1;
    }

    #endregion

    /// <summary>Render-thread state of the gain ramp of one bus.</summary>
    private struct BusRamp
    {
        /// <summary>A ramp is running: the gain is interpolated per sample.</summary>
        public bool Active;

        /// <summary>The ramp ended or was frozen: the gain stays at <see cref="Value"/> until another gain is published.</summary>
        public bool Holding;

        /// <summary>Gain at the start of the current block (running), or the held gain.</summary>
        public double Value;

        public double Target;
        public double Increment;
        public int FramesLeft;

        /// <summary>Publish count of the bus when the ramp started; a later publish ends the ramp.</summary>
        public int SeenPublishCount;
    }
}
