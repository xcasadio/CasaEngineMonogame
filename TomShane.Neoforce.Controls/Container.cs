using Microsoft.Xna.Framework;

namespace TomShane.Neoforce.Controls;

public struct ScrollBarValue
{
    public int Vertical;
    public int Horizontal;
}

public class Container : ClipControl
{
    private MainMenu _mainMenu;
    private ToolBarPanel _toolBarPanel;
    private StatusBar _statusBar;

    /// <summary>
    /// Scroll by PageSize (true) or StepSize (false)
    /// </summary>
    private bool _scrollAlot = true;

    public ScrollBarValue ScrollBarValue
    {
        get
        {
            var scb = new ScrollBarValue();
            scb.Vertical = VerticalScrollBar != null ? VerticalScrollBar.Value : 0;
            scb.Horizontal = HorizontalScrollBar != null ? HorizontalScrollBar.Value : 0;
            return scb;
        }
    }

    public override bool Visible
    {
        get => base.Visible;
        set
        {
            if (value)
            {
                if (DefaultControl != null)
                {
                    DefaultControl.Focused = true;
                }
            }
            base.Visible = value;
        }
    }

    public Control DefaultControl { get; set; }

    public bool AutoScroll { get; set; }

    public MainMenu MainMenu
    {
        get => _mainMenu;
        set
        {
            if (_mainMenu != null)
            {
                _mainMenu.Resize -= Bars_Resize;
                Remove(_mainMenu);
            }
            _mainMenu = value;

            if (_mainMenu != null)
            {
                Add(_mainMenu, false);
                _mainMenu.Resize += Bars_Resize;
            }
            AdjustMargins();
        }
    }

    public ToolBarPanel ToolBarPanel
    {
        get => _toolBarPanel;
        set
        {
            if (_toolBarPanel != null)
            {
                _toolBarPanel.Resize -= Bars_Resize;
                Remove(_toolBarPanel);
            }
            _toolBarPanel = value;

            if (_toolBarPanel != null)
            {
                Add(_toolBarPanel, false);
                _toolBarPanel.Resize += Bars_Resize;
            }
            AdjustMargins();
        }
    }

    public StatusBar StatusBar
    {
        get => _statusBar;
        set
        {
            if (_statusBar != null)
            {
                _statusBar.Resize -= Bars_Resize;
                Remove(_statusBar);
            }
            _statusBar = value;

            if (_statusBar != null)
            {
                Add(_statusBar, false);
                _statusBar.Resize += Bars_Resize;
            }
            AdjustMargins();
        }
    }

    /// <summary>
    /// Scroll by PageSize (true) or StepSize (false)
    /// </summary>
    public bool ScrollAlot
    {
        get => _scrollAlot;
        set => _scrollAlot = value;
    }

    /// <summary>
    /// Gets the container's vertical scroll bar.
    /// </summary>
    protected ScrollBar VerticalScrollBar { get; }

    /// <summary>
    /// Gets the container's horizontal scroll bar.
    /// </summary>
    protected ScrollBar HorizontalScrollBar { get; }

    public Container(Manager manager) : base(manager)
    {
        VerticalScrollBar = new ScrollBar(manager, Orientation.Vertical);
        VerticalScrollBar.Init();
        VerticalScrollBar.Detached = false;
        VerticalScrollBar.Anchor = Anchors.Top | Anchors.Right | Anchors.Bottom;
        VerticalScrollBar.ValueChanged += new EventHandler(ScrollBarValueChanged);
        VerticalScrollBar.Range = 0;
        VerticalScrollBar.PageSize = 0;
        VerticalScrollBar.Value = 0;
        VerticalScrollBar.Visible = false;

        HorizontalScrollBar = new ScrollBar(manager, Orientation.Horizontal);
        HorizontalScrollBar.Init();
        HorizontalScrollBar.Detached = false;
        HorizontalScrollBar.Anchor = Anchors.Right | Anchors.Left | Anchors.Bottom;
        HorizontalScrollBar.ValueChanged += new EventHandler(ScrollBarValueChanged);
        HorizontalScrollBar.Range = 0;
        HorizontalScrollBar.PageSize = 0;
        HorizontalScrollBar.Value = 0;
        HorizontalScrollBar.Visible = false;

        Add(VerticalScrollBar, false);
        Add(HorizontalScrollBar, false);
    }

