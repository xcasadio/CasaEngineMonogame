using CasaEngine.Graphics2D;
using CasaEngine.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Gameplay
{
    public abstract class HUDBase
    {
        readonly CharacterActor2D m_Character;
        Renderer2DComponent m_Renderer2DComponent;



        protected Renderer2DComponent Renderer2DComponent => m_Renderer2DComponent;

        protected CharacterActor2D CharacterActor2D => m_Character;


        public HUDBase(CharacterActor2D character)
        {
            if (character == null)
            {
                throw new ArgumentNullException("HUD() : CharacterActor2D is null");
            }

            m_Character = character;

#if !EDITOR
            m_Renderer2DComponent = GameHelper.GetDrawableGameComponent<Renderer2DComponent>(Engine.Instance.Game);

            if (m_Renderer2DComponent == null)
            {
                throw new InvalidOperationException("HUD() : Renderer2DComponent is null");
            }
#endif
        }



        public virtual void LoadContent(GraphicsDevice graphicsDevice)
        {

        }

        public abstract void Start();

        public virtual void Update(float elapsedTime)
        {

        }

        public virtual void Draw(float elapsedTime)
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

    }
}
