using System.Collections.ObjectModel;

namespace CasaEngine.Framework.Animations;

public sealed class AnimationEventTrack
{
    private readonly AnimationEventKeyframe[] _keyframes;
    private readonly ReadOnlyCollection<AnimationEventKeyframe> _keyframeView;

    public AnimationEventTrack(IReadOnlyList<AnimationEventKeyframe> keyframes)
    {
        ArgumentNullException.ThrowIfNull(keyframes);

        _keyframes = new AnimationEventKeyframe[keyframes.Count];
        _keyframeView = Array.AsReadOnly(_keyframes);

        var previousTime = float.NegativeInfinity;
        for (var index = 0; index < keyframes.Count; index++)
        {
            var keyframe = keyframes[index];
            if (string.IsNullOrWhiteSpace(keyframe.EventName))
            {
                throw new ArgumentException("Animation event names cannot be empty.", nameof(keyframes));
            }

            if (keyframe.TimeSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(keyframes), "Animation event times must be positive or zero.");
            }

            if (keyframe.TimeSeconds < previousTime)
            {
                throw new ArgumentException("Animation events must be sorted by ascending time.", nameof(keyframes));
            }

            previousTime = keyframe.TimeSeconds;
            _keyframes[index] = keyframe;
        }
    }

    public int Count => _keyframes.Length;

    public IReadOnlyList<AnimationEventKeyframe> Keyframes => _keyframeView;

    public AnimationEventKeyframe GetKeyframe(int index)
    {
        if ((uint)index >= (uint)_keyframes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _keyframes[index];
    }
}