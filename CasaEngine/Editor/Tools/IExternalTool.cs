using CasaEngine.Framework.Entities;

namespace CasaEngine.Editor.Tools;

public interface IExternalTool : IContainerControl
{
    ExternalTool ExternalTool { get; }

    void SetCurrentObject(string path, Entity obj);
}