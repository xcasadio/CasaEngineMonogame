using CasaEngine.Framework.Common;
using CasaEngine.Framework.Cutscenes.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Cutscenes;

public sealed class CutsceneAsset : ObjectBase
{
    public const int CurrentVersion = 1;

    public CutsceneAsset()
    {
        Name = $"Cutscene {Id}";
    }

    public int Version { get; set; } = CurrentVersion;

    public CutsceneActionData? RootAction { get; set; }

    public override void Load(JObject element)
    {
        base.Load(element);
        CutsceneAssetJsonSerializer.Load(this, element);
    }

    public CutsceneValidationResult Validate()
    {
        return CutsceneValidator.Validate(this);
    }
}