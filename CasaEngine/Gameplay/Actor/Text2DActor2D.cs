using CasaEngine.Graphics2D;
using CasaEngine.Game;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using CasaEngine.CoreSystems.Game;
using CasaEngine.Gameplay.Actor.Object;

namespace CasaEngine.Gameplay.Actor
{
    public class Text2DActor2D
        : Actor2D
    {

        private Text2DBehaviour m_Text2DBehaviour;
        private readonly Renderer2DComponent m_Renderer2DComponent;

        public SpriteFont SpriteFont;
        public float Rotation = 0.0f;
        public Point Origin = Point.Zero;
        public Vector2 Scale = Vector2.One;
        public Color Color = Color.White;
        public float ZOrder = 0.0f;
        public SpriteEffects SpriteEffect = SpriteEffects.None;



        public string Text
        {
            get;
            set;
        }

        public Text2DBehaviour Text2DBehaviour
        {
            //get { return m_Text2DBehaviour; }
            set => m_Text2DBehaviour = value;
        }



        public Text2DActor2D()
        {
            m_Renderer2DComponent = GameHelper.GetDrawableGameComponent<Renderer2DComponent>(Engine.Instance.Game);
        }



        public void Update(float elapsedTime_)
        {
            m_Text2DBehaviour.Update(elapsedTime_);
        }

        public void Draw(float elapsedTime_)
        {
            m_Renderer2DComponent.AddText2D(SpriteFont, Text, Position, Rotation, Scale, Color, ZOrder);
        }

        public override bool CompareTo(BaseObject other_)
        {
            throw new NotImplementedException();
        }
    }
}
