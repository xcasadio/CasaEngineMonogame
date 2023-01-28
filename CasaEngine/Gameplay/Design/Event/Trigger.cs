

using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using CasaEngine;
using CasaEngineCommon.Design;


namespace CasaEngine.Design.Event
{
    /// <summary>
    /// 
    /// </summary>
    public abstract
#if EDITOR
    partial
#endif
    class Trigger
    {

        private List<TriggerEvent> m_Events = new List<TriggerEvent>();
        private bool m_Activated = false;
        private int m_IterationMax = 1;
        private int m_Iteration = 0;



        /// <summary>
        /// Gets
        /// </summary>
        public bool Activated
        {
            get { return m_Activated; }
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public int IterationMax
        {
            get { return m_IterationMax; }
            set { m_IterationMax = value; }
        }

        /// <summary>
        /// Gets
        /// </summary>
#if EDITOR
        public
#else
		protected
#endif
        List<TriggerEvent> Events
        {
            get { return m_Events; }
        }



        /// <summary>
        /// 
        /// </summary>
        public Trigger()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="event_"></param>
        public Trigger(int iteration_)
        {
            m_IterationMax = iteration_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public Trigger(XmlElement el_, SaveOption option_)
        {
            Load(el_, option_);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public virtual void Load(BinaryReader br_, SaveOption option_)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elapsedTime_"></param>
        public abstract bool Activate(float TotalElapsedTime_);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="event_"></param>
        public void AddEvent(TriggerEvent event_)
        {
            m_Events.Add(event_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="TotalElapsedTime_"></param>
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

        /// <summary>
        /// 
        /// </summary>
        public void Reset()
        {
            m_Iteration = 0;
            m_Activated = false;
        }

    }
}
