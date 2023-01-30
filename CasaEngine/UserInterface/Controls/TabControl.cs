
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/


namespace XNAFinalEngine.UserInterface
{

    public class TabPage : Container
    {


        private Rectangle headerRectangle = Rectangle.Empty;



        protected internal Rectangle HeaderRectangle => headerRectangle;


        public TabPage(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            Color = Color.Transparent;
            Passive = true;
            CanFocus = false;
        } // TabPage



        protected internal void CalculateRectangle(Rectangle prev, Font font, Margins margins, Point offset, bool first)
        {
            int size = (int)Math.Ceiling(font.MeasureString(Text).X) + margins.Horizontal;

            if (first) offset.X = 0;

            headerRectangle = new Rectangle(prev.Right + offset.X, prev.Top, size, prev.Height);
        } // CalculateRectangle



        protected override void AdjustMargins()
        {
            ClientMargins = new Margins(0, 0, 0, 0);

            base.AdjustMargins();
        } // AdjustMargins


    } // TabPage

    public class TabControl : Container
    {


        private readonly List<TabPage> tabPages = new List<TabPage>();
        private int selectedIndex;
        private int hoveredIndex = -1;



        public TabPage[] TabPages => tabPages.ToArray();

        public virtual int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                if (selectedIndex >= 0 && selectedIndex < tabPages.Count && value >= 0 && value < tabPages.Count)
                {
                    TabPages[selectedIndex].Visible = false;
                }
                if (value >= 0 && value < tabPages.Count)
                {
                    TabPages[value].Visible = true;
                    ControlsList c = TabPages[value].ChildrenControls as ControlsList;
                    if (c.Count > 0) c[0].Focused = true;
                    selectedIndex = value;
                    if (!Suspended) OnPageChanged(new EventArgs());
                }
            }
        } // SelectedIndex

        public virtual TabPage SelectedPage
        {
            get => tabPages[SelectedIndex];
            set
            {
                for (int i = 0; i < tabPages.Count; i++)
                {
                    if (tabPages[i] == value)
                    {
                        SelectedIndex = i;
                        break;
                    }
                }
            }
        } // SelectedPage



        public event EventHandler PageChanged;



        public TabControl(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
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
            SkinLayer l1 = SkinInformation.Layers["Control"];
            SkinLayer l2 = SkinInformation.Layers["Header"];
            Color col = Color != UndefinedColor ? Color : Color.White;

            Rectangle r1 = new Rectangle(rect.Left, rect.Top + l1.OffsetY, rect.Width, rect.Height - l1.OffsetY);
            if (tabPages.Count <= 0)
            {
                r1 = rect;
            }

            base.DrawControl(r1);

            if (tabPages.Count > 0)
            {

                Rectangle prev = new Rectangle(rect.Left, rect.Top + l2.OffsetY, 0, l2.Height);
                for (int i = 0; i < tabPages.Count; i++)
                {
                    Font font = l2.Text.Font.Font;
                    Margins margins = l2.ContentMargins;
                    Point offset = new Point(l2.OffsetX, l2.OffsetY);
                    if (i > 0) prev = tabPages[i - 1].HeaderRectangle;

                    tabPages[i].CalculateRectangle(prev, font, margins, offset, i == 0);
                }

                for (int i = tabPages.Count - 1; i >= 0; i--)
                {
                    int li = tabPages[i].Enabled ? l2.States.Enabled.Index : l2.States.Disabled.Index;
                    Color lc = tabPages[i].Enabled ? l2.Text.Colors.Enabled : l2.Text.Colors.Disabled;
                    if (i == hoveredIndex)
                    {
                        li = l2.States.Hovered.Index;
                        lc = l2.Text.Colors.Hovered;
                    }


                    Margins m = l2.ContentMargins;
                    Rectangle rx = tabPages[i].HeaderRectangle;
                    Rectangle sx = new Rectangle(rx.Left + m.Left, rx.Top + m.Top, rx.Width - m.Horizontal, rx.Height - m.Vertical);
                    if (i != selectedIndex)
                    {
                        UserInterfaceManager.Renderer.DrawLayer(l2, rx, col, li);
                        UserInterfaceManager.Renderer.DrawString(l2.Text.Font.Font, tabPages[i].Text, sx, lc, l2.Text.Alignment);
                    }
                }

                Margins mi = l2.ContentMargins;
                Rectangle ri = tabPages[selectedIndex].HeaderRectangle;
                Rectangle si = new Rectangle(ri.Left + mi.Left, ri.Top + mi.Top, ri.Width - mi.Horizontal, ri.Height - mi.Vertical);
                UserInterfaceManager.Renderer.DrawLayer(l2, ri, col, l2.States.Focused.Index);
                UserInterfaceManager.Renderer.DrawString(l2.Text.Font.Font, tabPages[selectedIndex].Text, si, l2.Text.Colors.Focused, l2.Text.Alignment, l2.Text.OffsetX, l2.Text.OffsetY, false);
            }
        } // DrawControl



        public virtual TabPage AddPage(string text)
        {
            TabPage p = AddPage();
            p.Text = text;

            return p;
        } // AddPage

        public virtual TabPage AddPage()
        {
            TabPage page = new TabPage(UserInterfaceManager)
            {
                Left = 0,
                Top = 0,
                Width = ClientWidth,
                Height = ClientHeight,
                Anchor = Anchors.All,
                Text = "Tab " + (tabPages.Count + 1),
                Visible = false,
                AutoScroll = true,
            };
            Add(page, true);
            tabPages.Add(page);
            tabPages[0].Visible = true;

            return page;
        } // AddPage

        public virtual void RemovePage(TabPage page, bool dispose)
        {
            tabPages.Remove(page);
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

            if (tabPages.Count > 1)
            {
                Point p = new Point(e.State.X - Root.ControlLeftAbsoluteCoordinate, e.State.Y - Root.ControlTopAbsoluteCoordinate);
                for (int i = 0; i < tabPages.Count; i++)
                {
                    Rectangle r = tabPages[i].HeaderRectangle;
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
            if (tabPages.Count > 1)
            {
                int index = hoveredIndex;
                Point p = new Point(e.State.X - Root.ControlLeftAbsoluteCoordinate, e.State.Y - Root.ControlTopAbsoluteCoordinate);
                for (int i = 0; i < tabPages.Count; i++)
                {
                    Rectangle r = tabPages[i].HeaderRectangle;
                    if (p.X >= r.Left && p.X <= r.Right && p.Y >= r.Top && p.Y <= r.Bottom && tabPages[i].Enabled)
                    {
                        index = i;
                        break;
                    }
                    index = -1;
                }
                if (index != hoveredIndex)
                {
                    hoveredIndex = index;
                    Invalidate();
                }
            }
        } // OnMouseMove

        protected virtual void OnPageChanged(EventArgs e)
        {
            if (PageChanged != null) PageChanged.Invoke(this, e);
        } // OnPageChanged


    } // TabControl
} //  XNAFinalEngine.UserInterface