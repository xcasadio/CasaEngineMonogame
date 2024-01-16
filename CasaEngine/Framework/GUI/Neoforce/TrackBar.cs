using Microsoft.Xna.Framework;
using TomShane.Neoforce.Controls.Graphics;
using TomShane.Neoforce.Controls.Input;
using TomShane.Neoforce.Controls.Skins;

namespace TomShane.Neoforce.Controls;

public class TrackBar : Control
{
    private int _range = 100;
    private int _value;
    private int _stepSize = 1;
    private int _pageSize = 5;
    private bool _scale = true;
    private Button _btnSlider;

    public virtual int Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                if (_value < 0)
                {
                    _value = 0;
                }

                if (_value > _range)
                {
                    _value = _range;
                }

                Invalidate();
                if (!Suspended)
                {
                    OnValueChanged(new EventArgs());
                }
            }
        }
    }

    public virtual int Range
    {
        get => _range;
        set
        {
            if (_range != value)
            {
                _range = value;
                _range = value;
                if (_pageSize > _range)
                {
                    _pageSize = _range;
                }

                RecalcParams();
                if (!Suspended)
                {
                    OnRangeChanged(new EventArgs());
                }
            }
        }
    }

    public virtual int PageSize
    {
        get => _pageSize;
        set
        {
            if (_pageSize != value)
            {
                _pageSize = value;
                if (_pageSize > _range)
                {
                    _pageSize = _range;
                }

                RecalcParams();
                if (!Suspended)
                {
                    OnPageSizeChanged(new EventArgs());
                }
            }
        }
    }

    public virtual int StepSize
    {
        get => _stepSize;
        set
        {
            if (_stepSize != value)
            {
                _stepSize = value;
                if (_stepSize > _range)
                {
                    _stepSize = _range;
                }

                if (!Suspended)
                {
                    OnStepSizeChanged(new EventArgs());
                }
            }
        }
    }

    public virtual bool Scale
    {
        get => _scale;
        set => _scale = value;
    }

    public event EventHandler ValueChanged;
    public event EventHandler RangeChanged;
    public event EventHandler StepSizeChanged;
    public event EventHandler PageSizeChanged;

    public TrackBar()
    {
    }

    public override void Initialize(Manager manager)
    {
        base.Initialize(manager);

        _btnSlider = new Button();
        _btnSlider.Initialize(Manager);
        _btnSlider.Text = "";
        _btnSlider.CanFocus = true;
        _btnSlider.Parent = this;
        _btnSlider.Anchor = Anchors.Left | Anchors.Top | Anchors.Bottom;
        _btnSlider.Detached = true;
        _btnSlider.Movable = true;
        _btnSlider.Move += btnSlider_Move;
        _btnSlider.KeyPress += btnSlider_KeyPress;
        _btnSlider.GamePadPress += btnSlider_GamePadPress;
        _btnSlider.Skin = new SkinControl(Manager.Skin.Controls["TrackBar.Button"]);

        Width = 64;
        Height = 20;
        CanFocus = false;
    }

    protected internal override void InitializeSkin()
    {
        base.InitializeSkin();
        Skin = new SkinControl(Manager.Skin.Controls["TrackBar"]);
    }

    protected override void DrawControl(IRenderer renderer, Rectangle rect, GameTime gameTime)
    {
        RecalcParams();

        var p = Skin.Layers["Control"];
        var l = Skin.Layers["Scale"];

        var ratio = 0.66f;
        var h = (int)(ratio * rect.Height);
        var t = rect.Top + (Height - h) / 2;

        var px = _value / (float)_range;
        var w = (int)Math.Ceiling(px * (rect.Width - p.ContentMargins.Horizontal - _btnSlider.Width)) + 2;

        if (w < l.SizingMargins.Vertical)
        {
            w = l.SizingMargins.Vertical;
        }

        if (w > rect.Width - p.ContentMargins.Horizontal)
        {
            w = rect.Width - p.ContentMargins.Horizontal;
        }

        var r1 = new Rectangle(rect.Left + p.ContentMargins.Left, t + p.ContentMargins.Top, w, h - p.ContentMargins.Vertical);

        base.DrawControl(renderer, new Rectangle(rect.Left, t, rect.Width, h), gameTime);
        if (_scale)
        {
            renderer.DrawLayer(this, l, r1);
        }
    }

    void btnSlider_Move(object sender, MoveEventArgs e)
    {
        var p = Skin.Layers["Control"];
        var size = _btnSlider.Width;
        var w = Width - p.ContentMargins.Horizontal - size;
        var pos = e.Left;

        if (pos < p.ContentMargins.Left)
        {
            pos = p.ContentMargins.Left;
        }

        if (pos > w + p.ContentMargins.Left)
        {
            pos = w + p.ContentMargins.Left;
        }

        _btnSlider.SetPosition(pos, 0);

        var px = _range / (float)w;
        Value = (int)Math.Ceiling((pos - p.ContentMargins.Left) * px);
    }

    private void RecalcParams()
    {
        if (_btnSlider != null)
        {
            if (_btnSlider.Width > 12)
            {
                _btnSlider.Glyph = new Glyph(Manager.Skin.Images["Shared.Glyph"].Resource);
                _btnSlider.Glyph.SizeMode = SizeMode.Centered;
            }
            else
            {
                _btnSlider.Glyph = null;
            }

            var p = Skin.Layers["Control"];
            _btnSlider.Width = (int)(Height * 0.8);
            _btnSlider.Height = Height;
            var size = _btnSlider.Width;
            var w = Width - p.ContentMargins.Horizontal - size;

            var px = _range / (float)w;
            var pos = p.ContentMargins.Left + (int)Math.Ceiling(Value / px);

            if (pos < p.ContentMargins.Left)
            {
                pos = p.ContentMargins.Left;
            }

            if (pos > w + p.ContentMargins.Left)
            {
                pos = w + p.ContentMargins.Left;
            }

            _btnSlider.SetPosition(pos, 0);
        }
    }

    protected override void OnMousePress(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button == MouseButton.Left)
        {
            var pos = e.Position.X;

            if (pos < _btnSlider.Left)
            {
                Value -= _pageSize;
            }
            else if (pos >= _btnSlider.Left + _btnSlider.Width)
            {
                Value += _pageSize;
            }
        }
    }

    void btnSlider_GamePadPress(object sender, GamePadEventArgs e)
    {
        if (e.Button == GamePadActions.Left || e.Button == GamePadActions.Down)
        {
            Value -= _stepSize;
        }

        if (e.Button == GamePadActions.Right || e.Button == GamePadActions.Up)
        {
            Value += _stepSize;
        }
    }

    void btnSlider_KeyPress(object sender, KeyEventArgs e)
    {
        if (e.Key == Microsoft.Xna.Framework.Input.Keys.Left || e.Key == Microsoft.Xna.Framework.Input.Keys.Down)
        {
            Value -= _stepSize;
        }
        else if (e.Key == Microsoft.Xna.Framework.Input.Keys.Right || e.Key == Microsoft.Xna.Framework.Input.Keys.Up)
        {
            Value += _stepSize;
        }
        else if (e.Key == Microsoft.Xna.Framework.Input.Keys.PageDown)
        {
            Value -= _pageSize;
        }
        else if (e.Key == Microsoft.Xna.Framework.Input.Keys.PageUp)
        {
            Value += _pageSize;
        }
        else if (e.Key == Microsoft.Xna.Framework.Input.Keys.Home)
        {
            Value = 0;
        }
        else if (e.Key == Microsoft.Xna.Framework.Input.Keys.End)
        {
            Value = Range;
        }
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        RecalcParams();
    }

    protected virtual void OnValueChanged(EventArgs e)
    {
        ValueChanged?.Invoke(this, e);
    }

    protected virtual void OnRangeChanged(EventArgs e)
    {
        RangeChanged?.Invoke(this, e);
    }

    protected virtual void OnPageSizeChanged(EventArgs e)
    {
        PageSizeChanged?.Invoke(this, e);
    }

    protected virtual void OnStepSizeChanged(EventArgs e)
    {
        StepSizeChanged?.Invoke(this, e);
    }
}