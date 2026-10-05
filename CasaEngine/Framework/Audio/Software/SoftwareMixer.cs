using System.Buffers.Binary;
using System.Runtime.CompilerServices;

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
    }

    /// <summary>Routes the attached SPU to a bus, applied in order with the other commands.</summary>
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
    // added to its parent buffer; Master is scaled into the output. The per-bus buffers are also where
    // insert effects will sit, between the voices and this scaling.
    private void MixBuses(Span<float> output, int frameCount)
    {
        var sampleCount = frameCount * 2;

        for (var b = _busCount - 1; b >= 0; b--)
        {
            var source = BusBuffer(b, sampleCount);
            var target = BitConverter.Int32BitsToSingle(Volatile.Read(ref _busGainBits[b]));
            var gain = _busAppliedGain[b];
            var increment = (target - gain) / frameCount;

            if (b == MasterBus)
            {
                for (var i = 0; i < sampleCount; i += 2)
                {
                    gain += increment;
                    output[i] = source[i] * gain;
                    output[i + 1] = source[i + 1] * gain;
                }
            }
            else
            {
                var destination = BusBuffer(_busParent[b], sampleCount);

                for (var i = 0; i < sampleCount; i += 2)
                {
                    gain += increment;
                    destination[i] += source[i] * gain;
                    destination[i + 1] += source[i + 1] * gain;
                }
            }

            _busAppliedGain[b] = target;
        }
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

                SetParameters(ref voice, command.Parameters.Volume, command.Parameters.Pan, command.Parameters.Pitch, false);
                break;
            case MixerCommandKind.SetVolume:
                SetParameters(ref voice, command.Volume, voice.Pan, voice.Pitch, false);
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
            left = volume * voice.ExplicitLeft;
            right = volume * voice.ExplicitRight;
        }
        else if (voice.SourceChannels == 1)
        {
            var angle = (pan + 1.0) * QuarterPi;
            left = (float)(volume * Math.Cos(angle));
            right = (float)(volume * Math.Sin(angle));
        }
        else
        {
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

            leftGain += leftIncrement;
            rightGain += rightIncrement;
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

            leftGain += leftIncrement;
            rightGain += rightIncrement;
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
}
