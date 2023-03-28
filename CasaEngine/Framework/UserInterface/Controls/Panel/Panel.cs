
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using CasaEngine.Framework.UserInterface.Controls.Auxiliary;

namespace CasaEngine.Framework.UserInterface.Controls.Panel;

public class Panel : Container
{


    private readonly Bevel _bevel;
    private BevelStyle _bevelStyle = BevelStyle.None;
    private BevelBorder _bevelBorder = BevelBorder.None;
    private int _bevelMargin;
    private Color _bevelColor = Color.Black;



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
    } // BevelStyle

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
    } // BevelBorder

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
    } // BevelMargin

    public virtual Color BevelColor
    {
        get => _bevelColor;
        set => _bevel.Color = _bevelColor = value;
    } // BevelColor



    public event EventHandler BevelBorderChanged;
    public event EventHandler BevelStyleChanged;
    public event EventHandler BevelMarginChanged;



    public Panel(UserInterfaceManager userInterfaceManager)
        : base(userInterfaceManager)
    {
        Passive = false;
        CanFocus = false;
        Width = 64;
        Height = 64;

        _bevel = new Bevel(UserInterfaceManager)
        {
            Style = _bevelStyle,
            Border = _bevelBorder,
            Left = 0,
            Top = 0,
            Width = Width,
            Height = Height,
            Color = _bevelColor,
            Visible = _bevelBorder != BevelBorder.None,
            Anchor = Anchors.Left | Anchors.Top | Anchors.Right | Anchors.Bottom
        };
        Add(_bevel, false);
        AdjustMargins();
    } // Panel



    protected internal override void InitSkin()
    {
        base.InitSkin();
        SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["Panel"]);
    } // InitSkin



    protected override void DisposeManagedResources()
    {
        // A disposed object could be still generating events, because it is alive for a time, in a disposed state, but alive nevertheless.
        BevelBorderChanged = null;
        BevelStyleChanged = null;
        BevelMarginChanged = null;
        base.DisposeManagedResources();
    } // DisposeManagedResources



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
        ClientMargins = new Margins(SkinInformation.ClientMargins.Left + l, SkinInformation.ClientMargins.Top + t, SkinInformation.ClientMargins.Right + r, SkinInformation.ClientMargins.Bottom + b);

        base.AdjustMargins();
    } // AdjustMargins



    protected override void DrawControl(Rectangle rect)
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

        base.DrawControl(new Rectangle(x, y, w, h));
    } // DrawControl



    protected virtual void OnBevelBorderChanged(EventArgs e)
    {
        if (BevelBorderChanged != null)
        {
            BevelBorderChanged.Invoke(this, e);
        }
    } // OnBevelBorderChanged

    protected virtual void OnBevelStyleChanged(EventArgs e)
    {
        if (BevelStyleChanged != null)
        {
            BevelStyleChanged.Invoke(this, e);
        }
    } // OnBevelStyleChanged

    protected virtual void OnBevelMarginChanged(EventArgs e)
    {
        if (BevelMarginChanged != null)
        {
            BevelMarginChanged.Invoke(this, e);
        }
    } // OnBevelMarginChanged


} // Panel
  // XNAFinalEngine.UserInterface