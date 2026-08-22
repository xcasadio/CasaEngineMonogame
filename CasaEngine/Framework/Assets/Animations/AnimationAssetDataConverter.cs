using CasaEngine.Framework.Animations;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Assets.Animations;

public static class AnimationAssetDataConverter
{
    public static SkeletonAsset CreateSkeletonAsset(SkeletonDefinition skeletonDefinition)
    {
        ArgumentNullException.ThrowIfNull(skeletonDefinition);

        var skeletonAsset = new SkeletonAsset();
        for (var jointIndex = 0; jointIndex < skeletonDefinition.Count; jointIndex++)
        {
            var joint = skeletonDefinition.GetJoint(jointIndex);
            skeletonAsset.Joints.Add(new SkeletonJointAsset
            {
                Name = joint.Name,
                ParentIndex = joint.ParentIndex,
                LocalBindTransform = joint.LocalBindTransform,
                InverseBindMatrix = joint.InverseBindMatrix,
                SkinPaletteIndex = joint.SkinPaletteIndex,
            });
        }

        return skeletonAsset;
    }

    public static AnimationClipAsset CreateAnimationClipAsset(AnimationClip animationClip, Guid skeletonAssetId)
    {
        ArgumentNullException.ThrowIfNull(animationClip);

        var animationClipAsset = new AnimationClipAsset
        {
            SkeletonAssetId = skeletonAssetId,
            DurationSeconds = animationClip.DurationSeconds,
            LoopPeriodSeconds = animationClip.LoopPeriodSeconds > animationClip.DurationSeconds ? animationClip.LoopPeriodSeconds : 0f,
        };

        for (var jointIndex = 0; jointIndex < animationClip.Skeleton.Count; jointIndex++)
        {
            if (!animationClip.TryGetJointTrack(jointIndex, out var jointTrack) || jointTrack == null)
            {
                continue;
            }

            var jointTrackAsset = new AnimationJointTrackAsset
            {
                JointName = animationClip.Skeleton.GetJoint(jointIndex).Name,
            };

            CopyVector3Track(jointTrack.TranslationTrack, jointTrackAsset.TranslationKeyframes);
            CopyQuaternionTrack(jointTrack.RotationTrack, jointTrackAsset.RotationKeyframes);
            CopyVector3Track(jointTrack.ScaleTrack, jointTrackAsset.ScaleKeyframes);
            animationClipAsset.JointTracks.Add(jointTrackAsset);
        }

        if (animationClip.EventTrack != null)
        {
            for (var eventIndex = 0; eventIndex < animationClip.EventTrack.Count; eventIndex++)
            {
                var keyframe = animationClip.EventTrack.GetKeyframe(eventIndex);
                animationClipAsset.Events.Add(new AnimationEventAsset(keyframe.TimeSeconds, keyframe.EventName));
            }
        }

        return animationClipAsset;
    }

    public static SkeletonDefinition CreateSkeletonDefinition(SkeletonAsset skeletonAsset)
    {
        ArgumentNullException.ThrowIfNull(skeletonAsset);

        var joints = new SkeletonJointDefinition[skeletonAsset.Joints.Count];
        for (var jointIndex = 0; jointIndex < skeletonAsset.Joints.Count; jointIndex++)
        {
            var joint = skeletonAsset.Joints[jointIndex];
            joints[jointIndex] = new SkeletonJointDefinition(
                joint.Name,
                joint.ParentIndex,
                joint.LocalBindTransform,
                joint.InverseBindMatrix,
                joint.SkinPaletteIndex);
        }

        return new SkeletonDefinition(joints);
    }

