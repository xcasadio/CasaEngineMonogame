namespace CasaEngine.Framework.Animations;

public sealed class AnimationClipNode : IAnimationGraphRuntimeNode
{
    private readonly AnimationClipSampler _sampler = new();

    public AnimationClipNode(AnimationClip clip, bool loop = true)
    {
        Clip = clip ?? throw new ArgumentNullException(nameof(clip));
        Loop = loop;
    }

    public AnimationClip Clip { get; }

    public SkeletonDefinition Skeleton => Clip.Skeleton;

    public float TimeSeconds { get; set; }

    public bool Loop { get; set; }

    public float Speed { get; set; } = 1f;

    public void Advance(float elapsedSeconds)
    {
        if (Math.Abs(elapsedSeconds) <= float.Epsilon || Math.Abs(Speed) <= float.Epsilon)
        {
            return;
        }

        var nextTimeSeconds = TimeSeconds + (elapsedSeconds * Speed);
        if (Loop && Clip.DurationSeconds > float.Epsilon)
        {
            nextTimeSeconds %= Clip.DurationSeconds;
            if (nextTimeSeconds < 0f)
            {
                nextTimeSeconds += Clip.DurationSeconds;
            }
        }
        else
        {
            nextTimeSeconds = Math.Clamp(nextTimeSeconds, 0f, Clip.DurationSeconds);
        }

        TimeSeconds = nextTimeSeconds;
    }

    public void Evaluate(SkeletonPoseLocal outputPose)
    {
        ArgumentNullException.ThrowIfNull(outputPose);

        if (!ReferenceEquals(outputPose.Skeleton, Skeleton))
        {
            throw new ArgumentException("The output pose targets a different skeleton.", nameof(outputPose));
        }

        _sampler.Sample(Clip, TimeSeconds, outputPose, Loop);
    }
}