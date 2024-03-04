using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.GUI.Neoforce;

public class MenuItem : Unknown
{
    public readonly string Text = "MenuItem";
    public readonly List<MenuItem> Items = new();
    public readonly bool Separated;
    public readonly Texture2D Image = null;
    public readonly bool Enabled = true;
    public object Tag { get; set; }

    public MenuItem()
    {
    }

    public MenuItem(string text) : this()
    {
        Text = text;
    }

    public MenuItem(string text, bool separated) : this(text)
    {
        Separated = separated;
    }

    public event EventHandler Click;
    public event EventHandler Selected;

    internal void ClickInvoke(EventArgs e)
    {
        Click?.Invoke(this, e);
    }

    internal void SelectedInvoke(EventArgs e)
    {
        Selected?.Invoke(this, e);
    }

}