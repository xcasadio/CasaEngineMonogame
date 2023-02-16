using System.Xml;
using CasaEngine.Entities;
using CasaEngineCommon.Extension;
using CasaEngine.Gameplay.Actor.Event;
using CasaEngineCommon.Design;

namespace CasaEngine.Assets.Graphics2D
{
    public partial class Animation2D
    {

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

    }
}
