using CasaEngine.Gameplay.Actor.Object;
using CasaEngine.Graphics2D;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using CasaEngine.Game;
using System.Xml;
using CasaEngineCommon.Logger;
using CasaEngine.CoreSystems.Game;
using CasaEngineCommon.Design;
using CasaEngine.Assets.Graphics2D;

namespace CasaEngine.Gameplay.Actor
{
    public class AnimatedSpriteActor : Actor2D, IRenderable
    {

#if EDITOR
        public override bool CompareTo(BaseObject other_)
        {
            if (other_ is AnimatedSpriteActor)
            {
                AnimatedSpriteActor asa = (AnimatedSpriteActor)other_;
                //asa.m_Animations
            }

            return false;
        }
#endif

        private Dictionary<int, Animation2D> m_Animations = new Dictionary<int, Animation2D>();
        private Animation2DPlayer m_Animation2DPlayer;
        private Renderer2DComponent m_Renderer2DComponent;



        public Animation2DPlayer Animation2DPlayer => m_Animation2DPlayer;

        public int Depth
        {
            get;
            set;
        }

        public float ZOrder
        {
            get;
            set;
        }

        public bool Visible
        {
            get;
            set;
        }



        public AnimatedSpriteActor()
            : base()
        {

        }

        public AnimatedSpriteActor(AnimatedSpriteActor src_)
        {
            //return new 
        }



        public override BaseObject Clone()
        {
            throw new NotImplementedException();
        }

        protected override void CopyFrom(BaseObject ob_)
        {
            base.CopyFrom(ob_);

            AnimatedSpriteActor src = ob_ as AnimatedSpriteActor;

            m_Animations = new Dictionary<int, Animation2D>();

            foreach (KeyValuePair<int, Animation2D> pair in src.m_Animations)
            {
                m_Animations.Add(pair.Key, (Animation2D)pair.Value.Clone());
            }

            m_Animation2DPlayer = new Animation2DPlayer(m_Animations);
            m_Renderer2DComponent = src.m_Renderer2DComponent;
        }

        public override void Update(float elapsedTime_)
        {
            m_Animation2DPlayer.Update(elapsedTime_);
        }

        public virtual void Draw(float elapsedTime_)
        {
            m_Renderer2DComponent.AddSprite2D(
                m_Animation2DPlayer.CurrentAnimation.CurrentSpriteId,
                Position, 0.0f, Vector2.One, Color.White,
                1 - Position.Y / Engine.Instance.Game.GraphicsDevice.Viewport.Height,
                SpriteEffects.None);
        }

        public override void Load(XmlElement el_, SaveOption opt_)
        {
            base.Load(el_, opt_);

            XmlElement animListNode = (XmlElement)el_.SelectSingleNode("AnimationList");

            m_Animations.Clear();

            foreach (XmlNode node in animListNode.ChildNodes)
            {
                Animation2D anim2d = Engine.Instance.Asset2DManager.GetAnimation2DByName(node.Attributes["name"].Value);
                if (anim2d != null)
                {
                    m_Animations.Add(int.Parse(node.Attributes["index"].Value), anim2d);
                }
                else
                {
                    LogManager.Instance.WriteLineError("CharacterActor2D.Load() : can't find the animation : '" + node.Attributes["name"].Value + "'");
                }
            }
        }

        public void LoadAnimation(int index_, string anim2DName_)
        {
            m_Animations.Add(
                index_,
                (Animation2D)Engine.Instance.ObjectManager.GetObjectByPath(anim2DName_));

            foreach (var frame in m_Animations[index_].GetFrames())
            {
                Sprite2D sprite = (Sprite2D)Engine.Instance.ObjectManager.GetObjectByID(frame.spriteID);
                //sprite.LoadTextureFile(Engine.Instance.Game.GraphicsDevice);
                sprite.LoadTexture(Engine.Instance.Game.Content);
            }

            m_Animation2DPlayer = new Animation2DPlayer(m_Animations);
        }

        public virtual void Initialize()
        {
            m_Renderer2DComponent = GameHelper.GetDrawableGameComponent<Renderer2DComponent>(Engine.Instance.Game);
        }

    }
}
