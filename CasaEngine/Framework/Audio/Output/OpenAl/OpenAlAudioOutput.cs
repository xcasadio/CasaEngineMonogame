using CasaEngine.Core.Logging;

namespace CasaEngine.Framework.Audio.Output.OpenAl;

/// <summary>
/// Plays a stereo float stream through OpenAL Soft (the "openal" library shipped by MonoGame) from a
/// dedicated thread (ADR-0055). The output owns its own device and context, made current on the audio
/// thread only (ALC_EXT_thread_local_context), so it never touches MonoGame's process-global context.
/// </summary>
/// <remarks>
/// The thread opens the device, plays through one source with direct channels (no spatialization) and keeps
/// <see cref="BufferCount"/> buffers of <see cref="BufferFrames"/> frames queued. The loop is a hot path: it
/// allocates nothing and takes no lock. No exception leaves the thread: it is logged once and the output
/// becomes unavailable.
/// </remarks>
internal sealed class OpenAlAudioOutput : IAudioOutput
{
    private const int OpenTimeoutMilliseconds = 5000;
    private const int JoinTimeoutMilliseconds = 5000;
    private const int DefaultBufferCount = 4;

    private readonly object _lifecycleLock = new();
    private readonly ManualResetEventSlim _opened = new(false);
    private readonly ManualResetEventSlim _wake = new(false);
    private readonly int _requestedBufferFrames;
    private Thread? _thread;
    private volatile AudioRenderCallback? _callback;
    private volatile bool _available;
    private volatile bool _stopRequested;
    private int _sampleRate;
    private int _bufferFrames;
    private readonly int _bufferCount;
    private int _underrunCount;
    private bool _disposed;

