
#region License
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/
#endregion

#region Using directives
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
#endregion

namespace XNAFinalEngine.UserInterface
{

    /// <summary>
    /// Display a list of items.
    /// </summary>
    public class ListBox : Control
    {

        #region Variables
        
        private List<object> items = new List<object>();
        private readonly ScrollBar scrollBarVertical;
        private readonly ClipBox pane;
        private int itemIndex = -1;
        private bool hotTrack;
        private int itemsCount;
        private bool hideSelection = true;

        #endregion

        #region Properties

        public virtual List<object> Items
        {
            get { return items; }
            internal set { items = value; }
        } // Items

        public virtual bool HotTrack
        {
            get { return hotTrack; }
            set
            {
                if (hotTrack != value)
                {
                    hotTrack = value;
                    if (!Suspended) 
                        OnHotTrackChanged(new EventArgs());
                }
            }
        } // HotTrack

        public virtual int ItemIndex
        {
            get { return itemIndex; }
            set
            {
                if (value >= 0 && value < items.Count)
                {
                    itemIndex = value;
                }
                else
                {
                    itemIndex = -1;
                }
                ScrollTo(itemIndex);

                if (!Suspended) OnItemIndexChanged(new EventArgs());

            }
        } // ItemIndex

        public virtual bool HideSelection
        {
            get { return hideSelection; }
            set
            {
                if (hideSelection != value)
                {
                    hideSelection = value;
                    Invalidate();
                    if (!Suspended) OnHideSelectionChanged(new EventArgs());
                }
            }
        } // HideSelection

        #endregion

        #region Events

        public event EventHandler HotTrackChanged;
        public event EventHandler ItemIndexChanged;
        public event EventHandler HideSelectionChanged;

        #endregion

        #region Constructor

        /// <summary>
        /// Display a list of items.
        /// </summary>
        public ListBox(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            Width = 64;
            Height = 64;
            MinimumHeight = 16;

            scrollBarVertical = new ScrollBar(UserInterfaceManager, Orientation.Vertical)
            {
                Parent = this, 
                Range = 1, 
                PageSize = 1,
                StepSize = 10
            };
            scrollBarVertical.Left = Left + Width - scrollBarVertical.Width - SkinInformation.Layers["Control"].ContentMargins.Right;
            scrollBarVertical.Top = Top + SkinInformation.Layers["Control"].ContentMargins.Top;
            scrollBarVertical.Height = Height - SkinInformation.Layers["Control"].ContentMargins.Vertical;
            scrollBarVertical.Anchor = Anchors.Top | Anchors.Right | Anchors.Bottom;

            pane = new ClipBox(UserInterfaceManager)
            {
                Parent = this,
                Top = SkinInformation.Layers["Control"].ContentMargins.Top,
                Left = SkinInformation.Layers["Control"].ContentMargins.Left,
                Width = Width - scrollBarVertical.Width - SkinInformation.Layers["Control"].ContentMargins.Horizontal - 1,
                Height = Height - SkinInformation.Layers["Control"].ContentMargins.Vertical,
                Anchor = Anchors.All,
                Passive = true,
                CanFocus = false
            };
            pane.Draw += DrawPane;

            CanFocus = true;
            Passive = false;
        } // ListBox

        #endregion

        #region Dispose

        /// <summary>
        /// Dispose managed resources.
        /// </summary>
        protected override void DisposeManagedResources()
        {
            // A disposed object could be still generating events, because it is alive for a time, in a disposed state, but alive nevertheless.
            HotTrackChanged = null;
            ItemIndexChanged = null;
            HideSelectionChanged = null;
            base.DisposeManagedResources();
        } // DisposeManagedResources

        #endregion

        #region Auto Height

        public virtual void AutoHeight(int maxItems)
        {
            if (items != null && items.Count < maxItems) 
                maxItems = items.Count;
            if (items == null || items.Count == maxItems)
            {
                // No scroll bar for 3 or few items.
                scrollBarVertical.Visible = false;
                pane.Width = Width - SkinInformation.Layers["Control"].ContentMargins.Horizontal - 1;
            }
            else
            {
                // Reduce pane size to place the scroll bar.
                scrollBarVertical.Visible = true;
                pane.Width = Width - scrollBarVertical.Width - SkinInformation.Layers["Control"].ContentMargins.Horizontal - 1;
            }

            SkinText font = SkinInformation.Layers["Control"].Text;
            if (items != null && items.Count > 0)
            {
                // Calculate the height of the list box.
                int fontHeight = (int)font.Font.Font.MeasureString(items[0].ToString()).Y;
                Height = (fontHeight * maxItems) + (SkinInformation.Layers["Control"].ContentMargins.Vertical);// - UserInterfaceManager.Skin.OriginMargins.Vertical);
            }
            else
            {
                Height = 32;
            }
        } // AutoHeight

