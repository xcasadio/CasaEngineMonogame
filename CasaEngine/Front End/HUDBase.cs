using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Graphics2D;
using CasaEngine.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.CoreSystems.Game;

namespace CasaEngine.Gameplay
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class HUDBase
    {
        #region Fields

        CharacterActor2D m_Character;
        Renderer2DComponent m_Renderer2DComponent;

        #endregion

        #region Properties

        /// <summary>
        /// Gets
        /// </summary>
        protected Renderer2DComponent Renderer2DComponent
        {
            get { return m_Renderer2DComponent; }
        }

        /// <summary>
        /// Gets
        /// </summary>
        protected CharacterActor2D CharacterActor2D
        {
            get { return m_Character; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        /// <param name="character_"></param>
        public HUDBase(CharacterActor2D character_)
        {
            if (character_ == null)
            {
                throw new ArgumentNullException("HUD() : CharacterActor2D is null");
            }

            m_Character = character_;

#if !EDITOR
            m_Renderer2DComponent = GameHelper.GetDrawableGameComponent<Renderer2DComponent>(Engine.Instance.Game);

            if (m_Renderer2DComponent == null)
            {
                throw new InvalidOperationException("HUD() : Renderer2DComponent is null");
            }
#endif
        }

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elpaseTime_"></param>
        public virtual void LoadContent(GraphicsDevice graphicsDevice_)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="graphicsDevice_"></param>
        public abstract void Start();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elpaseTime_"></param>
        public virtual void Update(float elpaseTime_)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elpasedTime_"></param>
        public virtual void Draw(float elpasedTime_)
        {
            //@TODO : string is not GC friendly
            /*m_Renderer2DComponent.AddText2D(
                GameInfo.Instance.DefaultSpriteFont,
                "HP : " + m_Character.HP.ToString() + "/" + m_Character.HPMax.ToString(),
                Vector2.Zero, 
                0.0f, Vector2.One, Color.White, 0.0f);*/

            Viewport viewport = Engine.Instance.Game.GraphicsDevice.Viewport;

            m_Renderer2DComponent.AddText2D(
                Engine.Instance.DefaultSpriteFont,
                "Score : " + GameInfo.Instance.WorldInfo.Score,
                new Vector2((float)viewport.Width / 2.0f, 10.0f), 
                0.0f, Vector2.One, Color.White, 0.0f);            
        }

        #endregion
    }
}
