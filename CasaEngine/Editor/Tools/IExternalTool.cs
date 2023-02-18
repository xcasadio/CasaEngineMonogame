using CasaEngine.Framework.Entities;

namespace CasaEngine.Editor.Tools
{
    public interface IExternalTool
        : IContainerControl // System.Windows.Form 
    {
        ExternalTool ExternalTool { get; }

        void SetCurrentObject(string path, Entity obj);
    }
}
