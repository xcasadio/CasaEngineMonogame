using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace TomShane.Neoforce.Controls;
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

public abstract class MenuBase : Control
{
    protected internal int ItemIndex { get; set; } = -1;

    protected internal MenuBase ChildMenu { get; set; }

    protected internal MenuBase RootMenu { get; set; }

    protected internal MenuBase ParentMenu { get; set; }

    public List<MenuItem> Items { get; } = new();

    public MenuBase(Manager manager) : base(manager)
    {
        RootMenu = this;
    }

}