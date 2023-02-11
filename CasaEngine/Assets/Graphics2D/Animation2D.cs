using System.Xml;
using CasaEngine.Game;
using CasaEngine.Gameplay.Actor.Event;
using CasaEngineCommon.Design;
using CasaEngine.Gameplay.Actor.Object;

#if EDITOR
#endif

namespace CasaEngine.Assets.Graphics2D
{
    public
#if EDITOR
    partial
#endif
    class Animation2D
        : BaseObject, ISaveLoad
    {

        private string _name = string.Empty;
#if EDITOR
        private readonly List<Frame2D> _frames = new();
#else
        private Frame2D[] _Frames;
#endif

        private float _totalTime = 0;
        private Animation2DType _animation2DType = Animation2DType.Loop;

        private float _currentTime = 0;
        private int _currentFrame = 0;
        private bool _endAnimReached = false;

        public event EventHandler<Animation2DFrameChangedEventArgs> OnFrameChanged;
        public event EventHandler OnEndAnimationReached;

        private readonly Animation2DFrameChangedEventArgs _animation2DFrameChangedEventArgs = new(null, 0, 0);



        public string Name
        {
            get { return _name; }
#if EDITOR
            set { _name = value; }
#endif
        }

        public Animation2DType Animation2DType
        {
            get { return _animation2DType; }
#if EDITOR
            set { _animation2DType = value; }
#endif
        }

        public int CurrentFrameIndex => _currentFrame;

        public int CurrentSpriteId => _frames[_currentFrame].SpriteId;

        public float TotalTime => _totalTime;


        public Animation2D(XmlElement xmlNode, SaveOption option)
        {
            Load(xmlNode, option);
        }

#if EDITOR
        public
#else
		private
#endif
        Animation2D()
        {
#if EDITOR
            //_ID = _UnusedID++;
            ComputeTotalTime();
#endif
        }



        public Frame2D[] GetFrames()
        {
#if EDITOR
            return _frames.ToArray();
#else
            return _Frames;
#endif
        }

        public void ComputeTotalTime()
        {
            _totalTime = 0;

            foreach (Frame2D frame in _frames)
            {
                _totalTime += frame.Time;
            }
        }

        public void Update(float elapsedTime)
        {
            _currentTime += elapsedTime;
            ComputeCurrentFrame();
        }

        private void ComputeCurrentFrame()
        {
            bool endAnim = false;

            switch (_animation2DType)
            {
                case Animation2DType.Loop:
                    float r = _currentTime / _totalTime;

                    if (r >= 1.0f)
                    {
                        _currentTime -= _totalTime * (float)r;
                        endAnim = true;
                    }

                    while (_currentTime > _totalTime)
                    {
                        _currentTime -= _totalTime;
                    }
                    break;

                case Animation2DType.Once:
                    if (_currentTime >= _totalTime)
                    {
                        _currentTime = _totalTime;
                        endAnim = _endAnimReached == false;
                        _endAnimReached = true;
                    }
                    break;

                case Animation2DType.PingPong:
                    throw new NotImplementedException("Animation2D.ComputeCurrentFrame() : Animation2DType.PingPong is not supported");

                default:
                    throw new NotImplementedException("Animation2D.ComputeCurrentFrame() : Animation2DType '" + Enum.GetName(typeof(Animation2DType), _animation2DType) + "' is not supported");
            }

            if (endAnim == true
                && OnEndAnimationReached != null)
            {
                OnEndAnimationReached(this, EventArgs.Empty);
            }

            int index = 0;
            float time = 0;

            foreach (Frame2D frame in _frames)
            {
                time += frame.Time;

                if (time >= _currentTime)
                {
                    break;
                }

                index++;
            }

            if (index != _currentFrame)
            {
                int old = _currentFrame;
                _currentFrame = index;

                //@todo : check if skip frame
                //and active event from the skipped frame
#if !EDITOR
                foreach (EventActor e in _Frames[_CurrentFrame].Events)
                {
                    e.Do();
                }
#endif

                if (OnFrameChanged != null)
                {
                    _animation2DFrameChangedEventArgs.Animation2D = this;
                    _animation2DFrameChangedEventArgs.OldFrame = old;
                    _animation2DFrameChangedEventArgs.NewFrame = _currentFrame;
                    OnFrameChanged.Invoke(this, _animation2DFrameChangedEventArgs);
                }
            }
        }

        public Sprite2D GetCurrentSprite()
        {
            int id = _frames[_currentFrame].SpriteId;
            return Engine.Instance.Asset2DManager.GetSprite2DById(id);
        }

        public void ResetTime()
        {
            _currentTime = 0;
            _currentFrame = 0;
            _endAnimReached = false;
        }

        public override void Load(XmlElement element, SaveOption option)
        {
            base.Load(element, option);

            XmlNode animNode = element.SelectSingleNode("Animation2D");

            uint version = uint.Parse(animNode.Attributes["version"].Value);

            //_ID = uint.Parse(node_.Attributes["id"].Value);
            _name = animNode.Attributes["name"].Value;

            _animation2DType = (Animation2DType)Enum.Parse(typeof(Animation2DType), animNode.Attributes["type"].Value);

            int spriteId;
            float delay;
            List<EventActor> list = new List<EventActor>();

#if !EDITOR
            _Frames = new Frame2D[animNode.SelectSingleNode("FrameList").ChildNodes.Count];
            int i = 0;
#endif

            //frames
            foreach (XmlNode node in animNode.SelectSingleNode("FrameList").ChildNodes)
            {
                spriteId = int.Parse(node.Attributes["spriteID"].Value);
                delay = float.Parse(node.Attributes["time"].Value);
                list.Clear();

                //events
                if (version >= 1)
                {
                    foreach (XmlNode eventNode in node.SelectSingleNode("EventNodeList").ChildNodes)
                    {
                        list.Add(EventActorFactory.LoadEvent((XmlElement)eventNode, option));
                    }
                }

#if EDITOR
                AddFrame(spriteId, delay, list.ToArray());
                ComputeTotalTime();
#else
                _Frames[i] = new Frame2D(spriteID, delay);
                _Frames[i].Events = list.ToArray();
                i++;
#endif
            }

            ComputeTotalTime();
        }

        public override void Load(BinaryReader br, SaveOption option)
        {
            base.Load(br, option);
            throw new NotImplementedException();
        }

        public override BaseObject Clone()
        {
            Animation2D anim2D = new Animation2D();
            anim2D._animation2DType = _animation2DType;
            anim2D._currentFrame = _currentFrame;
            anim2D._currentTime = _currentTime;
#if EDITOR
            anim2D._frames.AddRange(_frames);
#else
            //anim2D._Frames = _Frames;
            anim2D._Frames = (Frame2D[]) _Frames.Clone();
#endif
            anim2D._name = _name;
            anim2D._totalTime = _totalTime;

            anim2D.CopyFrom(this);

            return anim2D;
        }

        public void InitializeEvent()
        {
            int count =
#if EDITOR
                _frames.Count;
#else
                _Frames.Length;
#endif

            for (int i = 0; i < count; i++)
            {
                foreach (EventActor e in _frames[i].Events)
                {
                    e.Initialize();
                }
            }
        }

    }
}
