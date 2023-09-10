using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TomShane.Neoforce.Controls;

///  <include file='Documents/Button.xml' path='Button/Class[@name="SizeMode"]/*' />
public enum SizeMode
{
    Normal,
    Auto,
    Centered,
    Stretched,
    /// <summary>
    /// Only Supported by ImageBox
    /// </summary>
    Tiled
}

///  <include file='Documents/Button.xml' path='Button/Class[@name="SizeMode"]/*' />
public enum ButtonMode
{
    Normal,
    PushButton
}

///  <include file='Documents/Button.xml' path='Button/Class[@name="Glyph"]/*' />          
public class Glyph
{

    public readonly Texture2D Image;
    public SizeMode SizeMode = SizeMode.Stretched;
    public Color Color = Color.White;
    public Point Offset = Point.Zero;
    public Rectangle SourceRect = Rectangle.Empty;

    public Glyph(Texture2D image)
    {
        Image = image;
    }

    public Glyph(Texture2D image, Rectangle sourceRect) : this(image)
    {
        SourceRect = sourceRect;
    }
}

///  <include file='Documents/Button.xml' path='Button/Class[@name="Button"]/*' />          
public class Button : ButtonBase
{

    private const string SkButton = "Button";
    private const string LrButton = "Control";

    private Glyph _glyph;
    private bool _pushed;

    public Glyph Glyph
    {
        get => _glyph;
        set
        {
            _glyph = value;
            if (!Suspended)
            {
                OnGlyphChanged(new EventArgs());
            }
        }
    }

    public ModalResult ModalResult { get; set; } = ModalResult.None;

    public ButtonMode Mode { get; set; } = ButtonMode.Normal;

    public bool Pushed
    {
        get => _pushed;
        set
        {
            _pushed = value;
            Invalidate();
        }
    }

    public event EventHandler GlyphChanged;

    public Button(Manager manager) : base(manager)
    {
        SetDefaultSize(72, 24);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
        }
        base.Dispose(disposing);
    }

    public override void Init()
    {
        base.Init();
    }

    protected internal override void InitSkin()
    {
        base.InitSkin();
        Skin = new SkinControl(Manager.Skin.Controls[SkButton]);
    }

    protected override void DrawControl(Renderer renderer, Rectangle rect, GameTime gameTime)
    {

        if (Mode == ButtonMode.PushButton && _pushed)
        {
            var l = Skin.Layers[LrButton];
            renderer.DrawLayer(l, rect, l.States.Pressed.Color, l.States.Pressed.Index);
            if (l.States.Pressed.Overlay)
            {
                renderer.DrawLayer(l, rect, l.Overlays.Pressed.Color, l.Overlays.Pressed.Index);
            }
        }
        else
        {
            base.DrawControl(renderer, rect, gameTime);
        }

        var layer = Skin.Layers[LrButton];
        var font = layer.Text != null && layer.Text.Font != null ? layer.Text.Font.Resource : null;
        var col = Color.White;
        var ox = 0; var oy = 0;

        if (ControlState == ControlState.Pressed)
        {
            if (layer.Text != null)
            {
                col = layer.Text.Colors.Pressed;
            }

            ox = 1; oy = 1;
        }
        if (_glyph != null)
        {
            var cont = layer.ContentMargins;
            var r = new Rectangle(rect.Left + cont.Left,
                rect.Top + cont.Top,
                rect.Width - cont.Horizontal,
                rect.Height - cont.Vertical);
            renderer.DrawGlyph(_glyph, r);
        }
        else
        {
            renderer.DrawString(this, layer, Text, rect, true, ox, oy);
        }
    }

    private void OnGlyphChanged(EventArgs e)
    {
        GlyphChanged?.Invoke(this, e);
    }

    protected override void OnClick(EventArgs e)
    {
        var ex = e is MouseEventArgs args ? args : new MouseEventArgs();

        if (ex.Button == MouseButton.Left || ex.Button == MouseButton.None)
        {
            _pushed = !_pushed;
        }

        base.OnClick(e);

        if ((ex.Button == MouseButton.Left || ex.Button == MouseButton.None) && Root != null)
        {
            if (Root is Window window)
            {
                var wnd = (Window)Root;
                if (ModalResult != ModalResult.None)
                {
                    wnd.Close(ModalResult);
                }
            }
        }
    }

}