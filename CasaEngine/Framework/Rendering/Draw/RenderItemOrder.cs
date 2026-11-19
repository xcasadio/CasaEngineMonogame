namespace CasaEngine.Framework.Rendering.Draw;

/// <summary>
/// Makes the draw order of a frame's <see cref="RenderItem"/>s a pure function of what is submitted and of the submission
/// order (ADR-0125). Two sources of run-to-run variation are removed:
/// <list type="bullet">
/// <item>The mesh and material parts of the <see cref="SortKeyGenerator"/> key are ids given by order of first appearance in
/// the frame, instead of identity hash codes of the vertex buffer and of a generated material's random <see cref="Guid"/>.
/// Items that share a buffer or a material id still share the id, so the state grouping is unchanged.</item>
/// <item>The comparison breaks ties on <see cref="RenderItem.SubmissionIndex"/>: <see cref="List{T}.Sort(Comparison{T})"/> is
/// not stable, and equal keys are common (the same mesh and material at several transforms, sub-meshes of one mesh,
/// transparent items at the same quantised distance).</item>
/// </list>
/// One instance serves one renderer: <see cref="Reset"/> starts a frame, then <see cref="GenerateKey"/> is called for each
/// item in submission order. The tables are reused from frame to frame and allocate nothing once warm.
/// </summary>
internal sealed class RenderItemOrder
{
    // Ids are keyed by reference for buffers (the buffer type may override equality) and by the material id value, so that
    // two material instances sharing an id keep sharing a key, as they did with the id hash.
    private readonly Dictionary<object, int> _meshIds = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Guid, int> _materialIds = new();

    /// <summary>Compares by <see cref="RenderItem.SortKey"/>, then by <see cref="RenderItem.SubmissionIndex"/>. Cached: no allocation per sort.</summary>
    public static readonly Comparison<RenderItem> Comparison = Compare;

    /// <summary>Starts a new frame: first-appearance ids restart from 0.</summary>
    public void Reset()
    {
        _meshIds.Clear();
        _materialIds.Clear();
    }

    /// <summary>
    /// Builds the sort key of the next submitted item. <paramref name="shaderId"/> is hashed as before (shader ids are
    /// stable); the mesh and material parts are the first-appearance ids of <paramref name="vertexBuffer"/> and
    /// <paramref name="materialId"/> in the current frame (truncated to 16 bits by the key layout).
    /// </summary>
    public ulong GenerateKey(RenderQueue queue, Guid shaderId, Guid materialId, object vertexBuffer, float distance)
    {
        if (!_materialIds.TryGetValue(materialId, out int materialIndex))
        {
            materialIndex = _materialIds.Count;
            _materialIds.Add(materialId, materialIndex);
        }

        if (!_meshIds.TryGetValue(vertexBuffer, out int meshIndex))
        {
            meshIndex = _meshIds.Count;
            _meshIds.Add(vertexBuffer, meshIndex);
        }

        return SortKeyGenerator.Generate(queue, shaderId.GetHashCode(), materialIndex, meshIndex, distance);
    }

    public static int Compare(RenderItem a, RenderItem b)
    {
        int byKey = a.SortKey.CompareTo(b.SortKey);
        return byKey != 0 ? byKey : a.SubmissionIndex.CompareTo(b.SubmissionIndex);
    }
}
