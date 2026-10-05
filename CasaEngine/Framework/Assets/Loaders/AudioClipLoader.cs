using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Decoding;

namespace CasaEngine.Framework.Assets.Loaders;

/// <summary>
/// Loads a wav or ogg file as a fully resident, backend neutral <see cref="PcmAudioClip"/>
/// (see docs/decisions/0055-engine-owned-software-audio-mixer-with-thin-native-outputs.md).
/// </summary>
/// <remarks>
/// The decoder is picked by extension: <see cref="WavDecoder"/> for .wav (PCM 8 to 32 bit, IEEE
/// float) and <see cref="OggDecoder"/> for .ogg (Vorbis); mono or stereo only. Long sounds should
/// be streamed instead, which is only available for wav for now.
/// </remarks>
public class AudioClipLoader : IAssetLoader
{
    private static readonly string[] _extensionSupported = { ".wav", ".ogg" };

    public object LoadAsset(string fileName, AssetContentManager assetContentManager)
    {
        try
        {
            var bytes = File.ReadAllBytes(fileName);
            var decoded = Path.GetExtension(fileName).Equals(".ogg", StringComparison.OrdinalIgnoreCase)
                ? OggDecoder.Decode(bytes, fileName)
                : WavDecoder.Decode(bytes, fileName);
            return new PcmAudioClip(decoded.Samples, decoded.SampleRate, decoded.ChannelCount);
        }
        catch (Exception exception)
        {
            Logs.WriteException(new Exception($"Can't load sound '{fileName}'", exception));
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
