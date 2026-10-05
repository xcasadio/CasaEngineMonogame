namespace CasaEngine.Framework.Audio.Software;

/// <summary>Render-thread state of one voice slot. Preallocated, mutated in place through a ref.</summary>
internal struct MixerVoice
{
    public bool Active;
    public int Generation;
    public bool IsStreaming;
    public bool Started;
    public bool Paused;
    public bool Looped;

    /// <summary>A resident voice reached its end but the event ring was full: retried each block.</summary>
    public bool EndPending;

    public int SourceChannels;
    public float Volume;
    public float Pan;
    public float Pitch;

    /// <summary>Source frames advanced per output frame, pitch included.</summary>
    public double Step;

    /// <summary>sourceRate / outputRate, without pitch.</summary>
    public double SourceRatio;

    public float CurrentLeftGain;
    public float CurrentRightGain;
    public float TargetLeftGain;
    public float TargetRightGain;

    // Resident voice.
    public short[] Samples;
    public int FrameCount;
    public double Position;

    // Streaming voice: fractional position relative to the 4 frame window, window, chunk queue.
    public double StreamFraction;
    public bool WindowStarted;
    public float L0, L1, L2, L3;
    public float R0, R1, R2, R3;
    public SampleChunk[] ChunkQueue;
    public int QueueHead;
    public int QueueCount;
    public SampleChunk CurrentChunk;
    public int CurrentIndex;

    /// <summary>Streaming buffers consumed (or dropped on queue overflow) since the voice was created.</summary>
    public int ConsumedBuffers;
}
