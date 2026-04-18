using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public sealed class AnimationClipCompressionSettings
{
    public static AnimationClipCompressionSettings Default { get; } = new();

    public float TranslationTolerance { get; init; } = 0.001f;

    public float ScaleTolerance { get; init; } = 0.001f;

    public float RotationToleranceRadians { get; init; } = MathHelper.ToRadians(0.25f);

    internal void Validate()
    {
        if (TranslationTolerance < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(TranslationTolerance));
        }

        if (ScaleTolerance < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(ScaleTolerance));
        }

        if (RotationToleranceRadians < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(RotationToleranceRadians));
        }
    }
}