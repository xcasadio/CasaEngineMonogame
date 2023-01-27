using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Design.Event;
using System.Xml;
using CasaEngineCommon.Extension;
using System.IO;
using CasaEngineCommon.Design;

namespace CasaEngine.Gameplay.Actor.Event
{
    /// <summary>
    /// 
    /// </summary>
    public abstract
#if EDITOR
    partial 
#endif
    class EventActor
        : IEvent, ISaveLoad
    {
        #region Fields

        private EventActorType m_EventActorType;

        #endregion

        #region Properties

        /// <summary>
        /// 
        /// </summary>
        public EventActorType EventActorType
        {
            get { return m_EventActorType; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type_"></param>
        protected EventActor(EventActorType type_)
        {
            m_EventActorType = type_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        protected EventActor(XmlElement el_, SaveOption option_)
        {
            Load( el_, option_);
        }

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        public abstract void Initialize();

        /// <summary>
        /// 
        /// </summary>
        public abstract void Do();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public virtual void Load(XmlElement el_, SaveOption option_)
        {
            m_EventActorType = (EventActorType) Enum.Parse(typeof(EventActorType), el_.Attributes["type"].Value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="br_"></param>
        /// <param name="option_"></param>
        public virtual void Load(BinaryReader br_, SaveOption option_)
        {
            m_EventActorType = (EventActorType)Enum.Parse(typeof(EventActorType), br_.ReadString());
        }

        #endregion
    }
}
