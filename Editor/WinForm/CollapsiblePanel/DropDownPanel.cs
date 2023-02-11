

using System.ComponentModel;
using System.ComponentModel.Design;
using System.Collections;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using System.Drawing;
using System.Drawing.Design;
using System.Drawing.Drawing2D;
using System.Threading;
using Control = System.Windows.Forms.Control;
using EventArgs = System.EventArgs;
using EventHandler = System.EventHandler;
using ListBox = System.Windows.Forms.ListBox;
using MouseEventHandler = System.Windows.Forms.MouseEventHandler;

namespace Editor.WinForm.CollapsiblePanel.CollapsiblePanel
{
    public class DropDownPanel : Panel
    {

        // Controls data
        private FlickerFreePanel pnlHeader;

        // Settings
        private AnimationSpeed animationSpeed = AnimationSpeed.Medium;
        // #############################################################
        private int animationStepDelay = 5; // TODO: compute this value dynamically, in function of Height
        private bool roundedCorners = true;
        private string headerText = "DropDownPanel";
        private int autoCollapseDelay = -1;
        private bool moveable = false;
        private HotTrackStyle hotTrackStyle = HotTrackStyle.Both;
        private Bitmap iconNormal = null;
        private Bitmap iconOver = null;
        private Font headerFont = DefaultFont;

        // Status data
        private bool expanded = true;
        private bool mouseOverHeader = false;
        private int fullHeight;
        private System.Windows.Forms.Timer autoCollapseTimer;
        private bool justMoved = false;
        private bool mouseDown = false;
        private int oldX;
        private int oldY;
        private Point homeLocation;

        // Graphic data
        private Bitmap bmpCollapseNormal;
        private Bitmap bmpCollapseOver;
        private Bitmap bmpExpandNormal;
        private Bitmap bmpExpandOver;
        private Font arrowFont;

        private GraphicsPath roundedHeaderCollapsed;
        private GraphicsPath roundedHeaderExpanded;
        private GraphicsPath squaredHeader;

        // Support data
        private StringFormat fullCenteredFormat;
        private StringFormat vCenteredFormat;

        // Temp variables used bt pnlHeader_Paint(): declared here to increase performances
        private GraphicsPath currPath;
        private int offset;
        private Pen p;
        private Bitmap bmp;
        private Brush b;
        private Bitmap currIcon;

        // Temp variables used by expand() and collapse(): declared here to increase performances
        private Control c;

        // Events
        // Delegate for managing the Event
        public delegate void DropDownPanelEventHandler(object sender, DropDownPanelEventArgs e);
        // The Event
        public event DropDownPanelEventHandler StatusChanged;
        // The Event dispatcher
        protected virtual void OnStatusChanged(DropDownPanelEventArgs e)
        {
            if (StatusChanged != null)
            {
                StatusChanged(this, e);
            }
        }

        // Managed Controls
        private bool manageControls = false;
        private ArrayList managedControls;

        // Header ContextMenu
        private ContextMenuStrip ctxHeader;
        private bool enableContextMenu = true;
        private ToolStripMenuItem itmExpand;
        private ToolStripMenuItem itmMoveable;
        private ToolStripMenuItem itmRestoreLocation;


