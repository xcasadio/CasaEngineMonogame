namespace CasaEngine.Framework.Audio.Streaming;

/// <summary>Description of the audio data held by a RIFF wav file.</summary>
public readonly struct WavFormatInfo
{
    public WavFormatInfo(
        int sampleRate,
        int channelCount,
        int bitsPerSample,
        int blockAlign,
        long dataOffset,
        long dataLength)
    {
        SampleRate = sampleRate;
        ChannelCount = channelCount;
        BitsPerSample = bitsPerSample;
        BlockAlign = blockAlign;
        DataOffset = dataOffset;
        DataLength = dataLength;
    }

    public int SampleRate { get; }

    public int ChannelCount { get; }

    public int BitsPerSample { get; }

    /// <summary>Bytes for one sample across every channel. A read never splits a block.</summary>
    public int BlockAlign { get; }

    /// <summary>Offset of the first audio byte in the file.</summary>
    public long DataOffset { get; }

    /// <summary>Length of the audio data in bytes.</summary>
    public long DataLength { get; }

    public int BytesPerSecond => SampleRate * BlockAlign;

    public TimeSpan Duration => BytesPerSecond > 0
        ? TimeSpan.FromSeconds((double)DataLength / BytesPerSecond)
        : TimeSpan.Zero;

    public override string ToString()
        => $"{SampleRate}Hz {ChannelCount}ch {BitsPerSample}bit, {DataLength} bytes ({Duration:g})";
}
