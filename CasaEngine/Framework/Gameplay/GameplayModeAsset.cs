using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Gameplay;

public sealed class GameplayModeAsset : ObjectBase
{
    public string ModeClassName { get; set; } = string.Empty;

    public GameplayMode CreateMode()
    {
        if (string.IsNullOrWhiteSpace(ModeClassName))
        {
            return null;
        }

        return ElementFactory.Create<GameplayMode>(ModeClassName);
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        ModeClassName = element["mode_class_name"].GetString();
    }
}