        /// <summary>
        /// Constructor of DropDownPanel.
        /// </summary>
        public DropDownPanel()
        {
            // Create pnlHeader
            pnlHeader = new FlickerFreePanel();
            pnlHeader.Location = new Point(0, 0);
            pnlHeader.Width = Width;
            pnlHeader.Height = 20;
            pnlHeader.BackColor = Color.Transparent;
            Controls.Add(pnlHeader);

            // Create useful data
            createData();

            // Create bitmaps
            createBitmaps();

            // Create paths
            createPaths();

            // Create managedControls
            managedControls = new ArrayList();

            // Create autoCollapseTimer
            createAutoCollapseTimer();

            homeLocation = Location;

            // Create ContextMenu
            ctxHeader = new ContextMenuStrip();
            if (expanded)
            {
                itmExpand = new ToolStripMenuItem("Collapse Panel");
            }
            else
            {
                itmExpand = new ToolStripMenuItem("Expand Panel");
            }
            itmExpand.Click += new EventHandler(ctxHeader_ItemClick);
            itmMoveable = new ToolStripMenuItem("Moveable");
            if (moveable)
            {
                itmMoveable.Checked = true;
            }
            else
            {
                itmMoveable.Checked = false;
            }
            itmMoveable.Click += new EventHandler(ctxHeader_ItemClick);
            itmRestoreLocation = new ToolStripMenuItem("Restore Home Location");
            itmRestoreLocation.Click += new EventHandler(ctxHeader_ItemClick);

            ctxHeader.Items.Add(itmExpand);
            ctxHeader.Items.Add(new ToolStripMenuItem("-"));
            ctxHeader.Items.Add(itmMoveable);
            ctxHeader.Items.Add(itmRestoreLocation);

            // Install event handlers
            pnlHeader.Paint += new PaintEventHandler(pnlHeader_Paint);
            pnlHeader.MouseDown += new MouseEventHandler(pnlHeader_MouseDown);
            pnlHeader.MouseMove += new MouseEventHandler(pnlHeader_MouseMove);
            pnlHeader.MouseUp += new MouseEventHandler(pnlHeader_MouseUp);
            pnlHeader.MouseEnter += new EventHandler(pnlHeader_MouseEnter);
            pnlHeader.MouseLeave += new EventHandler(pnlHeader_MouseLeave);
            //pnlHeader.Click += new EventHandler(pnlHeader_Click);

            SetStyle(ControlStyles.DoubleBuffer, true);
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        }



