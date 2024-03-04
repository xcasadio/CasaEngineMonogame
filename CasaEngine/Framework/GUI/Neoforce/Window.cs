using CasaEngine.Framework.GUI.Neoforce.Graphics;
using CasaEngine.Framework.GUI.Neoforce.Input;
using CasaEngine.Framework.GUI.Neoforce.Skins;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.GUI.Neoforce;

///  <include file='Documents/Window.xml' path='Window/Class[@name="Window"]/*' />          
public class Window : ModalContainer
{
    private const string SkWindow = "Window";
    private const string LrWindow = "Control";
    private const string LrCaption = "Caption";
    private const string LrFrameTop = "FrameTop";
    private const string LrFrameLeft = "FrameLeft";
    private const string LrFrameRight = "FrameRight";
    private const string LrFrameBottom = "FrameBottom";
    private const string LrIcon = "Icon";

    private const string SkButton = "Window.CloseButton";
    private const string LrButton = "Control";

    private const string SkShadow = "Window.Shadow";
    private const string LrShadow = "Control";

    private Button _btnClose;
    private bool _closeButtonVisible = true;
    private bool _iconVisible = true;
    private Texture2D _icon;
    private bool _captionVisible = true;
    private bool _borderVisible = true;
    private byte _oldAlpha = 255;
    private byte _dragAlpha = 200;

    public virtual Texture2D Icon
    {
        get => _icon;
        set => _icon = value;
    }

    public virtual bool Shadow { get; set; } = true;

    public virtual bool CloseButtonVisible
    {
        get => _closeButtonVisible;
        set
        {
            _closeButtonVisible = value;
            if (_btnClose != null)
            {
                _btnClose.Visible = value;
            }
        }
    }

    public virtual bool IconVisible
    {
        get => _iconVisible;
        set => _iconVisible = value;
    }

    public virtual bool CaptionVisible
    {
        get => _captionVisible;
        set
        {
            _captionVisible = value;
            AdjustMargins();
        }
    }

    public virtual bool BorderVisible
    {
        get => _borderVisible;
        set
        {
            _borderVisible = value;
            AdjustMargins();
        }
    }

