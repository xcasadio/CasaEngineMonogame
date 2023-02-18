using CasaEngine.Framework.Gameplay.Actor.Event;

namespace Editor.Tools.Event
{
    /// <summary>
    /// 
    /// </summary>
    public interface IEventWindowFactory
    {
        IAnimationEventBaseWindow CreateNewForm(EventActor event_);
    }
}
