using System.Xml;
using CasaEngine.Core.Design;

namespace CasaEngine.Framework.Gameplay.Actor.Event;

public static class EventActorFactory
{







    public static EventActor LoadEvent(XmlElement el, SaveOption option)
    {
        var type = (EventActorType)Enum.Parse(typeof(EventActorType), el.Attributes["type"].Value);

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