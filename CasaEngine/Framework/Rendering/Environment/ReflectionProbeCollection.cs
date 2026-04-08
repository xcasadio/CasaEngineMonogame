namespace CasaEngine.Framework.Rendering.Environment;

/// <summary>
/// World-owned mutable collection of reflection probes with a lightweight version counter for caches.
/// </summary>
public sealed class ReflectionProbeCollection : IReadOnlyList<ReflectionProbe>
{
    private readonly List<ReflectionProbe> _probes = [];
    private int _version;

    public int Version => _version;

    public int Count => _probes.Count;

    public ReflectionProbe this[int index] => _probes[index];

    public void Add(ReflectionProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        _probes.Add(probe);
        _version++;
    }

    public bool Remove(ReflectionProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        bool removed = _probes.Remove(probe);
        if (removed)
        {
            _version++;
        }

        return removed;
    }

    public bool Remove(Guid probeId)
    {
        for (int index = 0; index < _probes.Count; index++)
        {
            if (_probes[index].Id != probeId)
            {
                continue;
            }

            _probes.RemoveAt(index);
            _version++;
            return true;
        }

        return false;
    }

    public void Clear()
    {
        if (_probes.Count == 0)
        {
            return;
        }

        _probes.Clear();
        _version++;
    }

    public IEnumerator<ReflectionProbe> GetEnumerator() => _probes.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}