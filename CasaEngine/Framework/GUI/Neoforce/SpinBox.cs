using Microsoft.Xna.Framework;
using TomShane.Neoforce.Controls.Graphics;
using TomShane.Neoforce.Controls.Skins;

namespace TomShane.Neoforce.Controls;

public enum SpinBoxMode
{
    Range,
    List
}

public class SpinBox : TextBox
{
    private Button _btnUp;
    private Button _btnDown;
    private readonly List<object> _items = new();
    private float _value;
    private float _minimum;
    private float _maximum = 100;
    private float _step = 0.25f;
    private int _rounding = 2;
    private int _itemIndex = -1;

    public new virtual SpinBoxMode Mode { get; set; }

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

    public virtual List<object> Items => _items;

    public float Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                Invalidate();
            }
        }
    }

    public float Minimum
    {
        get => _minimum;
        set
        {
            if (_minimum != value)
            {
                _minimum = value;
            }
        }
    }

    public float Maximum
    {
        get => _maximum;
        set
        {
            if (_maximum != value)
            {
                _maximum = value;
            }
        }
    }

    public float Step
    {
        get => _step;
        set
        {
            if (_step != value)
            {
                _step = value;
            }
        }
    }

    public int ItemIndex
    {
        get => _itemIndex;
        set
        {
            if (Mode == SpinBoxMode.List)
            {
                _itemIndex = value;
                Text = _items[_itemIndex].ToString();
            }
        }
    }

    public int Rounding
    {
        get => _rounding;
        set
        {
            if (_rounding != value)
            {
                _rounding = value;
                Invalidate();
            }
        }
    }

    public override void Initialize(Manager manager)
    {
        base.Initialize(manager);

        _btnUp = new Button();
        _btnUp.Initialize(Manager);
        _btnUp.CanFocus = false;
        _btnUp.MousePress += btn_MousePress;
        Add(_btnUp, false);

        _btnDown = new Button();
        _btnDown.Initialize(Manager);
        _btnDown.CanFocus = false;
        _btnDown.MousePress += btn_MousePress;
        Add(_btnDown, false);

        var sc = new SkinControl(_btnUp.Skin);
        sc.Layers["Control"] = new SkinLayer(Skin.Layers["Button"]);
        sc.Layers["Button"].Name = "Control";
        _btnUp.Skin = _btnDown.Skin = sc;

        _btnUp.Glyph = new Glyph(Manager.Skin.Images["Shared.ArrowUp"].Resource);
        _btnUp.Glyph.SizeMode = SizeMode.Centered;
        _btnUp.Glyph.Color = Manager.Skin.Controls["Button"].Layers["Control"].Text.Colors.Enabled;

        _btnDown.Glyph = new Glyph(Manager.Skin.Images["Shared.ArrowDown"].Resource);
        _btnDown.Glyph.SizeMode = SizeMode.Centered;
        _btnDown.Glyph.Color = Manager.Skin.Controls["Button"].Layers["Control"].Text.Colors.Enabled;

        ReadOnly = true;

        Height = 20;
        Width = 64;
    }

    protected internal override void InitializeSkin()
    {
        base.InitializeSkin();
        Skin = new SkinControl(Manager.Skin.Controls["SpinBox"]);
    }

    protected override void DrawControl(IRenderer renderer, Rectangle rect, GameTime gameTime)
    {
        base.DrawControl(renderer, rect, gameTime);

        if (ReadOnly && Focused)
        {
            var lr = Skin.Layers[0];
            var rc = new Rectangle(rect.Left + lr.ContentMargins.Left,
                rect.Top + lr.ContentMargins.Top,
                Width - lr.ContentMargins.Horizontal - _btnDown.Width - _btnUp.Width,
                Height - lr.ContentMargins.Vertical);
            renderer.Draw(Manager.Skin.Images["ListBox.Selection"].Resource, rc, Color.FromNonPremultiplied(255, 255, 255, 128));
        }
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        if (_btnUp != null)
        {
            _btnUp.Width = 16;
            _btnUp.Height = Height - Skin.Layers["Control"].ContentMargins.Vertical;
            _btnUp.Top = Skin.Layers["Control"].ContentMargins.Top;
            _btnUp.Left = Width - 16 - 2 - 16 - 1;
        }
        if (_btnDown != null)
        {
            _btnDown.Width = 16;
            _btnDown.Height = Height - Skin.Layers["Control"].ContentMargins.Vertical;
            _btnDown.Top = Skin.Layers["Control"].ContentMargins.Top; ;
            _btnDown.Left = Width - 16 - 2;
        }
    }

    private void ShiftIndex(bool direction)
    {
        if (Mode == SpinBoxMode.List)
        {
            if (_items.Count > 0)
            {
                if (direction)
                {
                    _itemIndex += 1;
                }
                else
                {
                    _itemIndex -= 1;
                }

                if (_itemIndex < 0)
                {
                    _itemIndex = 0;
                }

                if (_itemIndex > _items.Count - 1)
                {
                    _itemIndex = _itemIndex = _items.Count - 1;
                }

                Text = _items[_itemIndex].ToString();
            }
        }
        else
        {
            if (direction)
            {
                _value += _step;
            }
            else
            {
                _value -= _step;
            }

            if (_value < _minimum)
            {
                _value = _minimum;
            }

            if (_value > _maximum)
            {
                _value = _maximum;
            }

            Text = _value.ToString("n" + _rounding.ToString());
        }
    }

    private void btn_MousePress(object sender, MouseEventArgs e)
    {
        Focused = true;
        if (sender == _btnUp)
        {
            ShiftIndex(true);
        }
        else if (sender == _btnDown)
        {
            ShiftIndex(false);
        }
    }

    protected override void OnKeyPress(KeyEventArgs e)
    {
        if (e.Key == Keys.Up)
        {
            e.Handled = true;
            ShiftIndex(true);
        }
        else if (e.Key == Keys.Down)
        {
            e.Handled = true;
            ShiftIndex(false);
        }

        base.OnKeyPress(e);
    }

    protected override void OnGamePadPress(GamePadEventArgs e)
    {
        if (e.Button == GamePadActions.Up)
        {
            e.Handled = true;
            ShiftIndex(true);
        }
        else if (e.Button == GamePadActions.Down)
        {
            e.Handled = true;
            ShiftIndex(false);
        }

        base.OnGamePadPress(e);
    }

    protected override void OnGamePadDown(GamePadEventArgs e)
    {
        base.OnGamePadDown(e);
    }

}