        /// <summary>
        /// Creates the bitmaps used in the DropDownPanel.
        /// </summary>
        private void createBitmaps()
        {
            string s = "»";
            // Create bmpCollapseNormal
            bmpCollapseNormal = new Bitmap(12, 12, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics gr = Graphics.FromImage(bmpCollapseNormal);
            gr.Clear(Color.Transparent);
            gr.DrawString(s, arrowFont, SystemBrushes.ControlDarkDark, new RectangleF(0.5f, 0.5f, 11, 11), fullCenteredFormat);
            gr.Dispose();
            bmpCollapseNormal.RotateFlip(RotateFlipType.Rotate270FlipNone);

            // Create bmpExpandNormal
            bmpExpandNormal = (Bitmap)bmpCollapseNormal.Clone();
            bmpExpandNormal.RotateFlip(RotateFlipType.RotateNoneFlipY);

            // Create bmpCollapseOver
            bmpCollapseOver = new Bitmap(12, 12, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            gr = Graphics.FromImage(bmpCollapseOver);
            gr.Clear(Color.Transparent);
            gr.DrawString(s, arrowFont, SystemBrushes.HotTrack, new RectangleF(0.5f, 0.5f, 11, 11), fullCenteredFormat);
            gr.Dispose();
            bmpCollapseOver.RotateFlip(RotateFlipType.Rotate270FlipNone);

            // Create bmpExpandOver
            bmpExpandOver = (Bitmap)bmpCollapseOver.Clone();
            bmpExpandOver.RotateFlip(RotateFlipType.RotateNoneFlipY);
        }

        /// <summary>
        /// Creates useful data.
        /// </summary>
        private void createData()
        {
            fullCenteredFormat = new StringFormat();
            fullCenteredFormat.Alignment = StringAlignment.Center;
            fullCenteredFormat.LineAlignment = StringAlignment.Center;

            vCenteredFormat = new StringFormat();
            vCenteredFormat.LineAlignment = StringAlignment.Center;

            arrowFont = new Font(FontFamily.GenericMonospace, 16, FontStyle.Bold, GraphicsUnit.Pixel);
        }

        /// <summary>
        /// Creates the Paths for the Header.
        /// </summary>
        private void createPaths()
        {
            // Create roundedHeaderCollapsed
            roundedHeaderCollapsed = new GraphicsPath();
            roundedHeaderCollapsed.AddLine(5, 0, Width - 5 - 1, 0);
            roundedHeaderCollapsed.AddArc(Width - 5 - 1 - 5, 5 - 5, 10, 10, -90, 90);
            roundedHeaderCollapsed.AddLine(Width - 1, 5, Width - 1, pnlHeader.Height - 5 - 1);
            roundedHeaderCollapsed.AddArc(Width - 5 - 1 - 5, pnlHeader.Height - 5 - 1 - 5, 10, 10, 0, 90);
            roundedHeaderCollapsed.AddLine(Width - 5 - 1, pnlHeader.Height - 1, 5, pnlHeader.Height - 1);
            roundedHeaderCollapsed.AddArc(0, pnlHeader.Height - 5 - 1 - 5, 10, 10, 90, 90);
            roundedHeaderCollapsed.AddLine(0, pnlHeader.Height - 5 - 1, 0, 5);
            roundedHeaderCollapsed.AddArc(0, 0, 10, 10, 180, 90);

            // Create roundedHeaderExpanded
            roundedHeaderExpanded = new GraphicsPath();
            roundedHeaderExpanded.AddLine(5, 0, Width - 5 - 1, 0);
            roundedHeaderExpanded.AddArc(Width - 5 - 1 - 5, 5 - 5, 10, 10, -90, 90);
            roundedHeaderExpanded.AddLine(Width - 1, 5, Width - 1, pnlHeader.Height - 1);
            roundedHeaderExpanded.AddLine(Width - 1, pnlHeader.Height - 1, 0, pnlHeader.Height - 1);
            roundedHeaderExpanded.AddLine(Width - 1, pnlHeader.Height - 1, 0, pnlHeader.Height - 1);
            roundedHeaderExpanded.AddArc(0, 0, 10, 10, 180, 90);

            // Create squaredHeader
            squaredHeader = new GraphicsPath();
            squaredHeader.AddLine(0, 0, Width - 1, 0);
            squaredHeader.AddLine(Width - 1, 0, Width - 1, pnlHeader.Height - 1);
            squaredHeader.AddLine(Width - 1, pnlHeader.Height - 1, 0, pnlHeader.Height - 1);
            squaredHeader.AddLine(0, pnlHeader.Height - 1, 0, 0);
        }

        private void createAutoCollapseTimer()
        {
            if (!DesignMode && autoCollapseDelay > 0)
            {
                autoCollapseTimer = new System.Windows.Forms.Timer();
                autoCollapseTimer.Tick += new EventHandler(autoCollapseTimer_Tick);
                autoCollapseTimer.Interval = autoCollapseDelay;
                autoCollapseTimer.Enabled = true;
                autoCollapseTimer.Start();
            }
        }

        private void startAutoCollapseTimer()
        {
            if (autoCollapseDelay > 0 && autoCollapseTimer != null)
            {
                autoCollapseTimer.Start();
            }
        }
        private void stopAutoCollapseTimer()
        {
            if (autoCollapseDelay > 0 && autoCollapseTimer != null)
            {
                autoCollapseTimer.Stop();
            }
        }



        protected override void OnPaint(PaintEventArgs e)
        {
            if (expanded && Height >= 2 * pnlHeader.Height)
            {
                Graphics gr = e.Graphics;
                gr.SmoothingMode = SmoothingMode.AntiAlias;
                Pen p = new Pen(SystemBrushes.ControlDark, 1);
                if (roundedCorners)
                {
                    gr.DrawLine(p, 0, pnlHeader.Height, 0, Height - 5 - 1);
                    gr.DrawArc(p, 0, Height - 5 - 1 - 5, 10, 10, 90, 90);
                    gr.DrawLine(p, 5, Height - 1, Width - 5 - 1, Height - 1);
                    gr.DrawArc(p, Width - 5 - 1 - 5, Height - 5 - 1 - 5, 10, 10, 0, 90);
                    gr.DrawLine(p, Width - 1, Height - 5 - 1, Width - 1, pnlHeader.Height);
                }
                else
                {
                    gr.DrawLine(p, 0, pnlHeader.Height, 0, Height);
                    gr.DrawLine(p, 0, Height - 1, Width - 1, Height - 1);
                    gr.DrawLine(p, Width - 1, Height - 1, Width - 1, pnlHeader.Height);
                }
            }
            base.OnPaint(e);
        }

        private void pnlHeader_Paint(object sender, PaintEventArgs e)
        {
            Graphics gr = e.Graphics;
            gr.SmoothingMode = SmoothingMode.AntiAlias;

            if (hotTrackStyle == HotTrackStyle.None || hotTrackStyle == HotTrackStyle.FillingOnly)
            {
                p = new Pen(SystemBrushes.ControlDark, 1);
                b = Brushes.Black;
                if (expanded)
                {
                    bmp = bmpCollapseNormal;
                }
                else
                {
                    bmp = bmpExpandNormal;
                }
            }
            else if (hotTrackStyle == HotTrackStyle.Both || hotTrackStyle == HotTrackStyle.LinesOnly)
            {
                if (mouseOverHeader)
                {
                    p = new Pen(SystemBrushes.HotTrack, 1);
                    b = SystemBrushes.HotTrack;
                    if (expanded)
                    {
                        bmp = bmpCollapseOver;
                    }
                    else
                    {
                        bmp = bmpExpandOver;
                    }
                }
                else
                {
                    p = new Pen(SystemBrushes.ControlDark, 1);
                    b = Brushes.Black;
                    if (expanded)
                    {
                        bmp = bmpCollapseNormal;
                    }
                    else
                    {
                        bmp = bmpExpandNormal;
                    }
                }
            }

            if (roundedCorners)
            {
                if (expanded)
                {
                    currPath = roundedHeaderExpanded;
                }
                else
                {
                    currPath = roundedHeaderCollapsed;
                }
            }
            else
            {
                currPath = squaredHeader;
            }

            if (mouseOverHeader && (hotTrackStyle == HotTrackStyle.Both || hotTrackStyle == HotTrackStyle.FillingOnly))
            {
                gr.FillPath(SystemBrushes.ControlLight, currPath);
            }
            gr.DrawPath(p, currPath);

            if (expanded)
            {
                // Panel is all visible
                gr.DrawImage(bmp, pnlHeader.Width - 12 - 6, pnlHeader.Height / 2 - 7, 12, 12);
            }
            else
            {
                gr.DrawImage(bmp, pnlHeader.Width - 12 - 6, pnlHeader.Height / 2 - 5, 12, 12);
            }

            // Draw icon if not null, and set up the correct offset
            offset = 0;
            currIcon = null;
            if (mouseOverHeader)
            {
                currIcon = iconOver;
            }
            else
            {
                currIcon = iconNormal;
            }
            if (currIcon != null)
            {
                offset = currIcon.Width + 4;
                gr.DrawImage(currIcon, new Rectangle(6, pnlHeader.Height / 2 - currIcon.Height / 2, currIcon.Width, currIcon.Height));
            }
            gr.DrawString(headerText, headerFont, b, new RectangleF(6 + offset, 0, pnlHeader.Width - 12 - 6 - 6, pnlHeader.Height - 1), vCenteredFormat);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            // Skip if the expanding/collapsing animation is running (prevents flickering)
            if (DesignMode)
            {
                fullHeight = Height;
            }
            pnlHeader.Width = Width;
            createPaths();
            Invalidate();
            Update();
            base.OnSizeChanged(e);
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            if (DesignMode)
            {
                homeLocation = Location;
            }
            base.OnLocationChanged(e);
        }

        /// <summary>
        /// Switches the status of the DropDownPanel: if it was expanded, this method will collapse it, and vice versa.
        /// </summary>
        public void SwitchStatus()
        {
            if (expanded)
            {
                expanded = false;
                collapse();
            }
            else
            {
                expanded = true;
                expand();
            }
        }

        private void collapse()
        {
            itmExpand.Text = "Expand Panel";
            fullHeight = Height;

            int[] d = new int[managedControls.Count];
            Control tmp;
            for (int i = 0; i < managedControls.Count; i++)
            {
                tmp = Parent.Controls[Parent.Controls.IndexOf((Control)managedControls[i])];
                d[i] = tmp.Location.Y - Location.Y - Height;
            }

            if (animationSpeed != AnimationSpeed.NoAnimation && !DesignMode)
            {
                // Perform animation
                while (Height > pnlHeader.Height + 24)
                {
                    Height -= 24;
                    Invalidate();
                    Update();
                    if (manageControls && !DesignMode)
                    {
                        for (int i = 0; i < managedControls.Count; i++)
                        {
                            c = (Control)managedControls[i];
                            c.Location = new Point(c.Location.X, c.Location.Y - 24);
                        }
                    }
                    Thread.Sleep(animationStepDelay);
                }
            }

            Height = pnlHeader.Height;

            if (manageControls)
            {
                for (int i = 0; i < managedControls.Count; i++)
                {
                    c = (Control)managedControls[i];
                    c.Location = new Point(c.Location.X, Location.Y + Height + d[i]);
                }
            }

            Invalidate();
            Update();

            // Clear memory
            GC.Collect();

            // Raise event
            OnStatusChanged(new DropDownPanelEventArgs(expanded));
        }

        private void expand()
        {
            itmExpand.Text = "Collapse Panel";

            int[] d = new int[managedControls.Count];
            Control tmp;
            for (int i = 0; i < managedControls.Count; i++)
            {
                tmp = Parent.Controls[Parent.Controls.IndexOf((Control)managedControls[i])];
                d[i] = tmp.Location.Y - Location.Y - Height;
            }

            if (animationSpeed != AnimationSpeed.NoAnimation && !DesignMode)
            {
                // Perform animation
                while (Height < fullHeight - 24)
                {
                    Height += 24;
                    Invalidate();
                    Update();
                    if (manageControls)
                    {
                        for (int i = 0; i < managedControls.Count; i++)
                        {
                            c = (Control)managedControls[i];
                            c.Location = new Point(c.Location.X, c.Location.Y + 24);
                        }
                    }
                    Thread.Sleep(animationStepDelay);
                }
            }

            Height = fullHeight;

            if (manageControls)
            {
                for (int i = 0; i < managedControls.Count; i++)
                {
                    c = (Control)managedControls[i];
                    c.Location = new Point(c.Location.X, Location.Y + Height + d[i]);
                }
            }

            Invalidate();
            Update();

            // Clear memory
            GC.Collect();

            // Raise event
            OnStatusChanged(new DropDownPanelEventArgs(expanded));
        }

        /// <summary>
        /// Restores the HomeLocation of the DropDownPanel.
        /// </summary>
        public void RestoreHomeLocation()
        {
            Location = homeLocation;
        }



        private void pnlHeader_MouseDown(object sender, MouseEventArgs e)
        {
            if (moveable && e.Button == MouseButtons.Left)
            {
                mouseDown = true;
                oldX = e.X;
                oldY = e.Y;
            }
        }

        private void pnlHeader_MouseMove(object sender, MouseEventArgs e)
        {
            if (moveable && mouseDown)
            {
                Location = new Point(Location.X + e.X - oldX, Location.Y + e.Y - oldY);
                justMoved = true;
            }
        }

        private void pnlHeader_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                mouseDown = false;
                if (!justMoved)
                {
                    SwitchStatus();
                }
                justMoved = false;
            }
            if (e.Button == MouseButtons.Right && enableContextMenu)
            {
                // Popup context menu for the Header
                ctxHeader.Show(pnlHeader, new Point(e.X - pnlHeader.Location.X, e.Y - pnlHeader.Location.Y));
            }
            BringToFront();
        }

