namespace CasaEngine.Framework.Audio.Streaming;

/// <summary>
/// Reads a RIFF wav file block by block, without loading it in memory.
/// </summary>
/// <remarks>
/// Only 16 bit PCM is supported, which is exactly what
/// <see cref="Microsoft.Xna.Framework.Audio.DynamicSoundEffectInstance"/> expects: the bytes read
/// here are submitted as-is, with no conversion. Any other wav flavour is rejected with a message
/// naming what was read, and stays playable as a non-streamed sound through the regular loader.
/// Reads never split a block, so a submitted buffer always contains whole samples.
/// The reader allocates nothing per read: the caller owns the buffer.
/// </remarks>
public sealed class WavStreamReader : IDisposable
{
    public const int SupportedBitsPerSample = 16;
    private const int PcmFormatTag = 1;
    private const int ExtensibleFormatTag = 0xFFFE;

    private readonly Stream _stream;
    private readonly bool _ownsStream;
    private readonly byte[] _headerBuffer = new byte[8];

    private long _dataBytesRead;
    private bool _isDisposed;

    /// <exception cref="InvalidDataException">The stream is not a usable RIFF wav file.</exception>
    /// <exception cref="NotSupportedException">The wav is valid but not 16 bit PCM.</exception>
    public WavStreamReader(Stream stream, bool ownsStream = true)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _ownsStream = ownsStream;

        if (!stream.CanRead)
        {
            throw new ArgumentException("The wav stream must be readable.", nameof(stream));
        }

        if (!stream.CanSeek)
        {
            throw new ArgumentException("The wav stream must be seekable, looping needs to rewind.", nameof(stream));
        }

