using System;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// The key lines of the animation demos' on-screen text, written from the keys the input configuration binds, never from
/// letters written in the code: on AZERTY the walk key reads Z, not W (ADR-0096). Formatted once, when a demo starts.
/// </summary>
internal static class AnimationHudText
{
    /// <summary>The key lines of the animation blend demo.</summary>
    internal sealed record BlendLines(
        string Pages,
        string WalkAndReset,
        string Explore,
        string Clips,
        string EasingAndVelocity,
        string WalkAndUpperBodyAction,
        string LayerWeight,
        string WalkAndRootMotionBurst,
        string RootMotionAndReset);

    /// <summary>The key lines of the animation IK demo.</summary>
    internal sealed record IkLines(string Toggles, string Weight);

    /// <param name="keyOf">The label of the key bound to an action of the input configuration.</param>
    public static BlendLines ForBlendDemo(Func<string, string> keyOf)
    {
        string forward = keyOf("Blend.Forward");
        string reset = keyOf("Blend.Reset");
        string nextPage = keyOf("Blend.NextPage");
        string action = keyOf("Blend.Action");

        return new BlendLines(
            $"[{nextPage}] Next page  [Shift+{nextPage}] Previous page",
            $"[{forward}] Walk  [Shift+{forward}] Run  [{reset}] Reset actor",
            $"[{forward}/{keyOf("Blend.Left")}/{keyOf("Blend.Right")}] Explore strafe/forward directional locomotion  [{reset}] Reset actor",
            $"[{keyOf("Blend.Clip1")}] Idle  [{keyOf("Blend.Clip2")}] Walk  [{keyOf("Blend.Clip3")}] Run",
            $"[{keyOf("Blend.Easing")}] Cycle easing  [{keyOf("Blend.KeepVelocity")}] Toggle velocity preservation",
            $"[{forward}] Walk  [Shift+{forward}] Run  [{action}] Trigger upper-body action",
            $"[{keyOf("Blend.WeightDown")}]/[{keyOf("Blend.WeightUp")}] Decrease/Increase layer weight",
            $"[{forward}] Walk  [Shift+{forward}] Run  [{action}] Trigger root-motion burst",
            $"[{keyOf("Blend.RootMotion")}] Toggle observe/apply  [{reset}] Reset trail and actor");
    }

    /// <param name="keyOf">The label of the key bound to an action of the input configuration.</param>
    public static IkLines ForIkDemo(Func<string, string> keyOf)
    {
        return new IkLines(
            $"[{keyOf("Ik.Orbit")}] Toggle auto orbit  [{keyOf("Ik.Toggle")}] Toggle IK  [{keyOf("Ik.Skeleton")}] Toggle skeleton  " +
            $"[{keyOf("Ik.TargetToPose")}] Reset target  [{keyOf("Ik.Reset")}] Reset actor",
            $"[{keyOf("Ik.WeightDown")}]/[{keyOf("Ik.WeightUp")}] Decrease/Increase IK weight");
    }
}
