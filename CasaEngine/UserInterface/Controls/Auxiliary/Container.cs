
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using CasaEngine.UserInterface.Controls.Menu;
using CasaEngine.UserInterface.Controls.ToolBar;

namespace CasaEngine.UserInterface.Controls.Auxiliary
{

    public struct ScrollBarValue
    {
        public int Vertical;
        public int Horizontal;
    } // ScrollBarValue

    public class Container : ClipControl
    {


        // Controls
        private readonly ScrollBar _scrollBarVertical;
        private readonly ScrollBar _scrollBarHorizontal;
        private ToolBarPanel _toolBarPanel;
        private MainMenu _mainMenu;
        private StatusBar _statusBar;

        // To avoid infinite recursion.
        private bool _adjustingScrolling;

        // The control that has inmediate focus. For example a button for closing a dialog.
        private Control _defaultControl;



        public virtual ScrollBarValue ScrollBarValue
        {
            get
            {
                var scrollBarValue = new ScrollBarValue
                {
                    Vertical = _scrollBarVertical.Value,
                    Horizontal = _scrollBarHorizontal.Value
                };
                return scrollBarValue;
            }
        } // ScrollBarValue

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
        } // Visible

        public virtual Control DefaultControl
        {
            get => _defaultControl;
            set
            {
                _defaultControl = value;
                if (DefaultControl != null)
                {
                    _defaultControl.Focused = true;
                }
            }
        } // DefaultControl

        public virtual bool AutoScroll { get; set; }