    public override void Init()
    {
        base.Init();
    }

    protected internal override void InitSkin()
    {
        base.InitSkin();
    }

    private void Bars_Resize(object sender, ResizeEventArgs e)
    {
        AdjustMargins();
    }

    protected override void AdjustMargins()
    {
        var m = Skin.ClientMargins;

        if (GetType() != typeof(Container))
        {
            m = ClientMargins;
        }

        if (_mainMenu != null && _mainMenu.Visible)
        {
            if (!_mainMenu.Initialized)
            {
                _mainMenu.Init();
            }

            _mainMenu.Left = m.Left;
            _mainMenu.Top = m.Top;
            _mainMenu.Width = Width - m.Horizontal;
            _mainMenu.Anchor = Anchors.Left | Anchors.Top | Anchors.Right;

            m.Top += _mainMenu.Height;
        }
        if (_toolBarPanel != null && _toolBarPanel.Visible)
        {
            if (!_toolBarPanel.Initialized)
            {
                _toolBarPanel.Init();
            }

            _toolBarPanel.Left = m.Left;
            _toolBarPanel.Top = m.Top;
            _toolBarPanel.Width = Width - m.Horizontal;
            _toolBarPanel.Anchor = Anchors.Left | Anchors.Top | Anchors.Right;

            m.Top += _toolBarPanel.Height;
        }
        if (_statusBar != null && _statusBar.Visible)
        {
            if (!_statusBar.Initialized)
            {
                _statusBar.Init();
            }

            _statusBar.Left = m.Left;
            _statusBar.Top = Height - m.Bottom - _statusBar.Height;
            _statusBar.Width = Width - m.Horizontal;
            _statusBar.Anchor = Anchors.Left | Anchors.Bottom | Anchors.Right;

            m.Bottom += _statusBar.Height;
        }
        if (VerticalScrollBar != null && VerticalScrollBar.Visible)
        {
            m.Right += VerticalScrollBar.Width + 2;
        }
        if (HorizontalScrollBar != null && HorizontalScrollBar.Visible)
        {
            m.Bottom += HorizontalScrollBar.Height + 2;
        }

        ClientMargins = m;

        PositionScrollBars();

        base.AdjustMargins();
    }

    public override void Add(Control control, bool client)
    {
        base.Add(control, client);
        CalcScrolling();
    }

