namespace CasaEngine.Framework.AI.Navigation;

[Flags]
public enum NavigationLayerMask
{
    None = 0,
    Ground = 1 << 0,
    Water = 1 << 1,
    Flying = 1 << 2,
    Door = 1 << 3,
    Dangerous = 1 << 4,
    All = ~0,
}