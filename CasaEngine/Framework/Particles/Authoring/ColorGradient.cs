using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Particles.Authoring;

/// <summary>
/// Color key on a normalized particle gradient.
/// </summary>
public readonly struct ColorGradientKey : IEquatable<ColorGradientKey>
{
    public ColorGradientKey(float time, Color color)
    {
        if (float.IsNaN(time) || float.IsInfinity(time))
        {
            throw new ArgumentOutOfRangeException(nameof(time), time, "Gradient key time must be finite.");
        }

        Time = Clamp01(time);
        Color = color;
    }

    public float Time { get; }

    public Color Color { get; }

    public bool Equals(ColorGradientKey other)
        => Time.Equals(other.Time) && Color.Equals(other.Color);

    public override bool Equals(object? obj)
        => obj is ColorGradientKey other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(Time, Color);

    public static bool operator ==(ColorGradientKey left, ColorGradientKey right)
        => left.Equals(right);

    public static bool operator !=(ColorGradientKey left, ColorGradientKey right)
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
/// Alpha key on a normalized particle gradient.
/// </summary>
public readonly struct AlphaGradientKey : IEquatable<AlphaGradientKey>
{
    public AlphaGradientKey(float time, float alpha)
    {
        if (float.IsNaN(time) || float.IsInfinity(time))
        {
            throw new ArgumentOutOfRangeException(nameof(time), time, "Gradient key time must be finite.");
        }

        if (float.IsNaN(alpha) || float.IsInfinity(alpha))
        {
            throw new ArgumentOutOfRangeException(nameof(alpha), alpha, "Gradient alpha must be finite.");
        }

        Time = Clamp01(time);
        Alpha = Clamp01(alpha);
    }

    public float Time { get; }

    public float Alpha { get; }

    public bool Equals(AlphaGradientKey other)
        => Time.Equals(other.Time) && Alpha.Equals(other.Alpha);

    public override bool Equals(object? obj)
        => obj is AlphaGradientKey other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(Time, Alpha);

    public static bool operator ==(AlphaGradientKey left, AlphaGradientKey right)
        => left.Equals(right);

    public static bool operator !=(AlphaGradientKey left, AlphaGradientKey right)
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
/// Linear color and alpha gradient evaluated by normalized particle age.
/// </summary>
public sealed class ColorGradient
{
    private readonly List<ColorGradientKey> _colorKeys = new();
    private readonly List<AlphaGradientKey> _alphaKeys = new();

    public IReadOnlyList<ColorGradientKey> ColorKeys => _colorKeys;

    public IReadOnlyList<AlphaGradientKey> AlphaKeys => _alphaKeys;

    public Color Evaluate(float time)
    {
        float normalizedTime = Clamp01(time);
        Color color = EvaluateColor(normalizedTime);

        if (_alphaKeys.Count == 0)
        {
            return color;
        }

        byte alpha = ToByte(EvaluateAlpha(normalizedTime) * 255.0f);
        return new Color(color.R, color.G, color.B, alpha);
    }

    public void AddColorKey(float time, Color color)
        => AddColorKey(new ColorGradientKey(time, color));

    public void AddColorKey(ColorGradientKey key)
        => InsertSorted(_colorKeys, key.Time, key);

    public void AddAlphaKey(float time, float alpha)
        => AddAlphaKey(new AlphaGradientKey(time, alpha));

    public void AddAlphaKey(AlphaGradientKey key)
        => InsertSorted(_alphaKeys, key.Time, key);

    public bool RemoveColorKeyAt(int index)
    {
        if (index < 0 || index >= _colorKeys.Count)
        {
            return false;
        }

        _colorKeys.RemoveAt(index);
        return true;
    }

    public bool RemoveAlphaKeyAt(int index)
    {
        if (index < 0 || index >= _alphaKeys.Count)
        {
            return false;
        }

        _alphaKeys.RemoveAt(index);
        return true;
    }

    public void Clear()
    {
        _colorKeys.Clear();
        _alphaKeys.Clear();
    }

    public static ColorGradient Constant(Color color)
    {
        var gradient = new ColorGradient();
        gradient.AddColorKey(0.0f, color);
        gradient.AddColorKey(1.0f, color);
        return gradient;
    }

    public static ColorGradient White => Constant(Color.White);

    public static ColorGradient Fire()
    {
        var gradient = new ColorGradient();
        gradient.AddColorKey(0.0f, new Color(255, 240, 160));
        gradient.AddColorKey(0.35f, new Color(255, 96, 16));
        gradient.AddColorKey(1.0f, new Color(48, 18, 8));
        gradient.AddAlphaKey(0.0f, 0.0f);
        gradient.AddAlphaKey(0.1f, 1.0f);
        gradient.AddAlphaKey(1.0f, 0.0f);
        return gradient;
    }

    public static ColorGradient Smoke()
    {
        var gradient = new ColorGradient();
        gradient.AddColorKey(0.0f, new Color(70, 70, 70));
        gradient.AddColorKey(1.0f, new Color(170, 170, 170));
        gradient.AddAlphaKey(0.0f, 0.0f);
        gradient.AddAlphaKey(0.2f, 0.6f);
        gradient.AddAlphaKey(1.0f, 0.0f);
        return gradient;
    }

    public static ColorGradient MagicBlue()
    {
        var gradient = new ColorGradient();
        gradient.AddColorKey(0.0f, new Color(100, 230, 255));
        gradient.AddColorKey(1.0f, new Color(32, 72, 255));
        gradient.AddAlphaKey(0.0f, 0.0f);
        gradient.AddAlphaKey(0.15f, 1.0f);
        gradient.AddAlphaKey(1.0f, 0.0f);
        return gradient;
    }

    private Color EvaluateColor(float normalizedTime)
    {
        if (_colorKeys.Count == 0)
        {
            return Color.White;
        }

        ColorGradientKey firstKey = _colorKeys[0];
        if (normalizedTime <= firstKey.Time)
        {
            return firstKey.Color;
        }

        int lastIndex = _colorKeys.Count - 1;
        ColorGradientKey lastKey = _colorKeys[lastIndex];
        if (normalizedTime >= lastKey.Time)
        {
            return lastKey.Color;
        }

        ColorGradientKey previousKey = firstKey;
        for (int keyIndex = 1; keyIndex <= lastIndex; keyIndex++)
        {
            ColorGradientKey nextKey = _colorKeys[keyIndex];
            if (normalizedTime > nextKey.Time)
            {
                previousKey = nextKey;
                continue;
            }

            float segmentLength = nextKey.Time - previousKey.Time;
            if (segmentLength <= 0.0f)
            {
                return nextKey.Color;
            }

            float segmentT = (normalizedTime - previousKey.Time) / segmentLength;
            return Lerp(previousKey.Color, nextKey.Color, segmentT);
        }

        return lastKey.Color;
    }

    private float EvaluateAlpha(float normalizedTime)
    {
        AlphaGradientKey firstKey = _alphaKeys[0];
        if (normalizedTime <= firstKey.Time)
        {
            return firstKey.Alpha;
        }

        int lastIndex = _alphaKeys.Count - 1;
        AlphaGradientKey lastKey = _alphaKeys[lastIndex];
        if (normalizedTime >= lastKey.Time)
        {
            return lastKey.Alpha;
        }

        AlphaGradientKey previousKey = firstKey;
        for (int keyIndex = 1; keyIndex <= lastIndex; keyIndex++)
        {
            AlphaGradientKey nextKey = _alphaKeys[keyIndex];
            if (normalizedTime > nextKey.Time)
            {
                previousKey = nextKey;
                continue;
            }

            float segmentLength = nextKey.Time - previousKey.Time;
            if (segmentLength <= 0.0f)
            {
                return nextKey.Alpha;
            }

            float segmentT = (normalizedTime - previousKey.Time) / segmentLength;
            return previousKey.Alpha + (nextKey.Alpha - previousKey.Alpha) * segmentT;
        }

        return lastKey.Alpha;
    }

    private static Color Lerp(Color from, Color to, float amount)
        => new(
            ToByte(from.R + (to.R - from.R) * amount),
            ToByte(from.G + (to.G - from.G) * amount),
            ToByte(from.B + (to.B - from.B) * amount),
            ToByte(from.A + (to.A - from.A) * amount));

    private static byte ToByte(float value)
    {
        if (value <= 0.0f)
        {
            return 0;
        }

        if (value >= 255.0f)
        {
            return 255;
        }

        return (byte)MathF.Round(value);
    }

    private static void InsertSorted<T>(List<T> keys, float time, T key)
    {
        for (int keyIndex = 0; keyIndex < keys.Count; keyIndex++)
        {
            float existingTime = keys[keyIndex] switch
            {
                ColorGradientKey colorKey => colorKey.Time,
                AlphaGradientKey alphaKey => alphaKey.Time,
                _ => 0.0f,
            };

            if (time < existingTime)
            {
                keys.Insert(keyIndex, key);
                return;
            }
        }

        keys.Add(key);
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