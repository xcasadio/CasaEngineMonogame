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

        private EventActorType _eventActorType;



        public EventActorType EventActorType => _eventActorType;


        protected EventActor(EventActorType type)
        {
            _eventActorType = type;
        }

        protected EventActor(XmlElement el, SaveOption option)
        {
            Load(el, option);
        }



        public abstract void Initialize();

        public abstract void Do();

        public virtual void Load(XmlElement el, SaveOption option)
        {
            _eventActorType = (EventActorType)Enum.Parse(typeof(EventActorType), el.Attributes["type"].Value);
        }

        public virtual void Load(BinaryReader br, SaveOption option)
        {
            _eventActorType = (EventActorType)Enum.Parse(typeof(EventActorType), br.ReadString());
        }

    }
}
