namespace CasaEngine.Framework.Audio;

/// <summary>
/// Optional capability of an <see cref="IAudioBackend"/> (plan decision P22): the backend measures, on the audio thread,
/// the level of each mixing bus and of the output, and publishes it without any lock, so several game-thread readers can
/// poll it without losing a peak. <see cref="AudioService"/> detects it with a type test; <see cref="IAudioBackend"/> is
/// unchanged and only <c>SoftwareAudioBackend</c> implements it.
/// </summary>
/// <remarks>
/// <para>
/// Buses are identified by their <see cref="IAudioBusBackend"/> index. A bus level is measured once per audio block, on
/// the signal the bus passes to its parent: after its insert effects and its own gain (for Master, after its gain and
/// before the limiter). The output level is the final signal, after the Master limiter and the hard clip.
/// </para>
/// <para>
/// Each reader owns an <see cref="AudioMeterCursor"/>: a read returns the maximum peak and the combined RMS of every
/// block published since the cursor, then advances it. A peak present in one block is therefore seen by every reader that
/// reads at least once per <see cref="MeterHistoryBlocks"/> blocks. Reading allocates nothing.
/// </para>
/// </remarks>
public interface IAudioMeteringBackend
{
    /// <summary>
    /// Number of blocks the backend keeps for the readers (a block is one audio render of at most one device buffer). A
    /// reader that skips more blocks than this loses the oldest ones, and says so in <see cref="AudioMeterRead.MissedBlockCount"/>.
    /// </summary>
    int MeterHistoryBlocks { get; }

    /// <summary>
    /// Fills <paramref name="buses"/>, indexed by backend bus index (indices beyond the buses that exist, or beyond the
    /// span, stay untouched or zero), with the levels of the blocks published since <paramref name="cursor"/>, and
    /// <paramref name="output"/> with the levels of the final output. When no block is new every level is zero and
    /// <see cref="AudioMeterRead.BlockCount"/> is 0. The cursor then moves to the newest block. A default cursor reads
    /// the whole history. Returns an empty read when the backend is unavailable.
    /// </summary>
    AudioMeterRead ReadLevels(ref AudioMeterCursor cursor, Span<AudioLevel> buses, out AudioLevel output);
}

/// <summary>Position of one reader in the published levels. Use a default value for a new reader; keep one per reader.</summary>
public struct AudioMeterCursor
{
    internal long LastBlock;
    internal bool Started;
}

/// <summary>Level of a bus or of the output over the blocks of one read. Linear amplitude, 1 is full scale.</summary>
public readonly struct AudioLevel
{
    public AudioLevel(float peakLeft, float peakRight, float rms, int overs)
    {
        PeakLeft = peakLeft;
        PeakRight = peakRight;
        Rms = rms;
        Overs = overs;
    }

    /// <summary>Largest absolute sample of the left channel.</summary>
    public float PeakLeft { get; }

    /// <summary>Largest absolute sample of the right channel.</summary>
    public float PeakRight { get; }

    /// <summary>Largest absolute sample of both channels.</summary>
    public float Peak => PeakLeft > PeakRight ? PeakLeft : PeakRight;

    /// <summary>Root mean square of both channels over every frame of the read.</summary>
    public float Rms { get; }

    /// <summary>
    /// Samples (left and right counted separately) beyond +-1 on the Master mix before the limiter and the hard clip.
    /// Only filled for the output; always 0 for a bus.
    /// </summary>
    public int Overs { get; }
}

/// <summary>What one <see cref="IAudioMeteringBackend.ReadLevels"/> covered.</summary>
public readonly struct AudioMeterRead
{
    public AudioMeterRead(int blockCount, int missedBlockCount, int frameCount)
    {
        BlockCount = blockCount;
        MissedBlockCount = missedBlockCount;
        FrameCount = frameCount;
    }

    /// <summary>Blocks read and aggregated.</summary>
    public int BlockCount { get; }

    /// <summary>Blocks published since the cursor that were no longer available (the reader waited too long).</summary>
    public int MissedBlockCount { get; }

    /// <summary>Frames of the blocks read.</summary>
    public int FrameCount { get; }
}
