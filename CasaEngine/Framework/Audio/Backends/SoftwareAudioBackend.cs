using System.Diagnostics;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio.Output;
using CasaEngine.Framework.Audio.Output.OpenAl;
using CasaEngine.Framework.Audio.Psx;
using CasaEngine.Framework.Audio.Software;

namespace CasaEngine.Framework.Audio.Backends;

/// <summary>
/// <see cref="IAudioBackend"/> implemented on the engine software mixer (ADR-0055): the game thread
/// owns a voice table and talks to <see cref="SoftwareMixer"/> through lock-free rings, while the
/// audio thread of an <see cref="IAudioOutput"/> renders the mix.
/// </summary>
/// <remarks>
/// <para>
/// Same observable contract as <see cref="MonoGameAudioBackend"/>, with two intended differences:
/// any positive sample rate is accepted for a streaming voice (no 8-48 kHz window), and a
/// <see cref="PcmAudioClip"/> of any rate plays (the mixer resamples).
/// </para>
/// <para>
/// Every public member must be called from the game thread. <see cref="GetState"/> stays
/// synchronous: each call drains the mixer events first (a resident voice that reached its end
/// becomes Stopped), without allocating. The pending buffer count is submitted minus the consumed count
/// that the audio thread publishes per slot (generation and count in one 64 bit value), so a lost event
/// cannot leave it too high.
/// </para>
/// <para>
/// Command ring full: a state-changing command that the mixer refuses is retried with a bounded
/// wait (<see cref="CommandRetryMilliseconds"/> at most), because the audio thread drains the ring
/// every few milliseconds. If the ring is still full a throttled error is logged and the command
/// is dropped; the local state is then left unchanged so it keeps matching the mixer. A Stop that
/// cannot be sent when a voice is released keeps its slot off the free list until it is resent. The only
/// command that is not retried is the volume update, which the per-frame fade ramps resend anyway.
/// A refused stream buffer (ring or chunk pool full) is dropped with a throttled warning.
/// </para>
/// <para>
/// When the output cannot be opened, or its audio thread dies later (one warning is logged), the
/// backend is unavailable and every call is a silent no-op; waiting on a full ring stops at once.
/// </para>
/// </remarks>
public sealed class SoftwareAudioBackend : IAudioBackend, IStereoVoiceBackend, IPsxSpuHost
{
    public const int DefaultVoiceCapacity = 64;

    /// <summary>Longest time a state-changing command waits for room in a full command ring.</summary>
    public const int CommandRetryMilliseconds = 100;

    private readonly VoiceSlot[] _slots;
    private readonly int[] _freeSlots;
    private readonly AudioLogThrottle _capacityLog = new();
    private readonly AudioLogThrottle _formatLog = new();
    private readonly AudioLogThrottle _ringLog = new();
    private readonly AudioLogThrottle _bufferLog = new();
    private readonly IAudioOutput _output;
    private readonly SoftwareMixer _mixer;
    private readonly AudioRenderCallback _renderCallback;

    // The one live SPU (attached to the mixer), and one whose Detach command is still to be sent (see ReleasePsxSpu).
    private PsxSpuSource _spuSource;
    private PsxSpuSource _spuDetachPending;

    private int _freeSlotCount;
    private int _pendingStopCount;
    private int _activeVoiceCount;
    private bool _isDisposed;
    private bool _outputDeathLogged;

    /// <summary>Opens the default OpenAL output.</summary>
    public SoftwareAudioBackend(int voiceCapacity = DefaultVoiceCapacity)
        : this(new OpenAlAudioOutput(), voiceCapacity)
    {
    }

    /// <summary>Takes ownership of <paramref name="output"/> (it is disposed with the backend).</summary>
    internal SoftwareAudioBackend(IAudioOutput output, int voiceCapacity = DefaultVoiceCapacity)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(voiceCapacity);

        _output = output;
        _slots = new VoiceSlot[voiceCapacity];
        _freeSlots = new int[voiceCapacity];

        for (var i = 0; i < voiceCapacity; i++)
        {
            _slots[i] = new VoiceSlot();
            // Filled in reverse so that the first Play takes slot 0.
            _freeSlots[i] = voiceCapacity - 1 - i;
        }

        _freeSlotCount = voiceCapacity;

