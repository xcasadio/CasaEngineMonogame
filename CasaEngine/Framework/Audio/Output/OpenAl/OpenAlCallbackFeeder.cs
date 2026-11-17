using System.Runtime.InteropServices;

namespace CasaEngine.Framework.Audio.Output.OpenAl;

/// <summary>
/// The body of the callback of an OpenAL Soft callback buffer (AL_SOFT_callback_buffer, ADR-0094): renders the
/// mixer into the memory OpenAL Soft hands over, estimates underruns from the spacing of the calls, and, once the
/// device is lost, renders the mixer at the device rate into a discarded buffer. No native code: the memory to
/// fill is an <see cref="IntPtr"/> and the clock is injected, so a test drives it without a device.
/// </summary>
/// <remarks>
/// <see cref="Fill"/> runs on a native real-time thread: it never allocates, blocks, logs, calls AL or ALC, or lets an
/// exception out. A render that throws is kept in <see cref="Failure"/> for the audio thread; from then on every
/// call gives silence. One render runs at a time: a call that finds the render taken gives silence.
/// </remarks>
internal sealed class OpenAlCallbackFeeder
{
    /// <summary>Size of one stereo float frame.</summary>
    public const int BytesPerFrame = 8;

    private readonly AudioRenderCallback _render;
    private readonly int _sampleRate;
    private readonly int _blockFrames;
    private readonly long _ticksPerSecond;
    private readonly Func<long> _timestamp;
    // Rendered blocks, reused; only touched while the render flag is held.
    private readonly float[] _scratch;
    // Never written: the source of every zero fill.
    private readonly float[] _silence;
    private int _rendering;
    private Exception _failure;

    // Written by the callback only.
    private long _lastCallTimestamp;
    private bool _hasLastCall;
    private int _maxRequestFrames;
    private int _underrunCount;

    // Set once by the audio thread at the end of the window (_counting is written last).
    private long _thresholdTicks;
    private int _counting;

    // Audio thread only (drain after a device loss).
    private bool _drainStarted;
    private long _drainStartTimestamp;
    private long _drainFramesDone;

    /// <param name="render">The mixer render; called with blocks of at most <paramref name="blockFrames"/> frames.</param>
    /// <param name="sampleRate">Device rate in Hz.</param>
    /// <param name="blockFrames">Frames of the array rendered into (the mixer block size).</param>
    /// <param name="ticksPerSecond">Unit of the timestamps (<c>Stopwatch.Frequency</c>).</param>
    /// <param name="timestamp">Clock in that unit (<c>Stopwatch.GetTimestamp</c>); created once by the caller.</param>
    public OpenAlCallbackFeeder(AudioRenderCallback render, int sampleRate, int blockFrames, long ticksPerSecond,
        Func<long> timestamp)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(timestamp);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(blockFrames, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ticksPerSecond, 0);

