namespace CasaEngine.Framework.Animations;

public sealed class AnimationCrossFadeSettings
{
    public static AnimationCrossFadeSettings Default { get; } = new();

    public AnimationTransitionEasingMode EasingMode { get; init; } = AnimationTransitionEasingMode.Linear;

    public bool PreserveRootTranslationVelocity { get; init; }

    public float RootTranslationVelocityWeight { get; init; } = 1f;

    internal void Validate()
    {
        if (RootTranslationVelocityWeight < 0f || RootTranslationVelocityWeight > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(RootTranslationVelocityWeight), "The root translation velocity weight must stay within [0, 1].");
        }
    }
}