namespace CasaEngine.Framework.AI.Navigation;

public readonly struct NavigationGridCell
{
    public static readonly NavigationGridCell Blocked = new(false, 1f, NavigationLayerMask.None);

    public NavigationGridCell(bool isWalkable, float cost, NavigationLayerMask layers)
    {
        if (float.IsNaN(cost) || float.IsInfinity(cost) || cost <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(cost), "Navigation cell cost must be a positive finite value.");
        }

        IsWalkable = isWalkable;
        Cost = cost;
        Layers = layers;
    }

    public bool IsWalkable { get; }

    public float Cost { get; }

    public NavigationLayerMask Layers { get; }

    public bool CanEnter(NavigationLayerMask layerMask)
    {
        return IsWalkable && (Layers & layerMask) != NavigationLayerMask.None;
    }
}