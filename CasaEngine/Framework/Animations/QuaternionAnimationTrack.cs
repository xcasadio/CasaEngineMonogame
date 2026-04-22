using System.Collections.ObjectModel;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public sealed class QuaternionAnimationTrack
{
    private readonly AnimationKeyframe<Quaternion>[] _keyframes;
    private readonly ReadOnlyCollection<AnimationKeyframe<Quaternion>> _keyframeView;

    public QuaternionAnimationTrack(IReadOnlyList<AnimationKeyframe<Quaternion>> keyframes)
    {
        ArgumentNullException.ThrowIfNull(keyframes);

        _keyframes = new AnimationKeyframe<Quaternion>[keyframes.Count];
        _keyframeView = Array.AsReadOnly(_keyframes);

        var previousTime = float.NegativeInfinity;
        for (var index = 0; index < keyframes.Count; index++)
        {
            var keyframe = keyframes[index];
            if (keyframe.TimeSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(keyframes), "Keyframe times must be positive or zero.");
            }

            if (keyframe.TimeSeconds < previousTime)
            {
                throw new ArgumentException("Keyframes must be sorted by ascending time.", nameof(keyframes));
            }

            var rotation = keyframe.Value.LengthSquared() <= float.Epsilon
                ? Quaternion.Identity
                : Quaternion.Normalize(keyframe.Value);

            previousTime = keyframe.TimeSeconds;
            _keyframes[index] = new AnimationKeyframe<Quaternion>(keyframe.TimeSeconds, rotation);
        }
    }

    public int KeyframeCount => _keyframes.Length;

    public float EndTimeSeconds => KeyframeCount == 0 ? 0f : _keyframes[^1].TimeSeconds;

    public IReadOnlyList<AnimationKeyframe<Quaternion>> Keyframes => _keyframeView;

    public AnimationKeyframe<Quaternion> GetKeyframe(int index)
    {
        if ((uint)index >= (uint)_keyframes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _keyframes[index];
    }
}