        public virtual MainMenu MainMenu
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
                Invalidate();
                AdjustMargins();
            }
        } // MainMenu

        public virtual ToolBarPanel ToolBarPanel
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
                Invalidate();
                AdjustMargins();
            }
        } // ToolBarPanel

        public virtual StatusBar StatusBar
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
                Invalidate();
                AdjustMargins();
            }
        } // StatusBar



        public Container(UserInterfaceManager userInterfaceManager)
            : base(userInterfaceManager)
        {
            DefaultControl = null;
            // Creates the scroll bars.
            _scrollBarVertical = new ScrollBar(UserInterfaceManager, Orientation.Vertical)
            {
                Detached = false,
                Anchor = Anchors.Top | Anchors.Right | Anchors.Bottom,
                Range = 0,
                PageSize = 0,
                Value = 0,
                Visible = false
            };
            _scrollBarVertical.ValueChanged += ScrollBarValueChanged;
            base.Add(_scrollBarVertical, false);

            _scrollBarHorizontal = new ScrollBar(UserInterfaceManager, Orientation.Horizontal)
            {
                Detached = false,
                Anchor = Anchors.Right | Anchors.Left | Anchors.Bottom,
                Range = 0,
                PageSize = 0,
                Value = 0,
                Visible = false
            };
            _scrollBarHorizontal.ValueChanged += ScrollBarValueChanged;
            base.Add(_scrollBarHorizontal, false);

            AdjustMargins();
        } // Container



        protected override void AdjustMargins()
        {
            // We get the default size of the client area.
            var m = SkinInformation.ClientMargins;

            // But probably this was changed in a upper AdjustMargins version.
            if (GetType() != typeof(Container))
            {
                m = ClientMargins;
            }
            // We add space to the menu in the client area is there is one.
            if (_mainMenu != null && _mainMenu.Visible)
            {
                _mainMenu.Left = m.Left;
                _mainMenu.Top = m.Top;
                _mainMenu.Width = Width - m.Horizontal;
                _mainMenu.Anchor = Anchors.Left | Anchors.Top | Anchors.Right;

                m.Top += _mainMenu.Height;
            }
            // We add space to the tool bar panel in the client area is there is one.
            if (_toolBarPanel != null && _toolBarPanel.Visible)
            {
                _toolBarPanel.Left = m.Left;
                _toolBarPanel.Top = m.Top;
                _toolBarPanel.Width = Width - m.Horizontal;
                _toolBarPanel.Anchor = Anchors.Left | Anchors.Top | Anchors.Right;

                m.Top += _toolBarPanel.Height;
            }
            // We add space to the status bar panel in the client area is there is one.
            if (_statusBar != null && _statusBar.Visible)
            {
                _statusBar.Left = m.Left;
                _statusBar.Top = Height - m.Bottom - _statusBar.Height;
                _statusBar.Width = Width - m.Horizontal;
                _statusBar.Anchor = Anchors.Left | Anchors.Bottom | Anchors.Right;

                m.Bottom += _statusBar.Height;
            }
            // We do the same for the scroll bars.
            if (_scrollBarVertical != null) // The null check is for property assigment in the new sentence.
            {
                if (_scrollBarVertical.Visible)
                {
                    m.Right += (_scrollBarVertical.Width + 2);
                }
                if (_scrollBarHorizontal.Visible)
                {
                    m.Bottom += (_scrollBarHorizontal.Height + 2);
                }
            }
            // Update client margins
            ClientMargins = m;

            PositionScrollBars();

            base.AdjustMargins();
        } // AdjustMargins



        private void PositionScrollBars()
        {
            if (_scrollBarVertical != null) // The null check is for property assigment in the new sentence.
            {
                _scrollBarVertical.Left = ClientLeft + ClientWidth + 1;
                _scrollBarVertical.Top = ClientTop + 1;
                _scrollBarVertical.Height = ClientArea.Height - ((_scrollBarHorizontal.Visible) ? 0 : 2);
                _scrollBarVertical.Range = ClientArea.VirtualHeight;
                _scrollBarVertical.PageSize = ClientArea.ClientHeight;

                _scrollBarHorizontal.Left = ClientLeft + 1;
                _scrollBarHorizontal.Top = ClientTop + ClientHeight + 1;
                _scrollBarHorizontal.Width = ClientArea.Width - ((_scrollBarVertical.Visible) ? 0 : 2);
                _scrollBarHorizontal.Range = ClientArea.VirtualWidth;
                _scrollBarHorizontal.PageSize = ClientArea.ClientWidth;
            }
        } // PositionScrollBars



        private void Bars_Resize(object sender, ResizeEventArgs e)
        {
            AdjustMargins();
        } // Bars_Resize

        void ScrollBarValueChanged(object sender, EventArgs e)
        {
            CalculateScrolling();
        } // ScrollBarValueChanged



        public override void Invalidate()
        {
            base.Invalidate();
            CalculateScrolling();
        } // Invalidate



        internal void CalculateScrolling()
        {
            // To avoid infinite recursion.
            if (_adjustingScrolling)
            {
                return;
            }

            _adjustingScrolling = true;

            if (AutoScroll)
            {


                var scrollBarVisible = _scrollBarVertical.Visible;
                _scrollBarVertical.Visible = ClientArea.VirtualHeight > ClientArea.ClientHeight;
                if (ClientArea.VirtualHeight <= ClientArea.ClientHeight)
                {
                    _scrollBarVertical.Value = 0;
                }

                // If visibility changes...
                if (scrollBarVisible != _scrollBarVertical.Visible)
                {
                    if (!_scrollBarVertical.Visible)
                    {
                        foreach (var c in ClientArea.ChildrenControls)
                        {
                            c.VerticalScrollingAmount = 0;
                            _scrollBarHorizontal.Refresh();
                            c.Invalidate();
                        }
                    }
                    AdjustMargins();
                }
                PositionScrollBars();

                foreach (var childControl in ClientArea.ChildrenControls)
                {
                    childControl.VerticalScrollingAmount = -_scrollBarVertical.Value;
                    _scrollBarVertical.Refresh();
                    childControl.Invalidate();
                }



                scrollBarVisible = _scrollBarHorizontal.Visible;
                _scrollBarHorizontal.Visible = ClientArea.VirtualWidth > ClientArea.ClientWidth;
                if (ClientArea.VirtualWidth <= ClientArea.ClientWidth)
                {
                    _scrollBarHorizontal.Value = 0;
                }

                if (scrollBarVisible != _scrollBarHorizontal.Visible)
                {
                    if (!_scrollBarHorizontal.Visible)
                    {
                        foreach (var c in ClientArea.ChildrenControls)
                        {
                            c.HorizontalScrollingAmount = 0;
                            _scrollBarVertical.Refresh();
                            c.Invalidate();
                        }
                    }
                    AdjustMargins();
                }
                PositionScrollBars();

                foreach (var childControl in ClientArea.ChildrenControls)
                {
                    childControl.HorizontalScrollingAmount = -_scrollBarHorizontal.Value;
                    _scrollBarHorizontal.Refresh();
                    childControl.Invalidate();
                }


            }
            _adjustingScrolling = false;
        } // CalculateScrolling



        public virtual void ScrollTo(Control control)
        {
            if (control != null && ClientArea != null && ClientArea.Contains(control))
            {
                if (control.ControlTopAbsoluteCoordinate + control.Height > ClientArea.ControlTopAbsoluteCoordinate + ClientArea.Height)
                {
                    _scrollBarVertical.Value = _scrollBarVertical.Value + control.ControlTopAbsoluteCoordinate - ClientArea.ControlTopAbsoluteCoordinate - _scrollBarVertical.PageSize + control.Height;
                }
                else if (control.ControlTopAbsoluteCoordinate < ClientArea.ControlTopAbsoluteCoordinate)
                {
                    _scrollBarVertical.Value = _scrollBarVertical.Value + control.ControlTopAbsoluteCoordinate - ClientArea.ControlTopAbsoluteCoordinate;
                }
                if (control.ControlLeftAbsoluteCoordinate + control.Width > ClientArea.ControlLeftAbsoluteCoordinate + ClientArea.Width)
                {
                    _scrollBarHorizontal.Value = _scrollBarHorizontal.Value + control.ControlLeftAbsoluteCoordinate - ClientArea.ControlLeftAbsoluteCoordinate - _scrollBarHorizontal.PageSize + control.Width;
                }
                else if (control.ControlLeftAbsoluteCoordinate < ClientArea.ControlLeftAbsoluteCoordinate)
                {
                    _scrollBarHorizontal.Value = _scrollBarHorizontal.Value + control.ControlLeftAbsoluteCoordinate - ClientArea.ControlLeftAbsoluteCoordinate;
                }
            }
        } // ScrollTo         



        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            CalculateScrolling();
        } // OnResize

        protected override void OnClick(EventArgs e)
        {
            var ex = e as MouseEventArgs;
            ex.Position = new Point(ex.Position.X + _scrollBarHorizontal.Value, ex.Position.Y + _scrollBarVertical.Value);
            base.OnClick(e);
        } // OnClick

        protected internal override void OnSkinChanged(EventArgs e)
        {
            base.OnSkinChanged(e);
            if (_scrollBarVertical != null && _scrollBarHorizontal != null)
            {
                _scrollBarVertical.Visible = false;
                _scrollBarHorizontal.Visible = false;
                CalculateScrolling();
            }
        } // OnSkinChanged


    } // Container
} // XNAFinalEngine.UserInterface