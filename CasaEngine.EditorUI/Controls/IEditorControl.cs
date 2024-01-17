using System.Windows.Controls;

namespace CasaEngine.EditorUI.Controls;

public interface IEditorControl
{
    void ShowControl(UserControl control, string panelTitle);
    void LoadLayout(string path);
    void SaveLayout(string path);
}