        Format = ReadHeader();
        _stream.Seek(Format.DataOffset, SeekOrigin.Begin);
    }

    public WavFormatInfo Format { get; }

    /// <summary>True once every audio byte has been read; <see cref="Rewind"/> starts over.</summary>
    public bool IsEndOfStream => _dataBytesRead >= Format.DataLength;

    /// <summary>Audio bytes read since the beginning of the data chunk.</summary>
    public long Position => _dataBytesRead;

    /// <summary>
    /// Fills <paramref name="buffer"/> with at most <paramref name="count"/> audio bytes, rounded
    /// down to a whole number of blocks. Returns 0 at the end of the stream.
    /// </summary>
    public int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (offset < 0 || count < 0 || offset + count > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "The requested range is outside the buffer.");
        }

        var remaining = Format.DataLength - _dataBytesRead;
        if (remaining <= 0)
        {
            return 0;
        }

        var wanted = (int)Math.Min(count, remaining);

        // Never hand back a partial sample: a truncated block would click.
        wanted -= wanted % Format.BlockAlign;
        if (wanted <= 0)
        {
            return 0;
        }

        var read = ReadExactly(buffer, offset, wanted);
        _dataBytesRead += read;
        return read;
    }

    /// <summary>Goes back to the first audio byte. This is how a streamed sound loops.</summary>
    public void Rewind()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        _stream.Seek(Format.DataOffset, SeekOrigin.Begin);
        _dataBytesRead = 0;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (_ownsStream)
        {
            _stream.Dispose();
        }
    }

    private WavFormatInfo ReadHeader()
    {
        _stream.Seek(0, SeekOrigin.Begin);

        Span<byte> riffHeader = stackalloc byte[12];
        ReadExactly(riffHeader);

        if (!Matches(riffHeader[..4], "RIFF") || !Matches(riffHeader.Slice(8, 4), "WAVE"))
        {
            throw new InvalidDataException("Not a RIFF/WAVE stream.");
        }

        var sampleRate = 0;
        var channelCount = 0;
        var bitsPerSample = 0;
        var blockAlign = 0;
        var formatTag = 0;
        var hasFormat = false;

        while (true)
        {
            var chunkHeaderRead = ReadUpTo(_headerBuffer, 0, 8);
            if (chunkHeaderRead < 8)
            {
                break;
            }

            var chunkId = _headerBuffer.AsSpan(0, 4);
            var chunkSize = BitConverter.ToUInt32(_headerBuffer, 4);

            if (Matches(chunkId, "fmt "))
            {
                if (chunkSize < 16)
                {
                    throw new InvalidDataException($"The 'fmt ' chunk is too short ({chunkSize} bytes).");
                }

                Span<byte> formatChunk = stackalloc byte[16];
                ReadExactly(formatChunk);

                formatTag = BitConverter.ToUInt16(formatChunk[..2]);
                channelCount = BitConverter.ToUInt16(formatChunk.Slice(2, 2));
                sampleRate = (int)BitConverter.ToUInt32(formatChunk.Slice(4, 4));
                blockAlign = BitConverter.ToUInt16(formatChunk.Slice(12, 2));
                bitsPerSample = BitConverter.ToUInt16(formatChunk.Slice(14, 2));
                hasFormat = true;

                // A 'fmt ' chunk is often 18 bytes (cbSize) or 40 (WAVE_FORMAT_EXTENSIBLE);
                // whatever follows the first 16 bytes is skipped.
                SkipChunkRemainder(chunkSize, 16);
                continue;
            }

            if (Matches(chunkId, "data"))
            {
                if (!hasFormat)
                {
                    throw new InvalidDataException("The 'data' chunk comes before 'fmt ' in this wav.");
                }

                var dataOffset = _stream.Position;
                var available = _stream.Length - dataOffset;

                // Some encoders write a wrong or streaming-friendly size; trust the file length.
                var dataLength = chunkSize == 0 || chunkSize > available ? available : chunkSize;

                ValidateFormat(formatTag, channelCount, sampleRate, bitsPerSample, ref blockAlign);

                if (dataLength < blockAlign)
                {
                    throw new InvalidDataException("The wav 'data' chunk holds less than one sample.");
                }

                return new WavFormatInfo(
                    sampleRate,
                    channelCount,
                    bitsPerSample,
                    blockAlign,
                    dataOffset,
                    dataLength - (dataLength % blockAlign));
            }

            SkipChunkRemainder(chunkSize, 0);
        }

        throw new InvalidDataException("No 'data' chunk found in this wav.");
    }

    private static void ValidateFormat(int formatTag, int channelCount, int sampleRate, int bitsPerSample, ref int blockAlign)
    {
        if (formatTag == ExtensibleFormatTag)
        {
            throw new NotSupportedException(
                "WAVE_FORMAT_EXTENSIBLE wav files are not supported by the audio streaming; export a plain 16 bit PCM wav.");
        }

        if (formatTag != PcmFormatTag)
        {
            throw new NotSupportedException(
                $"Only PCM wav files can be streamed, this one uses format tag {formatTag}.");
        }

        if (bitsPerSample != SupportedBitsPerSample)
        {
            throw new NotSupportedException(
                $"Only {SupportedBitsPerSample} bit PCM can be streamed, this wav is {bitsPerSample} bit. " +
                "Either convert it, or play it as a non-streamed sound.");
        }

        if (channelCount is not (1 or 2))
        {
            throw new NotSupportedException($"Only mono and stereo wav files can be streamed, this one has {channelCount} channels.");
        }

        if (sampleRate <= 0)
        {
            throw new InvalidDataException($"Invalid wav sample rate ({sampleRate}).");
        }

        var expectedBlockAlign = channelCount * (bitsPerSample / 8);
        if (blockAlign <= 0)
        {
            blockAlign = expectedBlockAlign;
        }
        else if (blockAlign != expectedBlockAlign)
        {
            throw new InvalidDataException(
                $"Inconsistent wav header: blockAlign is {blockAlign} but {expectedBlockAlign} was expected.");
        }
    }

    private void SkipChunkRemainder(uint chunkSize, int alreadyRead)
    {
        var toSkip = (long)chunkSize - alreadyRead;

        // RIFF chunks are word aligned: an odd size is followed by a padding byte.
        if ((chunkSize & 1) != 0)
        {
            toSkip++;
        }

        if (toSkip > 0)
        {
            _stream.Seek(toSkip, SeekOrigin.Current);
        }
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

    private void ReadExactly(Span<byte> buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = _stream.Read(buffer[total..]);
            if (read <= 0)
            {
                throw new InvalidDataException("Unexpected end of wav stream while reading the header.");
            }

            total += read;
        }
    }

    private int ReadExactly(byte[] buffer, int offset, int count)
    {
        var total = 0;
        while (total < count)
        {
            var read = _stream.Read(buffer, offset + total, count - total);
            if (read <= 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private int ReadUpTo(byte[] buffer, int offset, int count)
    {
        return ReadExactly(buffer, offset, count);
    }
}
