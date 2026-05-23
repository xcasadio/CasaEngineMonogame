namespace CasaEngine.Framework.AI.Navigation;

public sealed class NavigationQuery
{
    public NavigationAgentSettings AgentSettings { get; set; } = new();

    public NavigationLayerMask LayerMask { get; set; } = NavigationLayerMask.All;

    public bool AllowDiagonalMovement { get; set; }

    public bool PreventDiagonalCornerCutting { get; set; } = true;

    public bool CanEnter(NavigationGridCell cell)
    {
        return cell.CanEnter(LayerMask);
    }
}