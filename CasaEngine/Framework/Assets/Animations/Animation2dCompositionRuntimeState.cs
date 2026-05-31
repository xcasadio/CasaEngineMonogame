namespace CasaEngine.Framework.Assets.Animations;

public sealed class Animation2dCompositionRuntimeState
{
    private readonly List<Animation2dPartRuntimeState> _parts = new();
    private readonly List<int> _drawPartIndices = new();
    private readonly Dictionary<string, int> _partIndexById = new(StringComparer.Ordinal);
    private readonly DrawPartIndexComparer _drawPartIndexComparer;

    public Animation2dCompositionRuntimeState()
    {
        _drawPartIndexComparer = new DrawPartIndexComparer(this);
    }

    public IReadOnlyList<Animation2dPartRuntimeState> Parts => _parts;

    public IReadOnlyList<int> DrawPartIndices => _drawPartIndices;

    public int PartCount => _parts.Count;

    public void Reset(Animation2dCompositionData composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        _partIndexById.Clear();

        while (_parts.Count < composition.Parts.Count)
        {
            _parts.Add(new Animation2dPartRuntimeState());
        }

        if (_parts.Count > composition.Parts.Count)
        {
            _parts.RemoveRange(composition.Parts.Count, _parts.Count - composition.Parts.Count);
        }

        _drawPartIndices.Clear();

        for (var partIndex = 0; partIndex < composition.Parts.Count; partIndex++)
        {
            var part = composition.Parts[partIndex];
            _parts[partIndex].Reset(part, partIndex);
            _partIndexById[part.Id] = partIndex;
            _drawPartIndices.Add(partIndex);
        }

        UpdateDrawOrder();
    }

    internal void ApplyDefaults(Animation2dCompositionData composition)
    {
        for (var partIndex = 0; partIndex < composition.Parts.Count; partIndex++)
        {
            _parts[partIndex].Reset(composition.Parts[partIndex], partIndex);
        }
    }

    internal void UpdateDrawOrder()
    {
        _drawPartIndices.Sort(_drawPartIndexComparer);
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

    private sealed class DrawPartIndexComparer : IComparer<int>
    {
        private readonly Animation2dCompositionRuntimeState _state;

        public DrawPartIndexComparer(Animation2dCompositionRuntimeState state)
        {
            _state = state;
        }

        public int Compare(int x, int y)
        {
            var xPart = _state._parts[x];
            var yPart = _state._parts[y];
            var drawOrderComparison = xPart.DrawOrder.CompareTo(yPart.DrawOrder);
            return drawOrderComparison != 0
                ? drawOrderComparison
                : xPart.SourceIndex.CompareTo(yPart.SourceIndex);
        }
    }
}