using CasaEngine.Design.Event;
using System.Xml;
using CasaEngineCommon.Design;

namespace CasaEngine.Gameplay.Actor.Event
{
    public abstract
#if EDITOR
    partial
#endif
    class EventActor
        : IEvent, ISaveLoad
    {

        private EventActorType m_EventActorType;



        public EventActorType EventActorType => m_EventActorType;


        protected EventActor(EventActorType type_)
        {
            m_EventActorType = type_;
        }

        protected EventActor(XmlElement el_, SaveOption option_)
        {
            Load(el_, option_);
        }



        public abstract void Initialize();

        public abstract void Do();

        public virtual void Load(XmlElement el_, SaveOption option_)
        {
            m_EventActorType = (EventActorType)Enum.Parse(typeof(EventActorType), el_.Attributes["type"].Value);
        }

        public virtual void Load(BinaryReader br_, SaveOption option_)
        {
            m_EventActorType = (EventActorType)Enum.Parse(typeof(EventActorType), br_.ReadString());
        }

    }
}
