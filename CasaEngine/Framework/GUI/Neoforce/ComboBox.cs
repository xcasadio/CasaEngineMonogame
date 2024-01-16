using Microsoft.Xna.Framework;
using TomShane.Neoforce.Controls.Graphics;
using TomShane.Neoforce.Controls.Input;
using TomShane.Neoforce.Controls.Skins;

namespace TomShane.Neoforce.Controls;

public class ComboBox : TextBox
{
    private Button _btnDown;
    private List<object> _items = new();
    private ListBox _lstCombo;
    private int _maxItems = 5;

    public override bool ReadOnly
    {
        get => base.ReadOnly;
        set
        {
            base.ReadOnly = value;
            CaretVisible = !value;
            if (value)
            {
                Cursor = Manager.Skin.Cursors["Default"].Resource;
            }
            else
            {
                Cursor = Manager.Skin.Cursors["Text"].Resource;
            }
        }
    }

    public bool DrawSelection { get; set; } = true;

    public override string Text
    {
        get => base.Text;
        set
        {
            base.Text = value;
            //if (!items.Contains(value))  --- bug
            if (!_items.ConvertAll(item => item.ToString()).Contains(value))
            {
                ItemIndex = -1;
            }
        }
    }

    public virtual List<object> Items => _items;

    public int MaxItems
    {
        get => _maxItems;
        set
        {
            if (_maxItems != value)
            {
                _maxItems = value;
                if (!Suspended)
                {
                    OnMaxItemsChanged(new EventArgs());
                }
            }
        }
    }

    public int ItemIndex
    {
        get => _lstCombo.ItemIndex;
        set
        {
            if (_lstCombo != null)
            {
                if (value >= 0 && value < _items.Count)
                {
                    _lstCombo.ItemIndex = value;
                    Text = _lstCombo.Items[value].ToString();
                }
                else
                {
                    _lstCombo.ItemIndex = -1;
                }
            }
            if (!Suspended)
            {
                OnItemIndexChanged(new EventArgs());
            }
        }
    }

