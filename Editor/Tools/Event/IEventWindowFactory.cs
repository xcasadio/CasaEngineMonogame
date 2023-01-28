
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Editor.Sprite2DEditor.Event;
using CasaEngine.Gameplay.Actor.Event;

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
