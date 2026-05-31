using CasaEngine.Framework.Common;
using CasaEngine.Framework.Dialogue.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Dialogue.Assets;

public sealed class DialogueAsset : ObjectBase
{
    public const int CurrentVersion = 1;

    public DialogueAsset()
    {
        Name = $"Dialogue {Id}";
    }

    public int Version { get; set; } = CurrentVersion;
    public string StartNode { get; set; } = "Start";
    public byte[] ProgramBytes { get; set; } = Array.Empty<byte>();
    public Dictionary<string, string> LineTexts { get; } = new();
    public bool HasCompiledProgram => ProgramBytes.Length > 0;

    public static DialogueAsset FromCompiledProgram(string name, string startNode, byte[] programBytes, IReadOnlyDictionary<string, string> lineTexts)
    {
        ArgumentNullException.ThrowIfNull(programBytes);
        ArgumentNullException.ThrowIfNull(lineTexts);

        var asset = new DialogueAsset
        {
            StartNode = string.IsNullOrWhiteSpace(startNode) ? "Start" : startNode,
            ProgramBytes = programBytes.ToArray(),
        };
        if (!string.IsNullOrWhiteSpace(name))
        {
            asset.Name = name;
        }

        foreach (var pair in lineTexts)
        {
            asset.LineTexts[pair.Key] = pair.Value ?? string.Empty;
        }

        return asset;
    }

    public override void Load(JObject element)
    {
        base.Load(element);
        DialogueAssetJsonSerializer.Load(this, element);
    }
}