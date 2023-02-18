
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/


using CasaEngine.Framework.Graphics2D;
using CasaEngine.Framework.UserInterface.Controls.Auxiliary;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.UserInterface
{
    internal class Renderer
    {

        private SpriteBatch _spriteBatch;

        private RasterizerState _rasterizerState;



        internal void Initialize(GraphicsDevice graphicsDevice)
        {
            // Handle the dipose device sittuation.
            if (_spriteBatch != null && _spriteBatch.GraphicsDevice.IsDisposed)
            {
                _spriteBatch.Dispose();
            }
            _spriteBatch = new SpriteBatch(graphicsDevice);

            // The scissor test is very important, so a custom rasterizer state is created.
            _rasterizerState = new RasterizerState
            {
                CullMode = RasterizerState.CullNone.CullMode,
                DepthBias = RasterizerState.CullNone.DepthBias,
                FillMode = RasterizerState.CullNone.FillMode,
                MultiSampleAntiAlias = RasterizerState.CullNone.MultiSampleAntiAlias,
                ScissorTestEnable = true,
                SlopeScaleDepthBias = RasterizerState.CullNone.SlopeScaleDepthBias,
            };
        } // Initialize



        public void Begin()
        {
            _spriteBatch.Begin(SpriteSortMode.Immediate,
                              BlendState.NonPremultiplied, // AlphaBlend
                              SamplerState.LinearClamp,
                              DepthStencilState.None,
                              _rasterizerState);
        } // Begin



        public void End()
        {
            _spriteBatch.End();
        } // End



        public void Draw(Texture2D texture, Rectangle destination, Color color)
        {
            if (destination.Width > 0 && destination.Height > 0)
            {
                _spriteBatch.Draw(texture, destination, null, color, 0.0f, Vector2.Zero, SpriteEffects.None, 0);
            }
        } // Draw

        public void Draw(Texture2D texture, Rectangle destination, Rectangle source, Color color)
        {
            if (source.Width > 0 && source.Height > 0 && destination.Width > 0 && destination.Height > 0)
            {
                _spriteBatch.Draw(texture, destination, source, color, 0.0f, Vector2.Zero, SpriteEffects.None, 0);
            }
        } // Draw

        public void Draw(Texture2D texture, int left, int top, Color color)
        {
            _spriteBatch.Draw(texture, new Vector2(left, top), null, color, 0.0f, Vector2.Zero, 1.0f, SpriteEffects.None, 0);
        } // Draw

        public void Draw(Texture2D texture, int left, int top, Rectangle source, Color color)
        {
            if (source.Width > 0 && source.Height > 0)
            {
                _spriteBatch.Draw(texture, new Vector2(left, top), source, color, 0.0f, Vector2.Zero, 1.0f, SpriteEffects.None, 0);
            }
        } // Draw



        public void DrawString(Font font, string text, int left, int top, Color color)
        {
            //spriteBatch.DrawString(font, text, new Vector2(left, top), color, 0.0f, Vector2.Zero, 1.0f, SpriteEffects.None, 0);
            BmFontRenderer.DrawString(_spriteBatch, font, text, new Vector2(left, top), color, 0.0f, Vector2.Zero, 1.0f, SpriteEffects.None, 0);
        } // DrawString

        public void DrawString(Font font, string text, Rectangle rect, Color color, Alignment alignment)
        {
            DrawString(font, text, rect, color, alignment, 0, 0, true);
        } // DrawString

        public void DrawString(Font font, string text, Rectangle rect, Color color, Alignment alignment, bool ellipsis)
        {
            DrawString(font, text, rect, color, alignment, 0, 0, ellipsis);
        } // DrawString

        public void DrawString(Control control, SkinLayer layer, string text, Rectangle rect, bool margins)
        {
            DrawString(control, layer, text, rect, margins, 0, 0, true);
        } // DrawString

        public void DrawString(Control control, SkinLayer layer, string text, Rectangle rect, ControlState state, bool margins)
        {
            DrawString(control, layer, text, rect, state, margins, 0, 0, true);
        } // DrawString

        public void DrawString(Control control, SkinLayer layer, string text, Rectangle rect, bool margins, int ox, int oy)
        {
            DrawString(control, layer, text, rect, margins, ox, oy, true);
        } // DrawString

        public void DrawString(Control control, SkinLayer layer, string text, Rectangle rect, bool margins, int ox, int oy, bool ellipsis)
        {
            DrawString(control, layer, text, rect, control.ControlState, margins, ox, oy, ellipsis);
        } // DrawString

        public void DrawString(Control control, SkinLayer layer, string text, Rectangle rect, ControlState state, bool margins, int ox, int oy, bool ellipsis)
        {
            if (layer.Text != null)
            {
                if (margins)
                {
                    var m = layer.ContentMargins;
                    rect = new Rectangle(rect.Left + m.Left, rect.Top + m.Top, rect.Width - m.Horizontal, rect.Height - m.Vertical);
                }


                Color color;
                if (state == ControlState.Hovered && layer.States.Hovered.Index != -1)
                {
                    color = layer.Text.Colors.Hovered;
                }
                else if (state == ControlState.Pressed)
                {
                    color = layer.Text.Colors.Pressed;
                }
                else if (state == ControlState.Focused || control.Focused && state == ControlState.Hovered && layer.States.Hovered.Index == -1)
                {
                    color = layer.Text.Colors.Focused;
                }
                else if (state == ControlState.Disabled)
                {
                    color = layer.Text.Colors.Disabled;
                }
                else
                {
                    color = layer.Text.Colors.Enabled;
                }


                if (!string.IsNullOrEmpty(text))
                {
                    var font = layer.Text;
                    if (control.TextColor != Control.UndefinedColor && control.ControlState != ControlState.Disabled)
                    {
                        color = control.TextColor;
                    }

                    DrawString(font.Font.Font, text, rect, color, font.Alignment, font.OffsetX + ox, font.OffsetY + oy, ellipsis);
                }
            }
        } // DrawString

        public void DrawString(Font font, string text, Rectangle rect, Color color, Alignment alignment, int offsetX, int offsetY, bool ellipsis)
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
        } // DrawString

        private int GetTextCenter(float size1, float size2)
        {
            return (int)Math.Ceiling(size1 / 2 - size2 / 2);
        } // GetTextCenter



        public void DrawLayer(SkinLayer layer, Rectangle rect, Color color, int index)
        {
            var imageSize = new Size(layer.Image.Texture.Width, layer.Image.Texture.Height);
            var partSize = new Size(layer.Width, layer.Height);

            Draw(layer.Image.Texture.Resource, GetDestinationArea(rect, layer.SizingMargins, Alignment.TopLeft), GetSourceArea(imageSize, partSize, layer.SizingMargins, Alignment.TopLeft, index), color);
            Draw(layer.Image.Texture.Resource, GetDestinationArea(rect, layer.SizingMargins, Alignment.TopCenter), GetSourceArea(imageSize, partSize, layer.SizingMargins, Alignment.TopCenter, index), color);
            Draw(layer.Image.Texture.Resource, GetDestinationArea(rect, layer.SizingMargins, Alignment.TopRight), GetSourceArea(imageSize, partSize, layer.SizingMargins, Alignment.TopRight, index), color);
            Draw(layer.Image.Texture.Resource, GetDestinationArea(rect, layer.SizingMargins, Alignment.MiddleLeft), GetSourceArea(imageSize, partSize, layer.SizingMargins, Alignment.MiddleLeft, index), color);
            Draw(layer.Image.Texture.Resource, GetDestinationArea(rect, layer.SizingMargins, Alignment.MiddleCenter), GetSourceArea(imageSize, partSize, layer.SizingMargins, Alignment.MiddleCenter, index), color);
            Draw(layer.Image.Texture.Resource, GetDestinationArea(rect, layer.SizingMargins, Alignment.MiddleRight), GetSourceArea(imageSize, partSize, layer.SizingMargins, Alignment.MiddleRight, index), color);
            Draw(layer.Image.Texture.Resource, GetDestinationArea(rect, layer.SizingMargins, Alignment.BottomLeft), GetSourceArea(imageSize, partSize, layer.SizingMargins, Alignment.BottomLeft, index), color);
            Draw(layer.Image.Texture.Resource, GetDestinationArea(rect, layer.SizingMargins, Alignment.BottomCenter), GetSourceArea(imageSize, partSize, layer.SizingMargins, Alignment.BottomCenter, index), color);
            Draw(layer.Image.Texture.Resource, GetDestinationArea(rect, layer.SizingMargins, Alignment.BottomRight), GetSourceArea(imageSize, partSize, layer.SizingMargins, Alignment.BottomRight, index), color);
        } // DrawLayer

        public void DrawLayer(Control control, SkinLayer layer, Rectangle rect)
        {
            DrawLayer(control, layer, rect, control.ControlState);
        } // DrawLayer

        public void DrawLayer(Control control, SkinLayer layer, Rectangle rect, ControlState state)
        {
            Color color;
            var overlayColor = Color.White;
            int index;
            var overlayIndex = -1;

            if (state == ControlState.Hovered && layer.States.Hovered.Index != -1)
            {
                color = layer.States.Hovered.Color;
                index = layer.States.Hovered.Index;

                if (layer.States.Hovered.Overlay)
                {
                    overlayColor = layer.Overlays.Hovered.Color;
                    overlayIndex = layer.Overlays.Hovered.Index;
                }
            }
            else if (state == ControlState.Focused || control.Focused && state == ControlState.Hovered && layer.States.Hovered.Index == -1)
            {
                color = layer.States.Focused.Color;
                index = layer.States.Focused.Index;

                if (layer.States.Focused.Overlay)
                {
                    overlayColor = layer.Overlays.Focused.Color;
                    overlayIndex = layer.Overlays.Focused.Index;
                }
            }
            else if (state == ControlState.Pressed)
            {
                color = layer.States.Pressed.Color;
                index = layer.States.Pressed.Index;

                if (layer.States.Pressed.Overlay)
                {
                    overlayColor = layer.Overlays.Pressed.Color;
                    overlayIndex = layer.Overlays.Pressed.Index;
                }
            }
            else if (state == ControlState.Disabled)
            {
                color = layer.States.Disabled.Color;
                index = layer.States.Disabled.Index;

                if (layer.States.Disabled.Overlay)
                {
                    overlayColor = layer.Overlays.Disabled.Color;
                    overlayIndex = layer.Overlays.Disabled.Index;
                }
            }
            else
            {
                color = layer.States.Enabled.Color;
                index = layer.States.Enabled.Index;

                if (layer.States.Enabled.Overlay)
                {
                    overlayColor = layer.Overlays.Enabled.Color;
                    overlayIndex = layer.Overlays.Enabled.Index;
                }
            }

            if (control.Color != Control.UndefinedColor)
            {
                color = control.Color * (control.Color.A / 255f);
            }

            DrawLayer(layer, rect, color, index);

            if (overlayIndex != -1)
            {
                DrawLayer(layer, rect, overlayColor, overlayIndex);
            }
        } // DrawLayer

        private Rectangle GetSourceArea(Size imageSize, Size partSize, Margins margins, Alignment alignment, int index)
        {
            var rect = new Rectangle();
            var xc = (int)((float)imageSize.Width / partSize.Width);

            var xm = index % xc;
            var ym = index / xc;

            const int adj = 1;
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
        } // GetSourceArea

        public Rectangle GetDestinationArea(Rectangle area, Margins margins, Alignment alignment)
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
        } // GetDestinationArea



        public void DrawGlyph(Glyph glyph, Rectangle rect)
        {
            var imageSize = new Size(glyph.Texture.Width, glyph.Texture.Height);

            if (!glyph.SourceRectangle.IsEmpty)
            {
                imageSize = new Size(glyph.SourceRectangle.Width, glyph.SourceRectangle.Height);
            }

            if (glyph.SizeMode == SizeMode.Centered)
            {
                rect = new Rectangle(rect.X + (rect.Width - imageSize.Width) / 2 + glyph.Offset.X,
                                     rect.Y + (rect.Height - imageSize.Height) / 2 + glyph.Offset.Y,
                                     imageSize.Width,
                                     imageSize.Height);
            }
            else if (glyph.SizeMode == SizeMode.Normal || glyph.SizeMode == SizeMode.Auto)
            {
                rect = new Rectangle(rect.X + glyph.Offset.X, rect.Y + glyph.Offset.Y, imageSize.Width, imageSize.Height);
            }

            if (glyph.SourceRectangle.IsEmpty)
            {
                Draw(glyph.Texture.Resource, rect, glyph.Color);
            }
            else
            {
                Draw(glyph.Texture.Resource, rect, glyph.SourceRectangle, glyph.Color);
            }
        } // DrawGlyph


    } // Renderer
} // XNAFinalEngine.UserInterface