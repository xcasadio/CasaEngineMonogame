using CasaEngine.Framework.Animations;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Animation;

public class AnimationClipSamplerTests
{
    [Fact]
    public void AnimationClip_UsesLastKeyframeToComputeDuration_WhenNoDurationIsProvided()
    {
        var skeleton = CreateSkeleton();
        var clip = new AnimationClip(
            "walk",
            skeleton,
            new[]
            {
                new JointAnimationTrack(
                    0,
                    new Vector3AnimationTrack(
                        new[]
                        {
                            new AnimationKeyframe<Vector3>(0f, Vector3.Zero),
                            new AnimationKeyframe<Vector3>(0.75f, new Vector3(3f, 0f, 0f)),
                        }),
                    null,
                    null),
            });

        Assert.Equal(0.75f, clip.DurationSeconds);
    }

    [Fact]
    public void AnimationClipSampler_SamplesInterpolatedPoseIntoDestination()
    {
        var skeleton = CreateSkeleton();
        var clip = CreateClip(skeleton);
        var destination = skeleton.CreateLocalBindPose();
        var sampler = new AnimationClipSampler();

        destination.ClearDirty();
        sampler.Sample(clip, 0.5f, destination, loop: false);

        Assert.True(destination.IsDirty);
        Assert.Equal(0, destination.DirtyStartIndex);
        Assert.Equal(new Vector3(5f, 0f, 0f), destination.GetTransform(0).Translation);
        Assert.Equal(new Vector3(0f, 1f, 0f), destination.GetTransform(1).Translation);
    }

    [Fact]
    public void AnimationClipSampler_ClampsWhenLoopingIsDisabled()
    {
        var skeleton = CreateSkeleton();
        var clip = CreateClip(skeleton);
        var destination = skeleton.CreateLocalBindPose();
        var sampler = new AnimationClipSampler();

        sampler.Sample(clip, 2f, destination, loop: false);

        Assert.Equal(new Vector3(10f, 0f, 0f), destination.GetTransform(0).Translation);
    }

    [Fact]
    public void AnimationClipSampler_WrapsNegativeTimeWhenLooping()
    {
        var skeleton = CreateSkeleton();
        var clip = CreateClip(skeleton);
        var destination = skeleton.CreateLocalBindPose();
        var sampler = new AnimationClipSampler();

        sampler.Sample(clip, -0.25f, destination, loop: true);

        Assert.Equal(new Vector3(7.5f, 0f, 0f), destination.GetTransform(0).Translation);
    }

    private static SkeletonDefinition CreateSkeleton()
    {
        return new SkeletonDefinition(
            new[]
            {
                new SkeletonJointDefinition("Root", -1, BoneTransform.Identity, Matrix.Identity),
                new SkeletonJointDefinition(
                    "Child",
                    0,
                    new BoneTransform(new Vector3(0f, 1f, 0f), Quaternion.Identity, Vector3.One),
                    Matrix.Identity),
            });
    }

    private static AnimationClip CreateClip(SkeletonDefinition skeleton)
    {
        return new AnimationClip(
            "walk",
            skeleton,
            new[]
            {
                new JointAnimationTrack(
                    0,
                    new Vector3AnimationTrack(
                        new[]
                        {
                            new AnimationKeyframe<Vector3>(0f, Vector3.Zero),
                            new AnimationKeyframe<Vector3>(1f, new Vector3(10f, 0f, 0f)),
                        }),
                    null,
                    null),
            },
            durationSeconds: 1f);
    }
}