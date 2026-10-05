using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Decoding;

namespace CasaEngine.Framework.Assets.Loaders;

/// <summary>
/// Loads a wav file as a fully resident, backend neutral <see cref="PcmAudioClip"/>
/// (see docs/decisions/0055-engine-owned-software-audio-mixer-with-thin-native-outputs.md).
/// </summary>
/// <remarks>
/// Decoding is done by <see cref="WavDecoder"/>, so the supported encodings are its own (PCM 8 to
/// 32 bit, IEEE float, mono or stereo). Long sounds should be streamed instead.
/// </remarks>
public class WavAudioClipLoader : IAssetLoader
{
    private static readonly string[] _extensionSupported = { ".wav" };

    public object LoadAsset(string fileName, AssetContentManager assetContentManager)
    {
        try
        {
            var bytes = File.ReadAllBytes(fileName);
            var wav = WavDecoder.Decode(bytes, fileName);
            return new PcmAudioClip(wav.Samples, wav.SampleRate, wav.ChannelCount);
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
