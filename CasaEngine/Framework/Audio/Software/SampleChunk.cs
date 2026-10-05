namespace CasaEngine.Framework.Audio.Software;

/// <summary>
/// Fixed-size block of 16 bit PCM samples used to move streaming data from the producer thread
/// to the render thread without allocating. Ownership: the producer fills it, hands it over in a
/// submit command, the render thread consumes it and hands it back through the pool's return ring.
/// </summary>
internal sealed class SampleChunk
{
    public SampleChunk(int capacitySamples)
    {
        Samples = new short[capacitySamples];
    }

    public short[] Samples { get; }

    /// <summary>Number of valid samples (all channels counted), a whole number of frames.</summary>
    public int Count;

    /// <summary>True for the last chunk of a submitted buffer: consuming it reports the buffer.</summary>
    public bool EndsBuffer;

    /// <summary>Caller supplied sequence of the buffer this chunk belongs to.</summary>
    public int Sequence;
}
