using Microsoft.Xna.Framework;
using TomShane.Neoforce.Controls.Graphics;
using TomShane.Neoforce.Controls.Input;
using TomShane.Neoforce.Controls.Skins;

namespace TomShane.Neoforce.Controls;

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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
        }
        base.Dispose(disposing);
    }

    public override void Initialize(Manager manager)
    {
        base.Initialize(manager);

        SetDefaultSize(72, 24);
    }

    protected internal override void InitializeSkin()
    {
        base.InitializeSkin();
        Skin = new SkinControl(Manager.Skin.Controls[SkButton]);
    }

    protected override void DrawControl(IRenderer renderer, Rectangle rect, GameTime gameTime)
    {
        var layer = Skin.Layers[LrButton];

        if (layer == null)
        {
            return;
        }

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

        var font = layer.Text?.Font?.Resource;

        if (font == null)
        {
            return;
        }

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