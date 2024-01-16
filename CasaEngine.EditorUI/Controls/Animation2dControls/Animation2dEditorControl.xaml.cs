using Microsoft.Xna.Framework;
using System.Windows.Input;
using CasaEngine.Framework.Assets.Animations;
using Xceed.Wpf.AvalonDock.Layout.Serialization;
using Xceed.Wpf.AvalonDock;

namespace CasaEngine.Editor.Controls.Animation2dControls;

public partial class Animation2dEditorControl : EditorControlBase
{
    private string _animation2dFileName;

    protected override string LayoutFileName => "animation2dEditorLayout.xml";
    public override DockingManager DockingManager => dockingManagerAnimation2d;

    public Animation2dEditorControl()
    {
        InitializeComponent();
        Animation2dListControl.InitializeFromGameEditor(GameEditorAnimation2dControl.GameEditor);
    }

    protected override void LayoutSerializationCallback(object? sender, LayoutSerializationCallbackEventArgs e)
    {
        e.Content = e.Model.Title switch
        {
            "Animations 2d" => Animation2dListControl,
            "Animation 2d View" => GameEditorAnimation2dControl,
            "Details" => animation2dDetailsControl,
            "Logs" => this.FindParent<MainWindow>().LogsControl,
            "Content Browser" => this.FindParent<MainWindow>().ContentBrowserControl,
            _ => e.Content
        };
    }

    public void OpenAnimations2d(string fileName)
    {
        _animation2dFileName = fileName;
        Animation2dListControl.OpenAnimations2d(fileName);
    }

    private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        Animation2dListControl.SaveCurrentAnimation();
        e.Handled = true;
    }
}