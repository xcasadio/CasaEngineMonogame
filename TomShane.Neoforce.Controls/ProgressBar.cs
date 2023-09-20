using Microsoft.Xna.Framework;
using System;

namespace TomShane.Neoforce.Controls;

public enum ProgressBarMode
{
    Default,
    Infinite
}

public class ProgressBar : Control
{

    private int _range = 100;
    private int _value;
    private double _time;
    private int _sign = 1;
    private ProgressBarMode _mode = ProgressBarMode.Default;

    public int Value
    {
        get => _value;
        set
        {
            if (_mode == ProgressBarMode.Default)
            {
                if (_value != value)
                {
                    _value = value;
                    if (_value > _range)
                    {
                        _value = _range;
                    }

                    if (_value < 0)
                    {
                        _value = 0;
                    }

                    Invalidate();

                    if (!Suspended)
                    {
                        OnValueChanged(new EventArgs());
                    }
                }
            }
        }
    }

    public ProgressBarMode Mode
    {
        get => _mode;
        set
        {
            if (_mode != value)
            {
                _mode = value;
                if (_mode == ProgressBarMode.Infinite)
                {
                    _range = 100;
                    _value = 0;
                    _time = 0;
                    _sign = 1;
                }
                else
                {
                    _value = 0;
                    _range = 100;
                }
                Invalidate();

                if (!Suspended)
                {
                    OnModeChanged(new EventArgs());
                }
            }
        }
    }

    public int Range
    {
        get => _range;
        set
        {
            if (_range != value)
            {
                if (_mode == ProgressBarMode.Default)
                {
                    _range = value;
                    if (_range < 0)
                    {
                        _range = 0;
                    }

                    if (_range < _value)
                    {
                        _value = _range;
                    }

                    Invalidate();

                    if (!Suspended)
                    {
                        OnRangeChanged(new EventArgs());
                    }
                }
            }
        }
    }

    public event EventHandler ValueChanged;
    public event EventHandler RangeChanged;
    public event EventHandler ModeChanged;

    public ProgressBar(Manager manager)
        : base(manager)
    {
        Width = 128;
        Height = 16;
        MinimumHeight = 8;
        MinimumWidth = 32;
        Passive = true;
        CanFocus = false;
    }

    protected override void DrawControl(Renderer renderer, Rectangle rect, GameTime gameTime)
    {
        CheckLayer(Skin, "Control");
        CheckLayer(Skin, "Scale");

        base.DrawControl(renderer, rect, gameTime);

        if (Value > 0 || _mode == ProgressBarMode.Infinite)
        {
            var p = Skin.Layers["Control"];
            var l = Skin.Layers["Scale"];
            var r = new Rectangle(rect.Left + p.ContentMargins.Left,
                rect.Top + p.ContentMargins.Top,
                rect.Width - p.ContentMargins.Vertical,
                rect.Height - p.ContentMargins.Horizontal);

            var perc = (float)_value / _range * 100;
            var w = (int)(perc / 100 * r.Width);
            Rectangle rx;
            if (_mode == ProgressBarMode.Default)
            {
                if (w < l.SizingMargins.Vertical)
                {
                    w = l.SizingMargins.Vertical;
                }

                rx = new Rectangle(r.Left, r.Top, w, r.Height);
            }
            else
            {
                var s = r.Left + w;
                if (s > r.Left + p.ContentMargins.Left + r.Width - r.Width / 4)
                {
                    s = r.Left + p.ContentMargins.Left + r.Width - r.Width / 4;
                }

                rx = new Rectangle(s, r.Top, r.Width / 4, r.Height);
            }

            renderer.DrawLayer(this, l, rx);
        }
    }

    protected internal override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (_mode == ProgressBarMode.Infinite && Enabled && Visible)
        {
            _time += gameTime.ElapsedGameTime.TotalMilliseconds;
            if (_time >= 33f)
            {
                _value += _sign * (int)Math.Ceiling(_time / 20f);
                if (_value >= Range - Range / 4)
                {
                    _value = Range - Range / 4;
                    _sign = -1;
                }
                else if (_value <= 0)
                {
                    _value = 0;
                    _sign = 1;
                }
                _time = 0;
                Invalidate();
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

    protected virtual void OnModeChanged(EventArgs e)
    {
        ModeChanged?.Invoke(this, e);
    }

}