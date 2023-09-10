using System;
using System.Collections.Generic;
using FontStashSharp;
using Microsoft.Xna.Framework;

namespace TomShane.Neoforce.Controls;

public class TabControlGamePadActions : GamePadActions
{
    public readonly GamePadButton NextTab = GamePadButton.RightTrigger;
    public readonly GamePadButton PrevTab = GamePadButton.LeftTrigger;
}

public class TabPage : Control
{
    protected internal Rectangle HeaderRect { get; private set; } = Rectangle.Empty;

    public TabPage(Manager manager) : base(manager)
    {
        Color = Color.Transparent;
        Passive = true;
        CanFocus = false;
    }

    protected internal void CalcRect(Rectangle prev, SpriteFontBase font, Margins margins, Point offset, bool first)
    {
        var size = (int)Math.Ceiling(font.MeasureString(Text).X) + margins.Horizontal;

        if (first)
        {
            offset.X = 0;
        }

        HeaderRect = new Rectangle(prev.Right + offset.X, prev.Top, size, prev.Height);
    }

}

public class TabControl : Container
{
    private List<TabPage> _tabPages = new();
    private int _selectedIndex;
    private int _hoveredIndex = -1;

    public TabPage[] TabPages => _tabPages.ToArray();

    public virtual int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_selectedIndex >= 0 && _selectedIndex < _tabPages.Count && value >= 0 && value < _tabPages.Count)
            {
                TabPages[_selectedIndex].Visible = false;
            }
            if (value >= 0 && value < _tabPages.Count)
            {
                TabPages[value].Visible = true;
                var c = TabPages[value].Controls as ControlsList;
                if (c.Count > 0)
                {
                    c[0].Focused = true;
                }

                _selectedIndex = value;
                if (!Suspended)
                {
                    OnPageChanged(new EventArgs());
                }
            }
        }
    }

    public virtual TabPage SelectedPage
    {
        get => _tabPages[SelectedIndex];
        set
        {
            for (var i = 0; i < _tabPages.Count; i++)
            {
                if (_tabPages[i] == value)
                {
                    SelectedIndex = i;
                    break;
                }
            }
        }
    }

    public event EventHandler PageChanged;

    public TabControl(Manager manager) : base(manager)
    {
        GamePadActions = new TabControlGamePadActions();
        Manager.Input.GamePadDown += new GamePadEventHandler(Input_GamePadDown);
        CanFocus = false;
    }

    protected override void DrawControl(Renderer renderer, Rectangle rect, GameTime gameTime)
    {
        var l1 = Skin.Layers["Control"];
        var l2 = Skin.Layers["Header"];
        var col = Color != UndefinedColor ? Color : Color.White;

        var r1 = new Rectangle(rect.Left, rect.Top + l1.OffsetY, rect.Width, rect.Height - l1.OffsetY);
        if (_tabPages.Count <= 0)
        {
            r1 = rect;
        }

        base.DrawControl(renderer, r1, gameTime);

        if (_tabPages.Count > 0)
        {

            var prev = new Rectangle(rect.Left, rect.Top + l2.OffsetY, 0, l2.Height);
            for (var i = 0; i < _tabPages.Count; i++)
            {
                var font = l2.Text.Font.Resource;
                var margins = l2.ContentMargins;
                var offset = new Point(l2.OffsetX, l2.OffsetY);
                if (i > 0)
                {
                    prev = _tabPages[i - 1].HeaderRect;
                }

                _tabPages[i].CalcRect(prev, font, margins, offset, i == 0);
            }

            for (var i = _tabPages.Count - 1; i >= 0; i--)
            {
                var li = _tabPages[i].Enabled ? l2.States.Enabled.Index : l2.States.Disabled.Index;
                var lc = _tabPages[i].Enabled ? l2.Text.Colors.Enabled : l2.Text.Colors.Disabled;
                if (i == _hoveredIndex)
                {
                    li = l2.States.Hovered.Index;
                    lc = l2.Text.Colors.Hovered;
                }

                var m = l2.ContentMargins;
                var rx = _tabPages[i].HeaderRect;
                var sx = new Rectangle(rx.Left + m.Left, rx.Top + m.Top, rx.Width - m.Horizontal, rx.Height - m.Vertical);
                if (i != _selectedIndex)
                {
                    renderer.DrawLayer(l2, rx, col, li);
                    renderer.DrawString(l2.Text.Font.Resource, _tabPages[i].Text, sx, lc, l2.Text.Alignment);
                }
            }

            var mi = l2.ContentMargins;
            var ri = _tabPages[_selectedIndex].HeaderRect;
            var si = new Rectangle(ri.Left + mi.Left, ri.Top + mi.Top, ri.Width - mi.Horizontal, ri.Height - mi.Vertical);
            renderer.DrawLayer(l2, ri, col, l2.States.Focused.Index);
            renderer.DrawString(l2.Text.Font.Resource, _tabPages[_selectedIndex].Text, si, l2.Text.Colors.Focused, l2.Text.Alignment, l2.Text.OffsetX, l2.Text.OffsetY, false);
        }
    }

    public virtual TabPage AddPage(string text)
    {
        var p = AddPage();
        p.Text = text;

        return p;
    }

    public virtual TabPage AddPage()
    {
        var page = new TabPage(Manager);
        page.Init();
        page.Left = 0;
        page.Top = 0;
        page.Width = ClientWidth;
        page.Height = ClientHeight;
        page.Anchor = Anchors.All;
        page.Text = "Tab " + (_tabPages.Count + 1).ToString();
        page.Visible = false;
        Add(page, true);
        _tabPages.Add(page);
        _tabPages[0].Visible = true;

        return page;
    }

    public virtual void RemovePage(TabPage page, bool dispose)
    {
        _tabPages.Remove(page);
        if (dispose)
        {
            page.Dispose();
            page = null;
        }
        SelectedIndex = 0;
    }

    public virtual void RemovePage(TabPage page)
    {
        RemovePage(page, true);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (_tabPages.Count > 1)
        {
            var p = new Point(e.State.X - Root.AbsoluteLeft, e.State.Y - Root.AbsoluteTop);
            for (var i = 0; i < _tabPages.Count; i++)
            {
                var r = _tabPages[i].HeaderRect;
                if (p.X >= r.Left && p.X <= r.Right && p.Y >= r.Top && p.Y <= r.Bottom)
                {
                    SelectedIndex = i;
                    break;
                }
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_tabPages.Count > 1)
        {
            var index = _hoveredIndex;
            var p = new Point(e.State.X - Root.AbsoluteLeft, e.State.Y - Root.AbsoluteTop);
            for (var i = 0; i < _tabPages.Count; i++)
            {
                var r = _tabPages[i].HeaderRect;
                if (p.X >= r.Left && p.X <= r.Right && p.Y >= r.Top && p.Y <= r.Bottom && _tabPages[i].Enabled)
                {
                    index = i;
                    break;
                }

                index = -1;
            }
            if (index != _hoveredIndex)
            {
                _hoveredIndex = index;
                Invalidate();
            }
        }
    }

    void Input_GamePadDown(object sender, GamePadEventArgs e)
    {
        if (Contains(Manager.FocusedControl, true))
        {
            if (e.Button == (GamePadActions as TabControlGamePadActions).NextTab)
            {
                e.Handled = true;
                SelectedIndex += 1;
            }
            else if (e.Button == (GamePadActions as TabControlGamePadActions).PrevTab)
            {
                e.Handled = true;
                SelectedIndex -= 1;
            }
        }
    }

    protected virtual void OnPageChanged(EventArgs e)
    {
        PageChanged?.Invoke(this, e);
    }

}