using System.Xml;
using CasaEngine.Gameplay.Actor;
using CasaEngine.AI.Messaging;
using CasaEngine.AI.StateMachines;
using CasaEngine.Debugger;
using CasaEngine.Game;
using CasaEngine.Graphics2D;
using CasaEngineCommon.Logger;
using CasaEngine.Physics2D;
using FarseerPhysics.Dynamics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Gameplay.Design;
using CasaEngineCommon.Design;
using CasaEngine.Assets.Graphics2D;
using CasaEngine.Core.Game;
using CasaEngine.Core.Math.Shape2D;
using CasaEngine.Entities;
using CasaEngine.Helpers;

namespace CasaEngine.Gameplay
{
    public
#if EDITOR
    partial
#endif
    class CharacterActor2D
        : Actor2D, IFsmCapable<CharacterActor2D>, IRenderable, IAttackable
    {
        public static bool DisplayDebugInformation = true;


        private TeamInfo _teamInfo;

        private CharacterActor2DOrientation _orientation = CharacterActor2DOrientation.Right;
        private Vector2 _velocity;

        private Vector2 _initialPosition;
        private Vector2 _position; //utilisé si pas physique

        private FiniteStateMachine<CharacterActor2D> _fsm;
        private Controller _controller;

        // Used to load all animations
        private readonly Dictionary<int, string> _animationListToLoad = new();
        private Dictionary<int, Animation2D> _animations = new();
        private Animation2DPlayer _animation2DPlayer;
        private int _numberOfDirection = 8;
        private int _animationDirectioMask;
        private readonly Dictionary<int, int> _animationDirectionOffset = new();
        private Renderer2DComponent _renderer2DComponent;

        private Body _body;

        //use to avoid GC
        private Point _pointTmp;
        private readonly Message _message = new(-1, -1, (int)MessageType.AnimationChanged, 0.0, null);
        private Vector2 _vector2 = new();

#if !FINAL
        private ShapeRendererComponent _shapeRendererComponent;
#endif

        //use to attack only one time per attack
        private readonly List<ICollide2Dable> _alreadyAttacked = new();

        //for debugging : to delete
        Texture2D _whiteTexture;



        public SpriteEffects SpriteEffects
        {
            get;
            set;
        }

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

        public float Speed
        {
            get;
            set;
        }

        public float SpeedOffSet
        {
            get;
            set;
        }

        public int HpMax
        {
            get;
            set;
        }

        public int HpMaxOffSet
        {
            get;
            set;
        }

        public int Hp
        {
            get;
            set;
        }

        public int HpOffSet
        {
            get;
            set;
        }

        public int MpMax
        {
            get;
            set;
        }

        public int MpMaxOffSet
        {
            get;
            set;
        }

        public int Mp
        {
            get;
            set;
        }

        public int MpOffSet
        {
            get;
            set;
        }

        public int Strength
        {
            get;
            set;
        }

        public int StrengthOffSet
        {
            get;
            set;
        }

        public int Defense
        {
            get;
            set;
        }

        public int DefenseOffSet
        {
            get;
            set;
        }

        public IFiniteStateMachine<CharacterActor2D> StateMachine
        {
            get => _fsm;
            set => _fsm = value as FiniteStateMachine<CharacterActor2D>;
        }

        public CharacterActor2DOrientation Orientation
        {
            get => _orientation;
            set
            {
                if (_orientation != value
                    && value != 0)
                {
                    _body.ResetDynamics(); //to avoid the character slide on the ground
                    _orientation = value;
                }
            }
        }

        public Vector2 Direction
        {
            get;
            private set;
        }

        public new Vector2 Position
        {
            get
            {
                if (_body == null)
                {
                    return _position;
                }
                else
                {
                    return _body.Position;
                }
            }
            /*set { _Position = value; }*/
        }

        public Vector2 InitialPosition
        {
            get => _initialPosition;
            set => _initialPosition = value;
        }

        public Animation2DPlayer Animation2DPlayer => _animation2DPlayer;

        public int ComboNumber
        {
            get;
            set;
        }

        public bool IsPLayer
        {
            get;
            set;
        }

        public TeamInfo TeamInfo
        {
            get => _teamInfo;
            set => _teamInfo = value;
        }

        protected Controller Controller => _controller;

        protected event EventHandler EndAnimationReached
        {
            add => _animation2DPlayer.OnEndAnimationReached += value;
            remove => _animation2DPlayer.OnEndAnimationReached -= value;
        }



        protected CharacterActor2D(XmlElement el, SaveOption opt)
            : base(el, opt)
        {
        }

        public CharacterActor2D(CharacterActor2D charac)
            : this()
        {
            CopyFrom(charac);
        }

        public CharacterActor2D()
        {
            Speed = 1.0f;
            Initialize();
        }



        public BaseObject Clone()
        {
            return new CharacterActor2D(this);
        }

        protected void CopyFrom(BaseObject ob)
        {
            if (ob is CharacterActor2D == false)
            {
                throw new ArgumentException("the object is not a CharacterActor2D object");
            }

            base.CopyFrom(ob);

            var f = ob as CharacterActor2D;

            _orientation = f._orientation;
            _velocity = f._velocity;
            Speed = f.Speed;
            SpeedOffSet = f.SpeedOffSet;
            HpMax = f.HpMax;
            MpMax = f.MpMax;
            Strength = f.Strength;
            Defense = f.Defense;
            Hp = HpMax;
            Mp = MpMax;

            _animations = new Dictionary<int, Animation2D>();

            foreach (var pair in f._animations)
            {
                _animations.Add(pair.Key, (Animation2D)pair.Value.Clone());
            }

            foreach (var pair in f._animationListToLoad)
            {
                _animationListToLoad.Add(pair.Key, pair.Value);
            }
        }

        public void SetController(Controller controller)
        {
            _controller = controller;
        }

        private void Initialize()
        {
            _fsm = new FiniteStateMachine<CharacterActor2D>(this);

#if !EDITOR
            _Renderer2DComponent = GameHelper.GetDrawableGameComponent<Renderer2DComponent>(Engine.Instance.Game);
#endif
        }

        public virtual void Initialize(FarseerPhysics.Dynamics.World physicWorld)
        {
            foreach (var pair in _animationListToLoad)
            {
                var anim2d = Engine.Instance.ObjectManager.GetObjectByPath(pair.Value) as Animation2D;

                if (anim2d != null)
                {
                    _animations.Add(pair.Key, anim2d);
                    anim2d.InitializeEvent();

                    foreach (var frame in anim2d.GetFrames())
                    {
                        var sprite2d = Engine.Instance.ObjectManager.GetObjectById(frame.SpriteId) as Sprite2D;
                        //sprite2d.LoadTextureFile(Engine.Instance.Game.GraphicsDevice);
                        sprite2d.LoadTexture(Engine.Instance.Game.Content);
                    }
                }
                else
                {
                    LogManager.Instance.WriteLineError("CharacterActor2D.Initialize(World) : can't find the animation : '" + pair.Value + "'");
                }
            }

            foreach (var pair in _animations)
            {
                //Event
                pair.Value.InitializeEvent();

                //sprite
                //Engine.Instance.Asset2DManager.AddSprite2DToLoadingList(pair.Value);
            }

            //Engine.Instance.Asset2DManager.LoadLoadingList(Engine.Instance.Game.Content);

            _animation2DPlayer = new Animation2DPlayer(_animations);
            _animation2DPlayer.OnEndAnimationReached += OnEndAnimationReached;
            //_Animation2DPlayer.OnFrameChanged += new EventHandler<Animation2DFrameChangedEventArgs>(OnFrameChanged);

            InitializePhysics(physicWorld);

            Collision2DManager.Instance.RegisterObject(this);

            _shapeRendererComponent = GameHelper.GetDrawableGameComponent<ShapeRendererComponent>(Engine.Instance.Game);

            Hp = HpMax;
            Mp = MpMax;

            //for debugging
            _whiteTexture = (Engine.Instance.Game.Services.GetService(typeof(DebugManager)) as DebugManager).WhiteTexture;
        }

        public void InitializeController()
        {
            //if (_Controller != null)
            //{
            _controller.Initialize();
            //}
        }

        void OnEndAnimationReached(object sender, EventArgs e)
        {
            _message.SenderID = Id;
            _message.RecieverID = Id; //_Animation2DPlayer.CurrentAnimation.Id ??
            _message.Type = (int)MessageType.AnimationChanged;
            //_Message.ExtraInfo = _Animation2DPlayer.CurrentAnimation.Id; // @TODO : boxing is not GC friendly

            _fsm.HandleMessage(_message);
            _controller.StateMachine.HandleMessage(_message);
        }

        /*void OnFrameChanged(object sender, Animation2DFrameChangedEventArgs e)
        {
            
        }*/

        private void InitializePhysics(FarseerPhysics.Dynamics.World physicWorld)
        {
            if (physicWorld != null)
            {
                _body = FarseerPhysics.Factories.BodyFactory.CreateCircle(
                physicWorld,
                30.0f,
                0.00001f,
                _initialPosition,
                this);

                _body.BodyType = BodyType.Dynamic;
                _body.SleepingAllowed = true;
                _body.FixedRotation = true;
                _body.Friction = 0.0f;
                _body.Restitution = 0.0f;
            }
            else
            {
                _position = _initialPosition;
            }
        }

        public override void Update(float elapsedTime)
        {
            //if (_Controller != null)
            //{
            _controller.Update(elapsedTime);
            //}

            MoveCharacter(elapsedTime);
            _animation2DPlayer.Update(elapsedTime);
        }

        public void Draw(float elapsedTime)
        {
            _renderer2DComponent.AddSprite2D(
                _animation2DPlayer.CurrentAnimation.CurrentSpriteId,
                Position, 0.0f, Vector2.One, Color.White,
                1 - Position.Y / Engine.Instance.Game.GraphicsDevice.Viewport.Height,
                SpriteEffects);

            if (ShapeRendererComponent.DisplayCollisions)
            {
                var geometry2DObjectList = Shape2DObjectList;
                if (geometry2DObjectList != null)
                {
                    foreach (var g in geometry2DObjectList)
                    {
                        _shapeRendererComponent.AddShape2DObject(g, g.Flag == 0 ? Color.Green : Color.Red);
                    }
                }
            }

            if (DisplayDebugInformation)
            {
                //display HP
                _renderer2DComponent.AddSprite2D(
                    _whiteTexture,
                    Position + new Vector2(-50.0f, 10.0f),
                    0.0f,
                    new Vector2(100.0f, 5.0f), // scale
                    Color.Red, 0.00001f, SpriteEffects.None);

                _renderer2DComponent.AddSprite2D(
                    _whiteTexture,
                    Position + new Vector2(-50.0f, 10.0f),
                    0.0f,
                    new Vector2((float)Hp / (float)HpMax * 100.0f, 5.0f), // scale
                    Color.Green, 0.0f, SpriteEffects.None);

                //display direction
                var rr = _velocity;
                if (rr != Vector2.Zero)
                {
                    rr.Normalize();
                }
                _renderer2DComponent.AddLine2D(Position, Position + rr * 80.0f, Color.Blue);

                //for debugging State
                /*if (_Controller != null)
                {
                    _Renderer2DComponent.AddText2D(
                        Engine.Instance.DefaultSpriteFont,
                        "State : " + _Controller.StateMachine.CurrentState.Name,
                        position + new Vector2(-100.0f, -130.0f),
                        0.0f, Vector2.One, Color.Green, 0.0f);
                }

                if (_Body != null)
                {
                    _Renderer2DComponent.AddText2D(
                        Engine.Instance.DefaultSpriteFont,
                        "Speed : " + _Body.LinearVelocity.Length(),
                        position + new Vector2(-70.0f, -100.0f),
                        0.0f, Vector2.One, Color.White, 0.0f);
                }*/
            }
        }

        public override void Load(XmlElement el, SaveOption opt)
        {
            base.Load(el, opt);

            var statusNode = (XmlElement)el.SelectSingleNode("Status");

            Speed = float.Parse(statusNode.Attributes["speed"].Value);
            Strength = int.Parse(statusNode.Attributes["strength"].Value);
            Defense = int.Parse(statusNode.Attributes["defense"].Value);
            HpMax = int.Parse(statusNode.Attributes["HPMax"].Value);
            MpMax = int.Parse(statusNode.Attributes["MPMax"].Value);

            var animListNode = (XmlElement)el.SelectSingleNode("AnimationList");

            _animations.Clear();
            _animationListToLoad.Clear();

            foreach (XmlNode node in animListNode.ChildNodes)
            {
                _animationListToLoad.Add(int.Parse(node.Attributes["index"].Value), node.Attributes["name"].Value);
            }
        }


        public void SetPosition(Vector2 pos)
        {
            if (_body == null)
            {
                _position = pos;
            }
            else
            {
                _body.Position = pos;
            }
        }

        public void Move(ref Vector2 dir)
        {
            if (dir == Vector2.Zero)
            {
                //always when Vector2.Zero to stop movement
                //else if contact the character will continue to move
                _body.ResetDynamics();
            }
            else
            {
                Direction = dir;
            }

            _velocity = dir * (Speed + SpeedOffSet);
        }

        private void MoveCharacter(float elapsedTime)
        {
            if (_body == null)
            {
                _position += _velocity * elapsedTime;
            }
            else
            {
                _body.LinearVelocity = _velocity;
            }
        }



        public void SetAnimationParameters(int numberOfDirectionAnimation, int animationDirectionMask)
        {
            _numberOfDirection = numberOfDirectionAnimation;
            _animationDirectioMask = animationDirectionMask;
        }

        public void SetAnimationDirectionOffset(CharacterActor2DOrientation dir, int offset)
        {
            _animationDirectionOffset[(int)dir] = offset;
        }

        public void SetCurrentAnimation(int index)
        {
            _animation2DPlayer.SetCurrentAnimationById(index * _numberOfDirection + GetAnimationDirectionOffset());
        }

        public void SetCurrentAnimation(string name)
        {
            _animation2DPlayer.SetCurrentAnimationByName(name);
        }

        private int GetAnimationDirectionOffset()
        {
#if DEBUG
            var val = (int)_orientation & _animationDirectioMask;
#endif
            return _animationDirectionOffset[(int)_orientation & _animationDirectioMask];
        }

        public virtual void Hit(HitInfo info)
        {
            var a = (CharacterActor2D)info.ActorAttacking;

            AddHitEffect(ref info.ContactPoint);

            var cost = a.Strength - Defense;
            cost = cost < 0 ? 0 : cost;
            Hp -= cost;

            if (Hp <= 0)
            {
                a.KillSomeone(info);
                ToBeRemoved = true;

                //to delete : debug
                //respawn
                Hp = HpMax;
                SetPosition(Vector2.One * 10.0f);
                _controller.StateMachine.Transition(_controller.GetState(0));
            }
        }

        public void AddHitEffect(ref Vector2 contactPoint)
        {

        }

        public virtual void KillSomeone(HitInfo info)
        {
            if (IsPLayer)
            {
                //GameInfo.Instance.WorldInfo.BotKilled++;
            }
        }




        public Shape2DObject[] Shape2DObjectList
        {
            get
            {
                var sprite = (Sprite2D)Engine.Instance.ObjectManager.GetObjectById(_animation2DPlayer.CurrentAnimation.CurrentSpriteId);

                //return Manager.CreateShape2DFromSprite2D(id, position, SpriteEffects);

                Shape2DObject[] res = null;

                if (sprite.Collisions != null)
                {
                    res = sprite.Collisions.ToArray();

                    for (var i = 0; i < res.Length; i++)
                    {
                        var g = res[i].Clone();

                        //_PointTmp.X = g.Location.X + (int)(position.X - sprite.HotSpot.X);
                        //_PointTmp.Y = g.Location.Y + (int)(position.Y - sprite.HotSpot.Y);

                        if (SpriteEffects == SpriteEffects.FlipHorizontally)
                        {
                            _pointTmp.X = (int)Position.X - g.Location.X + (int)(sprite.HotSpot.X);
                            _pointTmp.Y = g.Location.Y + (int)(Position.Y - sprite.HotSpot.Y);
                        }
                        else if (SpriteEffects == SpriteEffects.FlipVertically)
                        {
                            _pointTmp.X = g.Location.X + (int)(Position.X - sprite.HotSpot.X);
                            _pointTmp.Y = (int)Position.Y - g.Location.Y + (int)(sprite.HotSpot.Y);
                        }
                        else
                        {
                            _pointTmp.X = g.Location.X + (int)(Position.X - sprite.HotSpot.X);
                            _pointTmp.Y = g.Location.Y + (int)(Position.Y - sprite.HotSpot.Y);
                        }

                        g.Location = _pointTmp;

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

        public bool CanAttackHim(IAttackable other)
        {
            if (_alreadyAttacked.Contains(other) == false)
            {
                return TeamInfo.CanAttack(other.TeamInfo);
            }

            return false;
        }

        public void DoANewAttack()
        {
            _alreadyAttacked.Clear();
        }



        public bool HandleMessage(Message message)
        {
            switch (message.Type)
            {
                case (int)MessageType.Hit:
                    Hit((HitInfo)message.ExtraInfo);
                    break;

                case (int)MessageType.HitSomeone:
                    var hitInfo = (HitInfo)message.ExtraInfo;
                    _alreadyAttacked.Add(hitInfo.ActorHit as ICollide2Dable);
                    break;
            }

            _fsm.HandleMessage(message);

            if (_controller != null)
            {
                _controller.StateMachine.HandleMessage(message);
            }

            return true;
        }



        public static CharacterActor2DOrientation GetCharacterDirectionFromVector2(Vector2 v)
        {
            var deadzone = 0.2f;
            CharacterActor2DOrientation dir = 0;

            if (v.X < -deadzone)
            {
                dir |= CharacterActor2DOrientation.Left;
            }
            else if (v.X > deadzone)
            {
                dir |= CharacterActor2DOrientation.Right;
            }

            if (v.Y < -deadzone)
            {
                dir |= CharacterActor2DOrientation.Up;
            }
            else if (v.Y > deadzone)
            {
                dir |= CharacterActor2DOrientation.Down;
            }

            return dir;
        }

    }
}
