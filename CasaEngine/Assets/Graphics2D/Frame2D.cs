using CasaEngine.Gameplay.Actor.Event;

namespace CasaEngine.Assets.Graphics2D
{
    public struct Frame2D
    {
        public int SpriteId;
        public float Time;

#if EDITOR
        public List<EventActor> Events;
#else
        public EventActor[] Events;
#endif

        public Frame2D(int spriteId, float time)
        {
            SpriteId = spriteId;
            Time = time;

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

        public bool CompareTo(Frame2D other)
        {
            if (SpriteId != other.SpriteId
                || Time != other.Time
                || Events.Count != other.Events.Count)
            {
                return false;
            }

            for (int i = 0; i < Events.Count; i++)
            {
                if (Events[i].CompareTo(other.Events[i]) == false)
                {
                    return false;
                }
            }

            return true;
        }
#endif
    }
}
