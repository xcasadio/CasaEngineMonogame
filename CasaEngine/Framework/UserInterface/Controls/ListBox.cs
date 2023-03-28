
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/


using CasaEngine.Engine.Input;
using CasaEngine.Framework.UserInterface.Controls.Auxiliary;
using ScrollBar = CasaEngine.Framework.UserInterface.Controls.Auxiliary.ScrollBar;

namespace CasaEngine.Framework.UserInterface.Controls;

public class ListBox : Control
{


    private List<object> _items = new();
    private readonly ScrollBar _scrollBarVertical;
    private readonly ClipBox _pane;
    private int _itemIndex = -1;
    private bool _hotTrack;
    private int _itemsCount;
    private bool _hideSelection = true;



    public virtual List<object> Items
    {
        get => _items;
        internal set => _items = value;
    } // Items

    public virtual bool HotTrack
    {
        get => _hotTrack;
        set
        {
            if (_hotTrack != value)
            {
                _hotTrack = value;
                if (!Suspended)
                {
                    OnHotTrackChanged(new EventArgs());
                }
            }
        }
    } // HotTrack

    public virtual int ItemIndex
    {
        get => _itemIndex;
        set
        {
            if (value >= 0 && value < _items.Count)
            {
                _itemIndex = value;
            }
            else
            {
                _itemIndex = -1;
            }
            ScrollTo(_itemIndex);

            if (!Suspended)
            {
                OnItemIndexChanged(new EventArgs());
            }
        }
    } // ItemIndex

    public virtual bool HideSelection
    {
        get => _hideSelection;
        set
        {
            if (_hideSelection != value)
            {
                _hideSelection = value;
                Invalidate();
                if (!Suspended)
                {
                    OnHideSelectionChanged(new EventArgs());
                }
            }
        }
    } // HideSelection



    public event EventHandler HotTrackChanged;
    public event EventHandler ItemIndexChanged;
    public event EventHandler HideSelectionChanged;



    public ListBox(UserInterfaceManager userInterfaceManager)
        : base(userInterfaceManager)
    {
        Width = 64;
        Height = 64;
        MinimumHeight = 16;

        _scrollBarVertical = new ScrollBar(UserInterfaceManager, Orientation.Vertical)
        {
            Parent = this,
            Range = 1,
            PageSize = 1,
            StepSize = 10
        };
        _scrollBarVertical.Left = Left + Width - _scrollBarVertical.Width - SkinInformation.Layers["Control"].ContentMargins.Right;
        _scrollBarVertical.Top = Top + SkinInformation.Layers["Control"].ContentMargins.Top;
        _scrollBarVertical.Height = Height - SkinInformation.Layers["Control"].ContentMargins.Vertical;
        _scrollBarVertical.Anchor = Anchors.Top | Anchors.Right | Anchors.Bottom;

        _pane = new ClipBox(UserInterfaceManager)
        {
            Parent = this,
            Top = SkinInformation.Layers["Control"].ContentMargins.Top,
            Left = SkinInformation.Layers["Control"].ContentMargins.Left,
            Width = Width - _scrollBarVertical.Width - SkinInformation.Layers["Control"].ContentMargins.Horizontal - 1,
            Height = Height - SkinInformation.Layers["Control"].ContentMargins.Vertical,
            Anchor = Anchors.All,
            Passive = true,
            CanFocus = false
        };
        _pane.Draw += DrawPane;

        CanFocus = true;
        Passive = false;
    } // ListBox



    protected override void DisposeManagedResources()
    {
        // A disposed object could be still generating events, because it is alive for a time, in a disposed state, but alive nevertheless.
        HotTrackChanged = null;
        ItemIndexChanged = null;
        HideSelectionChanged = null;
        base.DisposeManagedResources();
    } // DisposeManagedResources



