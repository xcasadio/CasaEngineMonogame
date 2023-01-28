using System;


using System.Collections.Generic;
using System.Linq;
using System.Xml;
using CasaEngine.Gameplay.Actor;
using CasaEngine.Gameplay.Actor.Object;
using CasaEngine.AI.Messaging;
using CasaEngine.AI.StateMachines;
using CasaEngine.Debugger;
using CasaEngine.Game;
using CasaEngine.Graphics2D;
using CasaEngine.Helper;
using CasaEngineCommon.Logger;
using CasaEngine.Math.Shape2D;
using CasaEngine.Physics2D;
using FarseerPhysics.Dynamics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Gameplay.Design;
using CasaEngine.Design;
using CasaEngine.CoreSystems.Game;
using CasaEngineCommon.Design;
using CasaEngine.Assets.Graphics2D;

namespace CasaEngine.Gameplay
{
    /// <summary>
    /// 
    /// </summary>
    public
#if EDITOR
    partial
#endif
    class CharacterActor2D
        : Actor2D, IFSMCapable<CharacterActor2D>, IRenderable, IAttackable
    {
        static public bool DisplayDebugInformation = true;


        private TeamInfo m_TeamInfo;

        private CharacterActor2DOrientation m_Orientation = CharacterActor2DOrientation.Right;
        private Vector2 m_Velocity = new Vector2();

        /// <summary>
        /// Start position
        /// </summary>
        private Vector2 m_InitialPosition;
        private Vector2 m_Position; //utilisé si pas physique

        private FiniteStateMachine<CharacterActor2D> m_FSM;
        private Controller m_Controller;

        // Used to load all animations
        private Dictionary<int, string> m_AnimationListToLoad = new Dictionary<int, string>();
        private Dictionary<int, Animation2D> m_Animations = new Dictionary<int, Animation2D>();
        private Animation2DPlayer m_Animation2DPlayer;
        private int m_NumberOfDirection = 8;
        private int m_AnimationDirectioMask = 0;
        private Dictionary<int, int> m_AnimationDirectionOffset = new Dictionary<int, int>();
        private Renderer2DComponent m_Renderer2DComponent;

        private Body m_Body;

        //use to avoid GC
        private Point m_PointTmp = new Point();
        private Message m_Message = new Message(-1, -1, (int)MessageType.AnimationChanged, 0.0, null);
        private Vector2 m_Vector2 = new Vector2();

#if !FINAL
        private ShapeRendererComponent m_ShapeRendererComponent;
#endif

        //use to attack only one time per attack
        private List<ICollide2Dable> m_AlreadyAttacked = new List<ICollide2Dable>();

        //for debugging : to delete
        Texture2D m_WhiteTexture;



        /// <summary>
        /// 
        /// </summary>
        public SpriteEffects SpriteEffects
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public int Depth
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public float ZOrder
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public bool Visible
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public float Speed
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public float SpeedOffSet
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public int HPMax
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public int HPMaxOffSet
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public int HP
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public int HPOffSet
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public int MPMax
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public int MPMaxOffSet
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public int MP
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public int MPOffSet
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public int Strength
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public int StrengthOffSet
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public int Defense
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public int DefenseOffSet
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the finite state machine
        /// </summary>
        public IFiniteStateMachine<CharacterActor2D> StateMachine
        {
            get { return m_FSM; }
            set { m_FSM = value as FiniteStateMachine<CharacterActor2D>; }
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public CharacterActor2DOrientation Orientation
        {
            get { return m_Orientation; }
            set
            {
                if (m_Orientation != value
                    && value != 0)
                {
                    m_Body.ResetDynamics(); //to avoid the character slide on the ground
                    m_Orientation = value;
                }
            }
        }

        /// <summary>
        /// Gets
        /// </summary>
        public Vector2 Direction
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public new Vector2 Position
        {
            get
            {
                if (m_Body == null)
                {
                    return m_Position;
                }
                else
                {
                    return m_Body.Position;
                }
            }
            /*set { m_Position = value; }*/
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public Vector2 InitialPosition
        {
            get { return m_InitialPosition; }
            set { m_InitialPosition = value; }
        }

        /// <summary>
        /// Gets
        /// </summary>
        public Animation2DPlayer Animation2DPlayer
        {
            get { return m_Animation2DPlayer; }
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public int ComboNumber
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        /// <returns></returns>
        public bool IsPLayer
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public TeamInfo TeamInfo
        {
            get { return m_TeamInfo; }
            set { m_TeamInfo = value; }
        }

        /// <summary>
        /// Gets
        /// </summary>
        protected Controller Controller
        {
            get { return m_Controller; }
        }

        /// <summary>
        /// 
        /// </summary>
        protected event EventHandler EndAnimationReached
        {
            add
            {
                m_Animation2DPlayer.OnEndAnimationReached += value;
            }
            remove
            {
                m_Animation2DPlayer.OnEndAnimationReached -= value;
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        protected CharacterActor2D(XmlElement el_, SaveOption opt_)
            : base(el_, opt_)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="charac_"></param>
        public CharacterActor2D(CharacterActor2D charac_)
            : this()
        {
            CopyFrom(charac_);
        }

        /// <summary>
        /// 
        /// </summary>
        public CharacterActor2D()
        {
            Speed = 1.0f;
            Initialize();
        }



        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override BaseObject Clone()
        {
            return new CharacterActor2D(this);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ob_"></param>
        protected override void CopyFrom(BaseObject ob_)
        {
            if (ob_ is CharacterActor2D == false)
            {
                throw new ArgumentException("the object is not a CharacterActor2D object");
            }

            base.CopyFrom(ob_);

            CharacterActor2D f = ob_ as CharacterActor2D;

            m_Orientation = f.m_Orientation;
            m_Velocity = f.m_Velocity;
            Speed = f.Speed;
            SpeedOffSet = f.SpeedOffSet;
            HPMax = f.HPMax;
            MPMax = f.MPMax;
            Strength = f.Strength;
            Defense = f.Defense;
            HP = HPMax;
            MP = MPMax;

            m_Animations = new Dictionary<int, Animation2D>();

            foreach (KeyValuePair<int, Animation2D> pair in f.m_Animations)
            {
                m_Animations.Add(pair.Key, (Animation2D)pair.Value.Clone());
            }

            foreach (var pair in f.m_AnimationListToLoad)
            {
                m_AnimationListToLoad.Add(pair.Key, pair.Value);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="controller_"></param>
        public void SetController(Controller controller_)
        {
            m_Controller = controller_;
        }

        /// <summary>
        /// 
        /// </summary>
        private void Initialize()
        {
            m_FSM = new FiniteStateMachine<CharacterActor2D>(this);

#if !EDITOR
            m_Renderer2DComponent = GameHelper.GetDrawableGameComponent<Renderer2DComponent>(Engine.Instance.Game);
#endif
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="physicWorld_"></param>
        public virtual void Initialize(FarseerPhysics.Dynamics.World physicWorld_)
        {
            foreach (var pair in m_AnimationListToLoad)
            {
                Animation2D anim2d = Engine.Instance.ObjectManager.GetObjectByPath(pair.Value) as Animation2D;

                if (anim2d != null)
                {
                    m_Animations.Add(pair.Key, anim2d);
                    anim2d.InitializeEvent();

                    foreach (var frame in anim2d.GetFrames())
                    {
                        Sprite2D sprite2d = Engine.Instance.ObjectManager.GetObjectByID(frame.spriteID) as Sprite2D;
                        //sprite2d.LoadTextureFile(Engine.Instance.Game.GraphicsDevice);
                        sprite2d.LoadTexture(Engine.Instance.Game.Content);
                    }
                }
                else
                {
                    LogManager.Instance.WriteLineError("CharacterActor2D.Initialize(World) : can't find the animation : '" + pair.Value + "'");
                }
            }

            foreach (KeyValuePair<int, Animation2D> pair in m_Animations)
            {
                //Event
                pair.Value.InitializeEvent();

                //sprite
                //Engine.Instance.Asset2DManager.AddSprite2DToLoadingList(pair.Value);
            }

            //Engine.Instance.Asset2DManager.LoadLoadingList(Engine.Instance.Game.Content);

            m_Animation2DPlayer = new Animation2DPlayer(m_Animations);
            m_Animation2DPlayer.OnEndAnimationReached += new EventHandler(OnEndAnimationReached);
            //m_Animation2DPlayer.OnFrameChanged += new EventHandler<Animation2DFrameChangedEventArgs>(OnFrameChanged);

            InitializePhysics(physicWorld_);

            Collision2DManager.Instance.RegisterObject(this);

            m_ShapeRendererComponent = GameHelper.GetDrawableGameComponent<ShapeRendererComponent>(Engine.Instance.Game);

            HP = HPMax;
            MP = MPMax;

            //for debugging
            m_WhiteTexture = (Engine.Instance.Game.Services.GetService(typeof(DebugManager)) as DebugManager).WhiteTexture;
        }

        /// <summary>
        /// 
        /// </summary>
        public void InitializeController()
        {
            //if (m_Controller != null)
            //{
            m_Controller.Initialize();
            //}
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnEndAnimationReached(object sender, EventArgs e)
        {
            m_Message.SenderID = this.ID;
            m_Message.RecieverID = this.ID; //m_Animation2DPlayer.CurrentAnimation.ID ??
            m_Message.Type = (int)MessageType.AnimationChanged;
            //m_Message.ExtraInfo = m_Animation2DPlayer.CurrentAnimation.ID; // @TODO : boxing is not GC friendly

            m_FSM.HandleMessage(m_Message);
            m_Controller.StateMachine.HandleMessage(m_Message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /*void OnFrameChanged(object sender, Animation2DFrameChangedEventArgs e)
        {
            
        }*/

        /// <summary>
        /// 
        /// </summary>
        /// <param name="physicWorld_"></param>
        private void InitializePhysics(FarseerPhysics.Dynamics.World physicWorld_)
        {
            if (physicWorld_ != null)
            {
                m_Body = FarseerPhysics.Factories.BodyFactory.CreateCircle(
                physicWorld_,
                30.0f,
                0.00001f,
                m_InitialPosition,
                this);

                m_Body.BodyType = BodyType.Dynamic;
                m_Body.SleepingAllowed = true;
                m_Body.FixedRotation = true;
                m_Body.Friction = 0.0f;
                m_Body.Restitution = 0.0f;
            }
            else
            {
                m_Position = m_InitialPosition;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elapsedTime_"></param>
        public override void Update(float elapsedTime_)
        {
            //if (m_Controller != null)
            //{
            m_Controller.Update(elapsedTime_);
            //}

            MoveCharacter(elapsedTime_);
            m_Animation2DPlayer.Update(elapsedTime_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elapsedTime_"></param>
        public void Draw(float elapsedTime_)
        {
            m_Renderer2DComponent.AddSprite2D(
                m_Animation2DPlayer.CurrentAnimation.CurrentSpriteId,
                Position, 0.0f, Vector2.One, Color.White,
                1 - Position.Y / Engine.Instance.Game.GraphicsDevice.Viewport.Height,
                SpriteEffects);

            if (ShapeRendererComponent.DisplayCollisions == true)
            {
                Shape2DObject[] geometry2DObjectList = Shape2DObjectList;
                if (geometry2DObjectList != null)
                {
                    foreach (Shape2DObject g in geometry2DObjectList)
                    {
                        m_ShapeRendererComponent.AddShape2DObject(g, g.Flag == 0 ? Color.Green : Color.Red);
                    }
                }
            }

            if (DisplayDebugInformation == true)
            {
                //display HP
                m_Renderer2DComponent.AddSprite2D(
                    m_WhiteTexture,
                    Position + new Vector2(-50.0f, 10.0f),
                    0.0f,
                    new Vector2(100.0f, 5.0f), // scale
                    Color.Red, 0.00001f, SpriteEffects.None);

                m_Renderer2DComponent.AddSprite2D(
                    m_WhiteTexture,
                    Position + new Vector2(-50.0f, 10.0f),
                    0.0f,
                    new Vector2((float)HP / (float)HPMax * 100.0f, 5.0f), // scale
                    Color.Green, 0.0f, SpriteEffects.None);

                //display direction
                Vector2 rr = m_Velocity;
                if (rr != Vector2.Zero)
                {
                    rr.Normalize();
                }
                m_Renderer2DComponent.AddLine2D(Position, Position + rr * 80.0f, Color.Blue);

                //for debugging State
                /*if (m_Controller != null)
                {
                    m_Renderer2DComponent.AddText2D(
                        Engine.Instance.DefaultSpriteFont,
                        "State : " + m_Controller.StateMachine.CurrentState.Name,
                        Position + new Vector2(-100.0f, -130.0f),
                        0.0f, Vector2.One, Color.Green, 0.0f);
                }

                if (m_Body != null)
                {
                    m_Renderer2DComponent.AddText2D(
                        Engine.Instance.DefaultSpriteFont,
                        "Speed : " + m_Body.LinearVelocity.Length(),
                        Position + new Vector2(-70.0f, -100.0f),
                        0.0f, Vector2.One, Color.White, 0.0f);
                }*/
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="opt_"></param>
        public override void Load(XmlElement el_, SaveOption opt_)
        {
            base.Load(el_, opt_);

            XmlElement statusNode = (XmlElement)el_.SelectSingleNode("Status");

            Speed = float.Parse(statusNode.Attributes["speed"].Value);
            Strength = int.Parse(statusNode.Attributes["strength"].Value);
            Defense = int.Parse(statusNode.Attributes["defense"].Value);
            HPMax = int.Parse(statusNode.Attributes["HPMax"].Value);
            MPMax = int.Parse(statusNode.Attributes["MPMax"].Value);

            XmlElement animListNode = (XmlElement)el_.SelectSingleNode("AnimationList");

            m_Animations.Clear();
            m_AnimationListToLoad.Clear();

            foreach (XmlNode node in animListNode.ChildNodes)
            {
                m_AnimationListToLoad.Add(int.Parse(node.Attributes["index"].Value), node.Attributes["name"].Value);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos_"></param>
        public void SetPosition(Vector2 pos_)
        {
            if (m_Body == null)
            {
                m_Position = pos_;
            }
            else
            {
                m_Body.Position = pos_;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dir_"></param>
        public void Move(ref Vector2 dir_)
        {
            if (dir_ == Vector2.Zero)
            {
                //always when Vector2.Zero to stop movement
                //else if contact the character will continue to move
                m_Body.ResetDynamics();
            }
            else
            {
                Direction = dir_;
            }

            m_Velocity = dir_ * (Speed + SpeedOffSet);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elapsedTime_"></param>
        private void MoveCharacter(float elapsedTime_)
        {
            if (m_Body == null)
            {
                m_Position += m_Velocity * elapsedTime_;
            }
            else
            {
                m_Body.LinearVelocity = m_Velocity;
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="numberOfDirectionAnimation_"></param>
        /// <param name="animationDirectioMask_"></param>
        public void SetAnimationParameters(int numberOfDirectionAnimation_, int animationDirectionMask_)
        {
            m_NumberOfDirection = numberOfDirectionAnimation_;
            m_AnimationDirectioMask = animationDirectionMask_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dir_"></param>
        /// <param name="offset_"></param>
        public void SetAnimationDirectionOffset(CharacterActor2DOrientation dir_, int offset_)
        {
            m_AnimationDirectionOffset[(int)dir_] = offset_;
        }

        /// <summary>
        /// Set the animation compared to the index and the direction of the character
        /// See AnimationDirectionMask, CharacterDirection, AnimationIndices, NumberCharacterDriection
        /// </summary>
        /// <param name="index_"></param>
        public void SetCurrentAnimation(int index_)
        {
            m_Animation2DPlayer.SetCurrentAnimationByID(index_ * m_NumberOfDirection + GetAnimationDirectionOffset());
        }

        /// <summary>
        /// Set the animation by name
        /// </summary>
        /// <param name="name_"></param>
        public void SetCurrentAnimation(string name_)
        {
            m_Animation2DPlayer.SetCurrentAnimationByName(name_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private int GetAnimationDirectionOffset()
        {
#if DEBUG
            int val = (int)m_Orientation & m_AnimationDirectioMask;
#endif
            return m_AnimationDirectionOffset[(int)m_Orientation & m_AnimationDirectioMask];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="info_"></param>
        public virtual void Hit(HitInfo info_)
        {
            CharacterActor2D a = (CharacterActor2D)info_.ActorAttacking;

            AddHitEffect(ref info_.ContactPoint);

            int cost = a.Strength - Defense;
            cost = cost < 0 ? 0 : cost;
            HP -= cost;

            if (HP <= 0)
            {
                a.IKillSomeone(info_);
                Delete = true;

                //to delete : debug
                //respawn
                HP = HPMax;
                SetPosition(Vector2.One * 10.0f);
                m_Controller.StateMachine.Transition(m_Controller.GetState(0));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="contactPoint_"></param>
        public void AddHitEffect(ref Vector2 contactPoint_)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="info_"></param>
        public virtual void IKillSomeone(HitInfo info_)
        {
            if (IsPLayer == true)
            {
                GameInfo.Instance.WorldInfo.BotKilled++;
            }
        }




        /// <summary>
        /// 
        /// </summary>
        public Shape2DObject[] Shape2DObjectList
        {
            get
            {
                Sprite2D sprite = (Sprite2D)Engine.Instance.ObjectManager.GetObjectByID(m_Animation2DPlayer.CurrentAnimation.CurrentSpriteId);

                //return Manager.CreateShape2DFromSprite2D(id, Position, SpriteEffects);

                Shape2DObject[] res = null;

                if (sprite.Collisions != null)
                {
                    res = sprite.Collisions.ToArray();

                    for (int i = 0; i < res.Length; i++)
                    {
                        Shape2DObject g = res[i].Clone();

                        //m_PointTmp.X = g.Location.X + (int)(Position.X - sprite.HotSpot.X);
                        //m_PointTmp.Y = g.Location.Y + (int)(Position.Y - sprite.HotSpot.Y);

                        if (SpriteEffects == SpriteEffects.FlipHorizontally)
                        {
                            m_PointTmp.X = (int)Position.X - g.Location.X + (int)(sprite.HotSpot.X);
                            m_PointTmp.Y = g.Location.Y + (int)(Position.Y - sprite.HotSpot.Y);
                        }
                        else if (SpriteEffects == SpriteEffects.FlipVertically)
                        {
                            m_PointTmp.X = g.Location.X + (int)(Position.X - sprite.HotSpot.X);
                            m_PointTmp.Y = (int)Position.Y - g.Location.Y + (int)(sprite.HotSpot.Y);
                        }
                        else
                        {
                            m_PointTmp.X = g.Location.X + (int)(Position.X - sprite.HotSpot.X);
                            m_PointTmp.Y = g.Location.Y + (int)(Position.Y - sprite.HotSpot.Y);
                        }

                        g.Location = m_PointTmp;

                        if (SpriteEffects == SpriteEffects.FlipHorizontally)
                        {
                            g.FlipHorizontally();
                        }
                        else if (SpriteEffects == SpriteEffects.FlipVertically)
                        {
                            g.FlipVertically();
                        }

                        res[i] = g;
                    }
                }

                return res;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="other_"></param>
        /// <returns></returns>
        public bool CanAttackHim(IAttackable other_)
        {
            if (m_AlreadyAttacked.Contains(other_) == false)
            {
                return TeamInfo.CanAttack(other_.TeamInfo);
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        public void DoANewAttack()
        {
            m_AlreadyAttacked.Clear();
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public bool HandleMessage(Message message)
        {
            switch (message.Type)
            {
                case (int)MessageType.Hit:
                    Hit((HitInfo)message.ExtraInfo);
                    break;

                case (int)MessageType.IHitSomeone:
                    HitInfo hitInfo = (HitInfo)message.ExtraInfo;
                    m_AlreadyAttacked.Add(hitInfo.ActorHit as ICollide2Dable);
                    break;
            }

            m_FSM.HandleMessage(message);

            if (m_Controller != null)
            {
                m_Controller.StateMachine.HandleMessage(message);
            }

            return true;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="v"></param>
        static public CharacterActor2DOrientation GetCharacterDirectionFromVector2(Vector2 v_)
        {
            float deadzone = 0.2f;
            CharacterActor2DOrientation dir = 0;

            if (v_.X < -deadzone)
            {
                dir |= CharacterActor2DOrientation.Left;
            }
            else if (v_.X > deadzone)
            {
                dir |= CharacterActor2DOrientation.Right;
            }

            if (v_.Y < -deadzone)
            {
                dir |= CharacterActor2DOrientation.Up;
            }
            else if (v_.Y > deadzone)
            {
                dir |= CharacterActor2DOrientation.Down;
            }

            return dir;
        }

    }
}
