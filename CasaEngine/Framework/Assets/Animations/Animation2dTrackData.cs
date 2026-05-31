using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Assets.Animations;

public enum Animation2dTrackProperty
{
    Sprite,
    Position,
    Visible,
    DrawOrder,
    FlipX,
    FlipY
}

public enum Animation2dInterpolationMode
{
    Step
}

public sealed class Animation2dTrackData
{
    public string TargetPartId { get; set; } = string.Empty;

    public Animation2dTrackProperty Property { get; set; }

    public Animation2dInterpolationMode Interpolation { get; set; } = Animation2dInterpolationMode.Step;

    public List<Animation2dGuidKeyframeData> SpriteKeyframes { get; } = new();

    public List<Animation2dVector2KeyframeData> PositionKeyframes { get; } = new();

    public List<Animation2dBoolKeyframeData> VisibleKeyframes { get; } = new();

    public List<Animation2dIntKeyframeData> DrawOrderKeyframes { get; } = new();

    public List<Animation2dBoolKeyframeData> FlipKeyframes { get; } = new();
}

public readonly record struct Animation2dGuidKeyframeData(float TimeSeconds, Guid Value);

public readonly record struct Animation2dVector2KeyframeData(float TimeSeconds, Vector2 Value);

public readonly record struct Animation2dBoolKeyframeData(float TimeSeconds, bool Value);

public readonly record struct Animation2dIntKeyframeData(float TimeSeconds, int Value);