using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Editor.WinForm.CollapsiblePanel.CollapsiblePanel
{
    [Designer(typeof(CollapsiblePanelDesigner))]
    [ToolboxBitmap(typeof(CollapsiblePanel), "Editor.WinForm.CollapsiblePanel.CollapsiblePanel.CollapsiblePanel.bmp")]
    [DefaultProperty("HeaderText")]
    public partial class CollapsiblePanel : Panel
    {


        private bool collapse = false;
        private int originalHight = 0;
        private bool useAnimation;
        private bool showHeaderSeparator = true;
        private bool roundedCorners;
        private int headerCornersRadius = 10;
        private bool headerTextAutoEllipsis;
        private string headerText;
        private Color headerTextColor;
        private Image headerImage;
        private Font headerFont;
        private RectangleF toolTipRectangle = new RectangleF();
        private bool useToolTip = false;




        [Browsable(false)]
        public new Color BackColor
        {
            get
            {
                return Color.Transparent;

            }
            set
            {
                base.BackColor = Color.Transparent;
            }
        }

        [DefaultValue(false)]
        [Description("Collapses the control when set to true")]
        [Category("CollapsiblePanel")]
        public bool Collapse
        {
            get { return collapse; }
            set
            {
                // If using animation make sure to ignore requests for collapse or expand while a previous
                // operation is in progress.
                if (useAnimation)
                {
                    // An operation is already in progress.
                    if (timerAnimation.Enabled)
                    {
                        return;
                    }
                }
                collapse = value;
                CollapseOrExpand();
                Refresh();
            }
        }

        [DefaultValue(50)]
        [Category("CollapsiblePanel")]
        [Description("Specifies the speed (in ms) of Expand/Collapse operation when using animation. UseAnimation property must be set to true.")]
        public int AnimationInterval
        {
            get
            {
                return timerAnimation.Interval;
            }
            set
            {
                // Update animation interval only during idle times.
                if (!timerAnimation.Enabled)
                {
                    timerAnimation.Interval = value;
                }
            }
        }

        [DefaultValue(false)]
        [Category("CollapsiblePanel")]
        [Description("Indicate if the panel uses amination during Expand/Collapse operation")]
        public bool UseAnimation
        {
            get { return useAnimation; }
            set { useAnimation = value; }
        }

        [DefaultValue(true)]
        [Category("CollapsiblePanel")]
        [Description("When set to true draws panel borders, and shows a line separating the panel's header from the rest of the control")]
        public bool ShowHeaderSeparator
        {
            get { return showHeaderSeparator; }
            set
            {
                showHeaderSeparator = value;
                Refresh();
            }
        }

        [DefaultValue(false)]
        [Category("CollapsiblePanel")]
        [Description("When set to true, draws a panel with rounded top corners, the radius can bet set through HeaderCornersRadius property")]
        public bool RoundedCorners
        {
            get
            {
                return roundedCorners;
            }
            set
            {
                roundedCorners = value;
                Refresh();
            }
        }


        [DefaultValue(10)]
        [Category("CollapsiblePanel")]
        [Description("Top corners radius, it should be in [1, 15] range")]
        public int HeaderCornersRadius
        {
            get
            {
                return headerCornersRadius;
            }

            set
            {
                if (value < 1 || value > 15)
                {
                    throw new ArgumentOutOfRangeException("HeaderCornersRadius", value, "Value should be in range [1, 90]");
                }
                else
                {
                    headerCornersRadius = value;
                    Refresh();
                }
            }
        }



        [DefaultValue(false)]
        [Category("CollapsiblePanel")]
        [Description("Enables the automatic handling of text that extends beyond the width of the label control.")]
        public bool HeaderTextAutoEllipsis
        {
            get { return headerTextAutoEllipsis; }
            set
            {
                headerTextAutoEllipsis = value;
                Refresh();
            }
        }

        [Category("CollapsiblePanel")]
        [Description("Text to show in panel's header")]
        public string HeaderText
        {
            get { return headerText; }
            set
            {
                headerText = value;
                Refresh();
            }
        }

        [Category("CollapsiblePanel")]
        [Description("Color of text header, and panel's borders when ShowHeaderSeparator is set to true")]
        public Color HeaderTextColor
        {
            get { return headerTextColor; }
            set
            {
                headerTextColor = value;
                Refresh();
            }
        }


        [Category("CollapsiblePanel")]
        [Description("Image that will be displayed in the top left corner of the panel")]
        public Image HeaderImage
        {
            get { return headerImage; }
            set
            {
                headerImage = value;
                Refresh();
            }
        }


        [Category("CollapsiblePanel")]
        [Description("The font used to display text in the panel's header.")]
        public Font HeaderFont
        {
            get { return headerFont; }
            set
            {
                headerFont = value;
                Refresh();
            }
        }



        public CollapsiblePanel()
        {
            InitializeComponent();
            pnlHeader.Width = Width - 1;

            headerFont = new Font(Font, FontStyle.Bold);
            headerTextColor = Color.Black;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            DrawHeaderPanel(e);
        }

        public void DrawHeaderCorners(Graphics g, Brush brush, float x, float y, float width, float height, float radius)
        {
            GraphicsPath gp = new GraphicsPath();

            gp.AddLine(x + radius, y, x + width - (radius * 2), y); // Line
            gp.AddArc(x + width - (radius * 2), y, radius * 2, radius * 2, 270, 90); // Corner
            gp.AddLine(x + width, y + radius, x + width, y + height); // Line
            gp.AddLine(x + width, y + height, x, y + height); // Line
            gp.AddLine(x, y + height, x, y + radius); // Line
            gp.AddArc(x, y, radius * 2, radius * 2, 180, 90); // Corner
            gp.CloseFigure();
            g.FillPath(brush, gp);
            if (showHeaderSeparator)
            {
                g.DrawPath(new Pen(headerTextColor), gp);
            }
            gp.Dispose();
        }

        private void DrawHeaderPanel(PaintEventArgs e)
        {
            Rectangle headerRect = pnlHeader.ClientRectangle;
            LinearGradientBrush headerBrush = new LinearGradientBrush(
                headerRect, Color.White, Color.LightGray, LinearGradientMode.Horizontal);

            if (!roundedCorners)
            {
                e.Graphics.FillRectangle(headerBrush, headerRect);
                if (showHeaderSeparator)
                {
                    e.Graphics.DrawRectangle(new Pen(headerTextColor), headerRect);
                }
            }
            else
            {
                DrawHeaderCorners(e.Graphics, headerBrush, headerRect.X, headerRect.Y,
                    headerRect.Width, headerRect.Height, headerCornersRadius);
            }


            // Draw header separator
            if (showHeaderSeparator)
            {
                Point start = new Point(pnlHeader.Location.X, pnlHeader.Location.Y + pnlHeader.Height);
                Point end = new Point(pnlHeader.Location.X + pnlHeader.Width, pnlHeader.Location.Y + pnlHeader.Height);
                e.Graphics.DrawLine(new Pen(headerTextColor, 2), start, end);
                // Draw rectangle lines for the rest of the control.
                Rectangle bodyRect = ClientRectangle;
                bodyRect.Y += pnlHeader.Height;
                bodyRect.Height -= (pnlHeader.Height + 1);
                bodyRect.Width -= 1;
                e.Graphics.DrawRectangle(new Pen(headerTextColor), bodyRect);
            }

            int headerRectHeight = pnlHeader.Height;
            // Draw header image.
            if (headerImage != null)
            {
                pictureBoxImage.Image = headerImage;
                pictureBoxImage.Visible = true;
            }
            else
            {
                pictureBoxImage.Image = null;
                pictureBoxImage.Visible = false;
            }


            // Calculate header string position.
            if (!string.IsNullOrEmpty(headerText))
            {
                useToolTip = false;
                int delta = pictureBoxExpandCollapse.Width + 5;
                int offset = 0;
                if (headerImage != null)
                {
                    offset = headerRectHeight;
                }
                PointF headerTextPosition = new PointF();
                Size headerTextSize = TextRenderer.MeasureText(headerText, headerFont);
                if (headerTextAutoEllipsis)
                {
                    if (headerTextSize.Width >= headerRect.Width - (delta + offset))
                    {
                        RectangleF rectLayout =
                            new RectangleF((float)headerRect.X + offset,
                            (float)(headerRect.Height - headerTextSize.Height) / 2,
                            (float)headerRect.Width - delta,
                            (float)headerTextSize.Height);
                        StringFormat format = new StringFormat();
                        format.Trimming = StringTrimming.EllipsisWord;
                        e.Graphics.DrawString(headerText, headerFont, new SolidBrush(headerTextColor),
                            rectLayout, format);

                        toolTipRectangle = rectLayout;
                        useToolTip = true;
                    }
                    else
                    {
                        headerTextPosition.X = (offset + headerRect.Width - headerTextSize.Width) / 2;
                        headerTextPosition.Y = (headerRect.Height - headerTextSize.Height) / 2;
                        e.Graphics.DrawString(headerText, headerFont, new SolidBrush(headerTextColor),
                            headerTextPosition);
                    }
                }
                else
                {
                    headerTextPosition.X = (offset + headerRect.Width - headerTextSize.Width) / 2;
                    headerTextPosition.Y = (headerRect.Height - headerTextSize.Height) / 2;
                    e.Graphics.DrawString(headerText, headerFont, new SolidBrush(headerTextColor),
                        headerTextPosition);
                }

            }
        }

        private void pictureBoxExpandCollapse_Click(object sender, EventArgs e)
        {
            Collapse = !Collapse;
        }

        private void CollapseOrExpand()
        {
            if (!useAnimation)
            {
                if (collapse)
                {
                    originalHight = Height;
                    Height = pnlHeader.Height + 3;
                    pictureBoxExpandCollapse.Image = Properties.Resources.expand;
                }
                else
                {
                    Height = originalHight;
                    pictureBoxExpandCollapse.Image = Properties.Resources.collapse;
                }
            }
            else
            {

                // Keep original height only in case of a collapse operation.
                if (collapse)
                {
                    originalHight = Height;
                }

                timerAnimation.Enabled = true;
                timerAnimation.Start();
            }
        }

        private void pictureBoxExpandCollapse_MouseMove(object sender, MouseEventArgs e)
        {
            if (!timerAnimation.Enabled)
            {
                if (!collapse)
                {
                    pictureBoxExpandCollapse.Image = Properties.Resources.collapse_hightlight;
                }
                else
                {
                    pictureBoxExpandCollapse.Image = Properties.Resources.expand_highlight;
                }
            }
        }

        private void pictureBoxExpandCollapse_MouseLeave(object sender, EventArgs e)
        {
            if (!timerAnimation.Enabled)
            {
                if (!collapse)
                {
                    pictureBoxExpandCollapse.Image = Properties.Resources.collapse;
                }
                else
                {
                    pictureBoxExpandCollapse.Image = Properties.Resources.expand;
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            pnlHeader.Width = Width - 1;
            Refresh();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            pnlHeader.Width = Width - 1;
            Refresh();
        }

        private void timerAnimation_Tick(object sender, EventArgs e)
        {
            if (collapse)
            {
                if (Height <= pnlHeader.Height + 3)
                {
                    timerAnimation.Stop();
                    timerAnimation.Enabled = false;
                    pictureBoxExpandCollapse.Image = Properties.Resources.expand;
                }
                else
                {
                    int newHight = Height - 20;
                    if (newHight <= pnlHeader.Height + 3)
                    {
                        newHight = pnlHeader.Height + 3;
                    }

                    Height = newHight;
                }


            }
            else
            {
                if (Height >= originalHight)
                {
                    timerAnimation.Stop();
                    timerAnimation.Enabled = false;
                    pictureBoxExpandCollapse.Image = Properties.Resources.collapse;
                }
                else
                {
                    int newHeight = Height + 20;
                    if (newHeight >= originalHight)
                    {
                        newHeight = originalHight;
                    }

                    Height = newHeight;
                }
            }
        }

        private void pnlHeader_MouseHover(object sender, EventArgs e)
        {
            if (useToolTip)
            {
                Point p = PointToClient(MousePosition);
                if (toolTipRectangle.Contains(p))
                {
                    toolTip.Show(headerText, pnlHeader, p);
                }
            }
        }

        private void pnlHeader_MouseLeave(object sender, EventArgs e)
        {
            if (useToolTip)
            {
                Point p = PointToClient(MousePosition);
                if (!toolTipRectangle.Contains(p))
                {
                    toolTip.Hide(pnlHeader);
                }
            }
        }



    }
}
