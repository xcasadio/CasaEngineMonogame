using System;
using System.Collections.Generic;
using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering.Models;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Builds the skinned <see cref="RiggedModel"/> for the three.js Soldier model and exposes
/// exactly three locomotion clips in the order expected by the blending demo: idle, walk,
/// run. The model already embeds every animation on a single skeleton, so the clips are
/// simply selected (by name, with the three.js index order as a fallback) and reordered.
/// <para/>
/// The source asset is <c>Soldier.glb</c> (copied from the three.js examples). The engine now
/// loads glTF/GLB directly via SharpGLTF, so the original three.js binary glTF is used as-is.
/// </summary>
public static class SoldierLocomotionModelFactory
{
    public const string IdleClipName = "Idle";
    public const string WalkClipName = "Walk";
    public const string RunClipName = "Run";

    // three.js webgl_animation_skinning_blending uses animations[0]=idle, [1]=run, [3]=walk.
    private const int ThreeJsIdleIndex = 0;
    private const int ThreeJsWalkIndex = 3;
    private const int ThreeJsRunIndex = 1;

    public static RiggedModel Create(CasaEngineGame game)
    {
        ArgumentNullException.ThrowIfNull(game);

        var model = game.AssetContentManager.LoadDirectly<RiggedModel>(@"SkinnedMesh\Soldier.glb");

        var skeleton = model.SkeletonDefinition
            ?? throw new InvalidOperationException("Soldier.glb did not expose a runtime skeleton.");

        var sourceClips = model.AnimationClips;
        if (sourceClips.Count == 0)
        {
            throw new InvalidOperationException("Soldier.glb does not provide any runtime animation clip.");
        }

        var idleClip = ResolveClip(sourceClips, "idle", ThreeJsIdleIndex);
        var walkClip = ResolveClip(sourceClips, "walk", ThreeJsWalkIndex);
        var runClip = ResolveClip(sourceClips, "run", ThreeJsRunIndex);

        var animationClips = new List<AnimationClip>(3)
        {
            RebindClip(idleClip, skeleton, IdleClipName),
            RebindClip(walkClip, skeleton, WalkClipName),
            RebindClip(runClip, skeleton, RunClipName),
        };

        model.OverrideRuntimeAnimationAssets(skeleton, animationClips);
        return model;
    }

    private static AnimationClip ResolveClip(IReadOnlyList<AnimationClip> clips, string nameFragment, int fallbackIndex)
    {
        for (var clipIndex = 0; clipIndex < clips.Count; clipIndex++)
        {
            var clip = clips[clipIndex];
            if (clip.Name != null && clip.Name.Contains(nameFragment, StringComparison.OrdinalIgnoreCase))
            {
                return clip;
            }
        }

        if (fallbackIndex >= 0 && fallbackIndex < clips.Count)
        {
            return clips[fallbackIndex];
        }

        // Last resort: clamp to the available clips so the demo still runs.
        return clips[Math.Min(Math.Max(fallbackIndex, 0), clips.Count - 1)];
    }

    private static AnimationClip RebindClip(AnimationClip sourceClip, SkeletonDefinition targetSkeleton, string clipName)
    {
        var jointTracks = new List<JointAnimationTrack>(targetSkeleton.Count);
        for (var jointIndex = 0; jointIndex < targetSkeleton.Count; jointIndex++)
        {
            if (sourceClip.TryGetJointTrack(jointIndex, out var jointTrack) && jointTrack != null)
            {
                jointTracks.Add(jointTrack);
            }
        }

        return new AnimationClip(clipName, targetSkeleton, jointTracks, sourceClip.DurationSeconds, sourceClip.EventTrack);
    }
}
