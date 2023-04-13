using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Extension;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Gameplay.Actor.Event;
#if EDITOR
#endif

namespace CasaEngine.Framework.Assets.Graphics2D
{
    public
#if EDITOR
    partial
#endif
    class Animation2D
        : Entity, ISaveLoad
    {

        private string _name = string.Empty;
#if EDITOR
        private readonly List<Frame2D> _frames = new();
#else
        private Frame2D[] _frames;
#endif

        private float _totalTime;
        private Animation2DType _animation2DType = Animation2DType.Loop;

        private float _currentTime;
        private int _currentFrame;
        private bool _endAnimReached;

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
            return _frames;
#endif
        }

        public void ComputeTotalTime()
        {
            _totalTime = 0;

            foreach (var frame in _frames)
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
            var endAnim = false;

            switch (_animation2DType)
            {
                case Animation2DType.Loop:
                    var r = _currentTime / _totalTime;

                    if (r >= 1.0f)
                    {
                        _currentTime -= _totalTime * r;
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

            if (endAnim
                && OnEndAnimationReached != null)
            {
                OnEndAnimationReached(this, EventArgs.Empty);
            }

            var index = 0;
            float time = 0;

            foreach (var frame in _frames)
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
                var old = _currentFrame;
                _currentFrame = index;

                //@todo : check if skip frame
                //and active event from the skipped frame
#if !EDITOR
                foreach (EventActor e in _frames[_currentFrame].Events)
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
            var id = _frames[_currentFrame].SpriteId;
            return EngineComponents.Asset2dManager.GetSprite2DById(id);
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

            var animNode = element.SelectSingleNode("Animation2D");

            var version = uint.Parse(animNode.Attributes["version"].Value);

            //_ID = uint.Parse(node_.Attributes["id"].Value);
            _name = animNode.Attributes["name"].Value;

            _animation2DType = (Animation2DType)Enum.Parse(typeof(Animation2DType), animNode.Attributes["type"].Value);

            int spriteId;
            float delay;
            var list = new List<EventActor>();

#if !EDITOR
            _frames = new Frame2D[animNode.SelectSingleNode("FrameList").ChildNodes.Count];
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
                _frames[i] = new Frame2D(spriteId, delay);
                _frames[i].Events = list.ToArray();
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

        public Entity Clone()
        {
            var anim2D = new Animation2D();
            anim2D._animation2DType = _animation2DType;
            anim2D._currentFrame = _currentFrame;
            anim2D._currentTime = _currentTime;
#if EDITOR
            anim2D._frames.AddRange(_frames);
#else
            //anim2D._Frames = _Frames;
            anim2D._frames = (Frame2D[])_frames.Clone();
#endif
            anim2D._name = _name;
            anim2D._totalTime = _totalTime;

            anim2D.CopyFrom(this);

            return anim2D;
        }

        public void InitializeEvent()
        {
            var count =
#if EDITOR
                _frames.Count;
#else
                _frames.Length;
#endif

            for (var i = 0; i < count; i++)
            {
                foreach (var e in _frames[i].Events)
                {
                    e.Initialize();
                }
            }
        }

#if EDITOR
        private static readonly uint Version = 1;
        //static private uint _UnusedID = 0;

        public int FrameCount => _frames.Count;

        public void AddFrame(int spriteId, float delay, EventActor[] events)
        {
            var frame = new Frame2D(spriteId, delay);
#if EDITOR
            if (events != null)
            {
                frame.Events.AddRange(events);
            }
#else
            frame.Events = events_;
#endif
            _frames.Add(frame);
            ComputeTotalTime();
        }

        public float GetFrameTime(int frameIndex)
        {
            return _frames[frameIndex].Time;
        }

        public void SetTime(float totalElapsedTime)
        {
            _currentTime = totalElapsedTime;
            ComputeCurrentFrame();
        }

        public void DeleteFrame(int index)
        {
            _frames.RemoveAt(index);
            ComputeTotalTime();
        }

        public void SetFrameDelay(int frameIndex, float delay)
        {
            var frame = _frames[frameIndex];
            frame.Time = delay;
            _frames[frameIndex] = frame;
            ComputeTotalTime();
        }

        public void SetFrameEvents(int frameIndex, List<EventActor> eventList)
        {
            var frame = _frames[frameIndex];
            frame.Events = eventList;
            _frames[frameIndex] = frame;
        }

        public void SetCurrentFrame(int frameIndex)
        {
            _currentTime = _frames[frameIndex].Time;
            _currentFrame = frameIndex;
        }

        public int GetCurrentFrameIndex()
        {
            return _currentFrame;
        }

        public void SetFrameSprite2D(int sprite2Did, int frameIndex)
        {
            var frame = _frames[frameIndex];
            frame.SpriteId = sprite2Did;
            _frames[frameIndex] = frame;
        }

        public override void Save(XmlElement el, SaveOption option)
        {
            base.Save(el, option);

            var animNode = el.OwnerDocument.CreateElement("Animation2D");
            el.AppendChild(animNode);

            el.OwnerDocument.AddAttribute(animNode, "version", Version.ToString());
            el.OwnerDocument.AddAttribute(animNode, "name", _name);
            el.OwnerDocument.AddAttribute(animNode, "type", Enum.GetName(typeof(Animation2DType), _animation2DType));

            var frameListNode = el.OwnerDocument.CreateElement("FrameList");
            animNode.AppendChild(frameListNode);

            for (var i = 0; i < _frames.Count; i++)
            {
                var frameNode = el.OwnerDocument.CreateElement("Frame");
                el.OwnerDocument.AddAttribute(frameNode, "spriteID", _frames[i].SpriteId.ToString());
                el.OwnerDocument.AddAttribute(frameNode, "time", GetFrameTime(i).ToString());

                //events
                var eventListNode = el.OwnerDocument.CreateElement("EventNodeList");
                frameNode.AppendChild(eventListNode);
                foreach (var e in _frames[i].Events)
                {
                    var eventNode = el.OwnerDocument.CreateElement("EventNode");
                    eventListNode.AppendChild(eventNode);
                    e.Save(eventNode, option);
                }

                frameListNode.AppendChild(frameNode);
            }
        }

        public override void Save(BinaryWriter bw, SaveOption option)
        {
            base.Save(bw, option);

            bw.Write(Version);
            bw.Write(_name);
            bw.Write((int)_animation2DType);
            bw.Write(_frames.Count);

            for (var i = 0; i < _frames.Count; i++)
            {
                bw.Write(_frames[i].SpriteId);
                bw.Write(GetFrameTime(i));
                bw.Write(_frames[i].Events.Count);

                foreach (var e in _frames[i].Events)
                {
                    e.Save(bw, option);
                }
            }
        }

        public void MoveFrameForward(int index)
        {
            if (index < _frames.Count - 1)
            {
                var frameTmp = _frames[index + 1];
                _frames[index + 1] = _frames[index];
                _frames[index] = frameTmp;
            }
        }

        public void MoveFrameBackward(int index)
        {
            if (index > 0)
            {
                var frameTmp = _frames[index - 1];
                _frames[index - 1] = _frames[index];
                _frames[index] = frameTmp;
            }
        }

        public bool CompareTo(Entity other)
        {
            if (other is Animation2D)
            {
                var o = other as Animation2D;

                if (_animation2DType != o._animation2DType
                    || _frames.Count != o._frames.Count)
                {
                    return false;
                }

                for (var i = 0; i < _frames.Count; i++)
                {
                    if (_frames[i].CompareTo(o._frames[i]) == false)
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }
#endif
    }
}
