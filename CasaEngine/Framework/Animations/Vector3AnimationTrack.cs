using System.Collections.ObjectModel;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public sealed class Vector3AnimationTrack
{
    private readonly AnimationKeyframe<Vector3>[] _keyframes;
    private readonly ReadOnlyCollection<AnimationKeyframe<Vector3>> _keyframeView;

    public Vector3AnimationTrack(IReadOnlyList<AnimationKeyframe<Vector3>> keyframes)
    {
        ArgumentNullException.ThrowIfNull(keyframes);

        _keyframes = new AnimationKeyframe<Vector3>[keyframes.Count];
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

            previousTime = keyframe.TimeSeconds;
            _keyframes[index] = keyframe;
        }
    }

    public int KeyframeCount => _keyframes.Length;

    public float EndTimeSeconds => KeyframeCount == 0 ? 0f : _keyframes[^1].TimeSeconds;

    public IReadOnlyList<AnimationKeyframe<Vector3>> Keyframes => _keyframeView;

    public AnimationKeyframe<Vector3> GetKeyframe(int index)
    {
        if ((uint)index >= (uint)_keyframes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _keyframes[index];
    }
}