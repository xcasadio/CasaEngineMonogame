namespace CasaEngine.Framework.Particles.Authoring;

/// <summary>
/// Key on a normalized particle curve.
/// </summary>
public readonly struct FloatCurveKey : IEquatable<FloatCurveKey>
{
    public FloatCurveKey(float time, float value)
    {
        if (float.IsNaN(time) || float.IsInfinity(time))
        {
            throw new ArgumentOutOfRangeException(nameof(time), time, "Curve key time must be finite.");
        }

        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Curve key value must be finite.");
        }

        Time = Clamp01(time);
        Value = value;
    }

    public float Time { get; }

    public float Value { get; }

    public bool Equals(FloatCurveKey other)
        => Time.Equals(other.Time) && Value.Equals(other.Value);

    public override bool Equals(object? obj)
        => obj is FloatCurveKey other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(Time, Value);

    public static bool operator ==(FloatCurveKey left, FloatCurveKey right)
        => left.Equals(right);

    public static bool operator !=(FloatCurveKey left, FloatCurveKey right)
        => !left.Equals(right);

    private static float Clamp01(float value)
    {
        if (value < 0.0f)
        {
            return 0.0f;
        }

        return value > 1.0f ? 1.0f : value;
    }
}

/// <summary>
/// Normalized linear curve used by particle lifetime modules.
/// </summary>
public sealed class FloatCurve
{
    private readonly List<FloatCurveKey> _keys = new();

    public IReadOnlyList<FloatCurveKey> Keys => _keys;

    public int Count => _keys.Count;

    public float Evaluate(float time)
    {
        if (_keys.Count == 0)
        {
            return 0.0f;
        }

        float normalizedTime = Clamp01(time);
        FloatCurveKey firstKey = _keys[0];
        if (normalizedTime <= firstKey.Time)
        {
            return firstKey.Value;
        }

        int lastIndex = _keys.Count - 1;
        FloatCurveKey lastKey = _keys[lastIndex];
        if (normalizedTime >= lastKey.Time)
        {
            return lastKey.Value;
        }

        FloatCurveKey previousKey = firstKey;
        for (int keyIndex = 1; keyIndex <= lastIndex; keyIndex++)
        {
            FloatCurveKey nextKey = _keys[keyIndex];
            if (normalizedTime > nextKey.Time)
            {
                previousKey = nextKey;
                continue;
            }

            float segmentLength = nextKey.Time - previousKey.Time;
            if (segmentLength <= 0.0f)
            {
                return nextKey.Value;
            }

            float segmentT = (normalizedTime - previousKey.Time) / segmentLength;
            return previousKey.Value + (nextKey.Value - previousKey.Value) * segmentT;
        }

        return lastKey.Value;
    }

    public void AddKey(float time, float value)
        => AddKey(new FloatCurveKey(time, value));

    public void AddKey(FloatCurveKey key)
    {
        for (int keyIndex = 0; keyIndex < _keys.Count; keyIndex++)
        {
            if (key.Time < _keys[keyIndex].Time)
            {
                _keys.Insert(keyIndex, key);
                return;
            }
        }

        _keys.Add(key);
    }

    public bool RemoveKeyAt(int index)
    {
        if (index < 0 || index >= _keys.Count)
        {
            return false;
        }

        _keys.RemoveAt(index);
        return true;
    }

    public void Clear()
    {
        _keys.Clear();
    }

    public static FloatCurve Constant(float value)
    {
        var curve = new FloatCurve();
        curve.AddKey(0.0f, value);
        curve.AddKey(1.0f, value);
        return curve;
    }

    public static FloatCurve FadeIn()
    {
        var curve = new FloatCurve();
        curve.AddKey(0.0f, 0.0f);
        curve.AddKey(1.0f, 1.0f);
        return curve;
    }

    public static FloatCurve FadeOut()
    {
        var curve = new FloatCurve();
        curve.AddKey(0.0f, 1.0f);
        curve.AddKey(1.0f, 0.0f);
        return curve;
    }

    public static FloatCurve Bell()
    {
        var curve = new FloatCurve();
        curve.AddKey(0.0f, 0.0f);
        curve.AddKey(0.5f, 1.0f);
        curve.AddKey(1.0f, 0.0f);
        return curve;
    }

    public static FloatCurve Pulse()
    {
        var curve = new FloatCurve();
        curve.AddKey(0.0f, 0.0f);
        curve.AddKey(0.1f, 1.0f);
        curve.AddKey(0.9f, 1.0f);
        curve.AddKey(1.0f, 0.0f);
        return curve;
    }

    public static FloatCurve EaseOut()
    {
        var curve = new FloatCurve();
        curve.AddKey(0.0f, 1.0f);
        curve.AddKey(0.35f, 0.65f);
        curve.AddKey(1.0f, 0.0f);
        return curve;
    }

    public static FloatCurve Pop()
    {
        var curve = new FloatCurve();
        curve.AddKey(0.0f, 0.45f);
        curve.AddKey(0.25f, 1.25f);
        curve.AddKey(1.0f, 0.65f);
        return curve;
    }

    private static float Clamp01(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return 0.0f;
        }

        if (value < 0.0f)
        {
            return 0.0f;
        }

        return value > 1.0f ? 1.0f : value;
    }
}