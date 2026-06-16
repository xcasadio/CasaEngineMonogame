using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Rendering.Models;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Builds a single skinned <see cref="RiggedModel"/> that exposes the kid character
/// geometry together with three locomotion clips (idle, walk, run) sampled on a shared
/// skeleton. The idle FBX provides the displayed mesh and skeleton; the walk and run
/// clips are loaded from their own FBX files and rebound onto the idle skeleton.
/// </summary>
public static class KidLocomotionModelFactory
{
    public const string IdleClipName = "Idle";
    public const string WalkClipName = "Walk";
    public const string RunClipName = "Run";

    public static RiggedModel Create(CasaEngineGame game)
    {
        ArgumentNullException.ThrowIfNull(game);

        var idleModel = game.AssetContentManager.LoadDirectly<RiggedModel>(@"SkinnedMesh\kid_idle.FBX");
        var rawModelLoader = new RiggedModelLoader();
        var walkModel = rawModelLoader.LoadAsset(Path.Combine(Environment.CurrentDirectory, "Content", "SkinnedMesh", "kid_walk.FBX"));
        var runModel = rawModelLoader.LoadAsset(Path.Combine(Environment.CurrentDirectory, "Content", "SkinnedMesh", "kid_run.FBX"));

        if (idleModel.SkeletonDefinition == null)
        {
            throw new InvalidOperationException("The idle rigged model did not expose a runtime skeleton.");
        }

        var skeleton = idleModel.SkeletonDefinition;
        ValidateSkeletonCompatibility(skeleton, walkModel.SkeletonDefinition, "walk");
        ValidateSkeletonCompatibility(skeleton, runModel.SkeletonDefinition, "run");

        if (idleModel.AnimationClips.Count == 0 || walkModel.AnimationClips.Count == 0 || runModel.AnimationClips.Count == 0)
        {
            throw new InvalidOperationException("The kid locomotion assets must each provide at least one runtime animation clip.");
        }

        var animationClips = new List<AnimationClip>(3)
        {
            RebindClip(idleModel.AnimationClips[0], skeleton, IdleClipName),
            RebindClip(walkModel.AnimationClips[0], skeleton, WalkClipName),
            RebindClip(runModel.AnimationClips[0], skeleton, RunClipName),
        };

        idleModel.OverrideRuntimeAnimationAssets(skeleton, animationClips);
        return idleModel;
    }

    private static void ValidateSkeletonCompatibility(SkeletonDefinition expectedSkeleton, SkeletonDefinition candidateSkeleton, string clipLabel)
    {
        if (candidateSkeleton == null)
        {
            throw new InvalidOperationException($"The {clipLabel} rigged model did not expose a runtime skeleton.");
        }

        if (expectedSkeleton.Count != candidateSkeleton.Count)
        {
            throw new InvalidOperationException($"The {clipLabel} animation skeleton does not match the displayed mesh skeleton.");
        }

        for (var jointIndex = 0; jointIndex < expectedSkeleton.Count; jointIndex++)
        {
            if (!string.Equals(expectedSkeleton.GetJoint(jointIndex).Name, candidateSkeleton.GetJoint(jointIndex).Name, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The {clipLabel} animation skeleton diverges at joint index {jointIndex}.");
            }
        }
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
