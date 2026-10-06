using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Configuration.Project;

namespace CasaEngine.Framework.Audio.Mixing;

/// <summary>
/// Applies the <c>.audioMixer</c> asset named by <see cref="ProjectSettings.AudioMixerAsset"/> to the live mixer (plan
/// decision P48), through an <see cref="AudioMixerAssetApplier"/>. A blank setting means the engine's default mixer.
/// </summary>
/// <remarks>
/// <see cref="Apply"/> never throws: an unknown asset, an unreadable file or a version from the future is logged with the
/// setting and leaves the default mixer. The asset is read fresh on every call (<see cref="AssetContentManager.LoadCopy{T}"/>),
/// so a saved change is picked up. Call it once the asset loaders are registered. Game thread only.
/// </remarks>
public sealed class ProjectAudioMixer
{
    private readonly AssetContentManager _assets;

    public ProjectAudioMixer(AudioService service, AssetContentManager assets)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(assets);

        _assets = assets;
        Applier = new AudioMixerAssetApplier(service);
    }

    public AudioMixerAssetApplier Applier { get; }

    /// <summary>Id of the asset currently applied, <see cref="Guid.Empty"/> when none.</summary>
    public Guid AppliedAssetId { get; private set; }

    /// <summary>Applies the project's mixer asset, or restores the default mixer when the setting is blank or fails.</summary>
    public void Apply(ProjectSettings settings)
    {
        string setting = settings?.AudioMixerAsset;
        try
        {
            if (string.IsNullOrWhiteSpace(setting))
            {
                Applier.Release();
                AppliedAssetId = Guid.Empty;
                return;
            }

            setting = setting.Trim();
            if (!TryResolveAssetId(setting, out Guid id))
            {
                throw new InvalidOperationException("no asset has this id or name");
            }

            var asset = _assets.LoadCopy<AudioMixerAsset>(id);
            Applier.Apply(asset);
            AppliedAssetId = id;
            Logs.WriteInfo($"Project audio mixer: '{asset.Name}' applied ({asset.Buses.Count} buses, {Applier.LastProblems.Count} problems).");
        }
        catch (Exception exception)
        {
            Logs.WriteWarning($"Project setting AudioMixerAsset '{setting}' could not be applied, the default mixer is used: {exception.Message}");
            try
            {
                Applier.Release();
            }
            catch (Exception releaseException)
            {
                Logs.WriteWarning($"Project audio mixer could not be released: {releaseException.Message}");
            }

            AppliedAssetId = Guid.Empty;
        }
    }

    /// <summary>Resolves an asset id (<see cref="Guid.TryParse(string, out Guid)"/>) or else a catalog name.</summary>
    public static bool TryResolveAssetId(string idOrName, out Guid id)
    {
        id = Guid.Empty;
        if (string.IsNullOrWhiteSpace(idOrName))
        {
            return false;
        }

        if (Guid.TryParse(idOrName, out id))
        {
            return true;
        }

        var info = AssetCatalog.Get(idOrName);
        if (info == null)
        {
            return false;
        }

        id = info.Id;
        return true;
    }
}