    public virtual byte DragAlpha
    {
        get => _dragAlpha;
        set => _dragAlpha = value;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
        }
        base.Dispose(disposing);
    }

    public override void Initialize(Manager manager)
    {
        SetMinimumSize(100, 75);
        SetDefaultSize(640, 480);

        AutoScroll = true;
        Movable = true;
        Resizable = true;

        base.Initialize(manager);

        _oldAlpha = Alpha;

        _btnClose = new Button();
        _btnClose.Initialize(manager);
        _btnClose.Skin = new SkinControl(manager.Skin.Controls[SkButton]);
        _btnClose.Detached = true;
        _btnClose.CanFocus = false;
        _btnClose.Text = null;
        _btnClose.Click += btnClose_Click;
        _btnClose.SkinChanged += btnClose_SkinChanged;
        Add(_btnClose, false);

        var lrButtonSkin = _btnClose.Skin.Layers[LrButton];
        _btnClose.Width = lrButtonSkin.Width - _btnClose.Skin.OriginMargins.Horizontal;
        _btnClose.Height = lrButtonSkin.Height - _btnClose.Skin.OriginMargins.Vertical;
        _btnClose.Anchor = Anchors.Top | Anchors.Right;
        _btnClose.Left = OriginWidth - Skin.OriginMargins.Right - _btnClose.Width + lrButtonSkin.OffsetX;
        _btnClose.Top = Skin.OriginMargins.Top + lrButtonSkin.OffsetY;

        CheckLayer(Skin, LrWindow);
        CheckLayer(Skin, LrCaption);
        CheckLayer(Skin, LrFrameTop);
        CheckLayer(Skin, LrFrameLeft);
        CheckLayer(Skin, LrFrameRight);
        CheckLayer(Skin, LrFrameBottom);
        CheckLayer(Manager.Skin.Controls[SkButton], LrButton);
        CheckLayer(Manager.Skin.Controls[SkShadow], LrShadow);

        AdjustMargins();

        Center();
    }

    protected internal override void InitializeSkin()
    {
        base.InitializeSkin();
        Skin = new SkinControl(Manager.Skin.Controls[SkWindow]);
        AdjustMargins();
    }

    void btnClose_SkinChanged(object sender, EventArgs e)
    {
        _btnClose.Skin = new SkinControl(Manager.Skin.Controls[SkButton]);
    }

    internal override void Render(IRenderer renderer, GameTime gameTime)
    {
        if (Visible && Shadow)
        {
            var c = Manager.Skin.Controls[SkShadow];
            var l = c.Layers[LrShadow];

            var cl = Color.FromNonPremultiplied(l.States.Enabled.Color.R, l.States.Enabled.Color.G, l.States.Enabled.Color.B, Alpha);

            renderer.Begin(BlendingMode.Default);
            renderer.DrawLayer(l, new Rectangle(Left - c.OriginMargins.Left, Top - c.OriginMargins.Top, Width + c.OriginMargins.Horizontal, Height + c.OriginMargins.Vertical), cl, 0);
            renderer.End();
        }
        base.Render(renderer, gameTime);
    }

    private Rectangle GetIconRect()
    {
        var l1 = Skin.Layers[LrCaption];
        var l5 = Skin.Layers[LrIcon];

        var s = l1.Height - l1.ContentMargins.Vertical;
        return new Rectangle(DrawingRect.Left + l1.ContentMargins.Left + l5.OffsetX,
            DrawingRect.Top + l1.ContentMargins.Top + l5.OffsetY,
            s, s);
    }

    protected override void DrawControl(IRenderer renderer, Rectangle rect, GameTime gameTime)
    {
        var l1 = _captionVisible ? Skin.Layers[LrCaption] : Skin.Layers[LrFrameTop];
        var l2 = Skin.Layers[LrFrameLeft];
        var l3 = Skin.Layers[LrFrameRight];
        var l4 = Skin.Layers[LrFrameBottom];
        var l5 = Skin.Layers[LrIcon];
        LayerStates s1, s2, s3, s4;
        var f1 = l1.Text.Font.Resource;
        var c1 = l1.Text.Colors.Enabled;

        if ((Focused || (Manager.FocusedControl != null && Manager.FocusedControl.Root == Root)) && ControlState != ControlState.Disabled)
        {
            s1 = l1.States.Focused;
            s2 = l2.States.Focused;
            s3 = l3.States.Focused;
            s4 = l4.States.Focused;
            c1 = l1.Text.Colors.Focused;
        }
        else if (ControlState == ControlState.Disabled)
        {
            s1 = l1.States.Disabled;
            s2 = l2.States.Disabled;
            s3 = l3.States.Disabled;
            s4 = l4.States.Disabled;
            c1 = l1.Text.Colors.Disabled;
        }
        else
        {
            s1 = l1.States.Enabled;
            s2 = l2.States.Enabled;
            s3 = l3.States.Enabled;
            s4 = l4.States.Enabled;
            c1 = l1.Text.Colors.Enabled;
        }

        renderer.DrawLayer(Skin.Layers[LrWindow], rect, Skin.Layers[LrWindow].States.Enabled.Color, Skin.Layers[LrWindow].States.Enabled.Index);

        if (_borderVisible)
        {
            renderer.DrawLayer(l1, new Rectangle(rect.Left, rect.Top, rect.Width, l1.Height), s1.Color, s1.Index);
            renderer.DrawLayer(l2, new Rectangle(rect.Left, rect.Top + l1.Height, l2.Width, rect.Height - l1.Height - l4.Height), s2.Color, s2.Index);
            renderer.DrawLayer(l3, new Rectangle(rect.Right - l3.Width, rect.Top + l1.Height, l3.Width, rect.Height - l1.Height - l4.Height), s3.Color, s3.Index);
            renderer.DrawLayer(l4, new Rectangle(rect.Left, rect.Bottom - l4.Height, rect.Width, l4.Height), s4.Color, s4.Index);

            if (_iconVisible && (_icon != null || l5 != null) && _captionVisible)
            {
                var i = _icon ?? l5.Image.Resource;
                renderer.Draw(i, GetIconRect(), Color.White);
            }

            var icosize = 0;
            if (l5 != null && _iconVisible && _captionVisible)
            {
                icosize = l1.Height - l1.ContentMargins.Vertical + 4 + l5.OffsetX;
            }
            var closesize = 0;
            if (_btnClose.Visible)
            {
                closesize = _btnClose.Width - _btnClose.Skin.Layers[LrButton].OffsetX;
            }

            var r = new Rectangle(rect.Left + l1.ContentMargins.Left + icosize,
                rect.Top + l1.ContentMargins.Top,
                rect.Width - l1.ContentMargins.Horizontal - closesize - icosize,
                l1.Height - l1.ContentMargins.Top - l1.ContentMargins.Bottom);
            var ox = l1.Text.OffsetX;
            var oy = l1.Text.OffsetY;
            renderer.DrawString(f1, Text, r, c1, l1.Text.Alignment, ox, oy, true);
        }
    }

    void btnClose_Click(object sender, EventArgs e)
    {
        Close(ModalResult = ModalResult.Cancel);
    }

    public void Center()
    {
        Left = Manager.ScreenWidth / 2 - Width / 2;
        Top = (Manager.ScreenHeight - Height) / 2;
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        SetMovableArea();
        base.OnResize(e);
    }

    protected override void OnMoveBegin(EventArgs e)
    {
        base.OnMoveBegin(e);

        _oldAlpha = Alpha;
        Alpha = _dragAlpha;
    }

    protected override void OnMoveEnd(EventArgs e)
    {
        base.OnMoveEnd(e);
        Alpha = _oldAlpha;
    }

    protected override void OnDoubleClick(EventArgs e)
    {
        base.OnDoubleClick(e);

        var ex = e is MouseEventArgs args ? args : new MouseEventArgs();

        if (IconVisible && ex.Button == MouseButton.Left)
        {
            var r = GetIconRect();
            r.Offset(AbsoluteLeft, AbsoluteTop);
            if (r.Contains(ex.Position))
            {
                Close();
            }
        }
    }

    protected override void AdjustMargins()
    {

        if (_captionVisible && _borderVisible)
        {
            ClientMargins = new Margins(Skin.ClientMargins.Left, Skin.Layers[LrCaption].Height, Skin.ClientMargins.Right, Skin.ClientMargins.Bottom);
        }
        else if (!_captionVisible && _borderVisible)
        {
            ClientMargins = new Margins(Skin.ClientMargins.Left, Skin.ClientMargins.Top, Skin.ClientMargins.Right, Skin.ClientMargins.Bottom);
        }
        else if (!_borderVisible)
        {
            ClientMargins = new Margins(0, 0, 0, 0);
        }

        if (_btnClose != null)
        {
            _btnClose.Visible = _closeButtonVisible && _captionVisible && _borderVisible;
        }

        SetMovableArea();

        base.AdjustMargins();
    }

    private void SetMovableArea()
    {
        if (_captionVisible && _borderVisible)
        {
            MovableArea = new Rectangle(Skin.OriginMargins.Left, Skin.OriginMargins.Top, Width, Skin.Layers[LrCaption].Height - Skin.OriginMargins.Top);
        }
        else if (!_captionVisible)
        {
            MovableArea = new Rectangle(0, 0, Width, Height);
        }
    }

}