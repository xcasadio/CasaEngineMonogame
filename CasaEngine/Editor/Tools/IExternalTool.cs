using CasaEngine.Gameplay.Actor.Object;

namespace CasaEngine.Editor.Tools
{
    public interface IExternalTool
        : IContainerControl // System.Windows.Form 
    {
        ExternalTool ExternalTool { get; }

        void SetCurrentObject(string path, BaseObject obj);
    }
}
