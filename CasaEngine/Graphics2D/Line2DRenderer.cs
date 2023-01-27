using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace CasaEngine.Graphics2D
{
    /// <summary>
    /// Line Batch
    /// For drawing lines in a spritebatch
    /// </summary>
    public class Line2DRenderer
    {
        private Texture2D _empty_texture;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="device"></param>
        public void Init(GraphicsDevice device)
        {
            _empty_texture = new Texture2D(device, 1, 1, false, SurfaceFormat.Color);
            _empty_texture.SetData(new[] { Color.White });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="batch"></param>
        /// <param name="color"></param>
        /// <param name="point1"></param>
        /// <param name="point2"></param>
        public void DrawLine(SpriteBatch batch, Color color, Vector2 point1, Vector2 point2)
        {
            DrawLine(batch, color, point1, point2, 0);
        }

        /// <summary>
        /// Draw a line into a SpriteBatch
        /// </summary>
        /// <param name="batch">SpriteBatch to draw line</param>
        /// <param name="color">The line color</param>
        /// <param name="point1">Start Point</param>
        /// <param name="point2">End Point</param>
        /// <param name="Layer">Layer or Z position</param>
        public void DrawLine(SpriteBatch batch, Color color, Vector2 point1, Vector2 point2, float Layer)
        {
            float angle = (float) System.Math.Atan2(point2.Y - point1.Y, point2.X - point1.X);
            float length = (point2 - point1).Length();

            batch.Draw(_empty_texture, point1, null, color,
                angle, Vector2.Zero, new Vector2(length, 1),
                SpriteEffects.None, Layer);
        }
    }
}
