using System.Windows.Forms;
using CasaEngine.Framework.SceneManagement;

namespace CasaEngine.EditorUI.Plugins.Tools;

public interface IExternalTool : IContainerControl
{
    ExternalTool ExternalTool { get; }

    void SetCurrentObject(string path, AActor obj);
}