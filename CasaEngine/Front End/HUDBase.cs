using CasaEngine.Game;
using CasaEngine.Gameplay;
using CasaEngine.Graphics2D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Front_End
{
    public abstract class HudBase
    {
        readonly CharacterActor2D _character;
        Renderer2DComponent _renderer2DComponent;



        protected Renderer2DComponent Renderer2DComponent => _renderer2DComponent;

        protected CharacterActor2D CharacterActor2D => _character;


        public HudBase(CharacterActor2D character)
        {
            if (character == null)
            {
                throw new ArgumentNullException("HUD() : CharacterActor2D is null");
            }

            _character = character;

#if !EDITOR
            _Renderer2DComponent = GameHelper.GetDrawableGameComponent<Renderer2DComponent>(Engine.Instance.Game);

            if (_Renderer2DComponent == null)
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
            /*_Renderer2DComponent.AddText2D(
                GameInfo.Instance.DefaultSpriteFont,
                "HP : " + _Character.HP.ToString() + "/" + _Character.HPMax.ToString(),
                Vector2.Zero, 
                0.0f, Vector2.One, Color.White, 0.0f);*/

            Viewport viewport = Engine.Instance.Game.GraphicsDevice.Viewport;

            _renderer2DComponent.AddText2D(
                Engine.Instance.DefaultSpriteFont,
                "Score : " + GameInfo.Instance.WorldInfo.Score,
                new Vector2((float)viewport.Width / 2.0f, 10.0f),
                0.0f, Vector2.One, Color.White, 0.0f);
        }

    }
}