    public virtual void AutoHeight(int maxItems)
    {
        if (_items != null && _items.Count < maxItems)
        {
            maxItems = _items.Count;
        }

        if (_items == null || _items.Count == maxItems)
        {
            // No scroll bar for 3 or few items.
            _scrollBarVertical.Visible = false;
            _pane.Width = Width - SkinInformation.Layers["Control"].ContentMargins.Horizontal - 1;
        }
        else
        {
            // Reduce pane size to place the scroll bar.
            _scrollBarVertical.Visible = true;
            _pane.Width = Width - _scrollBarVertical.Width - SkinInformation.Layers["Control"].ContentMargins.Horizontal - 1;
        }

        var font = SkinInformation.Layers["Control"].Text;
        if (_items != null && _items.Count > 0)
        {
            // Calculate the height of the list box.
            var fontHeight = (int)font.Font.Font.MeasureString(_items[0].ToString()).Y;
            Height = fontHeight * maxItems + SkinInformation.Layers["Control"].ContentMargins.Vertical;// - UserInterfaceManager.Skin.OriginMargins.Vertical);
        }
        else
        {
            Height = 32;
        }
    } // AutoHeight



    protected override void DrawControl(Rectangle rect)
    {
        _scrollBarVertical.Invalidate();
        _pane.Invalidate();
        base.DrawControl(rect);
    } // DrawControl

    private void DrawPane(object sender, DrawEventArgs e)
    {
        if (_items != null && _items.Count > 0)
        {
            var fontLayer = SkinInformation.Layers["Control"].Text;
            var selectedLayer = SkinInformation.Layers["ListBox.Selection"];
            var fontHeight = (int)fontLayer.Font.Font.MeasureString(_items[0].ToString()).Y;
            var v = _scrollBarVertical.Value / 10;
            if (!_scrollBarVertical.Visible) // If the scrooll bar is invisible then this value should be 0 (first page).
            {
                v = 0;
            }

            var p = _scrollBarVertical.PageSize / 10;
            var d = (int)(_scrollBarVertical.Value % 10 / 10f * fontHeight);
            // This is done to show all last elements in the same page.
            if (v + p + 1 > _items.Count)
            {
                v = _items.Count - p;
                if (v < 0)
                {
                    v = 0;
                }
            }
            // Draw elements
            for (var i = v; i <= v + p + 1; i++)
            {
                if (i < _items.Count)
                {
                    UserInterfaceManager.Renderer.DrawString(this, SkinInformation.Layers["Control"], _items[i].ToString(),
                        new Rectangle(e.Rectangle.Left, e.Rectangle.Top - d + (i - v) * fontHeight, e.Rectangle.Width, fontHeight), false);
                }
            }
            // Draw selection
            if (_itemIndex >= 0 && _itemIndex < _items.Count && (Focused || !_hideSelection))
            {
                var pos = -d + (_itemIndex - v) * fontHeight;
                if (pos > -fontHeight && pos < (p + 1) * fontHeight)
                {
                    UserInterfaceManager.Renderer.DrawLayer(this, selectedLayer, new Rectangle(e.Rectangle.Left, e.Rectangle.Top + pos, e.Rectangle.Width, fontHeight));
                    UserInterfaceManager.Renderer.DrawString(this, selectedLayer, _items[_itemIndex].ToString(), new Rectangle(e.Rectangle.Left, e.Rectangle.Top + pos, e.Rectangle.Width, fontHeight), false);
                }
            }
        }
    } // DrawPane



    protected internal override void Update(float elapsedTime)
    {
        base.Update(elapsedTime);

        // Scrool up and down with mouse wheel.
        if (Mouse.WheelDelta != 0)
        {
            if (Mouse.WheelDelta > 50)
            {
                _itemIndex -= _scrollBarVertical.PageSize / 10;
            }
            else if (Mouse.WheelDelta < -50)
            {
                _itemIndex += _scrollBarVertical.PageSize / 10;
            }

            if (_itemIndex < 0)
            {
                _itemIndex = 0;
            }
            else if (_itemIndex >= Items.Count)
            {
                _itemIndex = Items.Count - 1;
            }

            ItemIndex = _itemIndex;
        }

        if (Visible && _items != null && _items.Count != _itemsCount)
        {
            _itemsCount = _items.Count;
            ItemsChanged();
        }
    } // Update



