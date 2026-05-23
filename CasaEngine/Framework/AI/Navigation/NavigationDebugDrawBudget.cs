namespace CasaEngine.Framework.AI.Navigation;

public sealed class NavigationDebugDrawBudget
{
    public NavigationDebugDrawBudget(int maxPrimitiveCount)
    {
        if (maxPrimitiveCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPrimitiveCount));
        }

        MaxPrimitiveCount = maxPrimitiveCount;
    }

    public int MaxPrimitiveCount { get; }

    public int UsedPrimitiveCount { get; private set; }

    public int RemainingPrimitiveCount => Math.Max(0, MaxPrimitiveCount - UsedPrimitiveCount);

    public bool TryConsume()
    {
        if (UsedPrimitiveCount >= MaxPrimitiveCount)
        {
            return false;
        }

        UsedPrimitiveCount++;
        return true;
    }

    public void Reset()
    {
        UsedPrimitiveCount = 0;
    }
}