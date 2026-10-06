using NVorbis;

namespace CasaEngine.Framework.Audio.Decoding;

/// <summary>
/// Decodes a whole Ogg Vorbis file to interleaved 16 bit PCM.
/// </summary>
/// <remarks>
/// Uses NVorbis, which allocates for every decoded packet: this is acceptable at load time only,
/// never for streaming (AGENTS.md section 9.3, open question O11). Mono and stereo only. The float
/// output of the decoder is converted exactly like <see cref="WavDecoder"/> converts float wavs.
/// </remarks>
public static class OggDecoder
{
    private const int ReadChunkFrames = 4096;

    /// <param name="bytes">The whole ogg file.</param>
    /// <param name="sourceName">Name of the source (usually the file name), quoted in error messages.</param>
    /// <exception cref="InvalidDataException">The data is not a decodable Ogg Vorbis file.</exception>
    /// <exception cref="NotSupportedException">The file has more than two channels.</exception>
    public static DecodedPcm Decode(byte[] bytes, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(sourceName);

        return Decode(new MemoryStream(bytes, writable: false), sourceName);
    }

    /// <param name="stream">A seekable stream holding the whole ogg file; it is left open.</param>
    /// <param name="sourceName">Name of the source (usually the file name), quoted in error messages.</param>
    /// <exception cref="InvalidDataException">The data is not a decodable Ogg Vorbis file.</exception>
    /// <exception cref="NotSupportedException">The file has more than two channels.</exception>
    public static DecodedPcm Decode(Stream stream, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(sourceName);

        try
        {
            using var reader = new VorbisReader(stream, closeOnDispose: false);

            var channelCount = reader.Channels;
            var sampleRate = reader.SampleRate;

            if (channelCount is not (1 or 2))
            {
                throw new NotSupportedException($"'{sourceName}' has {channelCount} channels, only mono and stereo are supported.");
            }

            if (sampleRate <= 0)
            {
                throw new InvalidDataException($"'{sourceName}' has an invalid sample rate ({sampleRate}).");
            }

            var samples = new List<short>();
            var buffer = new float[ReadChunkFrames * channelCount];
            int read;

            while ((read = reader.ReadSamples(buffer, 0, buffer.Length)) > 0)
            {
                for (var i = 0; i < read; i++)
                {
                    samples.Add(PcmConversion.FloatToInt16(buffer[i]));
                }
            }

            return new DecodedPcm(samples.ToArray(), sampleRate, channelCount);
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // NVorbis reports malformed data with its own exception types (and sometimes
            // plain IO or argument exceptions on truncated input): one contract for callers.
            throw new InvalidDataException($"'{sourceName}' is not a valid Ogg Vorbis file. {exception.Message}", exception);
        }
    }
}