        #endregion

        #region Draw

        /// <summary>
        /// Prerender the control into the control's render target.
        /// </summary>
        protected override void DrawControl(Rectangle rect)
        {
            scrollBarVertical.Invalidate();
            pane.Invalidate();
            base.DrawControl(rect);
        } // DrawControl

        private void DrawPane(object sender, DrawEventArgs e)
        {
            if (items != null && items.Count > 0)
            {
                SkinText  fontLayer = SkinInformation.Layers["Control"].Text;
                SkinLayer selectedLayer = SkinInformation.Layers["ListBox.Selection"];
                int fontHeight = (int)fontLayer.Font.Font.MeasureString(items[0].ToString()).Y;
                int v = (scrollBarVertical.Value / 10);
                if (!scrollBarVertical.Visible) // If the scrooll bar is invisible then this value should be 0 (first page).
                    v = 0;
                int p = (scrollBarVertical.PageSize / 10);
                int d = (int)(((scrollBarVertical.Value % 10) / 10f) * fontHeight);
                // This is done to show all last elements in the same page.
                if (v + p + 1 > items.Count)
                {
                    v = items.Count - p;
                    if (v < 0)
                        v = 0;
                }
                // Draw elements
                for (int i = v; i <= v + p + 1; i++)
                {
                    if (i < items.Count)
                    {
                        UserInterfaceManager.Renderer.DrawString(this, SkinInformation.Layers["Control"], items[i].ToString(),
                                            new Rectangle(e.Rectangle.Left, e.Rectangle.Top - d + ((i - v) * fontHeight), e.Rectangle.Width, fontHeight), false);
                    }
                }
                // Draw selection
                if (itemIndex >= 0 && itemIndex < items.Count && (Focused || !hideSelection))
                {
                    int pos = -d + ((itemIndex - v) * fontHeight);
                    if (pos > -fontHeight && pos < (p + 1) * fontHeight)
                    {
                        UserInterfaceManager.Renderer.DrawLayer(this, selectedLayer, new Rectangle(e.Rectangle.Left, e.Rectangle.Top + pos, e.Rectangle.Width, fontHeight));
                        UserInterfaceManager.Renderer.DrawString(this, selectedLayer, items[itemIndex].ToString(), new Rectangle(e.Rectangle.Left, e.Rectangle.Top + pos, e.Rectangle.Width, fontHeight), false);
                    }
                }
            }
        } // DrawPane

        #endregion

        #region Update

        protected internal override void Update(float elapsedTime_)
        {
            base.Update(elapsedTime_);

            // Scrool up and down with mouse wheel.
            if (XNAFinalEngine.Input.Mouse.WheelDelta != 0)
            {
                if (XNAFinalEngine.Input.Mouse.WheelDelta > 50)
                    itemIndex -= scrollBarVertical.PageSize / 10;
                else if (XNAFinalEngine.Input.Mouse.WheelDelta < -50)
                    itemIndex += scrollBarVertical.PageSize / 10;
                if (itemIndex < 0)
                    itemIndex = 0;
                else if (itemIndex >= Items.Count)
                    itemIndex = Items.Count - 1;
                ItemIndex = itemIndex;
            }

            if (Visible && items != null && items.Count != itemsCount)
            {
                itemsCount = items.Count;
                ItemsChanged();
            }
        } // Update

        #endregion

        #region Track Item

