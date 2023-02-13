using CasaEngine.Gameplay.Actor.Event;

namespace Editor.Tools.Event.EventForm
{
    /// <summary>
    /// 
    /// </summary>
    internal class SoundEventWindowFactory
        : IEventWindowFactory
    {


        /// <summary>
        /// 
        /// </summary>
        /// <param name="event_"></param>
        /// <returns></returns>
        public IAnimationEventBaseWindow CreateNewForm(EventActor event_)
        {
            return new AnimationSoundEventForm(event_);
        }

    }
}
