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

    [Fact]
    public void AnimationClip_LoopPeriod_DefaultsToDuration_AndRejectsShorterPeriods()
    {
        var skeleton = CreateSkeleton();
        var clip = CreateClip(skeleton);

        Assert.Equal(clip.DurationSeconds, clip.LoopPeriodSeconds);

        var extended = clip.WithLoopPeriod(1.25f);
        Assert.Equal(1f, extended.DurationSeconds);
        Assert.Equal(1.25f, extended.LoopPeriodSeconds);
        Assert.Equal(clip.Name, extended.Name);
        Assert.Same(clip.Skeleton, extended.Skeleton);
        Assert.True(extended.TryGetJointTrack(0, out var track));
        Assert.True(clip.TryGetJointTrack(0, out var sourceTrack));
        Assert.Same(sourceTrack, track);
        Assert.Equal(1f, clip.LoopPeriodSeconds); // the source clip is untouched

        Assert.Throws<ArgumentException>(() => clip.WithLoopPeriod(0.5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => clip.WithLoopPeriod(-1f));
    }

    [Fact]
    public void AnimationClipSampler_InterpolatesFromLastKeyBackToFirst_OverTheExtraLoopPeriod()
    {
        // Keys at 0 s (x = 0) and 1 s (x = 10), cycle length 1.25 s: between 1 s and 1.25 s the
        // sampler must walk back from the last key to the first instead of jumping at the seam.
        var skeleton = CreateSkeleton();
        var clip = CreateClip(skeleton).WithLoopPeriod(1.25f);
        var destination = skeleton.CreateLocalBindPose();
        var sampler = new AnimationClipSampler();

        sampler.Sample(clip, 1f, destination, loop: true);
        Assert.Equal(10f, destination.GetTransform(0).Translation.X, 3);

        sampler.Sample(clip, 1.125f, destination, loop: true);
        Assert.Equal(5f, destination.GetTransform(0).Translation.X, 3);

        sampler.Sample(clip, 1.25f, destination, loop: true);
        Assert.Equal(0f, destination.GetTransform(0).Translation.X, 3);

        // Second cycle, same phase as 0.5 s.
        sampler.Sample(clip, 1.75f, destination, loop: true);
        Assert.Equal(5f, destination.GetTransform(0).Translation.X, 3);

        // Not looping: the clip still ends at its duration.
        sampler.Sample(clip, 1.125f, destination, loop: false);
        Assert.Equal(10f, destination.GetTransform(0).Translation.X, 3);
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