        /// <summary>
        /// This method select the element that is under the mouse pointer (if any).
        /// </summary>
        private void TrackItem(int x, int y)
        {
            if (items != null && items.Count > 0 && (pane.ControlRectangleRelativeToParent.Contains(new Point(x, y))))
            {
                SkinText font = SkinInformation.Layers["Control"].Text;
                int fontHeight = (int)font.Font.Font.MeasureString(items[0].ToString()).Y;
                int scrollbarValue = scrollBarVertical.Value;
                if (!scrollBarVertical.Visible)
                    scrollbarValue = 0;
                
                int lowerBound = (int)Math.Floor((float)scrollbarValue / 10f);
                int upperBound = (int) Math.Ceiling((float) (scrollbarValue + scrollBarVertical.PageSize)/10f);
                // Last page is special.
                if (upperBound > itemsCount)
                {
                    lowerBound = items.Count - (int)Math.Ceiling(scrollBarVertical.PageSize / 10f);
                    upperBound = items.Count;
                    if (lowerBound < 0)
                        lowerBound = 0;
                }
                // Calculate current item (without considering bounds or out of range)
                int i = (int)Math.Floor(lowerBound + ((float)y / fontHeight));
                
                if (i >= 0 && i < Items.Count && i >= lowerBound && i < upperBound)
                    ItemIndex = i;
                Focused = true;
            }
        } // TrackItem

        #endregion

        #region Items Changed

        private void ItemsChanged()
        {
            if (items != null && items.Count > 0)
            {
                SkinText font = SkinInformation.Layers["Control"].Text;
                int h = (int)font.Font.Font.MeasureString(items[0].ToString()).Y;

                int sizev = Height - SkinInformation.Layers["Control"].ContentMargins.Vertical;
                scrollBarVertical.Range = items.Count * 10;
                scrollBarVertical.PageSize = (int)Math.Floor((float)sizev * 10 / h);
                Invalidate();
            }
            else if (items == null || items.Count <= 0)
            {
                scrollBarVertical.Range = 1;
                scrollBarVertical.PageSize = 1;
                Invalidate();
            }
        } // ItemsChanged

        #endregion

        #region Scroll To

        public virtual void ScrollTo(int index)
        {
            ItemsChanged();
            if ((index * 10) < scrollBarVertical.Value)
            {
                scrollBarVertical.Value = index * 10;
            }
            else if (index >= (int)Math.Floor(((float)scrollBarVertical.Value + scrollBarVertical.PageSize) / 10f))
            {
                scrollBarVertical.Value = ((index + 1) * 10) - scrollBarVertical.PageSize;
            }
        } // ScrollTo

        #endregion

        #region On Events

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButton.Left || e.Button == MouseButton.Right)
                TrackItem(e.Position.X, e.Position.Y);
        } // OnMouseDown

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (hotTrack)
                TrackItem(e.Position.X, e.Position.Y);
        } // OnMouseMove

        protected override void OnKeyPress(KeyEventArgs e)
        {
            if (e.Key == Keys.Down)
            {
                e.Handled = true;
                itemIndex += scrollBarVertical.StepSize / 10;
            }
            else if (e.Key == Keys.Up)
            {
                e.Handled = true;
                itemIndex -= scrollBarVertical.StepSize / 10;
            }
            else if (e.Key == Keys.PageDown)
            {
                e.Handled = true;
                itemIndex += scrollBarVertical.PageSize / 10;
            }
            else if (e.Key == Keys.PageUp)
            {
                e.Handled = true;
                itemIndex -= scrollBarVertical.PageSize / 10;
            }
            else if (e.Key == Keys.Home)
            {
                e.Handled = true;
                itemIndex = 0;
            }
            else if (e.Key == Keys.End)
            {
                e.Handled = true;
                itemIndex = items.Count - 1;
            }

            if (itemIndex < 0) 
                itemIndex = 0;
            else if (itemIndex >= Items.Count) 
                itemIndex = Items.Count - 1;

            ItemIndex = itemIndex;

            base.OnKeyPress(e);
        } // OnKeyPress

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            ItemsChanged();
        } // OnResize
        
        protected virtual void OnItemIndexChanged(EventArgs e)
        {
            if (ItemIndexChanged != null) ItemIndexChanged.Invoke(this, e);
        } // OnItemIndexChanged

        protected virtual void OnHotTrackChanged(EventArgs e)
        {
            if (HotTrackChanged != null) HotTrackChanged.Invoke(this, e);
        } // OnHotTrackChanged

        protected virtual void OnHideSelectionChanged(EventArgs e)
        {
            if (HideSelectionChanged != null) HideSelectionChanged.Invoke(this, e);
        } // OnHideSelectionChanged

        #endregion

    } // ListBox
} // XNAFinalEngine.UserInterface
