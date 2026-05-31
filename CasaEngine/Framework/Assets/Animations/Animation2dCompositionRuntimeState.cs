namespace CasaEngine.Framework.Assets.Animations;

public sealed class Animation2dCompositionRuntimeState
{
    private readonly List<Animation2dPartRuntimeState> _parts = new();
    private readonly Dictionary<string, int> _partIndexById = new(StringComparer.Ordinal);

    public IReadOnlyList<Animation2dPartRuntimeState> Parts => _parts;

    public int PartCount => _parts.Count;

    public void Reset(Animation2dCompositionData composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        _parts.Clear();
        _partIndexById.Clear();

        for (var partIndex = 0; partIndex < composition.Parts.Count; partIndex++)
        {
            var part = composition.Parts[partIndex];
            var state = new Animation2dPartRuntimeState();
            state.Reset(part, partIndex);
            _parts.Add(state);
            _partIndexById[part.Id] = partIndex;
        }
    }

    public Animation2dPartRuntimeState GetPart(int index)
    {
        return _parts[index];
    }

    public bool TryGetPartIndex(string partId, out int index)
    {
        return _partIndexById.TryGetValue(partId, out index);
    }

    public bool TryGetPart(string partId, out Animation2dPartRuntimeState part)
    {
        if (_partIndexById.TryGetValue(partId, out var index))
        {
            part = _parts[index];
            return true;
        }

        part = null;
        return false;
    }
}