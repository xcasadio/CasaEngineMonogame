namespace CasaEngine.Framework.Animations;

public sealed class AnimationCrossFadeSettings
{
    public static AnimationCrossFadeSettings Default { get; } = new();

    public AnimationTransitionEasingMode EasingMode { get; init; } = AnimationTransitionEasingMode.Linear;

    public bool PreserveRootTranslationVelocity { get; init; }

    public float RootTranslationVelocityWeight { get; init; } = 1f;

    /// <summary>
    /// Selects the blending technique used for the transition. Defaults to
    /// <see cref="AnimationTransitionMode.CrossFade"/> so existing callers are unaffected.
    /// </summary>
    public AnimationTransitionMode TransitionMode { get; init; } = AnimationTransitionMode.CrossFade;

    /// <summary>
    /// When <see cref="TransitionMode"/> is <see cref="AnimationTransitionMode.Inertialize"/>,
    /// clamps the magnitude (per axis) of the captured translation offset at the start of the
    /// transition. Guards against a pathological pop (e.g. a teleport) turning into a huge,
    /// slowly-decaying overshoot. Defaults to a large value so ordinary transitions are
    /// unaffected.
    /// </summary>
    public float InertializeMaxTranslationOffset { get; init; } = 1000f;

    internal void Validate()
    {
        if (RootTranslationVelocityWeight < 0f || RootTranslationVelocityWeight > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(RootTranslationVelocityWeight), "The root translation velocity weight must stay within [0, 1].");
        }

        if (InertializeMaxTranslationOffset <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(InertializeMaxTranslationOffset), "The inertialization max translation offset must be positive.");
        }
    }
}