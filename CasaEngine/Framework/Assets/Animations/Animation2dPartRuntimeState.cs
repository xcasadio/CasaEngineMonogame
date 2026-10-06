using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Assets.Animations;

public sealed class Animation2dPartRuntimeState
{
    public string PartId { get; internal set; } = string.Empty;

    public int SourceIndex { get; internal set; }

    public Guid SpriteId { get; set; } = Guid.Empty;

    public Vector2 Position { get; set; } = Vector2.Zero;

    public float Rotation { get; set; }

    public int DrawOrder { get; set; }

    public bool Visible { get; set; } = true;

    public bool FlipX { get; set; }

    public bool FlipY { get; set; }

    /// <summary>True when a corner key is active: the part is a free quad (ADR-0069) instead of a rectangle.</summary>
    public bool HasCorners { get; set; }

    public Vector2 CornerTopLeft { get; set; }

    public Vector2 CornerTopRight { get; set; }

    public Vector2 CornerBottomLeft { get; set; }

    public Vector2 CornerBottomRight { get; set; }

    public Point CornerSourceOffset { get; set; }

    internal void Reset(Animation2dPartData part, int sourceIndex)
    {
        PartId = part.Id;
        SourceIndex = sourceIndex;
        SpriteId = part.DefaultSpriteId;
        Position = part.DefaultPosition;
        Rotation = part.DefaultRotation;
        DrawOrder = part.DefaultDrawOrder;
        Visible = part.DefaultVisible;
        FlipX = part.DefaultFlipX;
        FlipY = part.DefaultFlipY;
        HasCorners = false;
        CornerTopLeft = Vector2.Zero;
        CornerTopRight = Vector2.Zero;
        CornerBottomLeft = Vector2.Zero;
        CornerBottomRight = Vector2.Zero;
        CornerSourceOffset = Point.Zero;
    }
}