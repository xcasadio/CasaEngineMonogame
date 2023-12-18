using Microsoft.Xna.Framework;
using TomShane.Neoforce.Controls.Graphics;
using TomShane.Neoforce.Controls.Skins;

namespace TomShane.Neoforce.Controls;

public class Panel : Container
{
    private Bevel _bevel;
    private BevelStyle _bevelStyle = BevelStyle.None;
    private BevelBorder _bevelBorder = BevelBorder.None;
    private int _bevelMargin;
    private Color _bevelColor = Color.Transparent;

    public BevelStyle BevelStyle
    {
        get => _bevelStyle;
        set
        {
            if (_bevelStyle != value)
            {
                _bevelStyle = _bevel.Style = value;
                AdjustMargins();
                if (!Suspended)
                {
                    OnBevelStyleChanged(new EventArgs());
                }
            }
        }
    }

    public BevelBorder BevelBorder
    {
        get => _bevelBorder;
        set
        {
            if (_bevelBorder != value)
            {
                _bevelBorder = _bevel.Border = value;
                _bevel.Visible = _bevelBorder != BevelBorder.None;
                AdjustMargins();
                if (!Suspended)
                {
                    OnBevelBorderChanged(new EventArgs());
                }
            }
        }
    }

    public int BevelMargin
    {
        get => _bevelMargin;
        set
        {
            if (_bevelMargin != value)
            {
                _bevelMargin = value;
                AdjustMargins();
                if (!Suspended)
                {
                    OnBevelMarginChanged(new EventArgs());
                }
            }
        }
    }

    public virtual Color BevelColor
    {
        get => _bevelColor;
        set => _bevel.Color = _bevelColor = value;
    }

    public event EventHandler BevelBorderChanged;
    public event EventHandler BevelStyleChanged;
    public event EventHandler BevelMarginChanged;

    public override void Initialize(Manager manager)
    {
        _bevel = new Bevel();
        _bevel.Initialize(manager);
        _bevel.Style = _bevelStyle;
        _bevel.Border = _bevelBorder;
        _bevel.Left = 0;
        _bevel.Top = 0;
        _bevel.Width = Width;
        _bevel.Height = Height;
        _bevel.Color = _bevelColor;
        _bevel.Visible = _bevelBorder != BevelBorder.None;
        _bevel.Anchor = Anchors.Left | Anchors.Top | Anchors.Right | Anchors.Bottom;

        base.Initialize(manager);

        Passive = false;
        CanFocus = false;
        Width = 64;
        Height = 64;

        Add(_bevel, false);
        AdjustMargins();
    }

    protected internal override void InitializeSkin()
    {
        base.InitializeSkin();
        Skin = new SkinControl(Manager.Skin.Controls["Panel"]);
    }

    protected override void AdjustMargins()
    {
        var l = 0;
        var t = 0;
        var r = 0;
        var b = 0;
        var s = _bevelMargin;

        if (_bevelBorder != BevelBorder.None)
        {
            if (_bevelStyle != BevelStyle.Flat)
            {
                s += 2;
            }
            else
            {
                s += 1;
            }

            if (_bevelBorder == BevelBorder.Left || _bevelBorder == BevelBorder.All)
            {
                l = s;
            }
            if (_bevelBorder == BevelBorder.Top || _bevelBorder == BevelBorder.All)
            {
                t = s;
            }
            if (_bevelBorder == BevelBorder.Right || _bevelBorder == BevelBorder.All)
            {
                r = s;
            }
            if (_bevelBorder == BevelBorder.Bottom || _bevelBorder == BevelBorder.All)
            {
                b = s;
            }
        }
        ClientMargins = new Margins(Skin.ClientMargins.Left + l, Skin.ClientMargins.Top + t, Skin.ClientMargins.Right + r, Skin.ClientMargins.Bottom + b);

        base.AdjustMargins();
    }

    protected override void DrawControl(IRenderer renderer, Rectangle rect, GameTime gameTime)
    {
        var x = rect.Left;
        var y = rect.Top;
        var w = rect.Width;
        var h = rect.Height;
        var s = _bevelMargin;

        if (_bevelBorder != BevelBorder.None)
        {
            if (_bevelStyle != BevelStyle.Flat)
            {
                s += 2;
            }
            else
            {
                s += 1;
            }

            if (_bevelBorder == BevelBorder.Left || _bevelBorder == BevelBorder.All)
            {
                x += s;
                w -= s;
            }
            if (_bevelBorder == BevelBorder.Top || _bevelBorder == BevelBorder.All)
            {
                y += s;
                h -= s;
            }
            if (_bevelBorder == BevelBorder.Right || _bevelBorder == BevelBorder.All)
            {
                w -= s;
            }
            if (_bevelBorder == BevelBorder.Bottom || _bevelBorder == BevelBorder.All)
            {
                h -= s;
            }
        }

        base.DrawControl(renderer, new Rectangle(x, y, w, h), gameTime);
    }

    protected virtual void OnBevelBorderChanged(EventArgs e)
    {
        BevelBorderChanged?.Invoke(this, e);
    }

    protected virtual void OnBevelStyleChanged(EventArgs e)
    {
        BevelStyleChanged?.Invoke(this, e);
    }

    protected virtual void OnBevelMarginChanged(EventArgs e)
    {
        BevelMarginChanged?.Invoke(this, e);
    }

}