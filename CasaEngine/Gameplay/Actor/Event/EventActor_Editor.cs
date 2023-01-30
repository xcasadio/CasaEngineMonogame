using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;

namespace CasaEngine.Gameplay.Actor.Event
{
    public partial class EventActor
    {







        public virtual void Save(XmlElement el_, SaveOption option_)
        {
            el_.OwnerDocument.AddAttribute(el_, "type", Enum.GetName(typeof(EventActorType), m_EventActorType));
        }

        public virtual void Save(BinaryWriter bw_, SaveOption option_)
        {
            bw_.Write((int)m_EventActorType);
        }

        public virtual bool CompareTo(EventActor other_)
        {
            return m_EventActorType == other_.m_EventActorType;
        }

    }
}