    /// <param name="bufferCount">Number of queued buffers (default 4).</param>
    /// <param name="bufferFrames">Frames per buffer; 0 selects 10 ms at the device rate.</param>
    public OpenAlAudioOutput(int bufferCount = DefaultBufferCount, int bufferFrames = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bufferCount, 2);
        ArgumentOutOfRangeException.ThrowIfNegative(bufferFrames);
        _bufferCount = bufferCount;
        _requestedBufferFrames = bufferFrames;
    }

    public bool IsAvailable => _available;

    public int SampleRate => Volatile.Read(ref _sampleRate);

    public int BufferFrames => Volatile.Read(ref _bufferFrames);

    public int BufferCount => _bufferCount;

    public int UnderrunCount => Volatile.Read(ref _underrunCount);

    public bool TryOpen()
    {
        lock (_lifecycleLock)
        {
            if (_disposed)
            {
                return false;
            }

            if (_thread == null)
            {
                _thread = new Thread(ThreadMain)
                {
                    IsBackground = true,
                    Priority = ThreadPriority.AboveNormal,
                    Name = "CasaEngine Audio"
                };
                _thread.Start();
            }
        }

        if (!_opened.Wait(OpenTimeoutMilliseconds))
        {
            Logs.WriteWarning($"OpenAL audio output: the audio thread did not open the device within {OpenTimeoutMilliseconds} ms.");
            Dispose();
            return false;
        }

        return _available;
    }

    public void Start(AudioRenderCallback callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (!_available)
        {
            return;
        }

        _callback = callback;
        _wake.Set();
    }

    public void Dispose()
    {
        Thread? thread;
        lock (_lifecycleLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            thread = _thread;
        }

        _stopRequested = true;
        _wake.Set();

        if (thread != null && thread != Thread.CurrentThread && !thread.Join(JoinTimeoutMilliseconds))
        {
            Logs.WriteWarning($"OpenAL audio output: the audio thread did not stop within {JoinTimeoutMilliseconds} ms.");
        }

        _available = false;
    }

    private void ThreadMain()
    {
        var device = IntPtr.Zero;
        var context = IntPtr.Zero;
        var contextIsCurrent = false;
        var source = 0u;
        var sourceCreated = false;
        uint[]? buffers = null;

        try
        {
            if (!TryInitialize(ref device, ref context, ref contextIsCurrent, ref source, ref sourceCreated,
                    ref buffers, out var failure))
            {
                Logs.WriteWarning($"OpenAL audio output is not available: {failure}");
                return;
            }

            _available = true;
            _opened.Set();

            _wake.Wait();
            var callback = _callback;
            if (_stopRequested || callback == null)
            {
                return;
            }

            RunLoop(source, buffers!, callback);
        }
        catch (Exception exception)
        {
            Logs.WriteException(exception);
        }
        finally
        {
            _available = false;
            Cleanup(device, context, contextIsCurrent, source, sourceCreated, buffers);
            _opened.Set();
        }
    }

    private bool TryInitialize(ref IntPtr device, ref IntPtr context, ref bool contextIsCurrent, ref uint source,
        ref bool sourceCreated, ref uint[]? buffers, out string failure)
    {
        device = OpenAlNative.AlcOpenDevice(null);
        if (device == IntPtr.Zero)
        {
            failure = "alcOpenDevice returned no device (no default audio device?).";
            return false;
        }

        if (OpenAlNative.AlcIsExtensionPresent(device, OpenAlNative.AlcExtThreadLocalContext) == 0)
        {
            failure = $"the device lacks the {OpenAlNative.AlcExtThreadLocalContext} extension.";
            return false;
        }

        context = OpenAlNative.AlcCreateContext(device, IntPtr.Zero);
        if (context == IntPtr.Zero)
        {
            failure = $"alcCreateContext failed (ALC error 0x{OpenAlNative.AlcGetError(device):X}).";
            return false;
        }

        if (OpenAlNative.AlcSetThreadContext(context) == 0)
        {
            failure = "alcSetThreadContext failed.";
            return false;
        }

        contextIsCurrent = true;

        if (OpenAlNative.AlIsExtensionPresent(OpenAlNative.AlExtFloat32) == 0)
        {
            failure = $"missing OpenAL extension {OpenAlNative.AlExtFloat32}.";
            return false;
        }

        if (OpenAlNative.AlIsExtensionPresent(OpenAlNative.AlSoftDirectChannels) == 0)
        {
            failure = $"missing OpenAL extension {OpenAlNative.AlSoftDirectChannels}.";
            return false;
        }

        OpenAlNative.AlcGetIntegerv(device, OpenAlNative.AlcFrequency, 1, out var sampleRate);
        var alcError = OpenAlNative.AlcGetError(device);
        if (alcError != OpenAlNative.AlcNoError || sampleRate <= 0)
        {
            failure = $"could not read the device sample rate (ALC error 0x{alcError:X}, rate {sampleRate}).";
            return false;
        }

        OpenAlNative.AlGetError();
        OpenAlNative.AlGenSources(1, out source);
        sourceCreated = true;
        OpenAlNative.AlSourcei(source, OpenAlNative.AlDirectChannelsSoft, OpenAlNative.AlTrue);
        buffers = new uint[_bufferCount];
        OpenAlNative.AlGenBuffers(buffers.Length, buffers);
        var alError = OpenAlNative.AlGetError();
        if (alError != OpenAlNative.AlNoError)
        {
            failure = $"OpenAL error 0x{alError:X} while creating the source and buffers.";
            return false;
        }

        Volatile.Write(ref _bufferFrames, _requestedBufferFrames > 0 ? _requestedBufferFrames : Math.Max(1, sampleRate / 100));
        Volatile.Write(ref _sampleRate, sampleRate);
        failure = string.Empty;
        return true;
    }

    private void RunLoop(uint source, uint[] buffers, AudioRenderCallback callback)
    {
        var sampleRate = Volatile.Read(ref _sampleRate);
        var bufferFrames = Volatile.Read(ref _bufferFrames);
        var stream = new OpenAlStream(source, sampleRate);
        var loop = new OpenAlRefillLoop(stream, buffers, bufferFrames, callback);
        var sleepMilliseconds = Math.Max(1, (int)(bufferFrames * 1000L / sampleRate / 4));

        loop.Prime();
        var reportedUnderruns = 0;
        while (!_stopRequested)
        {
            loop.Pump();
            var underruns = loop.UnderrunCount;
            if (underruns != reportedUnderruns)
            {
                reportedUnderruns = underruns;
                Volatile.Write(ref _underrunCount, underruns);
            }

            Thread.Sleep(sleepMilliseconds);
        }

        loop.Stop();
    }

    private static void Cleanup(IntPtr device, IntPtr context, bool contextIsCurrent, uint source, bool sourceCreated,
        uint[]? buffers)
    {
        try
        {
            if (contextIsCurrent && sourceCreated)
            {
                OpenAlNative.AlSourceStop(source);
                OpenAlNative.AlDeleteSources(1, ref source);
            }

            if (contextIsCurrent && buffers != null)
            {
                OpenAlNative.AlDeleteBuffers(buffers.Length, buffers);
            }

            if (contextIsCurrent)
            {
                OpenAlNative.AlcSetThreadContext(IntPtr.Zero);
            }

            if (context != IntPtr.Zero)
            {
                OpenAlNative.AlcDestroyContext(context);
            }

            if (device != IntPtr.Zero)
            {
                OpenAlNative.AlcCloseDevice(device);
            }
        }
        catch (Exception exception)
        {
            Logs.WriteException(exception);
        }
    }
}