    private void TrackItem(int x, int y)
    {
        if (_items != null && _items.Count > 0 && _pane.ControlRectangleRelativeToParent.Contains(new Point(x, y)))
        {
            var font = SkinInformation.Layers["Control"].Text;
            var fontHeight = (int)font.Font.Font.MeasureString(_items[0].ToString()).Y;
            var scrollbarValue = _scrollBarVertical.Value;
            if (!_scrollBarVertical.Visible)
            {
                scrollbarValue = 0;
            }

            var lowerBound = (int)Math.Floor(scrollbarValue / 10f);
            var upperBound = (int)Math.Ceiling((scrollbarValue + _scrollBarVertical.PageSize) / 10f);
            // Last page is special.
            if (upperBound > _itemsCount)
            {
                lowerBound = _items.Count - (int)Math.Ceiling(_scrollBarVertical.PageSize / 10f);
                upperBound = _items.Count;
                if (lowerBound < 0)
                {
                    lowerBound = 0;
                }
            }
            // Calculate current item (without considering bounds or out of range)
            var i = (int)Math.Floor(lowerBound + (float)y / fontHeight);

            if (i >= 0 && i < Items.Count && i >= lowerBound && i < upperBound)
            {
                ItemIndex = i;
            }

            Focused = true;
        }
    } // TrackItem



    private void ItemsChanged()
    {
        if (_items != null && _items.Count > 0)
        {
            var font = SkinInformation.Layers["Control"].Text;
            var h = (int)font.Font.Font.MeasureString(_items[0].ToString()).Y;

            var sizev = Height - SkinInformation.Layers["Control"].ContentMargins.Vertical;
            _scrollBarVertical.Range = _items.Count * 10;
            _scrollBarVertical.PageSize = (int)Math.Floor((float)sizev * 10 / h);
            Invalidate();
        }
        else if (_items == null || _items.Count <= 0)
        {
            _scrollBarVertical.Range = 1;
            _scrollBarVertical.PageSize = 1;
            Invalidate();
        }
    } // ItemsChanged



    public virtual void ScrollTo(int index)
    {
        ItemsChanged();
        if (index * 10 < _scrollBarVertical.Value)
        {
            _scrollBarVertical.Value = index * 10;
        }
        else if (index >= (int)Math.Floor(((float)_scrollBarVertical.Value + _scrollBarVertical.PageSize) / 10f))
        {
            _scrollBarVertical.Value = (index + 1) * 10 - _scrollBarVertical.PageSize;
        }
    } // ScrollTo



    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButton.Left || e.Button == MouseButton.Right)
        {
            TrackItem(e.Position.X, e.Position.Y);
        }
    } // OnMouseDown

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_hotTrack)
        {
            TrackItem(e.Position.X, e.Position.Y);
        }
    } // OnMouseMove

    protected override void OnKeyPress(KeyEventArgs e)
    {
        if (e.Key == Keys.Down)
        {
            e.Handled = true;
            _itemIndex += _scrollBarVertical.StepSize / 10;
        }
        else if (e.Key == Keys.Up)
        {
            e.Handled = true;
            _itemIndex -= _scrollBarVertical.StepSize / 10;
        }
        else if (e.Key == Keys.PageDown)
        {
            e.Handled = true;
            _itemIndex += _scrollBarVertical.PageSize / 10;
        }
        else if (e.Key == Keys.PageUp)
        {
            e.Handled = true;
            _itemIndex -= _scrollBarVertical.PageSize / 10;
        }
        else if (e.Key == Keys.Home)
        {
            e.Handled = true;
            _itemIndex = 0;
        }
        else if (e.Key == Keys.End)
        {
            e.Handled = true;
            _itemIndex = _items.Count - 1;
        }

        if (_itemIndex < 0)
        {
            _itemIndex = 0;
        }
        else if (_itemIndex >= Items.Count)
        {
            _itemIndex = Items.Count - 1;
        }

        ItemIndex = _itemIndex;

        base.OnKeyPress(e);
    } // OnKeyPress

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        ItemsChanged();
    } // OnResize

    protected virtual void OnItemIndexChanged(EventArgs e)
    {
        if (ItemIndexChanged != null)
        {
            ItemIndexChanged.Invoke(this, e);
        }
    } // OnItemIndexChanged

    protected virtual void OnHotTrackChanged(EventArgs e)
    {
        if (HotTrackChanged != null)
        {
            HotTrackChanged.Invoke(this, e);
        }
    } // OnHotTrackChanged

    protected virtual void OnHideSelectionChanged(EventArgs e)
    {
        if (HideSelectionChanged != null)
        {
            HideSelectionChanged.Invoke(this, e);
        }
    } // OnHideSelectionChanged


} // ListBox
// XNAFinalEngine.UserInterface
