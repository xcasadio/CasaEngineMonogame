namespace CasaEngine.Tests.Audio;

/// <summary>Builds RIFF wav byte streams in memory, to test the streaming reader without files.</summary>
internal static class WavBuilder
{
    public const int PcmFormatTag = 1;
    public const int IeeeFloatFormatTag = 3;
    public const int ExtensibleFormatTag = 0xFFFE;

    public static byte[] CreatePcm16(
        int sampleRate = 22050,
        int channelCount = 2,
        int sampleCount = 100,
        int formatChunkSize = 16,
        byte[] extraChunk = null,
        string extraChunkId = null)
    {
        var data = new byte[sampleCount * channelCount * 2];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 251);
        }

        return Create(PcmFormatTag, sampleRate, channelCount, 16, data, formatChunkSize, extraChunk, extraChunkId);
    }

    public static byte[] Create(
        int formatTag,
        int sampleRate,
        int channelCount,
        int bitsPerSample,
        byte[] data,
        int formatChunkSize = 16,
        byte[] extraChunk = null,
        string extraChunkId = null)
    {
        var blockAlign = channelCount * (bitsPerSample / 8);
        using var buffer = new MemoryStream();
        using var writer = new BinaryWriter(buffer);

        writer.Write(Ascii("RIFF"));
        writer.Write(0); // patched below
        writer.Write(Ascii("WAVE"));

        writer.Write(Ascii("fmt "));
        writer.Write(formatChunkSize);
        writer.Write((ushort)formatTag);
        writer.Write((ushort)channelCount);
        writer.Write(sampleRate);
        writer.Write(sampleRate * blockAlign);
        writer.Write((ushort)blockAlign);
        writer.Write((ushort)bitsPerSample);
        for (var i = 16; i < formatChunkSize; i++)
        {
            writer.Write((byte)0);
        }

        if (extraChunk != null)
        {
            writer.Write(Ascii(extraChunkId ?? "LIST"));
            writer.Write(extraChunk.Length);
            writer.Write(extraChunk);
            if ((extraChunk.Length & 1) != 0)
            {
                writer.Write((byte)0);
            }
        }

        writer.Write(Ascii("data"));
        writer.Write(data.Length);
        writer.Write(data);

        writer.Flush();
        var bytes = buffer.ToArray();
        BitConverter.GetBytes(bytes.Length - 8).CopyTo(bytes, 4);
        return bytes;
    }

    public static byte[] ExpectedData(byte[] wav, int dataLength)
    {
        var start = wav.Length - dataLength;
        var expected = new byte[dataLength];
        Array.Copy(wav, start, expected, 0, dataLength);
        return expected;
    }

    private static byte[] Ascii(string value)
    {
        var bytes = new byte[value.Length];
        for (var i = 0; i < value.Length; i++)
        {
            bytes[i] = (byte)value[i];
        }

        return bytes;
    }
}
