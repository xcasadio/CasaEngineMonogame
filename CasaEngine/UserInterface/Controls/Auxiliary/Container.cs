
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

namespace XNAFinalEngine.UserInterface
{

    public struct ScrollBarValue
    {
        public int Vertical;
        public int Horizontal;
    } // ScrollBarValue

    public class Container : ClipControl
    {


        // Controls
        private readonly ScrollBar scrollBarVertical;
        private readonly ScrollBar scrollBarHorizontal;
        private ToolBarPanel toolBarPanel;
        private MainMenu mainMenu;
        private StatusBar statusBar;

        // To avoid infinite recursion.
        private bool adjustingScrolling;

        // The control that has inmediate focus. For example a button for closing a dialog.
        private Control defaultControl;



        public virtual ScrollBarValue ScrollBarValue
        {
            get
            {
                ScrollBarValue scrollBarValue = new ScrollBarValue
                {
                    Vertical = scrollBarVertical.Value,
                    Horizontal = scrollBarHorizontal.Value
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
                        DefaultControl.Focused = true;
                }
                base.Visible = value;
            }
        } // Visible

        public virtual Control DefaultControl
        {
            get => defaultControl;
            set
            {
                defaultControl = value;
                if (DefaultControl != null)
                    defaultControl.Focused = true;
            }
        } // DefaultControl

        public virtual bool AutoScroll { get; set; }

        public virtual MainMenu MainMenu
        {
            get => mainMenu;
            set
            {
                if (mainMenu != null)
                {
                    mainMenu.Resize -= Bars_Resize;
                    Remove(mainMenu);
                }
                mainMenu = value;

                if (mainMenu != null)
                {
                    Add(mainMenu, false);
                    mainMenu.Resize += Bars_Resize;
                }
                Invalidate();
                AdjustMargins();
            }
        } // MainMenu

        public virtual ToolBarPanel ToolBarPanel
        {
            get => toolBarPanel;
            set
            {
                if (toolBarPanel != null)
                {
                    toolBarPanel.Resize -= Bars_Resize;
                    Remove(toolBarPanel);
                }
                toolBarPanel = value;

                if (toolBarPanel != null)
                {
                    Add(toolBarPanel, false);
                    toolBarPanel.Resize += Bars_Resize;
                }
                Invalidate();
                AdjustMargins();
            }
        } // ToolBarPanel

        public virtual StatusBar StatusBar
        {
            get => statusBar;
            set
            {
                if (statusBar != null)
                {
                    statusBar.Resize -= Bars_Resize;
                    Remove(statusBar);
                }
                statusBar = value;

                if (statusBar != null)
                {
                    Add(statusBar, false);
                    statusBar.Resize += Bars_Resize;
                }
                Invalidate();
                AdjustMargins();
            }
        } // StatusBar



        public Container(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            DefaultControl = null;
            // Creates the scroll bars.
            scrollBarVertical = new ScrollBar(UserInterfaceManager, Orientation.Vertical)
            {
                Detached = false,
                Anchor = Anchors.Top | Anchors.Right | Anchors.Bottom,
                Range = 0,
                PageSize = 0,
                Value = 0,
                Visible = false
            };
            scrollBarVertical.ValueChanged += ScrollBarValueChanged;
            base.Add(scrollBarVertical, false);

            scrollBarHorizontal = new ScrollBar(UserInterfaceManager, Orientation.Horizontal)
            {
                Detached = false,
                Anchor = Anchors.Right | Anchors.Left | Anchors.Bottom,
                Range = 0,
                PageSize = 0,
                Value = 0,
                Visible = false
            };
            scrollBarHorizontal.ValueChanged += ScrollBarValueChanged;
            base.Add(scrollBarHorizontal, false);

            AdjustMargins();
        } // Container



        protected override void AdjustMargins()
        {
            // We get the default size of the client area.
            Margins m = SkinInformation.ClientMargins;

            // But probably this was changed in a upper AdjustMargins version.
            if (GetType() != typeof(Container))
            {
                m = ClientMargins;
            }
            // We add space to the menu in the client area is there is one.
            if (mainMenu != null && mainMenu.Visible)
            {
                mainMenu.Left = m.Left;
                mainMenu.Top = m.Top;
                mainMenu.Width = Width - m.Horizontal;
                mainMenu.Anchor = Anchors.Left | Anchors.Top | Anchors.Right;

                m.Top += mainMenu.Height;
            }
            // We add space to the tool bar panel in the client area is there is one.
            if (toolBarPanel != null && toolBarPanel.Visible)
            {
                toolBarPanel.Left = m.Left;
                toolBarPanel.Top = m.Top;
                toolBarPanel.Width = Width - m.Horizontal;
                toolBarPanel.Anchor = Anchors.Left | Anchors.Top | Anchors.Right;

                m.Top += toolBarPanel.Height;
            }
            // We add space to the status bar panel in the client area is there is one.
            if (statusBar != null && statusBar.Visible)
            {
                statusBar.Left = m.Left;
                statusBar.Top = Height - m.Bottom - statusBar.Height;
                statusBar.Width = Width - m.Horizontal;
                statusBar.Anchor = Anchors.Left | Anchors.Bottom | Anchors.Right;

                m.Bottom += statusBar.Height;
            }
            // We do the same for the scroll bars.
            if (scrollBarVertical != null) // The null check is for property assigment in the new sentence.
            {
                if (scrollBarVertical.Visible)
                {
                    m.Right += (scrollBarVertical.Width + 2);
                }
                if (scrollBarHorizontal.Visible)
                {
                    m.Bottom += (scrollBarHorizontal.Height + 2);
                }
            }
            // Update client margins
            ClientMargins = m;

            PositionScrollBars();

            base.AdjustMargins();
        } // AdjustMargins



