using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Dialogue.Assets;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Dialogue.Serialization;

public static class DialogueAssetJsonSerializer
{
    public static void Save(DialogueAsset asset, JObject node)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(node);

        node["id"] = asset.Id.ToString();
        node["name"] = asset.Name;
        node["type"] = nameof(DialogueAsset);
        node["version"] = asset.Version;
        node["schema_version"] = DialogueAsset.CurrentVersion;
        node["start_node"] = asset.StartNode;
        node["program_base64"] = asset.ProgramBytes.Length == 0 ? string.Empty : Convert.ToBase64String(asset.ProgramBytes);

        var lineTextsNode = new JObject();
        foreach (var pair in asset.LineTexts)
        {
            lineTextsNode[pair.Key] = pair.Value;
        }

        node["line_texts"] = lineTextsNode;
    }

    public static void Load(DialogueAsset asset, JObject node)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(node);

        int version = node["version"]?.GetInt32() ?? DialogueAsset.CurrentVersion;
        if (!CanMigrate(version))
        {
            throw new InvalidOperationException($"Dialogue asset version {version} is newer than supported version {DialogueAsset.CurrentVersion}.");
        }

        asset.Version = DialogueAsset.CurrentVersion;
        asset.StartNode = node["start_node"]?.GetString() ?? "Start";
        asset.ProgramBytes = DecodeProgram(node["program_base64"]?.GetString() ?? string.Empty);
        asset.LineTexts.Clear();

        if (node["line_texts"] is not JObject lineTextsNode)
        {
            return;
        }

        foreach (var property in lineTextsNode.Properties())
        {
            asset.LineTexts[property.Name] = property.Value.GetString();
        }
    }

    public static bool CanMigrate(int version)
        => version > 0 && version <= DialogueAsset.CurrentVersion;

    private static byte[] DecodeProgram(string programBase64)
    {
        if (string.IsNullOrWhiteSpace(programBase64))
        {
            return Array.Empty<byte>();
        }

        return Convert.FromBase64String(programBase64);
    }
}