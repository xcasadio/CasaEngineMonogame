using CasaEngine.Framework.GUI.Neoforce.Skins;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.GUI.Neoforce.Graphics;

public class RendererSpriteBatch : Component, IRenderer
{
    private SpriteBatch _spriteBatch;
    private readonly DeviceStates _deviceStates = new();
    private BlendingMode _blendingMode = BlendingMode.Default;
    private Matrix? _customMatrix;

    public Matrix? CustomMatrix
    {
        get => _customMatrix;
        set => _customMatrix = value;
    }

    public SpriteBatch SpriteBatch => _spriteBatch;

    public RendererSpriteBatch()
    {
    }

    public override void Initialize(Manager manager)
    {
        base.Initialize(manager);

        _spriteBatch = new SpriteBatch(Manager.GraphicsDevice);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_spriteBatch != null)
            {
                _spriteBatch.Dispose();
                _spriteBatch = null;
            }
        }
        base.Dispose(disposing);
    }

    public void Begin(BlendingMode mode)
    {
        _blendingMode = mode;
        var state = BlendState.Opaque;

        if (mode == BlendingMode.Additive)
        {
            state = BlendState.Additive;
        }
        else if (mode != BlendingMode.None)
        {
            state = _deviceStates.BlendState;
        }

        _spriteBatch.Begin(SpriteSortMode.Immediate, state, _deviceStates.SamplerState, _deviceStates.DepthStencilState, _deviceStates.RasterizerState, null, _customMatrix);
    }

    public void End()
    {
        _spriteBatch.End();
    }

    public void Draw(Texture2D texture, Rectangle destination, Color color)
    {
        if (destination.Width > 0 && destination.Height > 0)
        {
            _spriteBatch.Draw(texture, destination, null, color, 0.0f, Vector2.Zero, SpriteEffects.None, Manager.GlobalDepth);
        }
    }

    public void DrawTileTexture(Texture2D texture, Rectangle destination, Color color)
    {
        if (destination.Width > 0 && destination.Height > 0)
        {
            End();

            if (_customMatrix.HasValue)
            {
                _spriteBatch.Begin(SpriteSortMode.Texture, BlendState.AlphaBlend, SamplerState.LinearWrap, DepthStencilState.Default, RasterizerState.CullNone, null, _customMatrix.Value);
            }
            else
            {
                _spriteBatch.Begin(SpriteSortMode.Texture, BlendState.AlphaBlend, SamplerState.LinearWrap, DepthStencilState.Default, RasterizerState.CullNone);
            }
            _spriteBatch.Draw(texture, new Vector2(destination.X, destination.Y), destination, color, 0, Vector2.Zero, 1, SpriteEffects.None, 0);

            End();
            Begin(_blendingMode);
        }
    }

    public void Draw(Texture2D texture, Rectangle destination, Rectangle source, Color color)
    {
        if (source.Width > 0 && source.Height > 0 && destination.Width > 0 && destination.Height > 0)
        {
            _spriteBatch.Draw(texture, destination, source, color, 0.0f, Vector2.Zero, SpriteEffects.None, Manager.GlobalDepth);
        }
    }

    public void Draw(Texture2D texture, int left, int top, Color color)
    {
        _spriteBatch.Draw(texture, new Vector2(left, top), null, color, 0.0f, Vector2.Zero, 1.0f, SpriteEffects.None, Manager.GlobalDepth);
    }

    public void Draw(Texture2D texture, int left, int top, Rectangle source, Color color)
    {
        if (source.Width > 0 && source.Height > 0)
        {
            _spriteBatch.Draw(texture, new Vector2(left, top), source, color, 0.0f, Vector2.Zero, 1.0f, SpriteEffects.None, Manager.GlobalDepth);
        }
    }

    public void DrawString(SpriteFontBase font, string text, int left, int top, Color color)
    {
        font.DrawText(_spriteBatch, text, new Vector2(left, top), color, layerDepth: Manager.GlobalDepth);
    }

    public void DrawString(SpriteFontBase font, string text, Rectangle rect, Color color, Alignment alignment)
    {
        DrawString(font, text, rect, color, alignment, 0, 0, true);
    }

    public void DrawString(SpriteFontBase font, string text, Rectangle rect, Color color, Alignment alignment, bool ellipsis)
    {
        DrawString(font, text, rect, color, alignment, 0, 0, ellipsis);
    }

    public void DrawString(Control control, SkinLayer layer, string text, Rectangle rect)
    {
        DrawString(control, layer, text, rect, true, 0, 0, true);
    }

    public void DrawString(Control control, SkinLayer layer, string text, Rectangle rect, ControlState state)
    {
        DrawString(control, layer, text, rect, state, true, 0, 0, true);
    }

    public void DrawString(Control control, SkinLayer layer, string text, Rectangle rect, bool margins)
    {
        DrawString(control, layer, text, rect, margins, 0, 0, true);
    }

    public void DrawString(Control control, SkinLayer layer, string text, Rectangle rect, ControlState state, bool margins)
    {
        DrawString(control, layer, text, rect, state, margins, 0, 0, true);
    }

    public void DrawString(Control control, SkinLayer layer, string text, Rectangle rect, bool margins, int ox, int oy)
    {
        DrawString(control, layer, text, rect, margins, ox, oy, true);
    }

    public void DrawString(Control control, SkinLayer layer, string text, Rectangle rect, ControlState state, bool margins, int ox, int oy)
    {
        DrawString(control, layer, text, rect, state, margins, ox, oy, true);
    }

    public void DrawString(Control control, SkinLayer layer, string text, Rectangle rect, bool margins, int ox, int oy, bool ellipsis)
    {
        DrawString(control, layer, text, rect, control.ControlState, margins, ox, oy, ellipsis);
    }

    public void DrawString(Control control, SkinLayer layer, string text, Rectangle rect, ControlState state, bool margins, int ox, int oy, bool ellipsis)
    {
        if (layer.Text != null)
        {
            if (margins)
            {
                var m = layer.ContentMargins;
                rect = new Rectangle(rect.Left + m.Left, rect.Top + m.Top, rect.Width - m.Horizontal, rect.Height - m.Vertical);
            }

            Color col;
            if (state == ControlState.Hovered && layer.States.Hovered.Index != -1)
            {
                col = layer.Text.Colors.Hovered;
            }
            else if (state == ControlState.Pressed)
            {
                col = layer.Text.Colors.Pressed;
            }
            else if (state == ControlState.Focused || (control.Focused && state == ControlState.Hovered && layer.States.Hovered.Index == -1))
            {
                col = layer.Text.Colors.Focused;
            }
            else if (state == ControlState.Disabled)
            {
                col = layer.Text.Colors.Disabled;
            }
            else
            {
                col = layer.Text.Colors.Enabled;
            }

            if (!string.IsNullOrEmpty(text))
            {
                var font = layer.Text;
                if (control.TextColor != Control.UndefinedColor && control.ControlState != ControlState.Disabled)
                {
                    col = control.TextColor;
                }

                DrawString(font.Font.Resource, text, rect, col, font.Alignment, font.OffsetX + ox, font.OffsetY + oy, ellipsis);
            }
        }
    }

    public void DrawString(SpriteFontBase font, string text, Rectangle rect, Color color, Alignment alignment, int offsetX, int offsetY, bool ellipsis)
    {
        if (ellipsis)
        {
            const string elli = "...";
            var size = (int)Math.Ceiling(font.MeasureString(text).X);
            if (size > rect.Width)
            {
                var es = (int)Math.Ceiling(font.MeasureString(elli).X);
                for (var i = text.Length - 1; i > 0; i--)
                {
                    var c = 1;
                    if (char.IsWhiteSpace(text[i - 1]))
                    {
                        c = 2;
                        i--;
                    }
                    text = text.Remove(i, c);
                    size = (int)Math.Ceiling(font.MeasureString(text).X);
                    if (size + es <= rect.Width)
                    {
                        break;
                    }
                }
                text += elli;
            }
        }

        if (rect.Width > 0 && rect.Height > 0)
        {
            var pos = new Vector2(rect.Left, rect.Top);
            var size = font.MeasureString(text);

            var x = 0; var y = 0;

            switch (alignment)
            {
                case Alignment.TopLeft:
                    break;
                case Alignment.TopCenter:
                    x = GetTextCenter(rect.Width, size.X);
                    break;
                case Alignment.TopRight:
                    x = rect.Width - (int)size.X;
                    break;
                case Alignment.MiddleLeft:
                    y = GetTextCenter(rect.Height, size.Y);
                    break;
                case Alignment.MiddleRight:
                    x = rect.Width - (int)size.X;
                    y = GetTextCenter(rect.Height, size.Y);
                    break;
                case Alignment.BottomLeft:
                    y = rect.Height - (int)size.Y;
                    break;
                case Alignment.BottomCenter:
                    x = GetTextCenter(rect.Width, size.X);
                    y = rect.Height - (int)size.Y;
                    break;
                case Alignment.BottomRight:
                    x = rect.Width - (int)size.X;
                    y = rect.Height - (int)size.Y;
                    break;

                default:
                    x = GetTextCenter(rect.Width, size.X);
                    y = GetTextCenter(rect.Height, size.Y);
                    break;
            }

            pos.X = (int)(pos.X + x);
            pos.Y = (int)(pos.Y + y);

            DrawString(font, text, (int)pos.X + offsetX, (int)pos.Y + offsetY, color);
        }
    }

    private static int GetTextCenter(float size1, float size2)
    {
        return (int)Math.Ceiling(size1 / 2 - size2 / 2);
    }

    public void DrawLayer(SkinLayer layer, Rectangle rect, Color color, int index)
    {
        var imageSize = new Size(layer.Image.Resource.Width, layer.Image.Resource.Height);
        var partSize = new Size(layer.Width, layer.Height);

        Draw(layer.Image.Resource, GetDestinationArea(rect, layer.SizingMargins, Alignment.TopLeft), GetSourceArea(imageSize, partSize, layer.SizingMargins, Alignment.TopLeft, index), color);
        Draw(layer.Image.Resource, GetDestinationArea(rect, layer.SizingMargins, Alignment.TopCenter), GetSourceArea(imageSize, partSize, layer.SizingMargins, Alignment.TopCenter, index), color);
        Draw(layer.Image.Resource, GetDestinationArea(rect, layer.SizingMargins, Alignment.TopRight), GetSourceArea(imageSize, partSize, layer.SizingMargins, Alignment.TopRight, index), color);
        Draw(layer.Image.Resource, GetDestinationArea(rect, layer.SizingMargins, Alignment.MiddleLeft), GetSourceArea(imageSize, partSize, layer.SizingMargins, Alignment.MiddleLeft, index), color);
        Draw(layer.Image.Resource, GetDestinationArea(rect, layer.SizingMargins, Alignment.MiddleCenter), GetSourceArea(imageSize, partSize, layer.SizingMargins, Alignment.MiddleCenter, index), color);
        Draw(layer.Image.Resource, GetDestinationArea(rect, layer.SizingMargins, Alignment.MiddleRight), GetSourceArea(imageSize, partSize, layer.SizingMargins, Alignment.MiddleRight, index), color);
        Draw(layer.Image.Resource, GetDestinationArea(rect, layer.SizingMargins, Alignment.BottomLeft), GetSourceArea(imageSize, partSize, layer.SizingMargins, Alignment.BottomLeft, index), color);
        Draw(layer.Image.Resource, GetDestinationArea(rect, layer.SizingMargins, Alignment.BottomCenter), GetSourceArea(imageSize, partSize, layer.SizingMargins, Alignment.BottomCenter, index), color);
        Draw(layer.Image.Resource, GetDestinationArea(rect, layer.SizingMargins, Alignment.BottomRight), GetSourceArea(imageSize, partSize, layer.SizingMargins, Alignment.BottomRight, index), color);
    }

    private static Rectangle GetSourceArea(Size imageSize, Size partSize, Margins margins, Alignment alignment, int index)
    {
        var rect = new Rectangle();
        var xc = (int)((float)imageSize.Width / partSize.Width);
        var yc = (int)((float)imageSize.Height / partSize.Height);

        var xm = index % xc;
        var ym = index / xc;

        var adj = 1;
        margins.Left += margins.Left > 0 ? adj : 0;
        margins.Top += margins.Top > 0 ? adj : 0;
        margins.Right += margins.Right > 0 ? adj : 0;
        margins.Bottom += margins.Bottom > 0 ? adj : 0;

        margins = new Margins(margins.Left, margins.Top, margins.Right, margins.Bottom);
        switch (alignment)
        {
            case Alignment.TopLeft:
                {
                    rect = new Rectangle(0 + xm * partSize.Width,
                        0 + ym * partSize.Height,
                        margins.Left,
                        margins.Top);
                    break;
                }
            case Alignment.TopCenter:
                {
                    rect = new Rectangle(0 + xm * partSize.Width + margins.Left,
                        0 + ym * partSize.Height,
                        partSize.Width - margins.Left - margins.Right,
                        margins.Top);
                    break;
                }
            case Alignment.TopRight:
                {
                    rect = new Rectangle(partSize.Width + xm * partSize.Width - margins.Right,
                        0 + ym * partSize.Height,
                        margins.Right,
                        margins.Top);
                    break;
                }
            case Alignment.MiddleLeft:
                {
                    rect = new Rectangle(0 + xm * partSize.Width,
                        0 + ym * partSize.Height + margins.Top,
                        margins.Left,
                        partSize.Height - margins.Top - margins.Bottom);
                    break;
                }
            case Alignment.MiddleCenter:
                {
                    rect = new Rectangle(0 + xm * partSize.Width + margins.Left,
                        0 + ym * partSize.Height + margins.Top,
                        partSize.Width - margins.Left - margins.Right,
                        partSize.Height - margins.Top - margins.Bottom);
                    break;
                }
            case Alignment.MiddleRight:
                {
                    rect = new Rectangle(partSize.Width + xm * partSize.Width - margins.Right,
                        0 + ym * partSize.Height + margins.Top,
                        margins.Right,
                        partSize.Height - margins.Top - margins.Bottom);
                    break;
                }
            case Alignment.BottomLeft:
                {
                    rect = new Rectangle(0 + xm * partSize.Width,
                        partSize.Height + ym * partSize.Height - margins.Bottom,
                        margins.Left,
                        margins.Bottom);
                    break;
                }
            case Alignment.BottomCenter:
                {
                    rect = new Rectangle(0 + xm * partSize.Width + margins.Left,
                        partSize.Height + ym * partSize.Height - margins.Bottom,
                        partSize.Width - margins.Left - margins.Right,
                        margins.Bottom);
                    break;
                }
            case Alignment.BottomRight:
                {
                    rect = new Rectangle(partSize.Width + xm * partSize.Width - margins.Right,
                        partSize.Height + ym * partSize.Height - margins.Bottom,
                        margins.Right,
                        margins.Bottom);
                    break;
                }
        }

        return rect;
    }

    public static Rectangle GetDestinationArea(Rectangle area, Margins margins, Alignment alignment)
    {
        var rect = new Rectangle();

        var adj = 1;
        margins.Left += margins.Left > 0 ? adj : 0;
        margins.Top += margins.Top > 0 ? adj : 0;
        margins.Right += margins.Right > 0 ? adj : 0;
        margins.Bottom += margins.Bottom > 0 ? adj : 0;

        margins = new Margins(margins.Left, margins.Top, margins.Right, margins.Bottom);

        switch (alignment)
        {
            case Alignment.TopLeft:
                {
                    rect = new Rectangle(area.Left + 0,
                        area.Top + 0,
                        margins.Left,
                        margins.Top);
                    break;

                }
            case Alignment.TopCenter:
                {
                    rect = new Rectangle(area.Left + margins.Left,
                        area.Top + 0,
                        area.Width - margins.Left - margins.Right,
                        margins.Top);
                    break;

                }
            case Alignment.TopRight:
                {
                    rect = new Rectangle(area.Left + area.Width - margins.Right,
                        area.Top + 0,
                        margins.Right,
                        margins.Top);
                    break;

                }
            case Alignment.MiddleLeft:
                {
                    rect = new Rectangle(area.Left + 0,
                        area.Top + margins.Top,
                        margins.Left,
                        area.Height - margins.Top - margins.Bottom);
                    break;
                }
            case Alignment.MiddleCenter:
                {
                    rect = new Rectangle(area.Left + margins.Left,
                        area.Top + margins.Top,
                        area.Width - margins.Left - margins.Right,
                        area.Height - margins.Top - margins.Bottom);
                    break;
                }
            case Alignment.MiddleRight:
                {
                    rect = new Rectangle(area.Left + area.Width - margins.Right,
                        area.Top + margins.Top,
                        margins.Right,
                        area.Height - margins.Top - margins.Bottom);
                    break;
                }
            case Alignment.BottomLeft:
                {
                    rect = new Rectangle(area.Left + 0,
                        area.Top + area.Height - margins.Bottom,
                        margins.Left,
                        margins.Bottom);
                    break;
                }
            case Alignment.BottomCenter:
                {
                    rect = new Rectangle(area.Left + margins.Left,
                        area.Top + area.Height - margins.Bottom,
                        area.Width - margins.Left - margins.Right,
                        margins.Bottom);
                    break;
                }
            case Alignment.BottomRight:
                {
                    rect = new Rectangle(area.Left + area.Width - margins.Right,
                        area.Top + area.Height - margins.Bottom,
                        margins.Right,
                        margins.Bottom);
                    break;
                }
        }

        return rect;
    }

    public void DrawGlyph(Glyph glyph, Rectangle rect)
    {
        var imageSize = new Size(glyph.Image.Width, glyph.Image.Height);

        if (!glyph.SourceRect.IsEmpty)
        {
            imageSize = new Size(glyph.SourceRect.Width, glyph.SourceRect.Height);
        }

        if (glyph.SizeMode == SizeMode.Centered)
        {
            rect = new Rectangle(rect.X + (rect.Width - imageSize.Width) / 2 + glyph.Offset.X,
                rect.Y + (rect.Height - imageSize.Height) / 2 + glyph.Offset.Y,
                imageSize.Width,
                imageSize.Height);
        }
        else if (glyph.SizeMode == SizeMode.Normal)
        {
            rect = new Rectangle(rect.X + glyph.Offset.X, rect.Y + glyph.Offset.Y, imageSize.Width, imageSize.Height);
        }
        else if (glyph.SizeMode == SizeMode.Auto)
        {
            rect = new Rectangle(rect.X + glyph.Offset.X, rect.Y + glyph.Offset.Y, imageSize.Width, imageSize.Height);
        }

        if (glyph.SourceRect.IsEmpty)
        {
            Draw(glyph.Image, rect, glyph.Color);
        }
        else
        {
            Draw(glyph.Image, rect, glyph.SourceRect, glyph.Color);
        }
    }

    public void DrawCursor(Texture2D texture, Rectangle destination, Vector2 hotSpot)
    {
        _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
        _spriteBatch.Draw(texture, destination, null, Color.White, 0f, hotSpot, SpriteEffects.None, 0f);
        _spriteBatch.End();
    }

    public void DrawLayer(Control control, SkinLayer layer, Rectangle rect)
    {
        DrawLayer(control, layer, rect, control.ControlState);
    }

    public void DrawLayer(Control control, SkinLayer layer, Rectangle rect, ControlState state)
    {
        var c = Color.White;
        var oc = Color.White;
        var i = 0;
        var oi = -1;
        var l = layer;

        if (state == ControlState.Hovered && layer.States.Hovered.Index != -1)
        {
            c = l.States.Hovered.Color;
            i = l.States.Hovered.Index;

            if (l.States.Hovered.Overlay)
            {
                oc = l.Overlays.Hovered.Color;
                oi = l.Overlays.Hovered.Index;
            }
        }
        else if (state == ControlState.Focused || (control.Focused && state == ControlState.Hovered && layer.States.Hovered.Index == -1))
        {
            c = l.States.Focused.Color;
            i = l.States.Focused.Index;

            if (l.States.Focused.Overlay)
            {
                oc = l.Overlays.Focused.Color;
                oi = l.Overlays.Focused.Index;
            }
        }
        else if (state == ControlState.Pressed)
        {
            c = l.States.Pressed.Color;
            i = l.States.Pressed.Index;

            if (l.States.Pressed.Overlay)
            {
                oc = l.Overlays.Pressed.Color;
                oi = l.Overlays.Pressed.Index;
            }
        }
        else if (state == ControlState.Disabled)
        {
            c = l.States.Disabled.Color;
            i = l.States.Disabled.Index;

            if (l.States.Disabled.Overlay)
            {
                oc = l.Overlays.Disabled.Color;
                oi = l.Overlays.Disabled.Index;
            }
        }
        else
        {
            c = l.States.Enabled.Color;
            i = l.States.Enabled.Index;

            if (l.States.Enabled.Overlay)
            {
                oc = l.Overlays.Enabled.Color;
                oi = l.Overlays.Enabled.Index;
            }
        }

        if (control.Color != Control.UndefinedColor)
        {
            c = control.Color * (control.Color.A / 255f);
        }

        DrawLayer(l, rect, c, i);

        if (oi != -1)
        {
            DrawLayer(l, rect, oc, oi);
        }
    }
}