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

        private string m_Name = string.Empty;
#if EDITOR
        private readonly List<Frame2D> m_Frames = new List<Frame2D>();
#else
        private Frame2D[] m_Frames;
#endif

        private float m_TotalTime = 0;
        private Animation2DType m_Animation2DType = Animation2DType.Loop;

        private float m_CurrentTime = 0;
        private int m_CurrentFrame = 0;
        private bool m_EndAnimReached = false;

        public event EventHandler<Animation2DFrameChangedEventArgs> OnFrameChanged;
        public event EventHandler OnEndAnimationReached;

        private readonly Animation2DFrameChangedEventArgs m_Animation2DFrameChangedEventArgs = new Animation2DFrameChangedEventArgs(null, 0, 0);



        public string Name
        {
            get { return m_Name; }
#if EDITOR
            set { m_Name = value; }
#endif
        }

        public Animation2DType Animation2DType
        {
            get { return m_Animation2DType; }
#if EDITOR
            set { m_Animation2DType = value; }
#endif
        }

        public int CurrentFrameIndex => m_CurrentFrame;

        public int CurrentSpriteId => m_Frames[m_CurrentFrame].spriteID;

        public float TotalTime => m_TotalTime;


        public Animation2D(XmlElement xmlNode_, SaveOption option_)
        {
            Load(xmlNode_, option_);
        }

#if EDITOR
        public
#else
		private
#endif
        Animation2D()
        {
#if EDITOR
            //m_ID = m_UnusedID++;
            ComputeTotalTime();
#endif
        }



        public Frame2D[] GetFrames()
        {
#if EDITOR
            return m_Frames.ToArray();
#else
            return m_Frames;
#endif
        }

        public void ComputeTotalTime()
        {
            m_TotalTime = 0;

            foreach (Frame2D frame in m_Frames)
            {
                m_TotalTime += frame.time;
            }
        }

        public void Update(float elapsedTime_)
        {
            m_CurrentTime += elapsedTime_;
            ComputeCurrentFrame();
        }

        private void ComputeCurrentFrame()
        {
            bool endAnim = false;

            switch (m_Animation2DType)
            {
                case Animation2DType.Loop:
                    float r = m_CurrentTime / m_TotalTime;

                    if (r >= 1.0f)
                    {
                        m_CurrentTime -= m_TotalTime * (float)r;
                        endAnim = true;
                    }

                    while (m_CurrentTime > m_TotalTime)
                    {
                        m_CurrentTime -= m_TotalTime;
                    }
                    break;

                case Animation2DType.Once:
                    if (m_CurrentTime >= m_TotalTime)
                    {
                        m_CurrentTime = m_TotalTime;
                        endAnim = m_EndAnimReached == false;
                        m_EndAnimReached = true;
                    }
                    break;

                case Animation2DType.PingPong:
                    throw new NotImplementedException("Animation2D.ComputeCurrentFrame() : Animation2DType.PingPong is not supported");

                default:
                    throw new NotImplementedException("Animation2D.ComputeCurrentFrame() : Animation2DType '" + Enum.GetName(typeof(Animation2DType), m_Animation2DType) + "' is not supported");
            }

            if (endAnim == true
                && OnEndAnimationReached != null)
            {
                OnEndAnimationReached(this, EventArgs.Empty);
            }

            int index = 0;
            float time = 0;

            foreach (Frame2D frame in m_Frames)
            {
                time += frame.time;

                if (time >= m_CurrentTime)
                {
                    break;
                }

                index++;
            }

            if (index != m_CurrentFrame)
            {
                int old = m_CurrentFrame;
                m_CurrentFrame = index;

                //@todo : check if skip frame
                //and active event from the skipped frame
#if !EDITOR
                foreach (EventActor e in m_Frames[m_CurrentFrame].Events)
                {
                    e.Do();
                }
#endif

                if (OnFrameChanged != null)
                {
                    m_Animation2DFrameChangedEventArgs.Animation2D = this;
                    m_Animation2DFrameChangedEventArgs.OldFrame = old;
                    m_Animation2DFrameChangedEventArgs.NewFrame = m_CurrentFrame;
                    OnFrameChanged.Invoke(this, m_Animation2DFrameChangedEventArgs);
                }
            }
        }

        public Sprite2D GetCurrentSprite()
        {
            int id = m_Frames[m_CurrentFrame].spriteID;
            return Engine.Instance.Asset2DManager.GetSprite2DByID(id);
        }

        public void ResetTime()
        {
            m_CurrentTime = 0;
            m_CurrentFrame = 0;
            m_EndAnimReached = false;
        }

        public override void Load(XmlElement node_, SaveOption option_)
        {
            base.Load(node_, option_);

            XmlNode animNode = node_.SelectSingleNode("Animation2D");

            uint version = uint.Parse(animNode.Attributes["version"].Value);

            //m_ID = uint.Parse(node_.Attributes["id"].Value);
            m_Name = animNode.Attributes["name"].Value;

            m_Animation2DType = (Animation2DType)Enum.Parse(typeof(Animation2DType), animNode.Attributes["type"].Value);

            int spriteID;
            float delay;
            List<EventActor> list = new List<EventActor>();

#if !EDITOR
            m_Frames = new Frame2D[animNode.SelectSingleNode("FrameList").ChildNodes.Count];
            int i = 0;
#endif

            //frames
            foreach (XmlNode node in animNode.SelectSingleNode("FrameList").ChildNodes)
            {
                spriteID = int.Parse(node.Attributes["spriteID"].Value);
                delay = float.Parse(node.Attributes["time"].Value);
                list.Clear();

                //events
                if (version >= 1)
                {
                    foreach (XmlNode eventNode in node.SelectSingleNode("EventNodeList").ChildNodes)
                    {
                        list.Add(EventActorFactory.LoadEvent((XmlElement)eventNode, option_));
                    }
                }

#if EDITOR
                AddFrame(spriteID, delay, list.ToArray());
                ComputeTotalTime();
#else
                m_Frames[i] = new Frame2D(spriteID, delay);
                m_Frames[i].Events = list.ToArray();
                i++;
#endif
            }

            ComputeTotalTime();
        }

        public override void Load(BinaryReader br_, SaveOption option_)
        {
            base.Load(br_, option_);
            throw new NotImplementedException();
        }

        public override BaseObject Clone()
        {
            Animation2D anim2D = new Animation2D();
            anim2D.m_Animation2DType = m_Animation2DType;
            anim2D.m_CurrentFrame = m_CurrentFrame;
            anim2D.m_CurrentTime = m_CurrentTime;
#if EDITOR
            anim2D.m_Frames.AddRange(m_Frames);
#else
            //anim2D.m_Frames = m_Frames;
            anim2D.m_Frames = (Frame2D[]) m_Frames.Clone();
#endif
            anim2D.m_Name = m_Name;
            anim2D.m_TotalTime = m_TotalTime;

            anim2D.CopyFrom(this);

            return anim2D;
        }

        public void InitializeEvent()
        {
            int count =
#if EDITOR
                m_Frames.Count;
#else
                m_Frames.Length;
#endif

            for (int i = 0; i < count; i++)
            {
                foreach (EventActor e in m_Frames[i].Events)
                {
                    e.Initialize();
                }
            }
        }

    }
}
