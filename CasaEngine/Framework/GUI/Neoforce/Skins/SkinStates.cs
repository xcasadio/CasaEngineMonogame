namespace CasaEngine.Framework.GUI.Neoforce.Skins;

public struct SkinStates<T>
{
    public T Enabled;
    public T Hovered;
    public T Pressed;
    public T Focused;
    public T Disabled;

    public SkinStates(T enabled, T hovered, T pressed, T focused, T disabled)
    {
        Enabled = enabled;
        Hovered = hovered;
        Pressed = pressed;
        Focused = focused;
        Disabled = disabled;
    }
}