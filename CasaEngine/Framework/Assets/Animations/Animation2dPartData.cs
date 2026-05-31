using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using CasaEngine.Core.Serialization;

namespace CasaEngine.Framework.Assets.Animations;

public sealed class Animation2dPartData
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public Guid DefaultSpriteId { get; set; } = Guid.Empty;

    public Vector2 DefaultPosition { get; set; } = Vector2.Zero;

    public int DefaultDrawOrder { get; set; }

    public bool DefaultVisible { get; set; } = true;

    public bool DefaultFlipX { get; set; }

    public bool DefaultFlipY { get; set; }

    public static Animation2dPartData Load(JObject node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return new Animation2dPartData
        {
            Id = node["id"]?.Value<string>() ?? string.Empty,
            Name = node["name"]?.Value<string>() ?? string.Empty,
            DefaultSpriteId = node["default_sprite_id"]?.GetGuid() ?? Guid.Empty,
            DefaultPosition = node["default_position"]?.GetVector2() ?? Vector2.Zero,
            DefaultDrawOrder = node["default_draw_order"]?.Value<int>() ?? 0,
            DefaultVisible = node["default_visible"]?.Value<bool>() ?? true,
            DefaultFlipX = node["default_flip_x"]?.Value<bool>() ?? false,
            DefaultFlipY = node["default_flip_y"]?.Value<bool>() ?? false,
        };
    }
}