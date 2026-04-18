using CasaEngine.Framework.Animations;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Animation;

public class AnimationClipCompressorTests
{
    [Fact]
    public void AnimationClipCompressor_RemovesLinearVector3Keyframes()
    {
        var skeleton = CreateSkeleton();
        var clip = new AnimationClip(
            "linear",
            skeleton,
            new[]
            {
                new JointAnimationTrack(
                    0,
                    new Vector3AnimationTrack(
                        new[]
                        {
                            new AnimationKeyframe<Vector3>(0f, Vector3.Zero),
                            new AnimationKeyframe<Vector3>(0.25f, new Vector3(2.5f, 0f, 0f)),
                            new AnimationKeyframe<Vector3>(0.5f, new Vector3(5f, 0f, 0f)),
                            new AnimationKeyframe<Vector3>(0.75f, new Vector3(7.5f, 0f, 0f)),
                            new AnimationKeyframe<Vector3>(1f, new Vector3(10f, 0f, 0f)),
                        }),
                    null,
                    null),
            },
            durationSeconds: 1f);

        var compressedClip = AnimationClipCompressor.Compress(
            clip,
            new AnimationClipCompressionSettings
            {
                TranslationTolerance = 0.00001f,
                ScaleTolerance = 0f,
                RotationToleranceRadians = 0f,
            });

        Assert.True(compressedClip.TryGetJointTrack(0, out var jointTrack));
        Assert.NotNull(jointTrack);
        Assert.NotNull(jointTrack.TranslationTrack);
        Assert.Equal(2, jointTrack.TranslationTrack!.KeyframeCount);
    }

    [Fact]
    public void AnimationClipCompressor_RemovesTracksMatchingBindPose()
    {
        var skeleton = CreateSkeleton();
        var clip = new AnimationClip(
            "bind",
            skeleton,
            new[]
            {
                new JointAnimationTrack(
                    1,
                    new Vector3AnimationTrack(
                        new[]
                        {
                            new AnimationKeyframe<Vector3>(0f, new Vector3(0f, 1f, 0f)),
                        }),
                    null,
                    null),
            },
            durationSeconds: 1f);

        var compressedClip = AnimationClipCompressor.Compress(
            clip,
            new AnimationClipCompressionSettings
            {
                TranslationTolerance = 0.00001f,
                ScaleTolerance = 0f,
                RotationToleranceRadians = 0f,
            });

        Assert.False(compressedClip.TryGetJointTrack(1, out _));
    }

    [Fact]
    public void AnimationClipCompressor_PreservesPoseWithinConfiguredTolerance()
    {
        var skeleton = CreateSkeleton();
        var originalClip = CreateSlightlyCurvedClip(skeleton);
        var settings = new AnimationClipCompressionSettings
        {
            TranslationTolerance = 0.02f,
            ScaleTolerance = 0.00001f,
            RotationToleranceRadians = MathHelper.ToRadians(0.5f),
        };

        var compressedClip = AnimationClipCompressor.Compress(originalClip, settings);
        var sampler = new AnimationClipSampler();
        var originalPose = skeleton.CreateLocalBindPose();
        var compressedPose = skeleton.CreateLocalBindPose();

        Assert.True(GetVector3KeyframeCount(compressedClip, 0) < GetVector3KeyframeCount(originalClip, 0));
        Assert.True(GetQuaternionKeyframeCount(compressedClip, 0) < GetQuaternionKeyframeCount(originalClip, 0));

        for (var timeSeconds = 0f; timeSeconds <= 1f; timeSeconds += 0.05f)
        {
            sampler.Sample(originalClip, timeSeconds, originalPose, loop: false);
            sampler.Sample(compressedClip, timeSeconds, compressedPose, loop: false);

            var originalTransform = originalPose.GetTransform(0);
            var compressedTransform = compressedPose.GetTransform(0);

            Assert.True(
                Vector3.Distance(originalTransform.Translation, compressedTransform.Translation) <= settings.TranslationTolerance + 0.0001f,
                $"Translation error exceeded tolerance at t={timeSeconds}.");
            Assert.True(
                GetQuaternionErrorRadians(originalTransform.Rotation, compressedTransform.Rotation) <= settings.RotationToleranceRadians + 0.0001f,
                $"Rotation error exceeded tolerance at t={timeSeconds}.");
        }
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

    private static AnimationClip CreateSlightlyCurvedClip(SkeletonDefinition skeleton)
    {
        return new AnimationClip(
            "curved",
            skeleton,
            new[]
            {
                new JointAnimationTrack(
                    0,
                    new Vector3AnimationTrack(
                        new[]
                        {
                            new AnimationKeyframe<Vector3>(0f, Vector3.Zero),
                            new AnimationKeyframe<Vector3>(0.5f, new Vector3(5.01f, 0f, 0f)),
                            new AnimationKeyframe<Vector3>(1f, new Vector3(10f, 0f, 0f)),
                        }),
                    new QuaternionAnimationTrack(
                        new[]
                        {
                            new AnimationKeyframe<Quaternion>(0f, Quaternion.Identity),
                            new AnimationKeyframe<Quaternion>(0.5f, Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(45.2f))),
                            new AnimationKeyframe<Quaternion>(1f, Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(90f))),
                        }),
                    null),
            },
            durationSeconds: 1f);
    }

    private static int GetVector3KeyframeCount(AnimationClip clip, int jointIndex)
    {
        return clip.TryGetJointTrack(jointIndex, out var jointTrack) && jointTrack?.TranslationTrack != null
            ? jointTrack.TranslationTrack.KeyframeCount
            : 0;
    }

    private static int GetQuaternionKeyframeCount(AnimationClip clip, int jointIndex)
    {
        return clip.TryGetJointTrack(jointIndex, out var jointTrack) && jointTrack?.RotationTrack != null
            ? jointTrack.RotationTrack.KeyframeCount
            : 0;
    }

    private static float GetQuaternionErrorRadians(Quaternion first, Quaternion second)
    {
        var normalizedFirst = NormalizeQuaternion(first);
        var normalizedSecond = NormalizeQuaternion(second);
        var dot = Math.Abs(Quaternion.Dot(normalizedFirst, normalizedSecond));
        dot = Math.Clamp(dot, -1f, 1f);
        return 2f * MathF.Acos(dot);
    }

    private static Quaternion NormalizeQuaternion(Quaternion rotation)
    {
        return rotation.LengthSquared() <= float.Epsilon
            ? Quaternion.Identity
            : Quaternion.Normalize(rotation);
    }
}