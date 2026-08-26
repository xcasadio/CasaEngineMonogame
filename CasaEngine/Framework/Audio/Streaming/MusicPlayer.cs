namespace CasaEngine.Framework.Audio.Streaming;

/// <summary>
/// Plays streamed sounds (music, ambiences) by feeding decoded blocks to streaming voices.
/// </summary>
/// <remarks>
/// Several tracks can play at once, which is what makes a crossfade possible; each one gets its
/// own voice, so volume, pan and bus gain apply per track.
/// <para>
/// Blocks are read on the game thread from <see cref="Update"/>. For the sizes involved this is
/// marginal (a 22 kHz stereo 16 bit stream is about 88 KB/s), and it keeps the whole thing free
/// of threading. The queue is kept several hundred milliseconds deep so a long frame — shader
/// compilation, world load — does not starve the voice. Moving the reads to a background
/// producer is the natural next step if that ever becomes audible.
/// </para>
/// <para>
/// Looping is done by rewinding the reader: MonoGame refuses IsLooped on a dynamic voice.
/// </para>
/// </remarks>
public sealed class MusicPlayer : IDisposable
{
    /// <summary>About 185 ms of 22 kHz stereo 16 bit audio, and a multiple of every block size.</summary>
    public const int DefaultBufferSizeInBytes = 16384;

    /// <summary>Buffers kept queued ahead: roughly half a second of margin against a long frame.</summary>
    public const int DefaultQueuedBufferTarget = 3;

    private readonly AudioService _service;
    private readonly List<Track> _tracks = new();
    private readonly byte[] _scratchBuffer;
    private readonly int _queuedBufferTarget;
    private readonly AudioLogThrottle _log = new();

    private bool _isDisposed;

    public MusicPlayer(
        AudioService service,
        int bufferSizeInBytes = DefaultBufferSizeInBytes,
        int queuedBufferTarget = DefaultQueuedBufferTarget)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentOutOfRangeException.ThrowIfLessThan(bufferSizeInBytes, 1024);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(queuedBufferTarget);

        _service = service;
        _queuedBufferTarget = queuedBufferTarget;

