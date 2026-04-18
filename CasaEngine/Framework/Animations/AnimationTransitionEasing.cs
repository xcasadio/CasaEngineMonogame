namespace CasaEngine.Framework.Animations;

public enum AnimationTransitionEasingMode
{
    Linear,
    SmoothStep,
    EaseOutCubic,
}

public static class AnimationTransitionEasing
{
    public static float Evaluate(AnimationTransitionEasingMode easingMode, float normalizedTime)
    {
        var t = Math.Clamp(normalizedTime, 0f, 1f);

        return easingMode switch
        {
            AnimationTransitionEasingMode.SmoothStep => t * t * (3f - 2f * t),
            AnimationTransitionEasingMode.EaseOutCubic => 1f - MathF.Pow(1f - t, 3f),
            _ => t,
        };
    }
}