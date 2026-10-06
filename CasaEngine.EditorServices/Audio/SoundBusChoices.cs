using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Audio.Mixing;

namespace CasaEngine.EditorServices.Audio;

/// <summary>One entry of the bus list of the sound inspector.</summary>
/// <param name="Name">Bus name.</param>
/// <param name="IsUnknown">
/// True for the entry that stands for the bus of the sound when neither the engine nor the project mixer asset has a bus of
/// that name. It is shown, so the author sees it, but it is not a bus a sound can be given.
/// </param>
public sealed record SoundBusChoice(string Name, bool IsUnknown);

/// <summary>
/// The buses the sound inspector offers (plan decisions D21 and P57): the four buses of the engine, then the other buses of the
/// project's <c>.audioMixer</c> asset, then the bus of the sound itself when nothing lists it. Pure, apart from
/// <see cref="TryLoadProjectMixer"/> which reads the asset from disk.
/// </summary>
public static class SoundBusChoices
{
    /// <summary>The buses of the engine a sound can be routed to, in the order the inspector lists them.</summary>
    private static readonly string[] EngineBuses =
    {
        AudioBusNames.Sfx,
        AudioBusNames.Music,
        AudioBusNames.Voice,
        AudioBusNames.Ui,
    };

    /// <summary>
    /// The list of the inspector: Sfx, Music, Voice and Ui, then the buses of <paramref name="projectMixer"/> in file order, never
    /// Master nor Editor and never twice (names are compared ignoring case, like the mixer does). When <paramref name="currentBus"/>
    /// is in neither part, one more entry with <see cref="SoundBusChoice.IsUnknown"/> set comes last. Without an asset, only the
    /// four buses of the engine. A blank <paramref name="currentBus"/> adds nothing.
    /// </summary>
    public static List<SoundBusChoice> Resolve(AudioMixerAsset projectMixer, string currentBus)
    {
        var choices = new List<SoundBusChoice>(EngineBuses.Length + (projectMixer?.Buses.Count ?? 0) + 1);

        for (int index = 0; index < EngineBuses.Length; index++)
        {
            choices.Add(new SoundBusChoice(EngineBuses[index], false));
        }

        if (projectMixer != null)
        {
            for (int index = 0; index < projectMixer.Buses.Count; index++)
            {
                string name = projectMixer.Buses[index].Name;
                if (string.IsNullOrWhiteSpace(name)
                    || name.Equals(AudioBusNames.Master, StringComparison.OrdinalIgnoreCase)
                    || name.Equals(AudioBusNames.Editor, StringComparison.OrdinalIgnoreCase)
                    || Contains(choices, name))
                {
                    continue;
                }

                choices.Add(new SoundBusChoice(name, false));
            }
        }

        if (!string.IsNullOrWhiteSpace(currentBus) && !Contains(choices, currentBus))
        {
            choices.Add(new SoundBusChoice(currentBus, true));
        }

        return choices;
    }

    /// <summary>
    /// Reads the mixer asset the project setting <c>AudioMixerAsset</c> names (an id or a catalog name), from the project folder.
    /// False, with <paramref name="asset"/> null, when the setting is blank or the asset cannot be found or read: the caller then
    /// uses the default list. Never throws. A failure writes one line to the log: a warning here when the setting names nothing
    /// or the file is missing, the loader's own error when the file cannot be parsed.
    /// </summary>
    public static bool TryLoadProjectMixer(out AudioMixerAsset asset)
    {
        asset = null;

        string setting = GameSettings.ProjectSettings?.AudioMixerAsset;
        if (string.IsNullOrWhiteSpace(setting))
        {
            return false;
        }

        setting = setting.Trim();

        try
        {
            if (!ProjectAudioMixer.TryResolveAssetId(setting, out Guid id))
            {
                Logs.WriteWarning($"Sound inspector: the project setting AudioMixerAsset '{setting}' is not an asset id or name of the catalog, the default bus list is used.");
                return false;
            }

            AssetInfo info = AssetCatalog.Get(id);
            if (info == null || string.IsNullOrWhiteSpace(info.FileName))
            {
                Logs.WriteWarning($"Sound inspector: the project setting AudioMixerAsset '{setting}' names no asset of the catalog, the default bus list is used.");
                return false;
            }

            string fullPath = Path.Combine(EngineEnvironment.ProjectPath, info.FileName);
            if (!File.Exists(fullPath))
            {
                Logs.WriteWarning($"Sound inspector: the audio mixer asset '{info.FileName}' of the project setting AudioMixerAsset does not exist, the default bus list is used.");
                return false;
            }

            // The loader logs the cause itself when the file cannot be read, and returns null.
            asset = new AudioMixerAssetLoader().LoadAsset(fullPath, null) as AudioMixerAsset;
            return asset != null;
        }
        catch (Exception exception)
        {
            asset = null;
            Logs.WriteWarning($"Sound inspector: the project mixer asset could not be read, the default bus list is used: {exception.Message}");
            return false;
        }
    }

    private static bool Contains(List<SoundBusChoice> choices, string name)
    {
        for (int index = 0; index < choices.Count; index++)
        {
            if (choices[index].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
