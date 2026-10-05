namespace CasaEngine.Framework.Audio.Output.OpenAl;

/// <summary>
/// Refill bookkeeping of the audio thread: initial fill, refill of processed buffers in order,
/// underrun detection and restart. Hot path: allocates nothing after construction.
/// </summary>
internal sealed class OpenAlRefillLoop
{
    private readonly IOpenAlStream _stream;
    private readonly uint[] _buffers;
    private readonly int _bufferFrames;
    private readonly AudioRenderCallback _callback;
    private readonly float[] _scratch;
    private int _underrunCount;
    private bool _stopped;

    public OpenAlRefillLoop(IOpenAlStream stream, uint[] buffers, int bufferFrames, AudioRenderCallback callback)
    {
        _stream = stream;
        _buffers = buffers;
        _bufferFrames = bufferFrames;
        _callback = callback;
        _scratch = new float[2 * bufferFrames];
    }

    /// <summary>Number of times the source was found stopped while running (thread-safe read).</summary>
    public int UnderrunCount => Volatile.Read(ref _underrunCount);

    /// <summary>Renders and queues every buffer in order, then starts the source.</summary>
    public void Prime()
    {
        for (var i = 0; i < _buffers.Length; i++)
        {
            RenderAndQueue(_buffers[i]);
        }

        _stream.PlaySource();
    }

    /// <summary>
    /// One loop iteration: refills every processed buffer, then restarts a stopped (starved) source
    /// and counts the underrun. Does nothing once <see cref="Stop"/> was called.
    /// </summary>
    public void Pump()
    {
        if (_stopped)
        {
            return;
        }

        var processed = _stream.GetProcessedBufferCount();
        for (var i = 0; i < processed; i++)
        {
            RenderAndQueue(_stream.UnqueueBuffer());
        }

        if (_stream.IsSourceStopped())
        {
            Interlocked.Increment(ref _underrunCount);
            _stream.PlaySource();
        }
    }

    /// <summary>Makes every later <see cref="Pump"/> a no-op.</summary>
    public void Stop()
    {
        _stopped = true;
    }

    private void RenderAndQueue(uint buffer)
    {
        _callback(_scratch.AsSpan(0, 2 * _bufferFrames), _bufferFrames);
        _stream.FillAndQueueBuffer(buffer, _scratch, _bufferFrames);
    }
}
