using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Framework.Gameplay.Design.Event;

namespace CasaEngine.Framework.Gameplay.Actor.Event
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
