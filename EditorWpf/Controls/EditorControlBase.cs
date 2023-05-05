using System.IO;
using System.Windows.Controls;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout.Serialization;

namespace EditorWpf.Controls;

public abstract class EditorControlBase : UserControl, IEditorControl
{
    protected abstract string LayoutFileName { get; }
    protected abstract DockingManager DockingManager { get; }

    public void ShowControl(UserControl control, string panelTitle)
    {
        EditorControlHelper.ShowControl(control, DockingManager, panelTitle);
    }

    public void LoadLayout(string path)
    {
        var fileName = Path.Combine(path, LayoutFileName);

        if (!File.Exists(fileName))
        {
            return;
        }

        EditorControlHelper.LoadLayout(DockingManager, fileName, LayoutSerializationCallback);
    }

    protected abstract void LayoutSerializationCallback(object? sender, LayoutSerializationCallbackEventArgs e);

    public void SaveLayout(string path)
    {
        var fileName = Path.Combine(path, LayoutFileName);
        EditorControlHelper.SaveLayout(DockingManager, fileName);
    }
}