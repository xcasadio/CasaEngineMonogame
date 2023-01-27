using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Gameplay.Actor;
using CasaEngine.Graphics2D;
using CasaEngine.Game;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using CasaEngine.CoreSystems.Game;

namespace CasaEngine.Gameplay.Actor
{
    /// <summary>
    /// Display a text on screen with a customable behaviour
    /// </summary>
    public class Text2DActor2D
        : Actor2D
    {
        #region Fields

        private Text2DBehaviour m_Text2DBehaviour;
        private Renderer2DComponent m_Renderer2DComponent;

        public SpriteFont SpriteFont;
        public float Rotation = 0.0f;
        public Point Origin = Point.Zero;
        public Vector2 Scale = Vector2.One;
        public Color Color = Color.White;
        public float ZOrder = 0.0f;
        public SpriteEffects SpriteEffect = SpriteEffects.None;

        #endregion

        #region Properties        

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public string Text
        {
            get;
            set;
        }

        /// <summary>
        /// Sets
        /// </summary>
        public Text2DBehaviour Text2DBehaviour
        {
            //get { return m_Text2DBehaviour; }
            set { m_Text2DBehaviour = value; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        public Text2DActor2D()
        {
            m_Renderer2DComponent = GameHelper.GetDrawableGameComponent<Renderer2DComponent>(Engine.Instance.Game);
        }

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elapsedTime_"></param>
        public void Update(float elapsedTime_)
        {
            m_Text2DBehaviour.Update(elapsedTime_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elapsedTime_"></param>
        public void Draw(float elapsedTime_)
        {
            m_Renderer2DComponent.AddText2D(SpriteFont, Text, Position, Rotation, Scale, Color, ZOrder);
        }

        #endregion
    }
}
