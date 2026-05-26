namespace CasaEngine.Framework.Animations;

public sealed class JointAnimationTrack
{
    public JointAnimationTrack(
        int jointIndex,
        Vector3AnimationTrack translationTrack,
        QuaternionAnimationTrack rotationTrack,
        Vector3AnimationTrack scaleTrack)
    {
        if (jointIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(jointIndex));
        }

        if (translationTrack == null && rotationTrack == null && scaleTrack == null)
        {
            throw new ArgumentException("A joint animation track must animate at least one channel.", nameof(translationTrack));
        }

        JointIndex = jointIndex;
        TranslationTrack = translationTrack;
        RotationTrack = rotationTrack;
        ScaleTrack = scaleTrack;
    }

    public int JointIndex { get; }

    public Vector3AnimationTrack TranslationTrack { get; }

    public QuaternionAnimationTrack RotationTrack { get; }

    public Vector3AnimationTrack ScaleTrack { get; }

    public float EndTimeSeconds => Math.Max(
        TranslationTrack?.EndTimeSeconds ?? 0f,
        Math.Max(RotationTrack?.EndTimeSeconds ?? 0f, ScaleTrack?.EndTimeSeconds ?? 0f));
}