    public static AnimationClip CreateAnimationClip(AnimationClipAsset animationClipAsset, SkeletonDefinition skeletonDefinition)
    {
        ArgumentNullException.ThrowIfNull(animationClipAsset);
        ArgumentNullException.ThrowIfNull(skeletonDefinition);

        var jointTracks = new List<JointAnimationTrack>(animationClipAsset.JointTracks.Count);
        for (var trackIndex = 0; trackIndex < animationClipAsset.JointTracks.Count; trackIndex++)
        {
            var trackAsset = animationClipAsset.JointTracks[trackIndex];
            if (!skeletonDefinition.TryGetJointIndex(trackAsset.JointName, out var jointIndex))
            {
                throw new InvalidOperationException($"Animation clip '{animationClipAsset.Name}' references unknown joint '{trackAsset.JointName}'.");
            }

            var translationTrack = CreateVector3Track(trackAsset.TranslationKeyframes);
            var rotationTrack = CreateQuaternionTrack(trackAsset.RotationKeyframes);
            var scaleTrack = CreateVector3Track(trackAsset.ScaleKeyframes);

            if (translationTrack == null && rotationTrack == null && scaleTrack == null)
            {
                continue;
            }

            jointTracks.Add(new JointAnimationTrack(jointIndex, translationTrack, rotationTrack, scaleTrack));
        }

        AnimationEventTrack eventTrack = null;
        if (animationClipAsset.Events.Count > 0)
        {
            var eventKeyframes = new AnimationEventKeyframe[animationClipAsset.Events.Count];
            for (var eventIndex = 0; eventIndex < animationClipAsset.Events.Count; eventIndex++)
            {
                var animationEvent = animationClipAsset.Events[eventIndex];
                eventKeyframes[eventIndex] = new AnimationEventKeyframe(animationEvent.TimeSeconds, animationEvent.EventName);
            }

            eventTrack = new AnimationEventTrack(eventKeyframes);
        }

        var clipName = string.IsNullOrWhiteSpace(animationClipAsset.Name)
            ? "AnimationClip"
            : animationClipAsset.Name;
        return new AnimationClip(clipName, skeletonDefinition, jointTracks, animationClipAsset.DurationSeconds, eventTrack, animationClipAsset.LoopPeriodSeconds);
    }

    private static void CopyVector3Track(Vector3AnimationTrack sourceTrack, List<Vector3AnimationKeyframeAsset> destinationKeyframes)
    {
        destinationKeyframes.Clear();
        if (sourceTrack == null)
        {
            return;
        }

        for (var keyframeIndex = 0; keyframeIndex < sourceTrack.KeyframeCount; keyframeIndex++)
        {
            var keyframe = sourceTrack.GetKeyframe(keyframeIndex);
            destinationKeyframes.Add(new Vector3AnimationKeyframeAsset(keyframe.TimeSeconds, keyframe.Value));
        }
    }

    private static void CopyQuaternionTrack(QuaternionAnimationTrack sourceTrack, List<QuaternionAnimationKeyframeAsset> destinationKeyframes)
    {
        destinationKeyframes.Clear();
        if (sourceTrack == null)
        {
            return;
        }

        for (var keyframeIndex = 0; keyframeIndex < sourceTrack.KeyframeCount; keyframeIndex++)
        {
            var keyframe = sourceTrack.GetKeyframe(keyframeIndex);
            destinationKeyframes.Add(new QuaternionAnimationKeyframeAsset(keyframe.TimeSeconds, keyframe.Value));
        }
    }

    private static Vector3AnimationTrack CreateVector3Track(IReadOnlyList<Vector3AnimationKeyframeAsset> keyframes)
    {
        if (keyframes.Count == 0)
        {
            return null;
        }

        var runtimeKeyframes = new AnimationKeyframe<Vector3>[keyframes.Count];
        for (var keyframeIndex = 0; keyframeIndex < keyframes.Count; keyframeIndex++)
        {
            var keyframe = keyframes[keyframeIndex];
            runtimeKeyframes[keyframeIndex] = new AnimationKeyframe<Vector3>(keyframe.TimeSeconds, keyframe.Value);
        }

        return new Vector3AnimationTrack(runtimeKeyframes);
    }

    private static QuaternionAnimationTrack CreateQuaternionTrack(IReadOnlyList<QuaternionAnimationKeyframeAsset> keyframes)
    {
        if (keyframes.Count == 0)
        {
            return null;
        }

        var runtimeKeyframes = new AnimationKeyframe<Quaternion>[keyframes.Count];
        for (var keyframeIndex = 0; keyframeIndex < keyframes.Count; keyframeIndex++)
        {
            var keyframe = keyframes[keyframeIndex];
            runtimeKeyframes[keyframeIndex] = new AnimationKeyframe<Quaternion>(keyframe.TimeSeconds, keyframe.Value);
        }

        return new QuaternionAnimationTrack(runtimeKeyframes);
    }
}