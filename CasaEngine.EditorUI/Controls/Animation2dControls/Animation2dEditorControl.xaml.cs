using System;
using System.Windows.Input;
using Microsoft.Xna.Framework;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout.Serialization;

namespace CasaEngine.EditorUI.Controls.Animation2dControls;

public partial class Animation2dEditorControl : EditorControlBase
{
    private string _animation2dFileName;

    protected override string LayoutFileName => "animation2dEditorLayout.xml";
    public override DockingManager DockingManager => dockingManagerAnimation2d;

    public Animation2dEditorControl()
    {
        InitializeComponent();

        if (EngineHost.Instance?.IsStarted == true)
            Animation2dListControl.InitializeFromEngineHost(EngineHost.Instance);
        else
            EngineHost.InstanceStarted += OnEngineHostStarted;
    }

    private void OnEngineHostStarted(object? sender, EventArgs e)
    {
        if (EngineHost.Instance != null)
            Animation2dListControl.InitializeFromEngineHost(EngineHost.Instance);
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