    public event EventHandler MaxItemsChanged;
    public event EventHandler ItemIndexChanged;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // We added the listbox to another parent than this control, so we dispose it manually
            if (_lstCombo != null)
            {
                _lstCombo.Dispose();
                _lstCombo = null;
            }
            Manager.Input.MouseDown -= Input_MouseDown;
        }
        base.Dispose(disposing);
    }

    public override void Initialize(Manager manager)
    {
        manager.Input.MouseDown += Input_MouseDown;

        _btnDown = new Button();
        _btnDown.Initialize(manager);
        _btnDown.Skin = new SkinControl(manager.Skin.Controls["ComboBox.Button"]);
        _btnDown.CanFocus = false;
        _btnDown.Click += btnDown_Click;

        _lstCombo = new ListBox();
        _lstCombo.Initialize(manager);
        _lstCombo.HotTrack = true;
        _lstCombo.Detached = true;
        _lstCombo.Visible = false;
        _lstCombo.Click += lstCombo_Click;
        _lstCombo.FocusLost += lstCombo_FocusLost;
        _lstCombo.Items = _items;
        _lstCombo.Skin = new SkinControl(manager.Skin.Controls["ComboBox.ListBox"]);

        _btnDown.Glyph = new Glyph(manager.Skin.Images["Shared.ArrowDown"].Resource);
        _btnDown.Glyph.Color = manager.Skin.Controls["ComboBox.Button"].Layers["Control"].Text.Colors.Enabled;
        _btnDown.Glyph.SizeMode = SizeMode.Centered;

        base.Initialize(manager);

        Add(_btnDown, false);

        ReadOnly = true;
        Height = 20;
        Width = 64;
    }

    protected internal override void InitializeSkin()
    {
        base.InitializeSkin();
        Skin = new SkinControl(Manager.Skin.Controls["ComboBox"]);
        AdjustMargins();
        ReadOnly = ReadOnly; // To init the right cursor
    }

    protected override void DrawControl(IRenderer renderer, Rectangle rect, GameTime gameTime)
    {
        base.DrawControl(renderer, rect, gameTime);

        if (ReadOnly && (Focused || _lstCombo.Focused) && DrawSelection)
        {
            var lr = Skin.Layers[0];
            var rc = new Rectangle(rect.Left + lr.ContentMargins.Left,
                rect.Top + lr.ContentMargins.Top,
                Width - lr.ContentMargins.Horizontal - _btnDown.Width,
                Height - lr.ContentMargins.Vertical);
            renderer.Draw(Manager.Skin.Images["ListBox.Selection"].Resource, rc, Color.FromNonPremultiplied(255, 255, 255, 128));
        }
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        if (_btnDown != null)
        {
            _btnDown.Width = 16;
            _btnDown.Height = Height - Skin.Layers[0].ContentMargins.Vertical;
            _btnDown.Top = Skin.Layers[0].ContentMargins.Top;
            _btnDown.Left = Width - _btnDown.Width - 2;
        }
    }

    void btnDown_Click(object sender, EventArgs e)
    {
        if (_items != null && _items.Count > 0)
        {
            if (Root != null && Root is Container)
            {
                (Root as Container).Add(_lstCombo, false);
                _lstCombo.Alpha = Root.Alpha;
                _lstCombo.Left = AbsoluteLeft - Root.Left;
                _lstCombo.Top = AbsoluteTop - Root.Top + Height + 1;
            }
            else
            {
                Manager.Add(_lstCombo);
                _lstCombo.Alpha = Alpha;
                _lstCombo.Left = AbsoluteLeft;
                _lstCombo.Top = AbsoluteTop + Height + 1;
            }

            _lstCombo.AutoHeight(_maxItems);
            if (_lstCombo.AbsoluteTop + _lstCombo.Height > Manager.TargetHeight)
            {
                _lstCombo.Top = _lstCombo.Top - Height - _lstCombo.Height - 2;
            }

            _lstCombo.Visible = !_lstCombo.Visible;
            _lstCombo.Focused = true;
            _lstCombo.Width = Width;
            _lstCombo.AutoHeight(_maxItems);
        }
    }

    void Input_MouseDown(object sender, MouseEventArgs e)
    {
        if (ReadOnly &&
            e.Position.X >= AbsoluteLeft &&
            e.Position.X <= AbsoluteLeft + Width &&
            e.Position.Y >= AbsoluteTop &&
            e.Position.Y <= AbsoluteTop + Height)
        {
            return;
        }

        if (_lstCombo.Visible &&
            (e.Position.X < _lstCombo.AbsoluteLeft ||
             e.Position.X > _lstCombo.AbsoluteLeft + _lstCombo.Width ||
             e.Position.Y < _lstCombo.AbsoluteTop ||
             e.Position.Y > _lstCombo.AbsoluteTop + _lstCombo.Height) &&
            (e.Position.X < _btnDown.AbsoluteLeft ||
             e.Position.X > _btnDown.AbsoluteLeft + _btnDown.Width ||
             e.Position.Y < _btnDown.AbsoluteTop ||
             e.Position.Y > _btnDown.AbsoluteTop + _btnDown.Height))
        {
            //lstCombo.Visible = false;      
            btnDown_Click(sender, e);
        }
    }

    void lstCombo_Click(object sender, EventArgs e)
    {
        var ex = e is MouseEventArgs args ? args : new MouseEventArgs();

        if (ex.Button == MouseButton.Left || ex.Button == MouseButton.None)
        {
            _lstCombo.Visible = false;
            if (_lstCombo.ItemIndex >= 0)
            {
                Text = _lstCombo.Items[_lstCombo.ItemIndex].ToString();
                Focused = true;
                ItemIndex = _lstCombo.ItemIndex;
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Keys.Down)
        {
            e.Handled = true;
            btnDown_Click(this, new MouseEventArgs());
        }
        base.OnKeyDown(e);
    }

    protected override void OnGamePadDown(GamePadEventArgs e)
    {
        if (!e.Handled)
        {
            if (e.Button == GamePadActions.Click || e.Button == GamePadActions.Press || e.Button == GamePadActions.Down)
            {
                e.Handled = true;
                btnDown_Click(this, new MouseEventArgs());
            }
        }
        base.OnGamePadDown(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (ReadOnly && e.Button == MouseButton.Left)
        {
            btnDown_Click(this, new MouseEventArgs());
        }
    }

    protected virtual void OnMaxItemsChanged(EventArgs e)
    {
        MaxItemsChanged?.Invoke(this, e);
    }

    protected virtual void OnItemIndexChanged(EventArgs e)
    {
        ItemIndexChanged?.Invoke(this, e);
    }

    void lstCombo_FocusLost(object sender, EventArgs e)
    {
        //lstCombo.Visible = false;
        Invalidate();
    }

    protected override void AdjustMargins()
    {
        base.AdjustMargins();
        ClientMargins = new Margins(ClientMargins.Left, ClientMargins.Top, ClientMargins.Right + 16, ClientMargins.Bottom);
    }

}