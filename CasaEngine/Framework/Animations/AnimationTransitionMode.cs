namespace CasaEngine.Framework.Animations;

/// <summary>
/// Selects how <see cref="AnimationController.CrossFade(AnimationClip, float, AnimationCrossFadeSettings, bool, float)"/>
/// blends from the current animation into the target animation.
/// </summary>
public enum AnimationTransitionMode
{
    /// <summary>
    /// Linearly (or eased) blends the sampled source and target poses over the transition
    /// duration. Simple and robust, but smears short/hand-keyed clips because both poses are
    /// evaluated and mixed for the whole duration.
    /// </summary>
    CrossFade,

    /// <summary>
    /// Inertialization ("offset decay"), as described by David Bollo, "Inertialization:
    /// High-Performance Animation Transitions in Gears of War" (GDC 2018). The target
    /// animation plays back unmodified from the first frame; the pose discontinuity at the
    /// transition start is captured as a per-joint offset and velocity, then decayed to zero
    /// with a quintic curve over the transition duration and added on top of the target pose.
    /// </summary>
    Inertialize,
}
