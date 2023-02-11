using System.Xml;
using CasaEngineCommon.Design;

namespace CasaEngine.Gameplay.Actor.Event
{
    public static class EventActorFactory
    {







        static public EventActor LoadEvent(XmlElement el, SaveOption option)
        {
            EventActorType type = (EventActorType)Enum.Parse(typeof(EventActorType), el.Attributes["type"].Value);

            switch (type)
            {
                case EventActorType.PlaySound:
                    return new PlaySoundEvent(el, option);

                case EventActorType.SpawnActor:
                    throw new NotImplementedException();
            }

            return null;
        }

    }
}