        private void pnlHeader_MouseEnter(object sender, EventArgs e)
        {
            stopAutoCollapseTimer();
            mouseOverHeader = true;
            pnlHeader.Refresh();
        }

        private void pnlHeader_MouseLeave(object sender, EventArgs e)
        {
            startAutoCollapseTimer();
            mouseOverHeader = false;
            pnlHeader.Refresh();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            stopAutoCollapseTimer();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            startAutoCollapseTimer();
            base.OnMouseLeave(e);
        }

        protected override void OnClick(EventArgs e)
        {
            BringToFront();
        }

        private void autoCollapseTimer_Tick(object sender, EventArgs e)
        {
            autoCollapseTimer.Stop();
            if (expanded)
            {
                SwitchStatus();
            }
        }



        private void ctxHeader_ItemClick(object sender, EventArgs e)
        {
            ToolStripMenuItem i = (ToolStripMenuItem)sender;
            if (i == itmExpand)
            {
                SwitchStatus();
            }
            else if (i == itmMoveable)
            {
                if (moveable)
                {
                    moveable = false;
                    itmMoveable.Checked = false;
                }
                else
                {
                    moveable = true;
                    itmMoveable.Checked = true;
                }
            }
            else if (i == itmRestoreLocation)
            {
                RestoreHomeLocation();
            }
            // Add here other items
        }



