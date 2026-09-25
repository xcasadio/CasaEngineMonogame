using System.Buffers.Binary;

namespace CasaEngine.Framework.Audio.Streaming;

/// <summary>
/// Feeds every software stereo voice started by <see cref="AudioService.PlayClipStereo"/>,
/// buffer by buffer, from <see cref="AudioService.Update"/> (ADR-0039). Modeled on
/// <see cref="MusicPlayer"/>: reads happen on the game thread, and no MonoGame type appears here.
/// </summary>
/// <remarks>
/// A mono clip is turned into interleaved stereo 16 bit PCM at a rate MonoGame's streaming
/// voices accept (8 000-48 000 Hz): unchanged in that range, linearly interpolated below it,
/// averaged in blocks above it. Every output frame is
/// <c>left = round(sample * leftGain)</c>, <c>right = round(sample * rightGain)</c>, computed in
/// double precision from the (possibly resampled, unrounded) source value and rounded once, so a live
/// gain change (<see cref="SetGains"/>) only reaches the buffers not yet submitted — about
/// 60 ms of latency at the default queue depth.
/// <para>
/// <see cref="Update"/> submits at most <see cref="MaxBuffersPerUpdate"/> buffers per voice per
/// call, which bounds the loop even against a backend whose
/// <see cref="IAudioBackend.GetPendingBufferCount"/> never reports what was queued.
/// </para>
/// </remarks>
internal sealed class StereoVoiceMixer
{
    /// <summary>16 bit stereo: 2 channels * 2 bytes.</summary>
    private const int BytesPerFrame = 4;

    /// <summary>Highest output rate a MonoGame streaming voice accepts; bounds the scratch buffer.</summary>
    private const int MaxOutputSampleRate = 48000;

    /// <summary>Denominator used to size a buffer at about 20 ms of audio.</summary>
    private const int BufferFramesPerSecondDivisor = 50;

    /// <summary>Buffers kept queued ahead, like <see cref="MusicPlayer.DefaultQueuedBufferTarget"/>.</summary>
    private const int QueuedBufferTarget = 3;

    /// <summary>
    /// Hard cap on buffers submitted per voice per <see cref="Update"/> call, so a backend that
    /// never reports queued buffers cannot make the fill loop spin forever.
    /// </summary>
    private const int MaxBuffersPerUpdate = 3;

    private readonly AudioService _service;
    private readonly List<Entry> _entries = new();

    // SubmitStreamBuffer copies the data, so one scratch array feeds every voice.
    private readonly byte[] _scratchBuffer = new byte[(MaxOutputSampleRate / BufferFramesPerSecondDivisor) * BytesPerFrame];

