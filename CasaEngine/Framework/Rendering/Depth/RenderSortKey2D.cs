namespace CasaEngine.Framework.Rendering.Depth;

public readonly struct RenderSortKey2D : IComparable<RenderSortKey2D>, IEquatable<RenderSortKey2D>
{
    public static readonly RenderSortKey2D Default = new(
        (int)RenderPass2D.YSortedWorld,
        0,
        0,
        0,
        0,
        0,
        0);

    public RenderSortKey2D(
        int renderPass,
        int sortingLayer,
        int orderInLayer,
        int elevation,
        int sortCoordinate,
        int localSortOffset,
        int stableId)
    {
        RenderPass = renderPass;
        SortingLayer = sortingLayer;
        OrderInLayer = orderInLayer;
        Elevation = elevation;
        SortCoordinate = sortCoordinate;
        LocalSortOffset = localSortOffset;
        StableId = stableId;
    }

    public int RenderPass { get; }

    public int SortingLayer { get; }

    public int OrderInLayer { get; }

    public int Elevation { get; }

    public int SortCoordinate { get; }

    public int LocalSortOffset { get; }

    public int StableId { get; }

    public int CompareTo(RenderSortKey2D other)
    {
        var result = RenderPass.CompareTo(other.RenderPass);
        if (result != 0)
        {
            return result;
        }

        result = SortingLayer.CompareTo(other.SortingLayer);
        if (result != 0)
        {
            return result;
        }

        result = OrderInLayer.CompareTo(other.OrderInLayer);
        if (result != 0)
        {
            return result;
        }

        result = Elevation.CompareTo(other.Elevation);
        if (result != 0)
        {
            return result;
        }

        result = SortCoordinate.CompareTo(other.SortCoordinate);
        if (result != 0)
        {
            return result;
        }

        result = LocalSortOffset.CompareTo(other.LocalSortOffset);
        if (result != 0)
        {
            return result;
        }

        return StableId.CompareTo(other.StableId);
    }

    public bool Equals(RenderSortKey2D other)
        => RenderPass == other.RenderPass
           && SortingLayer == other.SortingLayer
           && OrderInLayer == other.OrderInLayer
           && Elevation == other.Elevation
           && SortCoordinate == other.SortCoordinate
           && LocalSortOffset == other.LocalSortOffset
           && StableId == other.StableId;

    public override bool Equals(object obj)
        => obj is RenderSortKey2D other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(RenderPass, SortingLayer, OrderInLayer, Elevation, SortCoordinate, LocalSortOffset, StableId);

    public static bool operator ==(RenderSortKey2D left, RenderSortKey2D right)
        => left.Equals(right);

    public static bool operator !=(RenderSortKey2D left, RenderSortKey2D right)
        => !left.Equals(right);
}