        /// <summary>
        /// The image shown in the Header in normal status. Height must be less than HeaderHeight - 4 pixels. Null to disable it.
        /// </summary>
        [Description("The image shown in the Header in normal status. Height must be less than HeaderHeight - 4 pixels. Null to disable it.")]
        public Bitmap HeaderIconNormal
        {
            get
            {
                return iconNormal;
            }
            set
            {
                if (value != null)
                {
                    if (value.Height > pnlHeader.Height - 4)
                    {
                        throw new ArgumentException("HeaderIcon: Height must be less than HeaderHeight - 4 pixels.");
                    }
                }
                iconNormal = value;
                pnlHeader.Invalidate();
                pnlHeader.Update();
            }
        }

        /// <summary>
        /// The image shown in the Header in HotTrack status. Height must be less than HeaderHeight - 4 pixels. Null to disable it.
        /// </summary>
        [Description("The image shown in the Header in HotTrack status. Height must be less than HeaderHeight - 4 pixels. Null to disable it.")]
        public Bitmap HeaderIconOver
        {
            get
            {
                return iconOver;
            }
            set
            {
                if (value != null)
                {
                    if (value.Height > pnlHeader.Height - 4)
                    {
                        throw new ArgumentException("HeaderIcon: Height must be less than HeaderHeight - 4 pixels.");
                    }
                }
                iconOver = value;
                pnlHeader.Invalidate();
                pnlHeader.Update();
            }
        }

