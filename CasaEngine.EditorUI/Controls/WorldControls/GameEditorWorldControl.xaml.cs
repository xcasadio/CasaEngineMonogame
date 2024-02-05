using System.Windows;
using System.Windows.Controls;
using CasaEngine.EditorUI.Controls.WorldControls.ViewModels;
using CasaEngine.Framework.Assets;

namespace CasaEngine.EditorUI.Controls.WorldControls;

public partial class GameEditorWorldControl : UserControl
{
    private WorldEditorViewModel? ScreenControlViewModel => DataContext as WorldEditorViewModel;

    public GameEditorWorldControl()
    {
        InitializeComponent();
        DataContext = new WorldEditorViewModel(gameEditor);
    }

    private void ButtonLaunchGame_Click(object sender, RoutedEventArgs e)
    {
        gameEditor.Game.IsRunningInGameEditorMode = !gameEditor.Game.IsRunningInGameEditorMode;
        gameEditor.Game.PhysicsEngineComponent.Enabled = gameEditor.Game.IsRunningInGameEditorMode;

        buttonLaunch.Content = gameEditor.Game.IsRunningInGameEditorMode ? "Running" : "Launch";
    }

    private void ButtonTranslate_Click(object sender, RoutedEventArgs e)
    {
        ScreenControlViewModel.IsTranslationMode = true;
    }

    private void ButtonRotate_Click(object sender, RoutedEventArgs e)
    {
        ScreenControlViewModel.IsRotationMode = true;
    }

    private void ButtonScale_Click(object sender, RoutedEventArgs e)
    {
        ScreenControlViewModel.IsScaleMode = true;
    }

    private void ButtonLocalSpace_Click(object sender, RoutedEventArgs e)
    {
        ScreenControlViewModel.IsTransformSpaceLocal = true;
    }

    private void ButtonWorldSpace_Click(object sender, RoutedEventArgs e)
    {
        ScreenControlViewModel.IsTransformSpaceWorld = true;
    }
}