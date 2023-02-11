using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;

namespace CasaEngine.Gameplay.Actor.Event
{
    public partial class EventActor
    {







        public virtual void Save(XmlElement el, SaveOption option)
        {
            el.OwnerDocument.AddAttribute(el, "type", Enum.GetName(typeof(EventActorType), _eventActorType));
        }

        public virtual void Save(BinaryWriter bw, SaveOption option)
        {
            bw.Write((int)_eventActorType);
        }

        public virtual bool CompareTo(EventActor other)
        {
            return _eventActorType == other._eventActorType;
        }

    }
}
