using CasaEngine.Framework.GUI.Neoforce.Skins;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.GUI.Neoforce.Graphics;

public interface IRenderer : IDisposable
{
    void Begin(BlendingMode mode);
    void End();
    void Draw(Texture2D texture, Rectangle destination, Color color);
    void Draw(Texture2D texture, Rectangle destination, Rectangle source, Color color);
    void Draw(Texture2D texture, int left, int top, Color color);
    void Draw(Texture2D texture, int left, int top, Rectangle source, Color color);
    void DrawTileTexture(Texture2D texture, Rectangle destination, Color color);
    void DrawString(SpriteFontBase font, string text, int left, int top, Color color);
    void DrawString(SpriteFontBase font, string text, Rectangle rect, Color color, Alignment alignment);
    void DrawString(SpriteFontBase font, string text, Rectangle rect, Color color, Alignment alignment, bool ellipsis);
    void DrawString(Control control, SkinLayer layer, string text, Rectangle rect);
    void DrawString(Control control, SkinLayer layer, string text, Rectangle rect, ControlState state);
    void DrawString(Control control, SkinLayer layer, string text, Rectangle rect, bool margins);
    void DrawString(Control control, SkinLayer layer, string text, Rectangle rect, ControlState state, bool margins);
    void DrawString(Control control, SkinLayer layer, string text, Rectangle rect, bool margins, int ox, int oy);
    void DrawString(Control control, SkinLayer layer, string text, Rectangle rect, ControlState state, bool margins, int ox, int oy);
    void DrawString(Control control, SkinLayer layer, string text, Rectangle rect, bool margins, int ox, int oy, bool ellipsis);
    void DrawString(Control control, SkinLayer layer, string text, Rectangle rect, ControlState state, bool margins, int ox, int oy, bool ellipsis);
    void DrawString(SpriteFontBase font, string text, Rectangle rect, Color color, Alignment alignment, int offsetX, int offsetY, bool ellipsis);
    void DrawLayer(SkinLayer layer, Rectangle rect, Color color, int index);
    void DrawLayer(Control control, SkinLayer layer, Rectangle rect);
    void DrawLayer(Control control, SkinLayer layer, Rectangle rect, ControlState state);
    void DrawGlyph(Glyph glyph, Rectangle rect);
    void DrawCursor(Texture2D texture, Rectangle destination, Vector2 hotSpot);
}