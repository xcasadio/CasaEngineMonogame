using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Gameplay;

public sealed class PlayerStartupSettings : ObjectBase
{
    public Guid DefaultPawnAssetId { get; set; } = Guid.Empty;
    public string PlayerControllerClass { get; set; } = nameof(PlayerController);
    public string AIControllerClass { get; set; } = nameof(AIController);
    public string HUDClass { get; set; }

    public override void Load(JObject element)
    {
        base.Load(element);

        DefaultPawnAssetId = element["default_pawn_asset_id"].GetGuid();
        PlayerControllerClass = element["player_controller_class"].GetString();

        if (element.ContainsKey("ai_controller_class"))
        {
            AIControllerClass = element["ai_controller_class"].GetString();
        }

        HUDClass = element.ContainsKey("hud_class")
            ? element["hud_class"].GetString()
            : element["hud_classClass"].GetString();
    }
}