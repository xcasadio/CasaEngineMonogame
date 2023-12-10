using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace TomShane.Neoforce.Controls;

///  <include file='Documents/ListBox.xml' path='ListBox/Class[@name="ListBox"]/*' />          
public class ListBox : Control
{

    private List<object> _items = new();
    private ScrollBar _sbVert;
    private ClipBox _pane;
    private int _itemIndex = -1;
    private bool _hotTrack;
    private int _itemsCount;
    private bool _hideSelection = true;

    public virtual List<object> Items
    {
        get => _items;
        internal set => _items = value;
    }

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
    }

    public virtual int ItemIndex
    {
        get => _itemIndex;
        set
        {
            //if (itemIndex != value)
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
        }
    }

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
    }

    public event EventHandler HotTrackChanged;
    public event EventHandler ItemIndexChanged;
    public event EventHandler HideSelectionChanged;

    public ListBox(Manager manager)
        : base(manager)
    {
        Width = 64;
        Height = 64;
        MinimumHeight = 16;

        _sbVert = new ScrollBar(Manager, Orientation.Vertical);
        _sbVert.Init();
        _sbVert.Parent = this;
        _sbVert.Left = Left + Width - _sbVert.Width - Skin.Layers["Control"].ContentMargins.Right;
        _sbVert.Top = Top + Skin.Layers["Control"].ContentMargins.Top;
        _sbVert.Height = Height - Skin.Layers["Control"].ContentMargins.Vertical;
        _sbVert.Anchor = Anchors.Top | Anchors.Right | Anchors.Bottom;
        _sbVert.PageSize = 25;
        _sbVert.Range = 1;
        _sbVert.PageSize = 1;
        _sbVert.StepSize = 10;

        _pane = new ClipBox(manager);
        _pane.Init();
        _pane.Parent = this;
        _pane.Top = Skin.Layers["Control"].ContentMargins.Top;
        _pane.Left = Skin.Layers["Control"].ContentMargins.Left;
        _pane.Width = Width - _sbVert.Width - Skin.Layers["Control"].ContentMargins.Horizontal - 1;
        _pane.Height = Height - Skin.Layers["Control"].ContentMargins.Vertical;
        _pane.Anchor = Anchors.All;
        _pane.Passive = true;
        _pane.CanFocus = false;
        _pane.Draw += DrawPane;

        CanFocus = true;
        Passive = false;
    }

    public override void Init()
    {
        base.Init();
    }

    public virtual void AutoHeight(int maxItems)
    {
        if (_items != null && _items.Count < maxItems)
        {
            maxItems = _items.Count;
        }

        if (maxItems < 3)
        {
            //maxItems = 3;
            _sbVert.Visible = false;
            _pane.Width = Width - Skin.Layers["Control"].ContentMargins.Horizontal - 1;
        }
        else
        {
            _pane.Width = Width - _sbVert.Width - Skin.Layers["Control"].ContentMargins.Horizontal - 1;
            _sbVert.Visible = true;
        }

        var font = Skin.Layers["Control"].Text;
        if (_items != null && _items.Count > 0)
        {
            var h = (int)font.Font.Resource.MeasureString(_items[0].ToString()).Y;
            Height = h * maxItems + Skin.Layers["Control"].ContentMargins.Vertical;// - Skin.OriginMargins.Vertical);
        }
        else
        {
            Height = 32;
        }
    }

    public override int MinimumHeight
    {
        get => base.MinimumHeight;
        set
        {
            base.MinimumHeight = value;
            if (_sbVert != null)
            {
                _sbVert.MinimumHeight = value;
            }
        }
    }

    protected override void DrawControl(Renderer renderer, Rectangle rect, GameTime gameTime)
    {
        _sbVert.Invalidate();
        _pane.Invalidate();
        //DrawPane(this, new DrawEventArgs(renderer, rect, gameTime));

        base.DrawControl(renderer, rect, gameTime);
    }

    private void DrawPane(object sender, DrawEventArgs e)
    {
        if (_items != null && _items.Count > 0)
        {
            var font = Skin.Layers["Control"].Text;
            var sel = Skin.Layers["ListBox.Selection"];
            var h = (int)font.Font.Resource.MeasureString(_items[0].ToString()).Y;
            var v = _sbVert.Value / 10;
            var p = _sbVert.PageSize / 10;
            var d = (int)(_sbVert.Value % 10 / 10f * h);
            var c = _items.Count;
            var s = _itemIndex;

            for (var i = v; i <= v + p + 1; i++)
            {
                if (i < c)
                {
                    e.Renderer.DrawString(this, Skin.Layers["Control"], _items[i].ToString(), new Rectangle(e.Rectangle.Left, e.Rectangle.Top - d + (i - v) * h, e.Rectangle.Width, h), false);
                }
            }
            if (s >= 0 && s < c && (Focused || !_hideSelection))
            {
                var pos = -d + (s - v) * h;
                if (pos > -h && pos < (p + 1) * h)
                {
                    e.Renderer.DrawLayer(this, sel, new Rectangle(e.Rectangle.Left, e.Rectangle.Top + pos, e.Rectangle.Width, h));
                    e.Renderer.DrawString(this, sel, _items[s].ToString(), new Rectangle(e.Rectangle.Left, e.Rectangle.Top + pos, e.Rectangle.Width, h), false);
                }
            }
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button == MouseButton.Left || e.Button == MouseButton.Right)
        {
            TrackItem(e.Position.X, e.Position.Y);
        }
    }

    private void TrackItem(int x, int y)
    {
        if (_items != null && _items.Count > 0 && _pane.ControlRect.Contains(new Point(x, y)))
        {
            var font = Skin.Layers["Control"].Text;
            var h = (int)font.Font.Resource.MeasureString(_items[0].ToString()).Y;
            var d = (int)(_sbVert.Value % 10 / 10f * h);
            var i = (int)Math.Floor(_sbVert.Value / 10f + (float)y / h);
            if (i >= 0 && i < Items.Count && i >= (int)Math.Floor(_sbVert.Value / 10f) && i < (int)Math.Ceiling((_sbVert.Value + _sbVert.PageSize) / 10f))
            {
                ItemIndex = i;
            }

            Focused = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_hotTrack)
        {
            TrackItem(e.Position.X, e.Position.Y);
        }
    }

    protected override void OnKeyPress(KeyEventArgs e)
    {
        if (e.Key == Keys.Down)
        {
            e.Handled = true;
            _itemIndex += _sbVert.StepSize / 10;
        }
        else if (e.Key == Keys.Up)
        {
            e.Handled = true;
            _itemIndex -= _sbVert.StepSize / 10;
        }
        else if (e.Key == Keys.PageDown)
        {
            e.Handled = true;
            _itemIndex += _sbVert.PageSize / 10;
        }
        else if (e.Key == Keys.PageUp)
        {
            e.Handled = true;
            _itemIndex -= _sbVert.PageSize / 10;
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
    }

    /// <summary>
    /// Handles mouse scroll events for the list box.
    /// </summary>
    /// <param name="e"></param>
    protected override void OnMouseScroll(MouseEventArgs e)
    {
        Focused = true;

        if (e.ScrollDirection == MouseScrollDirection.Down)
        {
            e.Handled = true;
            _itemIndex += _sbVert.StepSize / 10;
        }
        else if (e.ScrollDirection == MouseScrollDirection.Up)
        {
            e.Handled = true;
            _itemIndex -= _sbVert.StepSize / 10;
        }

        // Wrap index in collection range.
        if (_itemIndex < 0)
        {
            _itemIndex = 0;
        }
        else if (_itemIndex >= Items.Count)
        {
            _itemIndex = Items.Count - 1;
        }

        ItemIndex = _itemIndex;

        base.OnMouseScroll(e);
    }

    protected override void OnGamePadPress(GamePadEventArgs e)
    {
        if (e.Button == GamePadActions.Down)
        {
            e.Handled = true;
            _itemIndex += _sbVert.StepSize / 10;
        }
        else if (e.Button == GamePadActions.Up)
        {
            e.Handled = true;
            _itemIndex -= _sbVert.StepSize / 10;
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
        base.OnGamePadPress(e);
    }

    private void ItemsChanged()
    {
        if (_items != null && _items.Count > 0)
        {
            var font = Skin.Layers["Control"].Text;
            var h = (int)font.Font.Resource.MeasureString(_items[0].ToString()).Y;

            var sizev = Height - Skin.Layers["Control"].ContentMargins.Vertical;
            _sbVert.Range = _items.Count * 10;
            _sbVert.PageSize = (int)Math.Floor((float)sizev * 10 / h);
            Invalidate();
        }
        else if (_items == null || _items.Count <= 0)
        {
            _sbVert.Range = 1;
            _sbVert.PageSize = 1;
            Invalidate();
        }
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        ItemsChanged();
    }

    public virtual void ScrollTo(int index)
    {
        ItemsChanged();
        if (index * 10 < _sbVert.Value)
        {
            _sbVert.Value = index * 10;
        }
        else if (index >= (int)Math.Floor(((float)_sbVert.Value + _sbVert.PageSize) / 10f))
        {
            _sbVert.Value = (index + 1) * 10 - _sbVert.PageSize;
        }
    }

    protected internal override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (Visible && _items != null && _items.Count != _itemsCount)
        {
            _itemsCount = _items.Count;
            ItemsChanged();
        }
    }

    protected virtual void OnItemIndexChanged(EventArgs e)
    {
        ItemIndexChanged?.Invoke(this, e);
    }

    protected virtual void OnHotTrackChanged(EventArgs e)
    {
        HotTrackChanged?.Invoke(this, e);
    }

    protected virtual void OnHideSelectionChanged(EventArgs e)
    {
        HideSelectionChanged?.Invoke(this, e);
    }

}