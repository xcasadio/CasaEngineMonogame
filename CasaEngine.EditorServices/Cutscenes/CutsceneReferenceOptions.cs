using CasaEngine.Framework.Assets;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>One choice of a reference field: the label shown, and the text the property holds when it is chosen.</summary>
/// <param name="Label">What the author reads.</param>
/// <param name="Value">What the runtime reads, in the text form of the property: a GUID, a file name or a catalogue name.</param>
/// <param name="AssetId">The catalogued asset behind the choice.</param>
public sealed record CutsceneReferenceOption(string Label, string Value, Guid AssetId);

/// <summary>
/// The choices of a reference property of a cutscene action, built from the asset catalogue, and the way to find the
/// choice of a stored value. The value written is the one the runtime reads: the GUID of a sound, the file name of a
/// world, the catalogue name of any other asset.
/// </summary>
public static class CutsceneReferenceOptions
{
    /// <summary>True for the senses that designate an asset, so that a list of <see cref="CutsceneReferenceOption"/> applies.</summary>
    public static bool IsAssetReference(CutsceneActionPropertySemantic semantic) => GetAssetType(semantic) != null;

    /// <summary>The catalogue asset type a sense designates, or null for a sense that is not an asset.</summary>
    public static string? GetAssetType(CutsceneActionPropertySemantic semantic) => semantic switch
    {
        CutsceneActionPropertySemantic.Sound => "sound",
        CutsceneActionPropertySemantic.Sprite => "sprite",
        CutsceneActionPropertySemantic.Screen => "uiscreen",
        CutsceneActionPropertySemantic.World => "world",
        CutsceneActionPropertySemantic.Cutscene => "cutscene",
        CutsceneActionPropertySemantic.ButtonsMapping => "buttonsmapping",
        _ => null,
    };

    /// <summary>
    /// The choices for a sense, sorted by label without regard to case, then by value. A sense that is not an asset has none.
    /// Assets of another type are left out.
    /// </summary>
    public static List<CutsceneReferenceOption> Build(CutsceneActionPropertySemantic semantic, IEnumerable<AssetInfo> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        var options = new List<CutsceneReferenceOption>();
        string? assetType = GetAssetType(semantic);
        if (assetType == null)
        {
            return options;
        }

        foreach (var asset in assets)
        {
            if (asset == null || !string.Equals(asset.AssetType, assetType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string label = string.IsNullOrEmpty(asset.Name) ? asset.FileName : asset.Name;
            options.Add(new CutsceneReferenceOption(label, GetWrittenValue(semantic, asset), asset.Id));
        }

        options.Sort(static (left, right) =>
        {
            int byLabel = string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase);
            return byLabel != 0 ? byLabel : string.CompareOrdinal(left.Value, right.Value);
        });
        return options;
    }

    /// <summary>The text a property holds to designate an asset: the form the runtime reads for this sense.</summary>
    public static string GetWrittenValue(CutsceneActionPropertySemantic semantic, AssetInfo asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return semantic switch
        {
            CutsceneActionPropertySemantic.Sound => asset.Id.ToString(),
            CutsceneActionPropertySemantic.World => asset.FileName,
            _ => asset.Name,
        };
    }

    /// <summary>
    /// The choice a stored value designates, or null when the value is empty or names no listed asset. A sense that the
    /// runtime reads as a name or a GUID (screen, cutscene, buttons mapping) also finds an asset by its GUID; a sound
    /// is found by its GUID in any case; a world by its file name; a sprite by its name.
    /// </summary>
    public static CutsceneReferenceOption? Find(CutsceneActionPropertySemantic semantic, IReadOnlyList<CutsceneReferenceOption> options, string? value)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        bool byGuid = semantic is CutsceneActionPropertySemantic.Sound
            or CutsceneActionPropertySemantic.Screen
            or CutsceneActionPropertySemantic.Cutscene
            or CutsceneActionPropertySemantic.ButtonsMapping;
        bool byValue = semantic != CutsceneActionPropertySemantic.Sound;

        if (byValue)
        {
            for (int index = 0; index < options.Count; index++)
            {
                if (string.Equals(options[index].Value, value, StringComparison.Ordinal))
                {
                    return options[index];
                }
            }
        }

        if (byGuid && Guid.TryParse(value.Trim(), out Guid id))
        {
            for (int index = 0; index < options.Count; index++)
            {
                if (options[index].AssetId == id)
                {
                    return options[index];
                }
            }
        }

        return null;
    }
}