        /// <summary>
        /// Gets or sets the Font used in the Header.
        /// </summary>
        [Description("Gets or sets the Font used in the Header.")]
        public Font HeaderFont
        {
            get
            {
                return headerFont;
            }
            set
            {
                headerFont = value;
                pnlHeader.Invalidate();
                pnlHeader.Update();
            }
        }

        /// <summary>
        /// Gets or sets the status of the DropDownPanel.
        /// </summary>
        [Description("Gets or sets the status of the DropDownPanel.")]
        public bool Expanded
        {
            get
            {
                return expanded;
            }
            set
            {
                bool oldExpanded = expanded;
                expanded = value;
                if (!DesignMode)
                {
                    if (oldExpanded != expanded)
                    {
                        if (expanded)
                        {
                            collapse();
                            itmExpand.Text = "Expand Panel";
                        }
                        else
                        {
                            expand();
                            itmExpand.Text = "Collapse Panel";
                        }
                    }
                }
                else
                {
                    if (expanded)
                    {
                        Height = fullHeight;
                    }
                    else
                    {
                        Height = pnlHeader.Height;
                    }
                    Invalidate();
                    Update();
                }
            }
        }

        /// <summary>
        /// Gets or sets the Header's Height of the DropDownPanel. The min value is 16, the max value is 48.
        /// </summary>
        [Description("Gets or sets the Header's Height of the DropDownPanel. The min value is 16, the max value is 48.")]
        public int HeaderHeight
        {
            get
            {
                return pnlHeader.Height;
            }
            set
            {
                if (value < 16 || value > 48)
                {
                    throw new ArgumentException("HeaderHeight: min value is 16, max value is 48.");
                }
                pnlHeader.Height = value;
                createPaths();
                Padding = new Padding(0, value, 0, 0);
            }
        }

        /// <summary>
        /// Gets or sets if the Header can show its ContextMenu with some options by right-clicking on it.
        /// </summary>
        [Description("Gets or sets if the Header can show its ContextMenu with some options by right-clicking on it.")]
        public bool EnableHeaderMenu
        {
            get
            {
                return enableContextMenu;
            }
            set
            {
                enableContextMenu = value;
            }
        }

        /// <summary>
        /// Gets or sets the Expand/Collapse Animation Speed.
        /// </summary>
        [Description("Gets or sets the Expand/Collapse Animation Speed."), Editor(typeof(AnimationSpeedEditor), typeof(UITypeEditor))]
        public AnimationSpeed ExpandAnimationSpeed
        {
            get
            {
                return animationSpeed;
            }
            set
            {
                animationSpeed = value;
                switch (value)
                {
                    case AnimationSpeed.NoAnimation:
                        animationStepDelay = 0;
                        break;
                    case AnimationSpeed.High:
                        animationStepDelay = 5;
                        break;
                    case AnimationSpeed.Medium:
                        animationStepDelay = 10;
                        break;
                    case AnimationSpeed.Low:
                        animationStepDelay = 15;
                        break;
                }
            }
        }

