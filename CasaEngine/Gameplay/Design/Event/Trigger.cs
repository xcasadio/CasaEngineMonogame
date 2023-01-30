using System.Xml;
using CasaEngineCommon.Design;


namespace CasaEngine.Design.Event
{
    public abstract
#if EDITOR
    partial
#endif
    class Trigger
    {

        private readonly List<TriggerEvent> m_Events = new List<TriggerEvent>();
        private bool m_Activated = false;
        private int m_IterationMax = 1;
        private int m_Iteration = 0;



        public bool Activated => m_Activated;

        public int IterationMax
        {
            get => m_IterationMax;
            set => m_IterationMax = value;
        }

#if EDITOR
        public
#else
		protected
#endif
        List<TriggerEvent> Events
        {
            get { return m_Events; }
        }



        public Trigger()
        {
        }

        public Trigger(int iteration_)
        {
            m_IterationMax = iteration_;
        }

        public Trigger(XmlElement el_, SaveOption option_)
        {
            Load(el_, option_);
        }



        public virtual void Load(XmlElement el_, SaveOption option_)
        {
            uint loadedVersion = uint.Parse(el_.Attributes["version"].Value);

            foreach (XmlNode node in el_.SelectSingleNode("EventList").ChildNodes)
            {
                string assemblyFullName = node.Attributes["assemblyName"].Value;
                string typeFullName = node.Attributes["fullName"].Value;

                Type t = Type.GetType(typeFullName);
                TriggerEvent ev = (TriggerEvent)Activator.CreateInstance(t
#if EDITOR
                , new object[] { el_, option_ }
#endif
                );

                m_Events.Add(ev);
            }
        }

        public virtual void Load(BinaryReader br_, SaveOption option_)
        {

        }

        public abstract bool Activate(float TotalElapsedTime_);

        public void AddEvent(TriggerEvent event_)
        {
            m_Events.Add(event_);
        }

        public void Update(float TotalElapsedTime_)
        {
            if (Activate(TotalElapsedTime_) == true && (m_Iteration < m_IterationMax || m_IterationMax == -1))
            {
                foreach (TriggerEvent ev in m_Events.ToArray())
                {
                    ev.Do();
                }

                m_Activated = true;
                m_Iteration++;
            }
        }

        public void Reset()
        {
            m_Iteration = 0;
            m_Activated = false;
        }

    }
}
