
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/


using CasaEngine.Framework.UserInterface.Controls.Auxiliary;

namespace CasaEngine.Framework.UserInterface.Controls;

public class TabPage : Container
{


    private Rectangle _headerRectangle = Rectangle.Empty;



    protected internal Rectangle HeaderRectangle => _headerRectangle;


    public TabPage(UserInterfaceManager userInterfaceManager)
        : base(userInterfaceManager)
    {
        Color = Color.Transparent;
        Passive = true;
        CanFocus = false;
    } // TabPage



    protected internal void CalculateRectangle(Rectangle prev, Font font, Margins margins, Point offset, bool first)
    {
        var size = (int)Math.Ceiling(font.MeasureString(Text).X) + margins.Horizontal;

        if (first)
        {
            offset.X = 0;
        }

        _headerRectangle = new Rectangle(prev.Right + offset.X, prev.Top, size, prev.Height);
    } // CalculateRectangle



    protected override void AdjustMargins()
    {
        ClientMargins = new Margins(0, 0, 0, 0);

        base.AdjustMargins();
    } // AdjustMargins


} // TabPage

public class TabControl : Container
{


    private readonly List<TabPage> _tabPages = new();
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
                var c = TabPages[value].ChildrenControls as ControlsList;
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
    } // SelectedIndex

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
    } // SelectedPage



    public event EventHandler PageChanged;



    public TabControl(UserInterfaceManager userInterfaceManager)
        : base(userInterfaceManager)
    {
        CanFocus = false;
    } // TabControl



    protected override void DisposeManagedResources()
    {
        // A disposed object could be still generating events, because it is alive for a time, in a disposed state, but alive nevertheless.
        PageChanged = null;
        base.DisposeManagedResources();
    } // DisposeManagedResources



    protected override void DrawControl(Rectangle rect)
    {
        var l1 = SkinInformation.Layers["Control"];
        var l2 = SkinInformation.Layers["Header"];
        var col = Color != UndefinedColor ? Color : Color.White;

        var r1 = new Rectangle(rect.Left, rect.Top + l1.OffsetY, rect.Width, rect.Height - l1.OffsetY);
        if (_tabPages.Count <= 0)
        {
            r1 = rect;
        }

        base.DrawControl(r1);

        if (_tabPages.Count > 0)
        {

            var prev = new Rectangle(rect.Left, rect.Top + l2.OffsetY, 0, l2.Height);
            for (var i = 0; i < _tabPages.Count; i++)
            {
                var font = l2.Text.Font.Font;
                var margins = l2.ContentMargins;
                var offset = new Point(l2.OffsetX, l2.OffsetY);
                if (i > 0)
                {
                    prev = _tabPages[i - 1].HeaderRectangle;
                }

                _tabPages[i].CalculateRectangle(prev, font, margins, offset, i == 0);
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
                var rx = _tabPages[i].HeaderRectangle;
                var sx = new Rectangle(rx.Left + m.Left, rx.Top + m.Top, rx.Width - m.Horizontal, rx.Height - m.Vertical);
                if (i != _selectedIndex)
                {
                    UserInterfaceManager.Renderer.DrawLayer(l2, rx, col, li);
                    UserInterfaceManager.Renderer.DrawString(l2.Text.Font.Font, _tabPages[i].Text, sx, lc, l2.Text.Alignment);
                }
            }

            var mi = l2.ContentMargins;
            var ri = _tabPages[_selectedIndex].HeaderRectangle;
            var si = new Rectangle(ri.Left + mi.Left, ri.Top + mi.Top, ri.Width - mi.Horizontal, ri.Height - mi.Vertical);
            UserInterfaceManager.Renderer.DrawLayer(l2, ri, col, l2.States.Focused.Index);
            UserInterfaceManager.Renderer.DrawString(l2.Text.Font.Font, _tabPages[_selectedIndex].Text, si, l2.Text.Colors.Focused, l2.Text.Alignment, l2.Text.OffsetX, l2.Text.OffsetY, false);
        }
    } // DrawControl



    public virtual TabPage AddPage(string text)
    {
        var p = AddPage();
        p.Text = text;

        return p;
    } // AddPage

    public virtual TabPage AddPage()
    {
        var page = new TabPage(UserInterfaceManager)
        {
            Left = 0,
            Top = 0,
            Width = ClientWidth,
            Height = ClientHeight,
            Anchor = Anchors.All,
            Text = "Tab " + (_tabPages.Count + 1),
            Visible = false,
            AutoScroll = true,
        };
        Add(page, true);
        _tabPages.Add(page);
        _tabPages[0].Visible = true;

        return page;
    } // AddPage

    public virtual void RemovePage(TabPage page, bool dispose)
    {
        _tabPages.Remove(page);
        if (dispose)
        {
            page.Dispose();
        }
        SelectedIndex = 0;
    } // RemovePage

    public virtual void RemovePage(TabPage page)
    {
        RemovePage(page, true);
    } // RemovePage



    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (_tabPages.Count > 1)
        {
            var p = new Point(e.State.X - Root.ControlLeftAbsoluteCoordinate, e.State.Y - Root.ControlTopAbsoluteCoordinate);
            for (var i = 0; i < _tabPages.Count; i++)
            {
                var r = _tabPages[i].HeaderRectangle;
                if (p.X >= r.Left && p.X <= r.Right && p.Y >= r.Top && p.Y <= r.Bottom)
                {
                    SelectedIndex = i;
                    break;
                }
            }
        }
    } // OnMouseDown

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_tabPages.Count > 1)
        {
            var index = _hoveredIndex;
            var p = new Point(e.State.X - Root.ControlLeftAbsoluteCoordinate, e.State.Y - Root.ControlTopAbsoluteCoordinate);
            for (var i = 0; i < _tabPages.Count; i++)
            {
                var r = _tabPages[i].HeaderRectangle;
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
    } // OnMouseMove

    protected virtual void OnPageChanged(EventArgs e)
    {
        if (PageChanged != null)
        {
            PageChanged.Invoke(this, e);
        }
    } // OnPageChanged


} // TabControl
  //  XNAFinalEngine.UserInterface