        private void PositionScrollBars()
        {
            if (scrollBarVertical != null) // The null check is for property assigment in the new sentence.
            {
                scrollBarVertical.Left = ClientLeft + ClientWidth + 1;
                scrollBarVertical.Top = ClientTop + 1;
                scrollBarVertical.Height = ClientArea.Height - ((scrollBarHorizontal.Visible) ? 0 : 2);
                scrollBarVertical.Range = ClientArea.VirtualHeight;
                scrollBarVertical.PageSize = ClientArea.ClientHeight;

                scrollBarHorizontal.Left = ClientLeft + 1;
                scrollBarHorizontal.Top = ClientTop + ClientHeight + 1;
                scrollBarHorizontal.Width = ClientArea.Width - ((scrollBarVertical.Visible) ? 0 : 2);
                scrollBarHorizontal.Range = ClientArea.VirtualWidth;
                scrollBarHorizontal.PageSize = ClientArea.ClientWidth;
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
            if (adjustingScrolling)
                return;
            adjustingScrolling = true;

            if (AutoScroll)
            {


                bool scrollBarVisible = scrollBarVertical.Visible;
                scrollBarVertical.Visible = ClientArea.VirtualHeight > ClientArea.ClientHeight;
                if (ClientArea.VirtualHeight <= ClientArea.ClientHeight)
                    scrollBarVertical.Value = 0;

                // If visibility changes...
                if (scrollBarVisible != scrollBarVertical.Visible)
                {
                    if (!scrollBarVertical.Visible)
                    {
                        foreach (Control c in ClientArea.ChildrenControls)
                        {
                            c.VerticalScrollingAmount = 0;
                            scrollBarHorizontal.Refresh();
                            c.Invalidate();
                        }
                    }
                    AdjustMargins();
                }
                PositionScrollBars();

                foreach (Control childControl in ClientArea.ChildrenControls)
                {
                    childControl.VerticalScrollingAmount = -scrollBarVertical.Value;
                    scrollBarVertical.Refresh();
                    childControl.Invalidate();
                }



                scrollBarVisible = scrollBarHorizontal.Visible;
                scrollBarHorizontal.Visible = ClientArea.VirtualWidth > ClientArea.ClientWidth;
                if (ClientArea.VirtualWidth <= ClientArea.ClientWidth) scrollBarHorizontal.Value = 0;

                if (scrollBarVisible != scrollBarHorizontal.Visible)
                {
                    if (!scrollBarHorizontal.Visible)
                    {
                        foreach (Control c in ClientArea.ChildrenControls)
                        {
                            c.HorizontalScrollingAmount = 0;
                            scrollBarVertical.Refresh();
                            c.Invalidate();
                        }
                    }
                    AdjustMargins();
                }
                PositionScrollBars();

                foreach (Control childControl in ClientArea.ChildrenControls)
                {
                    childControl.HorizontalScrollingAmount = -scrollBarHorizontal.Value;
                    scrollBarHorizontal.Refresh();
                    childControl.Invalidate();
                }


            }
            adjustingScrolling = false;
        } // CalculateScrolling



        public virtual void ScrollTo(Control control)
        {
            if (control != null && ClientArea != null && ClientArea.Contains(control))
            {
                if (control.ControlTopAbsoluteCoordinate + control.Height > ClientArea.ControlTopAbsoluteCoordinate + ClientArea.Height)
                {
                    scrollBarVertical.Value = scrollBarVertical.Value + control.ControlTopAbsoluteCoordinate - ClientArea.ControlTopAbsoluteCoordinate - scrollBarVertical.PageSize + control.Height;
                }
                else if (control.ControlTopAbsoluteCoordinate < ClientArea.ControlTopAbsoluteCoordinate)
                {
                    scrollBarVertical.Value = scrollBarVertical.Value + control.ControlTopAbsoluteCoordinate - ClientArea.ControlTopAbsoluteCoordinate;
                }
                if (control.ControlLeftAbsoluteCoordinate + control.Width > ClientArea.ControlLeftAbsoluteCoordinate + ClientArea.Width)
                {
                    scrollBarHorizontal.Value = scrollBarHorizontal.Value + control.ControlLeftAbsoluteCoordinate - ClientArea.ControlLeftAbsoluteCoordinate - scrollBarHorizontal.PageSize + control.Width;
                }
                else if (control.ControlLeftAbsoluteCoordinate < ClientArea.ControlLeftAbsoluteCoordinate)
                {
                    scrollBarHorizontal.Value = scrollBarHorizontal.Value + control.ControlLeftAbsoluteCoordinate - ClientArea.ControlLeftAbsoluteCoordinate;
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
            MouseEventArgs ex = e as MouseEventArgs;
            ex.Position = new Point(ex.Position.X + scrollBarHorizontal.Value, ex.Position.Y + scrollBarVertical.Value);
            base.OnClick(e);
        } // OnClick

        protected internal override void OnSkinChanged(EventArgs e)
        {
            base.OnSkinChanged(e);
            if (scrollBarVertical != null && scrollBarHorizontal != null)
            {
                scrollBarVertical.Visible = false;
                scrollBarHorizontal.Visible = false;
                CalculateScrolling();
            }
        } // OnSkinChanged


    } // Container
} // XNAFinalEngine.UserInterface