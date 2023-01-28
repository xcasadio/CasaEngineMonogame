using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using CasaEngine;
using CasaEngineCommon.Pool;
using CasaEngine.Assets.Graphics2D;



namespace CasaEngine.Graphics2D.Tile
{
    /// <summary>
    /// 
    /// </summary>
    public class TileScrolling
        : TileLayer
    {

        private List<Sprite2D> m_Sprites = new List<Sprite2D>();
        private List<Sprite2D> m_DisplaySprites = new List<Sprite2D>();
        private Rectangle visibleTiles;





        /// <summary>
        /// 
        /// </summary>
        /// <param name="graphicsComponent"></param>
        /// <param name="Renderer2DComponent_"></param>
        public TileScrolling(GraphicsDeviceManager graphicsComponent/*, Renderer2DComponent Renderer2DComponent_*/)
            : base(Vector2.Zero, graphicsComponent/*, Renderer2DComponent_*/)
        {
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sprite_"></param>
        public void AddTile(Sprite2D sprite_)
        {
            m_Sprites.Add(sprite_);
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void DetermineVisibility()
        {
            base.DetermineVisibility();

            visibleTiles.X = (int)CameraPosition.X;
            visibleTiles.Y = (int)CameraPosition.Y;
            visibleTiles.Width = (int)(DisplaySize.X / CameraZoom);
            visibleTiles.Height = (int)(DisplaySize.Y / CameraZoom);

            //PooItem released in Renderer2DComponent
            m_DisplaySprites.Clear();

            throw new NotImplementedException();
            /*foreach (Sprite2D s in m_Sprites)
			{
				Rectangle rect = new Rectangle();

				rect.X = (int)s.Position.X;
				rect.Y = (int)s.Position.Y;
				rect.Width = (int)((float)s.PositionInTexture.Width);
				rect.Height = (int)((float)s.PositionInTexture.Height);

				if (visibleTiles.Intersects(rect))
				{
                    PoolItem<Sprite2D> sprite = Graphics2DPool.ResourcePoolSprite2D.GetItem();
                    sprite.Resource.Clone(s);
					// = GameInfo.Instance.Asset2DManager.GetSprite2DByID(s.ID);
                    sprite.Resource.Position = (s.Position - CameraPosition) * CameraZoom;
                    sprite.Resource.HotSpot = new Point((int)((float)s.HotSpot.X * CameraZoom), (int)((float)s.HotSpot.Y * CameraZoom));
                    sprite.Resource.Scale = new Vector2(CameraZoom);
                    sprite.Resource.Color = s.Color;
					m_DisplaySprites.Add(sprite);
				}
			}*/
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="batch"></param>
        protected override void DrawTiles(SpriteBatch batch)
        {
            foreach (Sprite2D s in m_DisplaySprites)
            {
                //SpriteRenderer.Draw(batch, s);
            }
        }

    }
}
