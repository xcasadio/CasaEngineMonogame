using CasaEngine.Gameplay.Actor.Event;

namespace CasaEngine.Assets.Graphics2D
{
    public struct Frame2D
    {
        public int spriteID;
        public float time;

#if EDITOR
        public List<EventActor> Events;
#else
        public EventActor[] Events;
#endif

        public Frame2D(int spriteID_, float time_)
        {
            spriteID = spriteID_;
            time = time_;

#if EDITOR
            Events = new List<EventActor>();
#else
            Events = null;
#endif
        }

#if EDITOR

        public string EventsToString()
        {
            if (Events == null)
            {
                return "0 event";
            }

            return Events.Count + " event" + (Events.Count <= 1 ? "" : "s");
        }

        public bool CompareTo(Frame2D other_)
        {
            if (spriteID != other_.spriteID
                || time != other_.time
                || Events.Count != other_.Events.Count)
            {
                return false;
            }

            for (int i = 0; i < Events.Count; i++)
            {
                if (Events[i].CompareTo(other_.Events[i]) == false)
                {
                    return false;
                }
            }

            return true;
        }
#endif
    }
}
