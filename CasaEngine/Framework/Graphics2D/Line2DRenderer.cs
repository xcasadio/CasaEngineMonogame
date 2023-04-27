using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Graphics2D;

public class Line2dRenderer
{
    private Texture2D? _emptyTexture;

    public void Init(GraphicsDevice device)
    {
        _emptyTexture = new Texture2D(device, 1, 1, false, SurfaceFormat.Color);
        _emptyTexture.SetData(new[] { Color.White });
    }

    public void DrawLine(SpriteBatch batch, Color color, Vector2 point1, Vector2 point2, float layer = 0f)
    {
        var angle = (float)Math.Atan2(point2.Y - point1.Y, point2.X - point1.X);
        var length = (point2 - point1).Length();

        batch.Draw(_emptyTexture, point1, null, color,
            angle, Vector2.Zero, new Vector2(length, 1),
            SpriteEffects.None, layer);
    }
}