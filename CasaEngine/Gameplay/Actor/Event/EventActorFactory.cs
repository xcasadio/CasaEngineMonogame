using System.Xml;
using CasaEngineCommon.Design;

namespace CasaEngine.Gameplay.Actor.Event
{
    public static class EventActorFactory
    {







        static public EventActor LoadEvent(XmlElement el_, SaveOption option_)
        {
            EventActorType type = (EventActorType)Enum.Parse(typeof(EventActorType), el_.Attributes["type"].Value);

            switch (type)
            {
                case EventActorType.PlaySound:
                    return new PlaySoundEvent(el_, option_);

                case EventActorType.SpawnActor:
                    throw new NotImplementedException();
            }

            return null;
        }

    }
}
