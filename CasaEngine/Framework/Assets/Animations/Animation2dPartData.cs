using Microsoft.Xna.Framework;

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
}