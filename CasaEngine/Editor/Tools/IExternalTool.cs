using CasaEngine.Framework.SceneManagement;

namespace CasaEngine.Editor.Tools;

public interface IExternalTool : IContainerControl
{
    ExternalTool ExternalTool { get; }

    void SetCurrentObject(string path, AActor obj);
}