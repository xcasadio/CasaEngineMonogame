namespace CasaEngine.Framework.Audio.Spatial;

/// <summary>
/// Fixed-capacity table of named game parameters with a value and a change version each. Game thread only.
/// Names are searched linearly (no dictionary): no allocation once a name exists.
/// </summary>
internal sealed class AudioGameParameterRegistry
{
    /// <summary>Number of parameters the registry can hold.</summary>
    public const int Capacity = 64;

    private const string FullMessage = "Audio: the game parameter registry is full (64 parameters), extra parameters are ignored.";

    private readonly string[] _names = new string[Capacity];
    private readonly float[] _values = new float[Capacity];
    private readonly int[] _versions = new int[Capacity];
    private readonly AudioLogThrottle _fullLog = new();
    private int _count;

    public AudioGameParameterRegistry()
    {
        Array.Fill(_values, float.NaN);
    }

    /// <summary>Number of registered names.</summary>
    public int Count => _count;

    /// <summary>
    /// Index of the parameter named <paramref name="name"/> (case-insensitive), registering it when new.
    /// -1 for a null or empty name, or, with one throttled warning, when the registry is full.
    /// </summary>
    public int GetOrCreateIndex(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return -1;
        }

        for (var i = 0; i < _count; i++)
        {
            if (string.Equals(_names[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        if (_count >= Capacity)
        {
            _fullLog.WriteWarning(FullMessage);
            return -1;
        }

        _names[_count] = name;
        return _count++;
    }

    /// <summary>Stores a value; the version is incremented only when the value changes. Invalid indexes are ignored.</summary>
    public void Set(int index, float value)
    {
        if ((uint)index >= (uint)_count)
        {
            return;
        }

        // float.Equals treats two NaN as equal.
        if (_values[index].Equals(value))
        {
            return;
        }

        _values[index] = value;
        _versions[index]++;
    }

    /// <summary>The stored value; NaN when never written or when the index is invalid.</summary>
    public float Get(int index)
    {
        return (uint)index < (uint)_count ? _values[index] : float.NaN;
    }

    /// <summary>The number of value changes of the parameter; 0 for an invalid index.</summary>
    public int GetVersion(int index)
    {
        return (uint)index < (uint)_count ? _versions[index] : 0;
    }
}
