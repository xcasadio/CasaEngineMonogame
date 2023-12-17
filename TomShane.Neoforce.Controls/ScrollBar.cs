using System;
using Microsoft.Xna.Framework;
using TomShane.Neoforce.Controls.Graphics;
using TomShane.Neoforce.Controls.Input;
using TomShane.Neoforce.Controls.Skins;

namespace TomShane.Neoforce.Controls;

public class ScrollBar : Control
{

    private int _range = 100;
    private int _value;
    private int _pageSize = 50;
    private int _stepSize = 1;
    private Orientation _orientation = Orientation.Vertical;

    private Button _btnMinus;
    private Button _btnPlus;
    private Button _btnSlider;

    private string _strButton = "ScrollBar.ButtonVert";
    private string _strRail = "ScrollBar.RailVert";
    private string _strSlider = "ScrollBar.SliderVert";
    private string _strGlyph = "ScrollBar.GlyphVert";
    private string _strMinus = "ScrollBar.ArrowUp";
    private string _strPlus = "ScrollBar.ArrowDown";

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

                if (_value > _range - _pageSize)
                {
                    _value = _range - _pageSize;
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
                if (!Suspended)
                {
                    OnStepSizeChanged(new EventArgs());
                }
            }
        }
    }

    public event EventHandler ValueChanged;
    public event EventHandler RangeChanged;
    public event EventHandler StepSizeChanged;
    public event EventHandler PageSizeChanged;

    public ScrollBar(Manager manager, Orientation orientation) : base(manager)
    {
        _orientation = orientation;
        CanFocus = false;

        if (orientation == Orientation.Horizontal)
        {
            _strButton = "ScrollBar.ButtonHorz";
            _strRail = "ScrollBar.RailHorz";
            _strSlider = "ScrollBar.SliderHorz";
            _strGlyph = "ScrollBar.GlyphHorz";
            _strMinus = "ScrollBar.ArrowLeft";
            _strPlus = "ScrollBar.ArrowRight";

            MinimumHeight = 16;
            MinimumWidth = 46;
            Width = 64;
            Height = 16;
        }
        else
        {
            _strButton = "ScrollBar.ButtonVert";
            _strRail = "ScrollBar.RailVert";
            _strSlider = "ScrollBar.SliderVert";
            _strGlyph = "ScrollBar.GlyphVert";
            _strMinus = "ScrollBar.ArrowUp";
            _strPlus = "ScrollBar.ArrowDown";

            MinimumHeight = 46;
            MinimumWidth = 16;
            Width = 16;
            Height = 64;
        }

        _btnMinus = new Button(Manager);
        _btnMinus.Init();
        _btnMinus.Text = "";
        _btnMinus.MousePress += ArrowPress;
        _btnMinus.CanFocus = false;

        _btnSlider = new Button(Manager);
        _btnSlider.Init();
        _btnSlider.Text = "";
        _btnSlider.CanFocus = false;
        _btnSlider.MinimumHeight = 16;
        _btnSlider.MinimumWidth = 16;

        _btnPlus = new Button(Manager);
        _btnPlus.Init();
        _btnPlus.Text = "";
        _btnPlus.MousePress += ArrowPress;
        _btnPlus.CanFocus = false;

        _btnSlider.Move += btnSlider_Move;

        Add(_btnMinus);
        Add(_btnSlider);
        Add(_btnPlus);
    }

    public void ScrollUp()
    {
        Value -= _stepSize;
        if (Value < 0)
        {
            Value = 0;
        }
    }

    public void ScrollDown()
    {
        Value += _stepSize;
        if (Value > _range - _pageSize)
        {
            Value = _range - _pageSize - 1;
        }
    }

    public void ScrollUp(bool alot)
    {
        if (alot)
        {
            Value -= _pageSize;
            if (Value < 0)
            {
                Value = 0;
            }
        }
        else
        {
            ScrollUp();
        }
    }

    public void ScrollDown(bool alot)
    {
        if (alot)
        {
            Value += _pageSize;
            if (Value > _range - _pageSize)
            {
                Value = _range - _pageSize - 1;
            }
        }
        else
        {
            ScrollDown();
        }
    }

    public override void Init()
    {
        base.Init();

        var sc = new SkinControl(_btnPlus.Skin);
        sc.Layers["Control"] = new SkinLayer(Skin.Layers[_strButton]);
        sc.Layers[_strButton].Name = "Control";
        _btnPlus.Skin = _btnMinus.Skin = sc;

        var ss = new SkinControl(_btnSlider.Skin);
        ss.Layers["Control"] = new SkinLayer(Skin.Layers[_strSlider]);
        ss.Layers[_strSlider].Name = "Control";
        _btnSlider.Skin = ss;

        _btnMinus.Glyph = new Glyph(Skin.Layers[_strMinus].Image.Resource);
        _btnMinus.Glyph.SizeMode = SizeMode.Centered;
        _btnMinus.Glyph.Color = Manager.Skin.Controls["Button"].Layers["Control"].Text.Colors.Enabled;

        _btnPlus.Glyph = new Glyph(Skin.Layers[_strPlus].Image.Resource);
        _btnPlus.Glyph.SizeMode = SizeMode.Centered;
        _btnPlus.Glyph.Color = Manager.Skin.Controls["Button"].Layers["Control"].Text.Colors.Enabled;

        _btnSlider.Glyph = new Glyph(Skin.Layers[_strGlyph].Image.Resource);
        _btnSlider.Glyph.SizeMode = SizeMode.Centered;

    }

    protected internal override void InitSkin()
    {
        base.InitSkin();
        Skin = new SkinControl(Manager.Skin.Controls["ScrollBar"]);
    }

    protected override void DrawControl(IRenderer renderer, Rectangle rect, GameTime gameTime)
    {
        RecalcParams();

        var bg = Skin.Layers[_strRail];
        renderer.DrawLayer(bg, rect, Color.White, bg.States.Enabled.Index);
    }

    void ArrowPress(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButton.Left)
        {
            if (sender == _btnMinus)
            {
                ScrollUp();
            }
            else if (sender == _btnPlus)
            {
                ScrollDown();
            }
        }
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        RecalcParams();
        if (Value + PageSize > Range)
        {
            Value = Range - PageSize;
        }
    }

    private void RecalcParams()
    {
        var skinLayerSlider = Skin?.Layers[_strSlider];

        if (_btnMinus != null && _btnPlus != null && _btnSlider != null && skinLayerSlider != null)
        {
            if (_orientation == Orientation.Horizontal)
            {
                _btnMinus.Width = Height;
                _btnMinus.Height = Height;

                _btnPlus.Width = Height;
                _btnPlus.Height = Height;
                _btnPlus.Left = Width - Height;
                _btnPlus.Top = 0;

                _btnSlider.Movable = true;
                var size = _btnMinus.Width + skinLayerSlider.OffsetX;

                _btnSlider.MinimumWidth = Height;
                var w = Width - 2 * size;
                _btnSlider.Width = (int)Math.Ceiling(_pageSize * w / (float)_range);
                _btnSlider.Height = Height;

                var px = (Range - PageSize) / (float)(w - _btnSlider.Width);
                var pos = (int)Math.Ceiling(Value / px);
                _btnSlider.SetPosition(size + pos, 0);
                if (_btnSlider.Left < size)
                {
                    _btnSlider.SetPosition(size, 0);
                }

                if (_btnSlider.Left + _btnSlider.Width + size > Width)
                {
                    _btnSlider.SetPosition(Width - size - _btnSlider.Width, 0);
                }
            }
            else
            {
                _btnMinus.Width = Width;
                _btnMinus.Height = Width;

                _btnPlus.Width = Width;
                _btnPlus.Height = Width;
                _btnPlus.Top = Height - Width;

                _btnSlider.Movable = true;
                var size = _btnMinus.Height + skinLayerSlider.OffsetY;

                _btnSlider.MinimumHeight = Width;
                var h = Height - 2 * size;
                _btnSlider.Height = (int)Math.Ceiling(_pageSize * h / (float)_range);
                _btnSlider.Width = Width;

                var px = (Range - PageSize) / (float)(h - _btnSlider.Height);
                var pos = (int)Math.Ceiling(Value / px);
                _btnSlider.SetPosition(0, size + pos);
                if (_btnSlider.Top < size)
                {
                    _btnSlider.SetPosition(0, size);
                }

                if (_btnSlider.Top + _btnSlider.Height + size > Height)
                {
                    _btnSlider.SetPosition(0, Height - size - _btnSlider.Height);
                }
            }
        }
    }

    void btnSlider_Move(object sender, MoveEventArgs e)
    {
        if (_orientation == Orientation.Horizontal)
        {
            var size = _btnMinus.Width + Skin.Layers[_strSlider].OffsetX;
            _btnSlider.SetPosition(e.Left, 0);
            if (_btnSlider.Left < size)
            {
                _btnSlider.SetPosition(size, 0);
            }

            if (_btnSlider.Left + _btnSlider.Width + size > Width)
            {
                _btnSlider.SetPosition(Width - size - _btnSlider.Width, 0);
            }
        }
        else
        {
            var size = _btnMinus.Height + Skin.Layers[_strSlider].OffsetY;
            _btnSlider.SetPosition(0, e.Top);
            if (_btnSlider.Top < size)
            {
                _btnSlider.SetPosition(0, size);
            }

            if (_btnSlider.Top + _btnSlider.Height + size > Height)
            {
                _btnSlider.SetPosition(0, Height - size - _btnSlider.Height);
            }
        }

        if (_orientation == Orientation.Horizontal)
        {
            var size = _btnMinus.Width + Skin.Layers[_strSlider].OffsetX;
            var w = Width - 2 * size - _btnSlider.Width;
            var px = (Range - PageSize) / (float)w;
            Value = (int)Math.Ceiling((_btnSlider.Left - size) * px);
        }
        else
        {
            var size = _btnMinus.Height + Skin.Layers[_strSlider].OffsetY;
            var h = Height - 2 * size - _btnSlider.Height;
            var px = (Range - PageSize) / (float)h;
            Value = (int)Math.Ceiling((_btnSlider.Top - size) * px);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _btnSlider.Passive = false;
        base.OnMouseUp(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        _btnSlider.Passive = true;

        if (e.Button == MouseButton.Left)
        {
            if (_orientation == Orientation.Horizontal)
            {
                var pos = e.Position.X;

                if (pos < _btnSlider.Left)
                {
                    ScrollUp(true);
                }
                else if (pos >= _btnSlider.Left + _btnSlider.Width)
                {
                    ScrollDown(true);
                }
            }
            else
            {
                var pos = e.Position.Y;

                if (pos < _btnSlider.Top)
                {
                    ScrollUp(true);
                }
                else if (pos >= _btnSlider.Top + _btnSlider.Height)
                {
                    ScrollDown(true);
                }
            }
        }
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