    public StereoVoiceMixer(AudioService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <summary>
    /// Starts a software stereo voice for <paramref name="clip"/>. The caller is responsible for
    /// zeroing Pan and Pitch on <paramref name="parameters"/> first: this only resamples, mixes
    /// the gains and applies Volume/IsLooped/bus through the regular streaming voice path.
    /// </summary>
    public AudioVoiceHandle Play(
        IAudioClipSamples clip,
        string busName,
        in AudioVoiceParameters parameters,
        float leftGain,
        float rightGain,
        object owner)
    {
        ComputeResampling(clip.SampleRate, out var mode, out var factor, out var outputSampleRate);

        var voice = _service.PlayStream(outputSampleRate, 2, busName, parameters, owner);
        if (!voice.IsValid)
        {
            return AudioVoiceHandle.None;
        }

        var entry = GetOrCreateEntry(voice.Index);
        entry.Voice = voice;
        entry.Samples = clip.MonoSamples;
        entry.SampleCount = clip.MonoSamples.Length;
        entry.Mode = mode;
        entry.Factor = factor;
        entry.SourceIndex = 0;
        entry.Phase = 0;
        entry.BufferFrames = Math.Max(1, outputSampleRate / BufferFramesPerSecondDivisor);
        entry.LeftGain = leftGain;
        entry.RightGain = rightGain;
        entry.IsLooped = parameters.IsLooped;
        entry.IsFinishing = false;
        entry.InUse = true;

        // Queue ahead before starting, so the first frame cannot starve.
        FillQueue(entry);
        _service.StartVoice(voice);

        return voice;
    }

    /// <summary>
    /// Changes the gains of an already-playing stereo voice. Buffers already submitted keep
    /// their gain; only the ones filled after this call use the new one.
    /// </summary>
    public void SetGains(AudioVoiceHandle voice, float leftGain, float rightGain)
    {
        if (TryGetEntry(voice, out var entry))
        {
            entry.LeftGain = leftGain;
            entry.RightGain = rightGain;
        }
    }

    /// <summary>False, with zeroed gains, for a stale or non-stereo handle.</summary>
    public bool TryGetGains(AudioVoiceHandle voice, out float leftGain, out float rightGain)
    {
        if (TryGetEntry(voice, out var entry))
        {
            leftGain = entry.LeftGain;
            rightGain = entry.RightGain;
            return true;
        }

        leftGain = 0f;
        rightGain = 0f;
        return false;
    }

    /// <summary>Tops up every queue and releases the voices that finished or were stopped elsewhere.</summary>
    public void Update(float elapsedSeconds)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            if (!entry.InUse)
            {
                continue;
            }

            // The voice may have been released elsewhere: StopVoicesOwnedBy, StopAll, a fade out.
            if (!_service.IsAlive(entry.Voice))
            {
                ReleaseEntry(entry);
                continue;
            }

            FillQueue(entry);

            if (entry.IsFinishing && _service.GetPendingBufferCount(entry.Voice) == 0)
            {
                _service.Stop(entry.Voice);
                ReleaseEntry(entry);
            }
        }
    }

    private void FillQueue(Entry entry)
    {
        var submitted = 0;

        while (!entry.IsFinishing
               && submitted < MaxBuffersPerUpdate
               && _service.GetPendingBufferCount(entry.Voice) < QueuedBufferTarget)
        {
            var framesWritten = WriteFrames(entry, entry.BufferFrames);
            if (framesWritten == 0)
            {
                entry.IsFinishing = true;
                break;
            }

            _service.SubmitStreamBuffer(entry.Voice, _scratchBuffer, 0, framesWritten * BytesPerFrame);
            submitted++;

            if (framesWritten < entry.BufferFrames)
            {
                // Not looped, and the clip just ran out in the middle of this buffer.
                entry.IsFinishing = true;
            }
        }
    }

    /// <summary>
    /// Writes up to <paramref name="frameCount"/> interleaved stereo frames to the scratch
    /// buffer and advances the entry's read position. Returns the number of frames actually
    /// written, which is less than requested only at the end of a non-looped clip.
    /// </summary>
    private int WriteFrames(Entry entry, int frameCount)
    {
        var samples = entry.Samples.Span;
        var written = 0;

        while (written < frameCount)
        {
            if (entry.SourceIndex >= entry.SampleCount)
            {
                if (!entry.IsLooped)
                {
                    break;
                }

                entry.SourceIndex = 0;
                entry.Phase = 0;
            }

            var source = entry.Mode switch
            {
                ResampleMode.Upsample => InterpolateUpsampled(entry, samples),
                ResampleMode.Downsample => AverageDownsampled(entry, samples),
                _ => ReadDirect(entry, samples),
            };

            // One rounding only, after the gain: the resampled value stays unrounded until here.
            var offset = written * BytesPerFrame;
            BinaryPrimitives.WriteInt16LittleEndian(_scratchBuffer.AsSpan(offset, 2), ClampRound(source * (double)entry.LeftGain));
            BinaryPrimitives.WriteInt16LittleEndian(_scratchBuffer.AsSpan(offset + 2, 2), ClampRound(source * (double)entry.RightGain));
            written++;
        }

        return written;
    }

    private static double ReadDirect(Entry entry, ReadOnlySpan<short> samples)
    {
        var value = samples[entry.SourceIndex];
        entry.SourceIndex++;
        return value;
    }

    private static double InterpolateUpsampled(Entry entry, ReadOnlySpan<short> samples)
    {
        var current = samples[entry.SourceIndex];
        var nextIndex = entry.SourceIndex + 1;
        var next = nextIndex < entry.SampleCount
            ? samples[nextIndex]
            : entry.IsLooped ? samples[0] : current;

        var t = entry.Phase / (double)entry.Factor;
        var value = current + ((next - current) * t);

        entry.Phase++;
        if (entry.Phase >= entry.Factor)
        {
            entry.Phase = 0;
            entry.SourceIndex++;
        }

        return value;
    }

    private static double AverageDownsampled(Entry entry, ReadOnlySpan<short> samples)
    {
        var group = Math.Min(entry.Factor, entry.SampleCount - entry.SourceIndex);
        long sum = 0;
        for (var i = 0; i < group; i++)
        {
            sum += samples[entry.SourceIndex + i];
        }

        entry.SourceIndex += group;
        return sum / (double)group;
    }

    private static short ClampRound(double value)
    {
        var rounded = Math.Round(value, MidpointRounding.AwayFromZero);

        if (rounded >= short.MaxValue)
        {
            return short.MaxValue;
        }

        if (rounded <= short.MinValue)
        {
            return short.MinValue;
        }

        return (short)rounded;
    }

    /// <summary>
    /// Picks how <paramref name="sourceSampleRate"/> reaches a rate MonoGame accepts: unchanged
    /// in [8 000, 48 000] Hz; below it, the smallest integer factor that reaches 8 000 Hz by
    /// linear interpolation; above it, the smallest integer factor that reaches at most
    /// 48 000 Hz by averaging that many source samples per output sample.
    /// </summary>
    private static void ComputeResampling(int sourceSampleRate, out ResampleMode mode, out int factor, out int outputSampleRate)
    {
        if (sourceSampleRate is >= 8000 and <= MaxOutputSampleRate)
        {
            mode = ResampleMode.None;
            factor = 1;
            outputSampleRate = sourceSampleRate;
            return;
        }

        if (sourceSampleRate < 8000)
        {
            var upsampleFactor = 1;
            while (sourceSampleRate * upsampleFactor < 8000)
            {
                upsampleFactor++;
            }

            mode = ResampleMode.Upsample;
            factor = upsampleFactor;
            outputSampleRate = sourceSampleRate * upsampleFactor;
            return;
        }

        var downsampleFactor = 1;
        while (sourceSampleRate / downsampleFactor > MaxOutputSampleRate)
        {
            downsampleFactor++;
        }

        mode = ResampleMode.Downsample;
        factor = downsampleFactor;
        outputSampleRate = (int)Math.Round(sourceSampleRate / (double)downsampleFactor);
    }

    private Entry GetOrCreateEntry(int index)
    {
        while (_entries.Count <= index)
        {
            _entries.Add(new Entry());
        }

        return _entries[index];
    }

    private bool TryGetEntry(AudioVoiceHandle voice, out Entry entry)
    {
        entry = null;

        if (!voice.IsValid || voice.Index >= _entries.Count)
        {
            return false;
        }

        var candidate = _entries[voice.Index];
        if (!candidate.InUse || candidate.Voice != voice)
        {
            return false;
        }

        entry = candidate;
        return true;
    }

    private static void ReleaseEntry(Entry entry)
    {
        entry.InUse = false;
        entry.Voice = AudioVoiceHandle.None;
        entry.Samples = ReadOnlyMemory<short>.Empty;
        entry.SampleCount = 0;
    }

    private enum ResampleMode
    {
        None,
        Upsample,
        Downsample,
    }

    // Class rather than struct: entries are mutated in place through the list.
    private sealed class Entry
    {
        public AudioVoiceHandle Voice;
        public ReadOnlyMemory<short> Samples;
        public int SampleCount;
        public ResampleMode Mode;
        public int Factor;
        public int SourceIndex;
        public int Phase;
        public int BufferFrames;
        public float LeftGain;
        public float RightGain;
        public bool IsLooped;
        public bool InUse;
        public bool IsFinishing;
    }
}
