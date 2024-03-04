namespace CasaEngine.Framework.GUI.Neoforce;

public class KeyEventArgs : EventArgs
{
    public Keys Key = Keys.None;
    public bool Control;
    public bool Shift;
    public bool Alt;
    public bool Caps;

    public KeyEventArgs()
    {
    }

    public KeyEventArgs(Keys key)
    {
        Key = key;
        Control = false;
        Shift = false;
        Alt = false;
        Caps = false;
    }

    public KeyEventArgs(Keys key, bool control, bool shift, bool alt, bool caps)
    {
        Key = key;
        Control = control;
        Shift = shift;
        Alt = alt;
        Caps = caps;
    }

}