        try
        {
            if (output.TryOpen())
            {
                _mixer = new SoftwareMixer(output.SampleRate, voiceCapacity);
                // Created once: the audio thread must not see a new delegate per frame.
                _renderCallback = _mixer.Render;
                output.Start(_renderCallback);
            }
        }
        catch (Exception exception)
        {
            // The backend must stay usable (silent) so the game keeps running without sound.
            Logs.WriteError($"Audio: the software audio backend could not start, sound is disabled. {exception}");
            _mixer = null;
            output.Dispose();
        }

        if (_mixer == null)
        {
            Logs.WriteWarning("Audio: no software audio output available, the game keeps running without sound.");
        }
    }

    /// <summary>
    /// True while the mixer exists and the output is still running. Once the audio thread died the
    /// backend stays silent for good (a device loss is not a death, see the output implementation).
    /// </summary>
    public bool IsAvailable => IsOutputAlive();

    public int VoiceCapacity => _slots.Length;

    public int ActiveVoiceCount => _activeVoiceCount;

    public bool SupportsStreaming => IsAvailable;

    /// <summary>Number of times the output ran out of queued audio.</summary>
    public int UnderrunCount => _output.UnderrunCount;

    /// <summary>Device sample rate in Hz, 0 when unavailable.</summary>
    public int OutputSampleRate => _mixer?.OutputSampleRate ?? 0;

    /// <summary>Audio queued ahead of the device (output latency), in milliseconds; 0 when unavailable.</summary>
    public int LeadMilliseconds
    {
        get
        {
            var rate = OutputSampleRate;
            return rate <= 0 ? 0 : (int)((long)_output.BufferCount * _output.BufferFrames * 1000L / rate);
        }
    }

    /// <summary>Stream chunks the mixer dropped because a voice queue was full.</summary>
    public int DroppedChunkCount => _mixer?.DroppedChunkCount ?? 0;

    /// <summary>Consumption reports the mixer lost because its event ring was full.</summary>
    public int DroppedEventCount => _mixer?.DroppedEventCount ?? 0;

    public AudioVoiceHandle Play(IAudioClip clip, in AudioVoiceParameters parameters)
    {
        return PlayResident(clip, in parameters, explicitGains: false, 0f, 0f);
    }

    /// <summary>
    /// Plays a mono <see cref="PcmAudioClip"/> with explicit channel gains, mixed on the audio thread at
    /// any clip rate (<see cref="IStereoVoiceBackend"/>). A clip that is not mono is refused with
    /// <see cref="AudioVoiceHandle.None"/>; a clip of another type throws like <see cref="Play"/>.
    /// </summary>
    public AudioVoiceHandle PlayStereo(IAudioClip clip, in AudioVoiceParameters parameters, float leftGain, float rightGain)
    {
        return PlayResident(clip, in parameters, explicitGains: true, leftGain, rightGain);
    }

    public void SetStereoGains(AudioVoiceHandle voice, float leftGain, float rightGain)
    {
        if (!TryGetSlot(voice, out var slot) || !slot.MixerAlive || !slot.ExplicitGains)
        {
            return;
        }

        var wait = new RingWait(_output);
        bool sent;
        while (!(sent = _mixer.TrySetStereoGains(voice.Index, slot.Generation, leftGain, rightGain)) && wait.Next())
        {
        }

        if (!sent)
        {
            ReportRingFull();
        }
    }

    private AudioVoiceHandle PlayResident(IAudioClip clip, in AudioVoiceParameters parameters, bool explicitGains, float leftGain, float rightGain)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (!IsOutputAlive())
        {
            return AudioVoiceHandle.None;
        }

        if (clip is not PcmAudioClip pcmClip)
        {
            throw new ArgumentException(
                $"{nameof(SoftwareAudioBackend)} only plays {nameof(PcmAudioClip)} instances, got '{clip.GetType().FullName}'.",
                nameof(clip));
        }

        if (pcmClip.IsDisposed)
        {
            return AudioVoiceHandle.None;
        }

        if (explicitGains && pcmClip.ChannelCount != 1)
        {
            if (_formatLog.ShouldWrite())
            {
                _formatLog.WriteNow("Audio: a stereo voice was refused, its clip is not mono.");
            }

            return AudioVoiceHandle.None;
        }

        var slotIndex = TakeFreeSlot();
        if (slotIndex < 0)
        {
            if (_capacityLog.ShouldWrite())
            {
                _capacityLog.WriteNow($"Audio: no free voice ({VoiceCapacity} in use), sound refused.");
            }

            return AudioVoiceHandle.None;
        }

        var slot = _slots[slotIndex];
        slot.Generation++;

        var wait = new RingWait(_output);
        bool sent;
        while (!(sent = explicitGains
                   ? _mixer.TryStartResidentStereoVoice(slotIndex, slot.Generation, pcmClip, parameters, leftGain, rightGain)
                   : _mixer.TryStartResidentVoice(slotIndex, slot.Generation, pcmClip, parameters)) && wait.Next())
        {
        }

        if (!sent)
        {
            ReportRingFull();
            ReturnSlot(slotIndex);
            return AudioVoiceHandle.None;
        }

        slot.InUse = true;
        slot.IsStreaming = false;
        slot.ExplicitGains = explicitGains;
        slot.MixerAlive = true;
        slot.State = AudioVoiceState.Playing;
        slot.SubmittedBuffers = 0;
        _activeVoiceCount++;
        return new AudioVoiceHandle(slotIndex, slot.Generation);
    }

    public void SetParameters(AudioVoiceHandle voice, in AudioVoiceParameters parameters)
    {
        if (!TryGetSlot(voice, out var slot) || !slot.MixerAlive)
        {
            return;
        }

        var wait = new RingWait(_output);
        bool sent;
        while (!(sent = _mixer.TrySetParameters(voice.Index, slot.Generation, parameters)) && wait.Next())
        {
        }

        if (!sent)
        {
            ReportRingFull();
        }
    }

    public void SetVolume(AudioVoiceHandle voice, float volume)
    {
        if (!TryGetSlot(voice, out var slot) || !slot.MixerAlive)
        {
            return;
        }

        // Not retried: called every frame by the fade ramps, which resend the next value.
        if (!_mixer.TrySetVolume(voice.Index, slot.Generation, volume))
        {
            ReportRingFull();
        }
    }

    public AudioVoiceState GetState(AudioVoiceHandle voice)
    {
        return TryGetSlot(voice, out var slot) ? slot.State : AudioVoiceState.Stopped;
    }

    public void Pause(AudioVoiceHandle voice)
    {
        if (TryGetSlot(voice, out var slot) && slot.State == AudioVoiceState.Playing
            && SendSimple(MixerCommandKind.Pause, voice.Index, slot.Generation))
        {
            slot.State = AudioVoiceState.Paused;
        }
    }

    public void Resume(AudioVoiceHandle voice)
    {
        if (TryGetSlot(voice, out var slot) && slot.State == AudioVoiceState.Paused
            && SendSimple(MixerCommandKind.Resume, voice.Index, slot.Generation))
        {
            slot.State = AudioVoiceState.Playing;
        }
    }

    public void Stop(AudioVoiceHandle voice)
    {
        if (TryGetSlot(voice, out var slot) && slot.State != AudioVoiceState.Stopped)
        {
            StopSlot(voice.Index, slot);
        }
    }

    public void Release(AudioVoiceHandle voice)
    {
        if (!TryGetSlot(voice, out var slot))
        {
            return;
        }

        ReleaseSlot(voice.Index, slot, wait: true);
    }

    public void StopAll()
    {
        if (!IsOutputAlive())
        {
            return;
        }

        var stopAllSent = SendSimple(MixerCommandKind.StopAll, 0, 0);

        for (var i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].InUse)
            {
                // StopAll refused: every slot still sounding goes through the pending stop path.
                ReleaseSlot(i, _slots[i], wait: false, stopAlreadySent: stopAllSent);
            }
        }
    }

    public AudioVoiceHandle CreateStreamingVoice(int sampleRate, int channelCount, in AudioVoiceParameters parameters)
    {
        if (!IsOutputAlive())
        {
            return AudioVoiceHandle.None;
        }

        if (channelCount is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(channelCount), channelCount, "Only mono and stereo are supported.");
        }

        if (sampleRate <= 0)
        {
            if (_formatLog.ShouldWrite())
            {
                _formatLog.WriteNow($"Audio: stream refused, its sample rate ({sampleRate} Hz) is not positive.");
            }

            return AudioVoiceHandle.None;
        }

        var slotIndex = TakeFreeSlot();
        if (slotIndex < 0)
        {
            if (_capacityLog.ShouldWrite())
            {
                _capacityLog.WriteNow($"Audio: no free voice ({VoiceCapacity} in use), stream refused.");
            }

            return AudioVoiceHandle.None;
        }

        var slot = _slots[slotIndex];
        slot.Generation++;

        var wait = new RingWait(_output);
        bool sent;
        while (!(sent = _mixer.TryCreateStreamingVoice(slotIndex, slot.Generation, channelCount, sampleRate, parameters))
               && wait.Next())
        {
        }

        if (!sent)
        {
            ReportRingFull();
            ReturnSlot(slotIndex);
            return AudioVoiceHandle.None;
        }

        slot.InUse = true;
        slot.IsStreaming = true;
        slot.ExplicitGains = false;
        slot.MixerAlive = true;
        slot.CanStart = true;
        slot.State = AudioVoiceState.Stopped;
        slot.SubmittedBuffers = 0;
        _activeVoiceCount++;
        return new AudioVoiceHandle(slotIndex, slot.Generation);
    }

    public void SubmitBuffer(AudioVoiceHandle voice, byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (!TryGetSlot(voice, out var slot) || !slot.IsStreaming || !slot.MixerAlive || count <= 0)
        {
            return;
        }

        // The mixer copies the data into pooled chunks, so the caller can reuse its array.
        var pcm = new ReadOnlySpan<byte>(buffer, offset, count);

        if (_mixer.TrySubmitStreamingBuffer(voice.Index, slot.Generation, pcm, slot.NextSequence))
        {
            slot.NextSequence++;
            slot.SubmittedBuffers++;
        }
        else if (_bufferLog.ShouldWrite())
        {
            _bufferLog.WriteNow("Audio: the mixer refused a stream buffer (command ring or chunk pool full), it was dropped.");
        }
    }

    public int GetPendingBufferCount(AudioVoiceHandle voice)
    {
        if (!TryGetSlot(voice, out var slot) || !slot.IsStreaming)
        {
            return 0;
        }

        // Submitted minus consumed, the consumed count being published by the audio thread for this
        // generation only (0 until it applied the creation). It never relies on events.
        var pending = slot.SubmittedBuffers - _mixer.GetConsumedBufferCount(voice.Index, slot.Generation);
        return pending > 0 ? pending : 0;
    }

    public void Start(AudioVoiceHandle voice)
    {
        if (TryGetSlot(voice, out var slot) && slot.IsStreaming && slot.CanStart && slot.State == AudioVoiceState.Stopped
            && SendSimple(MixerCommandKind.StartStreaming, voice.Index, slot.Generation))
        {
            slot.State = AudioVoiceState.Playing;
        }
    }

    /// <summary>
    /// Creates the one live SPU (<see cref="IPsxSpuHost"/>). The SPU, its rings and its buffers are allocated here;
    /// the attach order waits for room in the command ring like a voice start. Fails when an SPU is alive or when
    /// the detach of the previous one could not be sent yet.
    /// </summary>
    public bool TryCreatePsxSpu(PsxSpuHardwareTables tables, out PsxSpuPort port)
    {
        ArgumentNullException.ThrowIfNull(tables);
        port = null;

        if (!IsOutputAlive())
        {
            return false;
        }

        RetryPendingStops();

        if (_spuSource != null || _spuDetachPending != null)
        {
            return false;
        }

        var source = new PsxSpuSource(tables);
        var command = new MixerCommand { Kind = MixerCommandKind.AttachPsxSpu, Spu = source };
        var wait = new RingWait(_output);
        bool sent;
        while (!(sent = _mixer.TryEnqueueCommand(in command)) && wait.Next())
        {
        }

        if (!sent)
        {
            ReportRingFull();
            return false;
        }

        _spuSource = source;
        port = new PsxSpuPort(source, this);
        return true;
    }

    /// <summary>
    /// Called by <see cref="PsxSpuPort.Dispose"/>: sends the Detach command, with the bounded wait of a Stop; when
    /// the ring stays full the order is kept and resent by <see cref="RetryPendingStops"/>, so it is never lost.
    /// </summary>
    internal void ReleasePsxSpu(PsxSpuSource source)
    {
        if (_isDisposed || !ReferenceEquals(_spuSource, source))
        {
            return;
        }

        _spuSource = null;

        if (_mixer == null || !_output.IsAvailable)
        {
            return;
        }

        var command = new MixerCommand { Kind = MixerCommandKind.DetachPsxSpu, Spu = source };
        var wait = new RingWait(_output);
        bool sent;
        while (!(sent = _mixer.TryEnqueueCommand(in command)) && wait.Next())
        {
        }

        if (!sent)
        {
            ReportRingFull();
            _spuDetachPending = source;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        // The output first: it joins the audio thread, so nothing renders while the state is released.
        _isDisposed = true;
        _output.Dispose();
        _spuSource = null;
        _spuDetachPending = null;

        for (var i = 0; i < _slots.Length; i++)
        {
            _slots[i].Reset();
        }

        _activeVoiceCount = 0;
        _freeSlotCount = 0;
        _pendingStopCount = 0;
    }

    private void StopSlot(int slotIndex, VoiceSlot slot)
    {
        if (SendSimple(MixerCommandKind.Stop, slotIndex, slot.Generation))
        {
            slot.State = AudioVoiceState.Stopped;
            slot.MixerAlive = false;
            slot.CanStart = false;
            slot.SubmittedBuffers = 0;
        }
    }

    /// <summary>
    /// Frees the slot once the mixer was told to stop its voice. When the Stop command cannot be
    /// enqueued the slot stays off the free list in a pending stop state, so a new voice can never
    /// share it with one that still sounds; <see cref="RetryPendingStops"/> resends the order.
    /// </summary>
    private void ReleaseSlot(int slotIndex, VoiceSlot slot, bool wait, bool stopAlreadySent = false)
    {
        if (!slot.MixerAlive || stopAlreadySent)
        {
            ReturnSlot(slotIndex);
            return;
        }

        var sent = wait ? SendSimple(MixerCommandKind.Stop, slotIndex, slot.Generation) : TryEnqueueSimple(MixerCommandKind.Stop, slotIndex, slot.Generation);
        if (sent)
        {
            ReturnSlot(slotIndex);
            return;
        }

        // The handle becomes stale at once (InUse false); the slot keeps its generation for the retry.
        if (slot.InUse)
        {
            _activeVoiceCount--;
        }

        slot.InUse = false;
        slot.State = AudioVoiceState.Stopped;
        slot.MixerAlive = false;
        slot.CanStart = false;
        slot.SubmittedBuffers = 0;
        slot.StopPending = true;
        _pendingStopCount++;
    }

    // Cheap when nothing is pending (one int compare). No wait: a full ring is simply tried again at the next call.
    private void RetryPendingStops()
    {
        if (_spuDetachPending != null)
        {
            var command = new MixerCommand { Kind = MixerCommandKind.DetachPsxSpu, Spu = _spuDetachPending };
            if (_mixer.TryEnqueueCommand(in command))
            {
                _spuDetachPending = null;
            }
        }

        if (_pendingStopCount == 0)
        {
            return;
        }

        for (var i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];

            if (slot.StopPending && TryEnqueueSimple(MixerCommandKind.Stop, i, slot.Generation))
            {
                _pendingStopCount--;
                ReturnSlot(i);
            }
        }
    }

    // The audio thread is gone: nothing renders any more, so slots waiting for a stop are free.
    private void FreePendingStopsAfterDeath()
    {
        for (var i = 0; _pendingStopCount > 0 && i < _slots.Length; i++)
        {
            if (_slots[i].StopPending)
            {
                _pendingStopCount--;
                ReturnSlot(i);
            }
        }
    }

    private bool TryEnqueueSimple(MixerCommandKind kind, int slotIndex, int generation)
    {
        var command = new MixerCommand { Kind = kind, Slot = slotIndex, Generation = generation };
        return _mixer.TryEnqueueCommand(in command);
    }

    private bool SendSimple(MixerCommandKind kind, int slotIndex, int generation)
    {
        var command = new MixerCommand { Kind = kind, Slot = slotIndex, Generation = generation };
        var wait = new RingWait(_output);
        bool sent;
        while (!(sent = _mixer.TryEnqueueCommand(in command)) && wait.Next())
        {
        }

        if (!sent)
        {
            ReportRingFull();
        }

        return sent;
    }

    // Cheap: two field reads. The first time the output is found dead one warning is logged.
    private bool IsOutputAlive()
    {
        if (_isDisposed || _mixer == null)
        {
            return false;
        }

        if (_output.IsAvailable)
        {
            return true;
        }

        if (!_outputDeathLogged)
        {
            _outputDeathLogged = true;
            Logs.WriteWarning("Audio: the audio output thread stopped, the software audio backend is now silent.");
            FreePendingStopsAfterDeath();
        }

        return false;
    }

    private void ReportRingFull()
    {
        if (_ringLog.ShouldWrite())
        {
            _ringLog.WriteNow("Audio: the mixer command ring stayed full, a command was dropped.", isError: true);
        }
    }

    private int TakeFreeSlot()
    {
        RetryPendingStops();

        if (_freeSlotCount == 0)
        {
            return -1;
        }

        _freeSlotCount--;
        return _freeSlots[_freeSlotCount];
    }

    private void ReturnSlot(int slotIndex)
    {
        var slot = _slots[slotIndex];

        if (slot.InUse)
        {
            _activeVoiceCount--;
        }

        slot.Reset();
        // The old handle becomes stale; the mixer ignores late commands and events of the old generation.
        slot.Generation++;
        _freeSlots[_freeSlotCount] = slotIndex;
        _freeSlotCount++;
    }

    private bool TryGetSlot(AudioVoiceHandle voice, out VoiceSlot slot)
    {
        slot = null;

        if (!IsOutputAlive() || !voice.IsValid || voice.Index >= _slots.Length)
        {
            return false;
        }

        RetryPendingStops();
        DrainEvents();

        var candidate = _slots[voice.Index];
        if (!candidate.InUse || candidate.Generation != voice.Generation)
        {
            return false;
        }

        slot = candidate;
        return true;
    }

    // Allocation free: MixerEvent is a struct filled by the ring.
    private void DrainEvents()
    {
        while (_mixer.TryDequeueEvent(out var mixerEvent))
        {
            if ((uint)mixerEvent.Slot >= (uint)_slots.Length)
            {
                continue;
            }

            var slot = _slots[mixerEvent.Slot];
            // Only voice ends matter here; consumed buffers are read from the published counter.
            if (!slot.InUse || slot.Generation != mixerEvent.Generation)
            {
                continue;
            }

            if (mixerEvent.Kind == MixerEventKind.VoiceEnded)
            {
                slot.State = AudioVoiceState.Stopped;
                slot.MixerAlive = false;
                slot.CanStart = false;
            }
        }
    }

    private sealed class VoiceSlot
    {
        public int Generation;
        public bool InUse;
        public bool IsStreaming;

        /// <summary>The voice was started by <see cref="PlayStereo"/>.</summary>
        public bool ExplicitGains;

        /// <summary>False once the mixer no longer holds the voice (ended or stopped): commands would be ignored.</summary>
        public bool MixerAlive;

        /// <summary>True for a created streaming voice that was never stopped or ended, so Start still applies.</summary>
        public bool CanStart;

        public AudioVoiceState State;

        /// <summary>Buffers accepted by the mixer for this voice; pending = this minus the consumed counter.</summary>
        public int SubmittedBuffers;

        /// <summary>The voice was released but its Stop command is still to be sent: the slot is off the free list.</summary>
        public bool StopPending;
        public int NextSequence;

        public void Reset()
        {
            InUse = false;
            IsStreaming = false;
            ExplicitGains = false;
            MixerAlive = false;
            CanStart = false;
            State = AudioVoiceState.Stopped;
            SubmittedBuffers = 0;
            StopPending = false;
            NextSequence = 0;
        }
    }

    // Bounded wait for room in the command ring; a struct so the retry path allocates nothing.
    private struct RingWait
    {
        private readonly IAudioOutput _output;
        private long _deadline;
        private SpinWait _spin;
        private bool _started;

        public RingWait(IAudioOutput output)
        {
            _output = output;
        }

        public bool Next()
        {
            // A dead output never drains the ring: give up at once instead of spinning.
            if (!_output.IsAvailable)
            {
                return false;
            }

            if (!_started)
            {
                _started = true;
                _deadline = Stopwatch.GetTimestamp() + CommandRetryMilliseconds * Stopwatch.Frequency / 1000;
            }
            else if (Stopwatch.GetTimestamp() >= _deadline)
            {
                return false;
            }

            _spin.SpinOnce();
            return true;
        }
    }
}