    protected internal override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
    }

    protected override void DrawControl(Renderer renderer, Rectangle rect, GameTime gameTime)
    {
        base.DrawControl(renderer, rect, gameTime);
    }

    protected internal override void OnSkinChanged(EventArgs e)
    {
        base.OnSkinChanged(e);
        if (VerticalScrollBar != null && HorizontalScrollBar != null)
        {
            VerticalScrollBar.Visible = false;
            HorizontalScrollBar.Visible = false;
            CalcScrolling();
        }
    }

    private void PositionScrollBars()
    {
        if (VerticalScrollBar != null)
        {
            VerticalScrollBar.Left = ClientLeft + ClientWidth + 1;
            VerticalScrollBar.Top = ClientTop + 1;
            var m = HorizontalScrollBar != null && HorizontalScrollBar.Visible ? 0 : 2;
            VerticalScrollBar.Height = ClientArea.Height - m;
            VerticalScrollBar.Range = ClientArea.VirtualHeight;
            VerticalScrollBar.PageSize = ClientArea.ClientHeight;
        }

        if (HorizontalScrollBar != null)
        {
            HorizontalScrollBar.Left = ClientLeft + 1;
            HorizontalScrollBar.Top = ClientTop + ClientHeight + 1;
            var m = VerticalScrollBar != null && VerticalScrollBar.Visible ? 0 : 2;
            HorizontalScrollBar.Width = ClientArea.Width - m;
            HorizontalScrollBar.Range = ClientArea.VirtualWidth;
            HorizontalScrollBar.PageSize = ClientArea.ClientWidth;
        }
    }

    private void CalcScrolling()
    {
        if (VerticalScrollBar != null && AutoScroll)
        {
            var vis = VerticalScrollBar.Visible;
            VerticalScrollBar.Visible = ClientArea.VirtualHeight > ClientArea.ClientHeight;
            if (ClientArea.VirtualHeight <= ClientArea.ClientHeight)
            {
                VerticalScrollBar.Value = 0;
            }

            if (vis != VerticalScrollBar.Visible)
            {
                if (!VerticalScrollBar.Visible)
                {
                    foreach (var c in ClientArea.Controls)
                    {
                        c.TopModifier = 0;
                        c.Invalidate();
                    }
                }
                AdjustMargins();
            }

            PositionScrollBars();
            foreach (var c in ClientArea.Controls)
            {
                c.TopModifier = -VerticalScrollBar.Value;
                c.Invalidate();
            }
        }

        if (HorizontalScrollBar != null && AutoScroll)
        {
            var vis = HorizontalScrollBar.Visible;
            HorizontalScrollBar.Visible = ClientArea.VirtualWidth > ClientArea.ClientWidth;
            if (ClientArea.VirtualWidth <= ClientArea.ClientWidth)
            {
                HorizontalScrollBar.Value = 0;
            }

            if (vis != HorizontalScrollBar.Visible)
            {
                if (!HorizontalScrollBar.Visible)
                {
                    foreach (var c in ClientArea.Controls)
                    {
                        c.LeftModifier = 0;
                        VerticalScrollBar.Refresh();
                        c.Invalidate();
                    }
                }
                AdjustMargins();
            }

            PositionScrollBars();
            foreach (var c in ClientArea.Controls)
            {
                c.LeftModifier = -HorizontalScrollBar.Value;
                HorizontalScrollBar.Refresh();
                c.Invalidate();
            }
        }
    }

    public virtual void ScrollTo(int x, int y)
    {
        VerticalScrollBar.Value = y;
        HorizontalScrollBar.Value = x;
    }

    public virtual void ScrollTo(Control control)
    {
        if (control != null && ClientArea != null && ClientArea.Contains(control, true))
        {
            if (control.AbsoluteTop + control.Height > ClientArea.AbsoluteTop + ClientArea.Height)
            {
                VerticalScrollBar.Value = VerticalScrollBar.Value + control.AbsoluteTop - ClientArea.AbsoluteTop - VerticalScrollBar.PageSize + control.Height;
            }
            else if (control.AbsoluteTop < ClientArea.AbsoluteTop)
            {
                VerticalScrollBar.Value = VerticalScrollBar.Value + control.AbsoluteTop - ClientArea.AbsoluteTop;
            }
            if (control.AbsoluteLeft + control.Width > ClientArea.AbsoluteLeft + ClientArea.Width)
            {
                HorizontalScrollBar.Value = HorizontalScrollBar.Value + control.AbsoluteLeft - ClientArea.AbsoluteLeft - HorizontalScrollBar.PageSize + control.Width;
            }
            else if (control.AbsoluteLeft < ClientArea.AbsoluteLeft)
            {
                HorizontalScrollBar.Value = HorizontalScrollBar.Value + control.AbsoluteLeft - ClientArea.AbsoluteLeft;
            }
        }
    }

    void ScrollBarValueChanged(object sender, EventArgs e)
    {
        CalcScrolling();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        CalcScrolling();

        // Crappy fix to certain scrolling issue
        //if (sbVert != null) sbVert.Value -= 1; 
        //if (sbHorz != null) sbHorz.Value -= 1;      
    }

    public override void Invalidate()
    {
        base.Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        var ex = e as MouseEventArgs;
        ex.Position = new Point(ex.Position.X + HorizontalScrollBar.Value, ex.Position.Y + VerticalScrollBar.Value);

        base.OnClick(e);
    }

    protected override void OnMouseScroll(MouseEventArgs e)
    {
        if (!ClientArea.Enabled)
        {
            return;
        }

        // If current control doesn't scroll, scroll the parent control
        if (VerticalScrollBar.Range - VerticalScrollBar.PageSize < 1)
        {
            Control c = this;

            while (c != null)
            {
                var p = c.Parent as Container;

                if (p != null && p.Enabled)
                {
                    p.OnMouseScroll(e);

                    break;
                }

                c = c.Parent;
            }

            return;
        }

        if (e.ScrollDirection == MouseScrollDirection.Down)
        {
            VerticalScrollBar.ScrollDown(ScrollAlot);
        }
        else
        {
            VerticalScrollBar.ScrollUp(ScrollAlot);
        }

        base.OnMouseScroll(e);
    }
}