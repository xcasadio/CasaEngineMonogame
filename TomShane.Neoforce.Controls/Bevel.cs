using Microsoft.Xna.Framework;
using TomShane.Neoforce.Controls.Graphics;

namespace TomShane.Neoforce.Controls;

public enum BevelStyle
{
    None,
    Flat,
    Etched,
    Bumped,
    Lowered,
    Raised
}

public enum BevelBorder
{
    None,
    Left,
    Top,
    Right,
    Bottom,
    All
}

public class Bevel : Control
{
    private BevelBorder _border = BevelBorder.All;
    private BevelStyle _style = BevelStyle.Etched;

    public BevelBorder Border
    {
        get => _border;
        set
        {
            if (_border != value)
            {
                _border = value;
                if (!Suspended)
                {
                    OnBorderChanged(new EventArgs());
                }
            }
        }
    }

    public BevelStyle Style
    {
        get => _style;
        set
        {
            if (_style != value)
            {
                _style = value;
                if (!Suspended)
                {
                    OnStyleChanged(new EventArgs());
                }
            }
        }
    }

    public event EventHandler BorderChanged;
    public event EventHandler StyleChanged;

    public Bevel()
    {
        CanFocus = false;
        Passive = true;
        Width = 64;
        Height = 64;
    }

    protected override void DrawControl(IRenderer renderer, Rectangle rect, GameTime gameTime)
    {
        if (Border != BevelBorder.None && Style != BevelStyle.None)
        {
            if (Border != BevelBorder.All)
            {
                DrawPart(renderer, rect, Border, Style, false);
            }
            else
            {
                DrawPart(renderer, rect, BevelBorder.Left, Style, true);
                DrawPart(renderer, rect, BevelBorder.Top, Style, true);
                DrawPart(renderer, rect, BevelBorder.Right, Style, true);
                DrawPart(renderer, rect, BevelBorder.Bottom, Style, true);
            }
        }
    }

    private void DrawPart(IRenderer renderer, Rectangle rect, BevelBorder pos, BevelStyle style, bool all)
    {
        var layer = Skin.Layers["Control"];
        var c1 = Utilities.ParseColor(layer.Attributes["LightColor"].Value);
        var c2 = Utilities.ParseColor(layer.Attributes["DarkColor"].Value);
        var c3 = Utilities.ParseColor(layer.Attributes["FlatColor"].Value);

        if (Color != UndefinedColor)
        {
            c3 = Color;
        }

        var img = Skin.Layers["Control"].Image.Resource;

        var x1 = 0; var y1 = 0; var w1 = 0; var h1 = 0;
        var x2 = 0; var y2 = 0; var w2 = 0; var h2 = 0;

        if (style == BevelStyle.Bumped || style == BevelStyle.Etched)
        {
            if (all && (pos == BevelBorder.Top || pos == BevelBorder.Bottom))
            {
                rect = new Rectangle(rect.Left + 1, rect.Top, rect.Width - 2, rect.Height);
            }
            else if (all && pos == BevelBorder.Left)
            {
                rect = new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height - 1);
            }
            switch (pos)
            {
                case BevelBorder.Left:
                    {
                        x1 = rect.Left; y1 = rect.Top; w1 = 1; h1 = rect.Height;
                        x2 = x1 + 1; y2 = y1; w2 = w1; h2 = h1;
                        break;
                    }
                case BevelBorder.Top:
                    {
                        x1 = rect.Left; y1 = rect.Top; w1 = rect.Width; h1 = 1;
                        x2 = x1; y2 = y1 + 1; w2 = w1; h2 = h1;
                        break;
                    }
                case BevelBorder.Right:
                    {
                        x1 = rect.Left + rect.Width - 2; y1 = rect.Top; w1 = 1; h1 = rect.Height;
                        x2 = x1 + 1; y2 = y1; w2 = w1; h2 = h1;
                        break;
                    }
                case BevelBorder.Bottom:
                    {
                        x1 = rect.Left; y1 = rect.Top + rect.Height - 2; w1 = rect.Width; h1 = 1;
                        x2 = x1; y2 = y1 + 1; w2 = w1; h2 = h1;
                        break;
                    }
            }
        }
        else
        {
            switch (pos)
            {
                case BevelBorder.Left:
                    {
                        x1 = rect.Left; y1 = rect.Top; w1 = 1; h1 = rect.Height;
                        break;
                    }
                case BevelBorder.Top:
                    {
                        x1 = rect.Left; y1 = rect.Top; w1 = rect.Width; h1 = 1;
                        break;
                    }
                case BevelBorder.Right:
                    {
                        x1 = rect.Left + rect.Width - 1; y1 = rect.Top; w1 = 1; h1 = rect.Height;
                        break;
                    }
                case BevelBorder.Bottom:
                    {
                        x1 = rect.Left; y1 = rect.Top + rect.Height - 1; w1 = rect.Width; h1 = 1;
                        break;
                    }
            }
        }

        switch (Style)
        {
            case BevelStyle.Bumped:
                {
                    renderer.Draw(img, new Rectangle(x1, y1, w1, h1), c1);
                    renderer.Draw(img, new Rectangle(x2, y2, w2, h2), c2);
                    break;
                }
            case BevelStyle.Etched:
                {
                    renderer.Draw(img, new Rectangle(x1, y1, w1, h1), c2);
                    renderer.Draw(img, new Rectangle(x2, y2, w2, h2), c1);
                    break;
                }
            case BevelStyle.Raised:
                {
                    var c = c1;
                    if (pos == BevelBorder.Left || pos == BevelBorder.Top)
                    {
                        c = c1;
                    }
                    else
                    {
                        c = c2;
                    }

                    renderer.Draw(img, new Rectangle(x1, y1, w1, h1), c);
                    break;
                }
            case BevelStyle.Lowered:
                {
                    var c = c1;
                    if (pos == BevelBorder.Left || pos == BevelBorder.Top)
                    {
                        c = c2;
                    }
                    else
                    {
                        c = c1;
                    }

                    renderer.Draw(img, new Rectangle(x1, y1, w1, h1), c);
                    break;
                }
            default:
                {
                    renderer.Draw(img, new Rectangle(x1, y1, w1, h1), c3);
                    break;
                }
        }
    }

    protected virtual void OnBorderChanged(EventArgs e)
    {
        BorderChanged?.Invoke(this, e);
    }

    protected virtual void OnStyleChanged(EventArgs e)
    {
        StyleChanged?.Invoke(this, e);
    }
}