        /// <summary>
        /// Gets or sets the style of the HotTrack for the Header.
        /// </summary>
        [Description("Gets or sets the style of the HotTrack for the Header."), Editor(typeof(HotTrackStyleEditor), typeof(UITypeEditor))]
        public HotTrackStyle HotTrackStyle
        {
            get
            {
                return hotTrackStyle;
            }
            set
            {
                hotTrackStyle = value;
            }
        }

        /// <summary>
        /// Gets or sets if the corners of the DropDownPanel must be drawn rounded.
        /// </summary>
        [Description("Gets or sets if the corners of the DropDownPanel must be drawn rounded.")]
        public bool RoundedCorners
        {
            get
            {
                return roundedCorners;
            }
            set
            {
                roundedCorners = value;
                Invalidate();
                Update();
            }
        }

        /// <summary>
        /// Gets or sets the Text of the DropDownPanel Header.
        /// </summary>
        [Description("Gets or sets the Text of the DropDownPanel Header.")]
        public string HeaderText
        {
            get
            {
                return headerText;
            }
            set
            {
                headerText = value;
                pnlHeader.Invalidate();
                pnlHeader.Update();
            }
        }

        /// <summary>
        /// The home Location of the DropDownPanel, used when its location is restored.
        /// </summary>
        [Description("The home Location of the DropDownPanel, used when its location is restored.")]
        public Point HomeLocation
        {
            get
            {
                return homeLocation;
            }
            set
            {
                homeLocation = value;
            }
        }

        /// <summary>
        /// Gets or sets the AutoCollapse Delay in milliseconds. Use negative values to disable this function.
        /// </summary>
        [Description("Gets or sets the AutoCollapse Delay in milliseconds. Use negative values to disable this function.")]
        public int AutoCollapseDelay
        {
            get
            {
                return autoCollapseDelay;
            }
            set
            {
                autoCollapseDelay = value;
                createAutoCollapseTimer();
            }
        }

        /// <summary>
        /// Gets or sets if the DropDownPanel has to manage the specified Controls.
        /// </summary>
        [Description("Gets or sets if the DropDownPanel has to manage the specified Controls.")]
        public bool ManageControls
        {
            get
            {
                return manageControls;
            }
            set
            {
                manageControls = value;
            }
        }

        /// <summary>
        /// Adds a Control to the managed Controls list.
        /// </summary>
        /// <param name="c">The Control to manage.</param>
        public void AddManagedControl(Control c)
        {
            for (int i = 0; i < managedControls.Count; i++)
            {
                if ((Control)managedControls[i] == c)
                {
                    return;
                }
            }
            managedControls.Add(c);
        }

