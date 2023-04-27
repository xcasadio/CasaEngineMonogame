using System.Windows.Controls;

namespace EditorWpf.Controls;

public interface IEditorControl
{
    void ShowControl(UserControl control, string panelTitle);
    void LoadLayout(string path);
    void SaveLayout(string path);
}