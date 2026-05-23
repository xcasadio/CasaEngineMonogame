namespace CasaEngine.Framework.AI.Navigation;

public sealed class NavigationAgentSettings
{
    public float Radius { get; set; } = 0.35f;

    public float Height { get; set; } = 1.8f;

    public float MaxSpeed { get; set; } = 3f;

    public NavigationLayerMask LayerMask { get; set; } = NavigationLayerMask.All;
}