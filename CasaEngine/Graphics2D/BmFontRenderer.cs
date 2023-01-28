using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Asset.Fonts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;



namespace CasaEngine.Graphics2D
{
    /// <summary>
    /// 
    /// </summary>
    static public class BmFontRenderer
    {
        /// <summary>
        /// Adds a string to a batch of sprites for rendering using the specified font,
        /// text, position, and color.
        /// </summary>
        /// <param name="spriteFont">A font for displaying text.</param>
        /// <param name="text">Text string.</param>
        /// <param name="position">The location (in screen coordinates) to draw the sprite.</param>
        /// <param name="color">The color to tint a sprite. Use Color.White for full color with no tinting.</param>
        static public void DrawString(SpriteBatch spriteBatch, Font spriteFont, string text, Vector2 position, Color color)
        {
            DrawString(spriteBatch, spriteFont, text, position, color, 0.0f, Vector2.Zero, 1.0f, SpriteEffects.None, 0.00001f);
        }

        static public void DrawString(SpriteBatch spriteBatch, Font spriteFont, StringBuilder text, Vector2 position, Color color)
        {
            DrawString(spriteBatch, spriteFont, text.ToString(), position, color);
        }

        static public void DrawString(SpriteBatch spriteBatch, Font spriteFont, string text, Vector2 position, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)
        {
            DrawString(spriteBatch, spriteFont, text, position, color, rotation, origin, new Vector2(scale), effects, layerDepth);
        }

        static public void DrawString(SpriteBatch spriteBatch, Font spriteFont, StringBuilder text, Vector2 position, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)
        {
            DrawString(spriteBatch, spriteFont, text.ToString(), position, color, rotation, origin, new Vector2(scale), effects, layerDepth);
        }

        static public void DrawString(SpriteBatch spriteBatch, Font spriteFont, StringBuilder text, Vector2 position, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth)
        {
            DrawString(spriteBatch, spriteFont, text.ToString(), position, color, rotation, origin, scale, effects, layerDepth);
        }

        /// <summary>
        /// Adds a string to a batch of sprites for rendering using the specified font,
        /// text, position, and color.
        /// </summary>
        /// <param name="SpriteBatch">A SpriteBatch to draw the string. SpriteBatch.Begin() must be called before.</param>
        /// <param name="spriteFont">A font for displaying text.</param>
        /// <param name="text">Text string.</param>
        /// <param name="position">The location (in screen coordinates) to draw the sprite.</param>
        /// <param name="color">The color to tint a sprite. Use Color.White for full color with no tinting.</param>
        /// <param name="rotation">Specifies the angle (in radians) to rotate the sprite about its center.</param>
        /// <param name="origin">The sprite origin; the default is (0,0) which represents the upper-left corner.</param>
        /// <param name="scale">Scale factor.</param>
        /// <param name="effects">Effects to apply.</param>
        /// <param name="layerDepth">The depth of a layer. By default, 0 represents the front layer and 1 represents
        /// a back layer. Use SpriteSortMode if you want sprites to be sorted during
        /// drawing.</param>
        static public void DrawString(SpriteBatch spriteBatch, Font spriteFont, string text, Vector2 position, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth)
        {
            char[] array = text.ToCharArray();

            for (int i = 0; i < array.Length; i++)
            {
                FontChar c = spriteFont.GetFontChar(array[i]);

                if (c == null || array[i] == '\n' || array[i] == '\r' || array[i] == '\t')
                {
                    position.X += spriteFont.Spacing;
                    continue;
                }

                float xOffset = c.XOffset * scale.X;
                float yOffset = c.YOffset * scale.Y;
                float xAdvance = c.XAdvance * scale.X;
                //float width = c.Width * scale.X;
                //float height = c.Height * scale.Y;                

                //TODO : what is kerning ??
                // Adjust for kerning
                /*float kernAmount = 0f;
                
                if (spriteFont.Kernings && !firstCharOfLine )
                {
                    m_nextChar = (char)text[i];
                    Kerning kern = m_charSet.Characters[lastChar].KerningList.Find( FindKerningNode );
                    if ( kern != null )
                    {
                        kernAmount = kern.Amount * sizeScale;
                        position.X += kernAmount;
                        lineWidth += kernAmount;
                        wordWidth += kernAmount;
                    }
                }*/

                spriteBatch.Draw(spriteFont.Textures[c.Page].Resource,
                        new Vector2(position.X + xOffset, position.Y + yOffset),
                        new Rectangle(c.X, c.Y, c.Width, c.Height),
                        color, rotation, origin, scale, effects, layerDepth);

                position.X += xAdvance;
            } // end for
        }
    }
}
