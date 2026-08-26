using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
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
/// </remarks>
public class SoundEffectLoader : IAssetLoader
{
    private static readonly string[] _extensionSupported = { ".wav" };

    public object LoadAsset(string fileName, AssetContentManager assetContentManager)
    {
        try
        {
            using var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            var soundEffect = SoundEffect.FromStream(fileStream);
            soundEffect.Name = fileName;
            return new MonoGameAudioClip(soundEffect);
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