        // SubmitBuffer copies the data, so one scratch array feeds every track.
        _scratchBuffer = new byte[bufferSizeInBytes];
    }

    /// <summary>Number of tracks currently loaded, playing or fading.</summary>
    public int ActiveTrackCount { get; private set; }

    /// <summary>
    /// Starts a streaming <see cref="SoundAsset"/>. Returns <see cref="MusicTrackHandle.None"/>
    /// when the asset is not streamable, its file cannot be opened, or no voice is available.
    /// </summary>
    /// <param name="fadeInSeconds">Zero starts at the asset volume, otherwise the volume ramps up.</param>
    public MusicTrackHandle Play(SoundAsset asset, float fadeInSeconds = 0f, object owner = null)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (_isDisposed)
        {
            return MusicTrackHandle.None;
        }

        if (!asset.IsStreaming)
        {
            _log.WriteWarning($"Audio: sound '{asset.Name}' is not marked as streaming, it cannot be played as music.");
            return MusicTrackHandle.None;
        }

        var reader = OpenReader(asset);
        if (reader == null)
        {
            return MusicTrackHandle.None;
        }

        var targetParameters = asset.CreateVoiceParameters();
        var startParameters = fadeInSeconds > 0f
            ? targetParameters.WithVolume(AudioVoiceParameters.MinVolume)
            : targetParameters;

        var voice = _service.PlayStream(
            reader.Format.SampleRate,
            reader.Format.ChannelCount,
            asset.BusName,
            startParameters,
            owner);

        if (!voice.IsValid)
        {
            reader.Dispose();
            return MusicTrackHandle.None;
        }

        var index = TakeTrackSlot();
        var track = _tracks[index];
        track.Reader = reader;
        track.Voice = voice;
        track.IsLooped = asset.IsLooped;
        track.IsFinishing = false;
        track.InUse = true;
        ActiveTrackCount++;

        // Queue ahead before starting, so the first frame cannot starve.
        FillQueue(track);
        _service.StartVoice(voice);

        if (fadeInSeconds > 0f)
        {
            _service.FadeVoice(voice, targetParameters.Volume, fadeInSeconds);
        }

        return new MusicTrackHandle(index, track.Generation);
    }

    /// <summary>Stops a track, immediately or after a fade out.</summary>
    public void Stop(MusicTrackHandle track, float fadeOutSeconds = 0f)
    {
        if (!TryGetTrack(track, out var entry))
        {
            return;
        }

        if (fadeOutSeconds > 0f)
        {
            // The voice is released at the end of the ramp; Update then drops the track.
            _service.StopWithFade(entry.Voice, fadeOutSeconds);
            return;
        }

        _service.Stop(entry.Voice);
        ReleaseTrack(entry);
    }

    /// <summary>Stops every track, immediately or after a fade out.</summary>
    public void StopAll(float fadeOutSeconds = 0f)
    {
        for (var i = 0; i < _tracks.Count; i++)
        {
            var entry = _tracks[i];
            if (!entry.InUse)
            {
                continue;
            }

            if (fadeOutSeconds > 0f)
            {
                _service.StopWithFade(entry.Voice, fadeOutSeconds);
                continue;
            }

            _service.Stop(entry.Voice);
            ReleaseTrack(entry);
        }
    }

    /// <summary>
    /// Fades <paramref name="from"/> out while fading a new track in, over the same duration.
    /// Both tracks play at once during the transition, which is exactly what MediaPlayer could
    /// not do.
    /// </summary>
    public MusicTrackHandle Crossfade(MusicTrackHandle from, SoundAsset to, float durationSeconds, object owner = null)
    {
        ArgumentNullException.ThrowIfNull(to);

        var next = Play(to, durationSeconds, owner);

        // If the new track could not start, keep the current one rather than leaving silence.
        if (next.IsValid)
        {
            Stop(from, durationSeconds);
        }

        return next;
    }

    public void Pause(MusicTrackHandle track)
    {
        if (TryGetTrack(track, out var entry))
        {
            _service.Pause(entry.Voice);
        }
    }

    public void Resume(MusicTrackHandle track)
    {
        if (TryGetTrack(track, out var entry))
        {
            _service.Resume(entry.Voice);
        }
    }

    public bool IsAlive(MusicTrackHandle track) => TryGetTrack(track, out _);

    public bool IsPlaying(MusicTrackHandle track)
        => TryGetTrack(track, out var entry) && _service.IsPlaying(entry.Voice);

    /// <summary>Volume asked for on that track, before the bus gain.</summary>
    public float GetVolume(MusicTrackHandle track)
        => TryGetTrack(track, out var entry) ? _service.GetVoiceVolume(entry.Voice) : 0f;

    /// <summary>Ramps the track volume, without stopping it.</summary>
    public void FadeVolume(MusicTrackHandle track, float targetVolume, float durationSeconds)
    {
        if (TryGetTrack(track, out var entry))
        {
            _service.FadeVoice(entry.Voice, targetVolume, durationSeconds);
        }
    }

    /// <summary>How far the decoder has read into the file. Loops back to zero on a rewind.</summary>
    public TimeSpan GetPosition(MusicTrackHandle track)
    {
        if (!TryGetTrack(track, out var entry) || entry.Reader.Format.BytesPerSecond <= 0)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromSeconds((double)entry.Reader.Position / entry.Reader.Format.BytesPerSecond);
    }

    public int GetPendingBufferCount(MusicTrackHandle track)
        => TryGetTrack(track, out var entry) ? _service.GetPendingBufferCount(entry.Voice) : 0;

    /// <summary>Tops up every queue and drops the tracks that reached their end.</summary>
    public void Update(float elapsedSeconds)
    {
        if (_isDisposed)
        {
            return;
        }

        for (var i = 0; i < _tracks.Count; i++)
        {
            var track = _tracks[i];
            if (!track.InUse)
            {
                continue;
            }

            // The voice may have been released elsewhere, typically at the end of a fade out.
            if (!_service.IsAlive(track.Voice))
            {
                ReleaseTrack(track);
                continue;
            }

            FillQueue(track);

            if (track.IsFinishing && _service.GetPendingBufferCount(track.Voice) == 0)
            {
                _service.Stop(track.Voice);
                ReleaseTrack(track);
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        StopAll();
        _isDisposed = true;
    }

    private void FillQueue(Track track)
    {
        while (!track.IsFinishing && _service.GetPendingBufferCount(track.Voice) < _queuedBufferTarget)
        {
            var read = track.Reader.Read(_scratchBuffer, 0, _scratchBuffer.Length);

            if (read == 0)
            {
                if (!track.IsLooped)
                {
                    track.IsFinishing = true;
                    return;
                }

                // Loop: rewind and keep feeding, without submitting a partial buffer first.
                track.Reader.Rewind();
                read = track.Reader.Read(_scratchBuffer, 0, _scratchBuffer.Length);

                if (read == 0)
                {
                    track.IsFinishing = true;
                    return;
                }
            }

            _service.SubmitStreamBuffer(track.Voice, _scratchBuffer, 0, read);
        }
    }

    private WavStreamReader OpenReader(SoundAsset asset)
    {
        var provider = _service.ClipProvider;
        if (provider == null)
        {
            _log.WriteWarning($"Audio: no clip provider is wired, music '{asset.Name}' cannot be played.");
            return null;
        }

        var stream = provider.OpenStream(asset.AudioFileAssetId);
        if (stream == null)
        {
            _log.WriteWarning($"Audio: the audio file of music '{asset.Name}' ({asset.AudioFileAssetId}) could not be opened.");
            return null;
        }

        try
        {
            return new WavStreamReader(stream);
        }
        catch (Exception exception)
        {
            stream.Dispose();
            _log.WriteError($"Audio: music '{asset.Name}' cannot be streamed. {exception.Message}");
            return null;
        }
    }

    private int TakeTrackSlot()
    {
        for (var i = 0; i < _tracks.Count; i++)
        {
            if (!_tracks[i].InUse)
            {
                return i;
            }
        }

        _tracks.Add(new Track());
        return _tracks.Count - 1;
    }

    private bool TryGetTrack(MusicTrackHandle handle, out Track track)
    {
        track = null;

        if (_isDisposed || !handle.IsValid || handle.Index >= _tracks.Count)
        {
            return false;
        }

        var candidate = _tracks[handle.Index];
        if (!candidate.InUse || candidate.Generation != handle.Generation)
        {
            return false;
        }

        track = candidate;
        return true;
    }

    private void ReleaseTrack(Track track)
    {
        if (!track.InUse)
        {
            return;
        }

        track.Reader?.Dispose();
        track.Reader = null;
        track.Voice = AudioVoiceHandle.None;
        track.InUse = false;
        track.IsFinishing = false;
        track.IsLooped = false;
        track.Generation++;
        ActiveTrackCount--;
    }

    private sealed class Track
    {
        public WavStreamReader Reader;
        public AudioVoiceHandle Voice;
        public bool IsLooped;
        public bool IsFinishing;
        public bool InUse;
        public int Generation;
    }
}
