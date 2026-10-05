namespace CasaEngine.Framework.Audio.Software;

/// <summary>
/// Pool of <see cref="SampleChunk"/>. <see cref="Rent"/> and <see cref="Recycle"/> belong to the
/// producer thread (the pool only grows there, never during rendering); the render thread gives
/// chunks back with <see cref="ReturnFromRender"/> through a lock-free ring whose capacity is at
/// least the maximum chunk count, so that call can never fail.
/// </summary>
internal sealed class SampleChunkPool
{
    private readonly SpscRingBuffer<SampleChunk> _returns;
    private readonly SampleChunk[] _free;
    private readonly int _chunkSamples;
    private readonly int _maxCount;
    private int _freeCount;
    private int _createdCount;

    public SampleChunkPool(int chunkSamples, int initialCount, int maxCount)
    {
        _chunkSamples = chunkSamples;
        _maxCount = maxCount;
        _returns = new SpscRingBuffer<SampleChunk>(maxCount);
        _free = new SampleChunk[maxCount];

        for (var i = 0; i < initialCount; i++)
        {
            _free[_freeCount++] = new SampleChunk(chunkSamples);
            _createdCount++;
        }
    }

    public int CreatedCount => _createdCount;

    /// <summary>Producer side: true when <paramref name="count"/> calls to <see cref="Rent"/> are guaranteed to succeed.</summary>
    public bool CanRent(int count)
    {
        while (_returns.TryDequeue(out var returned))
        {
            _free[_freeCount++] = returned;
        }

        return _freeCount + (_maxCount - _createdCount) >= count;
    }

    /// <summary>Producer side. Returns null when the maximum chunk count is reached.</summary>
    public SampleChunk Rent()
    {
        while (_returns.TryDequeue(out var returned))
        {
            _free[_freeCount++] = returned;
        }

        SampleChunk chunk;

        if (_freeCount > 0)
        {
            chunk = _free[--_freeCount];
        }
        else if (_createdCount < _maxCount)
        {
            chunk = new SampleChunk(_chunkSamples);
            _createdCount++;
        }
        else
        {
            return null;
        }

        chunk.Count = 0;
        chunk.EndsBuffer = false;
        chunk.Sequence = 0;
        return chunk;
    }

    /// <summary>Producer side: gives back a chunk that never reached the render thread.</summary>
    public void Recycle(SampleChunk chunk)
    {
        _free[_freeCount++] = chunk;
    }

    /// <summary>Render side.</summary>
    public void ReturnFromRender(SampleChunk chunk)
    {
        _returns.TryEnqueue(chunk);
    }
}
