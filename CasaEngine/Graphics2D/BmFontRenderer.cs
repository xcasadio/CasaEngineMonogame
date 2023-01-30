using System.Text;
using CasaEngine.Asset.Fonts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;



namespace CasaEngine.Graphics2D
{
    static public class BmFontRenderer
    {
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
