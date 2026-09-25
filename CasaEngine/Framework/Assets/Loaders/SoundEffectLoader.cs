using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Streaming;
using Microsoft.Xna.Framework.Audio;

namespace CasaEngine.Framework.Assets.Loaders;

/// <summary>
/// Loads a wav file as a fully resident <see cref="IAudioClip"/>.
/// </summary>
/// <remarks>
/// MonoGame DesktopGL only decodes RIFF wav here (PCM 8/16/24 bit, IEEE float 32 bit, MS-ADPCM
/// and IMA4). There is no mp3 support at all, and ogg is only reachable through its music
/// streaming path, so a sound effect must be a wav. Long sounds should be streamed instead of
/// going through this loader.
/// When the wav is 16 bit PCM mono, its decoded samples are also kept on the returned
/// <see cref="MonoGameAudioClip"/> (ADR-0039), so it can be played on a software stereo voice
/// through <see cref="AudioService.PlayClipStereo"/>. Any other wav flavour (stereo, another bit
/// depth, ADPCM, float, or a parse failure) simply skips that: the clip is created exactly as
/// before, without samples.
/// </remarks>
public class SoundEffectLoader : IAssetLoader
{
    private static readonly string[] _extensionSupported = { ".wav" };

    public object LoadAsset(string fileName, AssetContentManager assetContentManager)
    {
        try
        {
            var bytes = File.ReadAllBytes(fileName);

            using var soundStream = new MemoryStream(bytes);
            var soundEffect = SoundEffect.FromStream(soundStream);
            soundEffect.Name = fileName;

            var monoSamples = TryDecodeMonoPcm16(bytes, out var sampleRate);
            return monoSamples != null
                ? new MonoGameAudioClip(soundEffect, monoSamples, sampleRate)
                : new MonoGameAudioClip(soundEffect);
        }
        catch (NoAudioHardwareException exception)
        {
            Logs.WriteWarning($"No audio hardware available, '{fileName}' was not loaded. {exception.Message}");
            return null;
        }
        catch (Exception exception)
        {
            Logs.WriteException(new Exception($"Can't load sound '{fileName}'", exception));
            return null;
        }
    }

    /// <summary>
    /// Decodes <paramref name="bytes"/> as 16 bit PCM mono, for the software stereo voice path.
    /// Returns null for every other case (stereo, another bit depth, ADPCM, float, or a wav the
    /// reader cannot parse at all): the caller then falls back to a sample-less clip, exactly as
    /// before this path existed.
    /// </summary>
    private static short[] TryDecodeMonoPcm16(byte[] bytes, out int sampleRate)
    {
        sampleRate = 0;

        try
        {
            using var stream = new MemoryStream(bytes);
            using var reader = new WavStreamReader(stream);

            if (reader.Format.ChannelCount != 1 || reader.Format.BitsPerSample != WavStreamReader.SupportedBitsPerSample)
            {
                return null;
            }

            var dataLength = (int)reader.Format.DataLength;
            var byteBuffer = new byte[dataLength];

            var totalRead = 0;
            while (totalRead < byteBuffer.Length)
            {
                var read = reader.Read(byteBuffer, totalRead, byteBuffer.Length - totalRead);
                if (read <= 0)
                {
                    break;
                }

                totalRead += read;
            }

            var samples = new short[totalRead / sizeof(short)];
            Buffer.BlockCopy(byteBuffer, 0, samples, 0, samples.Length * sizeof(short));

            sampleRate = reader.Format.SampleRate;
            return samples;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
        {
            // A wav the streaming reader does not handle (another encoding or bit depth, a header it
            // cannot parse) is still a valid sound effect: it just has no samples for the stereo path.
            sampleRate = 0;
            return null;
        }
    }

    public bool IsFileSupported(string fileName)
    {
        return IsSoundFile(fileName);
    }

    public static bool IsSoundFile(string fileName)
    {
        var extension = Path.GetExtension(fileName);

        foreach (var supported in _extensionSupported)
        {
            if (string.Equals(extension, supported, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
