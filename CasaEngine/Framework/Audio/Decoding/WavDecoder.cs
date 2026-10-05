using System.Buffers.Binary;

namespace CasaEngine.Framework.Audio.Decoding;

/// <summary>
/// A wav file decoded to interleaved 16 bit PCM.
/// </summary>
/// <param name="Samples">Interleaved 16 bit samples, <see cref="ChannelCount"/> per frame.</param>
/// <param name="SampleRate">Sample rate in Hz.</param>
/// <param name="ChannelCount">1 or 2.</param>
public readonly record struct DecodedWav(short[] Samples, int SampleRate, int ChannelCount);

/// <summary>
/// Decodes a RIFF/WAVE file held in memory to interleaved 16 bit PCM.
/// </summary>
/// <remarks>
/// Supported: integer PCM (8 bit unsigned, 16, 24 and 32 bit signed), IEEE float 32 bit, and
/// WAVE_FORMAT_EXTENSIBLE carrying one of those two sub formats; mono and stereo. Other encodings
/// (ADPCM, a-law, ...) and more than two channels are rejected: ADPCM is deferred to a later slice.
/// Conversion to 16 bit: 8 bit is re-centered and scaled, 24 and 32 bit keep their top 16 bits
/// (truncation), float is clamped to [-1, 1], scaled by 32767 and rounded.
/// Unlike <see cref="Streaming.WavStreamReader"/>, which tolerates a wrong data size for streaming,
/// a data chunk declaring more bytes than the file holds is an error here.
/// </remarks>
public static class WavDecoder
{
    private const int PcmFormatTag = 1;
    private const int IeeeFloatFormatTag = 3;
    private const int ExtensibleFormatTag = 0xFFFE;
    private const int MinFormatChunkSize = 16;
    private const int ExtensibleFormatChunkSize = 40;

    /// <param name="bytes">The whole wav file.</param>
    /// <param name="sourceName">Name of the source (usually the file name), quoted in error messages.</param>
    /// <exception cref="InvalidDataException">The data is not a usable RIFF wav file.</exception>
    /// <exception cref="NotSupportedException">The wav is valid but uses an unsupported encoding or layout.</exception>
    public static DecodedWav Decode(ReadOnlySpan<byte> bytes, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(sourceName);

        if (bytes.Length < 12 || !Matches(bytes[..4], "RIFF") || !Matches(bytes.Slice(8, 4), "WAVE"))
        {
            throw new InvalidDataException($"'{sourceName}' is not a RIFF/WAVE file.");
        }

        var hasFormat = false;
        var encoding = SampleEncoding.Pcm;
        var channelCount = 0;
        var sampleRate = 0;
        var blockAlign = 0;
        var bitsPerSample = 0;
        var position = 12;

        while (position + 8 <= bytes.Length)
        {
            var chunkId = bytes.Slice(position, 4);
            var chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(position + 4, 4));
            var bodyStart = position + 8;
            var remaining = bytes.Length - bodyStart;

            if (Matches(chunkId, "fmt "))
            {
                if (chunkSize < MinFormatChunkSize || chunkSize > remaining)
                {
                    throw new InvalidDataException($"'{sourceName}' has an invalid 'fmt ' chunk (size {chunkSize}).");
                }

                var format = bytes.Slice(bodyStart, (int)chunkSize);
                int formatTag = BinaryPrimitives.ReadUInt16LittleEndian(format[..2]);
                channelCount = BinaryPrimitives.ReadUInt16LittleEndian(format.Slice(2, 2));
                sampleRate = (int)BinaryPrimitives.ReadUInt32LittleEndian(format.Slice(4, 4));
                blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(format.Slice(12, 2));
                bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(format.Slice(14, 2));

                if (formatTag == ExtensibleFormatTag)
                {
                    formatTag = ReadExtensibleSubFormat(format, sourceName);
                }

                encoding = formatTag switch
                {
                    PcmFormatTag => SampleEncoding.Pcm,
                    IeeeFloatFormatTag => SampleEncoding.Float,
                    _ => throw new NotSupportedException(
                        $"'{sourceName}' uses wav format tag {formatTag}, only integer PCM and IEEE float are supported."),
                };

                hasFormat = true;
            }
            else if (Matches(chunkId, "data"))
            {
                if (!hasFormat)
                {
                    throw new InvalidDataException($"'{sourceName}' has its 'data' chunk before its 'fmt ' chunk.");
                }

                if (chunkSize > remaining)
                {
                    throw new InvalidDataException(
                        $"'{sourceName}' is truncated: its 'data' chunk declares {chunkSize} bytes but only {remaining} are present.");
                }

                ValidateFormat(sourceName, encoding, channelCount, sampleRate, bitsPerSample, blockAlign);

                if (chunkSize % blockAlign != 0)
                {
                    throw new InvalidDataException(
                        $"'{sourceName}' has a 'data' chunk of {chunkSize} bytes that is not a whole number of {blockAlign} byte blocks.");
                }

                var samples = ConvertToInt16(bytes.Slice(bodyStart, (int)chunkSize), encoding, bitsPerSample);
                return new DecodedWav(samples, sampleRate, channelCount);
            }

            // RIFF chunks are word aligned: an odd size is followed by a padding byte.
            var next = (long)bodyStart + chunkSize + (chunkSize & 1);
            if (next > bytes.Length)
            {
                break;
            }

            position = (int)next;
        }