        _render = render;
        _sampleRate = sampleRate;
        _blockFrames = blockFrames;
        _ticksPerSecond = ticksPerSecond;
        _timestamp = timestamp;
        _scratch = new float[2 * blockFrames];
        _silence = new float[2 * blockFrames];
    }

    /// <summary>The exception thrown by the render, or null. Once set, every call gives silence.</summary>
    public Exception Failure => Volatile.Read(ref _failure);

    /// <summary>Late calls counted since the end of the window (thread-safe read).</summary>
    public int UnderrunCount => Volatile.Read(ref _underrunCount);

    /// <summary>Largest request, in frames, seen so far (thread-safe read).</summary>
    public int MaxRequestFrames => Volatile.Read(ref _maxRequestFrames);

    /// <summary>True once <see cref="CompleteWindow"/> set the threshold and late calls are counted.</summary>
    public bool IsCounting => Volatile.Read(ref _counting) != 0;

    /// <summary>The gap between two calls above which a late call is counted, in the unit of the timestamps.</summary>
    public long ThresholdTicks => Volatile.Read(ref _thresholdTicks);

    /// <summary><see cref="ThresholdTicks"/> in milliseconds.</summary>
    public double ThresholdMilliseconds => ThresholdTicks * 1000.0 / _ticksPerSecond;

    /// <summary>
    /// Ends the settling window (audio thread): the threshold becomes the larger of the largest device latency read
    /// in the window and twice the duration of the largest request the callback saw in it (P4 of the plan, an
    /// estimate), and late calls are counted from now on. Without a request in the window there is nothing to
    /// base the threshold on: nothing changes and the result is false (try again on a later pass).
    /// </summary>
    /// <param name="maxLatencyNanoseconds">Largest <c>ALC_DEVICE_LATENCY_SOFT</c> reading of the window; negative
    /// when the device clock extension is missing or no reading was available.</param>
    /// <returns>True once the threshold is set.</returns>
    public bool CompleteWindow(long maxLatencyNanoseconds)
    {
        var maxFrames = Volatile.Read(ref _maxRequestFrames);
        if (maxFrames <= 0)
        {
            return false;
        }

        var requestTicks = 2L * maxFrames * _ticksPerSecond / _sampleRate;
        var latencyTicks = maxLatencyNanoseconds > 0
            ? (long)(maxLatencyNanoseconds * (double)_ticksPerSecond / 1_000_000_000.0)
            : 0L;

        Volatile.Write(ref _thresholdTicks, Math.Max(requestTicks, latencyTicks));
        Volatile.Write(ref _counting, 1);
        return true;
    }

    /// <summary>
    /// The callback: writes <paramref name="numBytes"/> bytes at <paramref name="destination"/> (rendered stereo float
    /// frames, then zeros for a remainder shorter than a frame, or all zeros when no render is possible) and returns
    /// the byte count asked.
    /// </summary>
    public int Fill(IntPtr destination, int numBytes)
    {
        if (numBytes <= 0)
        {
            return 0;
        }

        try
        {
            var frames = numBytes / BytesPerFrame;
            ObserveCall(frames);

            if (Volatile.Read(ref _failure) != null || Interlocked.CompareExchange(ref _rendering, 1, 0) != 0)
            {
                WriteSilence(destination, 0, numBytes);
                return numBytes;
            }

            try
            {
                var done = 0;
                while (done < frames)
                {
                    var blockFrames = Math.Min(_blockFrames, frames - done);
                    _render(_scratch.AsSpan(0, 2 * blockFrames), blockFrames);
                    Marshal.Copy(_scratch, 0, IntPtr.Add(destination, done * BytesPerFrame), 2 * blockFrames);
                    done += blockFrames;
                }

                WriteSilence(destination, frames * BytesPerFrame, numBytes - frames * BytesPerFrame);
            }
            finally
            {
                Volatile.Write(ref _rendering, 0);
            }
        }
        catch (Exception exception)
        {
            Interlocked.CompareExchange(ref _failure, exception, null);
            try
            {
                WriteSilence(destination, 0, numBytes);
            }
            catch (Exception)
            {
                // Nothing more can be done on this thread; the failure is already recorded.
            }
        }

        return numBytes;
    }

    /// <summary>Starts the drain after a device loss: the frames due are counted from <paramref name="timestamp"/>.</summary>
    public void StartDrain(long timestamp)
    {
        _drainStartTimestamp = timestamp;
        _drainFramesDone = 0;
        _drainStarted = true;
    }

    /// <summary>
    /// Audio thread, after a device loss: renders the mixer into the discarded buffer for the frames due since the
    /// previous drain (elapsed time times the rate, in blocks of at most the block size, at most one second of
    /// frames per call), under the same guard as <see cref="Fill"/>. A render that throws is kept in
    /// <see cref="Failure"/>.
    /// </summary>
    /// <returns>The number of frames rendered.</returns>
    public int Drain()
    {
        if (!_drainStarted || Volatile.Read(ref _failure) != null)
        {
            return 0;
        }

        var elapsed = _timestamp() - _drainStartTimestamp;
        var due = elapsed * _sampleRate / _ticksPerSecond - _drainFramesDone;
        if (due <= 0 || Interlocked.CompareExchange(ref _rendering, 1, 0) != 0)
        {
            return 0;
        }

        try
        {
            if (due > _sampleRate)
            {
                // Longer than the cap (a stalled thread): the excess is skipped, not rendered.
                _drainFramesDone += due - _sampleRate;
                due = _sampleRate;
            }

            var done = 0;
            while (done < due)
            {
                var blockFrames = (int)Math.Min(_blockFrames, due - done);
                _render(_scratch.AsSpan(0, 2 * blockFrames), blockFrames);
                done += blockFrames;
            }

            _drainFramesDone += done;
            return done;
        }
        catch (Exception exception)
        {
            Interlocked.CompareExchange(ref _failure, exception, null);
            return 0;
        }
        finally
        {
            Volatile.Write(ref _rendering, 0);
        }
    }

    private void ObserveCall(int frames)
    {
        var now = _timestamp();
        if (frames > Volatile.Read(ref _maxRequestFrames))
        {
            Volatile.Write(ref _maxRequestFrames, frames);
        }

        if (_hasLastCall && Volatile.Read(ref _counting) != 0 && now - _lastCallTimestamp > Volatile.Read(ref _thresholdTicks))
        {
            Interlocked.Increment(ref _underrunCount);
        }

        _lastCallTimestamp = now;
        _hasLastCall = true;
    }

    // Zeros of OpenAL Soft's memory without allocating: whole floats from the zero array, then single bytes.
    private void WriteSilence(IntPtr destination, int byteOffset, int byteCount)
    {
        var done = 0;
        var blockBytes = _silence.Length * sizeof(float);
        while (byteCount - done >= sizeof(float))
        {
            var bytes = Math.Min(blockBytes, byteCount - done) / sizeof(float) * sizeof(float);
            Marshal.Copy(_silence, 0, IntPtr.Add(destination, byteOffset + done), bytes / sizeof(float));
            done += bytes;
        }

        for (; done < byteCount; done++)
        {
            Marshal.WriteByte(destination, byteOffset + done, 0);
        }
    }
}