        /// <summary>
        /// Removes a Control from the managed Controls list.
        /// </summary>
        /// <param name="c">The Control to remove.</param>
        public void RemoveManagedControl(Control c)
        {
            for (int i = 0; i < managedControls.Count; i++)
            {
                if ((Control)managedControls[i] == c)
                {
                    managedControls.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>
        /// Gets or sets if the DropDownPanel can be moved by dragging its Header.
        /// </summary>
        [Description("Gets or sets if the DropDownPanel can be moved by dragging its Header.")]
        public bool Moveable
        {
            get
            {
                return moveable;
            }
            set
            {
                moveable = value;
                if (moveable)
                {
                    itmMoveable.Checked = true;
                }
                else
                {
                    itmMoveable.Checked = false;
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        public void AddControlAndManageControlLocation(Control c_)
        {
            int offset = c_.Height - Height + HeaderHeight + Margin.Bottom;
            Height = c_.Height + HeaderHeight;
            Controls.Add(c_);

            //move other control
            for (int i = 0; i < managedControls.Count; i++)
            {
                c = (Control)managedControls[i];
                c.Location = new Point(c.Location.X, c.Location.Y + offset);
            }
        }
    }

    /// <summary>
    /// Represents the speed values of the animation.
    /// </summary>
    public enum AnimationSpeed
    {
        NoAnimation, High, Medium, Low
    }

    /// <summary>
    /// Represents the HotTrack Style.
    /// </summary>
    public enum HotTrackStyle
    {
        Both, LinesOnly, FillingOnly, None
    }

    public class AnimationSpeedEditor : UITypeEditor
    {

        public ListBox lstValues;
        public IWindowsFormsEditorService wfes;

        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            if (context != null && context.Instance != null)
            {
                return UITypeEditorEditStyle.DropDown;
            }
            return base.GetEditStyle(context);
        }

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            if (context == null || context.Instance == null)
            {
                return value;
            }
            wfes = (IWindowsFormsEditorService)provider.GetService(typeof(IWindowsFormsEditorService));
            if (wfes == null)
            {
                return value;
            }

            lstValues = new ListBox();
            lstValues.BorderStyle = BorderStyle.None;
            lstValues.Items.Add("NoAnimation");
            lstValues.Items.Add("High");
            lstValues.Items.Add("Medium");
            lstValues.Items.Add("Low");

            lstValues.SelectedIndex = (int)value;

            lstValues.MouseUp += new MouseEventHandler(lstValues_MouseUp);

            wfes.DropDownControl(lstValues);

            int val = lstValues.SelectedIndex;

            lstValues.Dispose();
            lstValues = null;

            switch (val)
            {
                case 0:
                    return AnimationSpeed.NoAnimation;
                case 1:
                    return AnimationSpeed.High;
                case 2:
                    return AnimationSpeed.Medium;
                case 3:
                    return AnimationSpeed.Low;
            }

            return AnimationSpeed.High;

        }

        private void lstValues_MouseUp(object sender, MouseEventArgs e)
        {
            if (wfes != null)
            {
                wfes.CloseDropDown();
            }
        }

    }

    public class HotTrackStyleEditor : UITypeEditor
    {

        public ListBox lstValues;
        public IWindowsFormsEditorService wfes;

        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            if (context != null && context.Instance != null)
            {
                return UITypeEditorEditStyle.DropDown;
            }
            return base.GetEditStyle(context);
        }

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            if (context == null || context.Instance == null)
            {
                return value;
            }
            wfes = (IWindowsFormsEditorService)provider.GetService(typeof(IWindowsFormsEditorService));
            if (wfes == null)
            {
                return value;
            }

            lstValues = new ListBox();
            lstValues.BorderStyle = BorderStyle.None;
            lstValues.Items.Add("Both");
            lstValues.Items.Add("LinesOnly");
            lstValues.Items.Add("FillingOnly");
            lstValues.Items.Add("None");

            lstValues.SelectedIndex = (int)value;

            lstValues.MouseUp += new MouseEventHandler(lstValues_MouseUp);

            wfes.DropDownControl(lstValues);

            int val = lstValues.SelectedIndex;

            lstValues.Dispose();
            lstValues = null;

            switch (val)
            {
                case 0:
                    return HotTrackStyle.Both;
                case 1:
                    return HotTrackStyle.LinesOnly;
                case 2:
                    return HotTrackStyle.FillingOnly;
                case 3:
                    return HotTrackStyle.None;
            }

            return HotTrackStyle.Both;

        }

        private void lstValues_MouseUp(object sender, MouseEventArgs e)
        {
            if (wfes != null)
            {
                wfes.CloseDropDown();
            }
        }

    }

    /// <summary>
    /// Class for incapsulate the EventArgs of a DropDownPanel.
    /// </summary>
    public class DropDownPanelEventArgs : EventArgs
    {

        private bool expanded;

        /// <summary>
        /// Constructor of DropDownPanelEventArgs.
        /// </summary>
        /// <param name="expanded">The status of the DropDownPanel.</param>
        public DropDownPanelEventArgs(bool expanded)
        {
            this.expanded = expanded;
        }

        public bool PanelExpanded
        {
            get
            {
                return expanded;
            }
        }
    }

    /// <summary>
    /// Class for creating a Panel without flickerings.
    /// </summary>
    public class FlickerFreePanel : Panel
    {

        public FlickerFreePanel()
        {
            SetStyle(ControlStyles.DoubleBuffer, true);
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        }

    }

}