        throw new InvalidDataException(
            hasFormat
                ? $"'{sourceName}' has no 'data' chunk."
                : $"'{sourceName}' has no 'fmt ' chunk.");
    }

    private static int ReadExtensibleSubFormat(ReadOnlySpan<byte> format, string sourceName)
    {
        // WAVEFORMATEXTENSIBLE: 16 byte base, cbSize, valid bits, channel mask, then the SubFormat GUID.
        if (format.Length < ExtensibleFormatChunkSize)
        {
            throw new InvalidDataException(
                $"'{sourceName}' has a WAVE_FORMAT_EXTENSIBLE 'fmt ' chunk that is too short ({format.Length} bytes).");
        }

        // The first two bytes of the GUID hold the format tag, the rest is the fixed KSDATAFORMAT suffix.
        return BinaryPrimitives.ReadUInt16LittleEndian(format.Slice(24, 2));
    }

    private static void ValidateFormat(
        string sourceName, SampleEncoding encoding, int channelCount, int sampleRate, int bitsPerSample, int blockAlign)
    {
        if (channelCount is not (1 or 2))
        {
            throw new NotSupportedException($"'{sourceName}' has {channelCount} channels, only mono and stereo are supported.");
        }

        if (sampleRate <= 0)
        {
            throw new InvalidDataException($"'{sourceName}' has an invalid sample rate ({sampleRate}).");
        }

        var depthSupported = encoding == SampleEncoding.Float
            ? bitsPerSample == 32
            : bitsPerSample is 8 or 16 or 24 or 32;

        if (!depthSupported)
        {
            throw new NotSupportedException(
                $"'{sourceName}' is {bitsPerSample} bit {(encoding == SampleEncoding.Float ? "float" : "PCM")}, which is not supported.");
        }

        var expectedBlockAlign = channelCount * (bitsPerSample / 8);
        if (blockAlign != expectedBlockAlign)
        {
            throw new InvalidDataException(
                $"'{sourceName}' has an inconsistent header: block align is {blockAlign} but {expectedBlockAlign} was expected.");
        }
    }

    private static short[] ConvertToInt16(ReadOnlySpan<byte> data, SampleEncoding encoding, int bitsPerSample)
    {
        var bytesPerSample = bitsPerSample / 8;
        var samples = new short[data.Length / bytesPerSample];

        for (var i = 0; i < samples.Length; i++)
        {
            var sample = data.Slice(i * bytesPerSample, bytesPerSample);

            samples[i] = encoding == SampleEncoding.Float
                ? FloatToInt16(BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(sample)))
                : bitsPerSample switch
                {
                    8 => (short)((sample[0] - 128) << 8),
                    16 => BinaryPrimitives.ReadInt16LittleEndian(sample),
                    // The top 16 bits of the signed value: the two most significant bytes.
                    24 => BinaryPrimitives.ReadInt16LittleEndian(sample[1..]),
                    _ => (short)(BinaryPrimitives.ReadInt32LittleEndian(sample) >> 16),
                };
        }

        return samples;
    }

    private static short FloatToInt16(float value)
    {
        // NaN compares false with everything: map it to silence rather than an arbitrary value.
        if (float.IsNaN(value))
        {
            return 0;
        }

        return (short)MathF.Round(Math.Clamp(value, -1f, 1f) * short.MaxValue);
    }

    private static bool Matches(ReadOnlySpan<byte> value, string ascii)
    {
        if (value.Length != ascii.Length)
        {
            return false;
        }

        for (var i = 0; i < ascii.Length; i++)
        {
            if (value[i] != (byte)ascii[i])
            {
                return false;
            }
        }

        return true;
    }

    private enum SampleEncoding
    {
        Pcm,
        Float,
    }
}
