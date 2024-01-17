using System.Windows;
using System.Windows.Controls;
using CasaEngine.Framework.Assets;

namespace CasaEngine.EditorUI.Controls.WorldControls;

public partial class GameEditorWorldControl : UserControl
{
    private GameScreenControlViewModel? ScreenControlViewModel => DataContext as GameScreenControlViewModel;

    public GameEditorWorldControl()
    {
        InitializeComponent();

        DataContext = new GameScreenControlViewModel(gameEditor);
    }

    private void ButtonLaunchGame_Click(object sender, RoutedEventArgs e)
    {
        gameEditor.Game.GameManager.IsRunningInGameEditorMode = !gameEditor.Game.GameManager.IsRunningInGameEditorMode;
        gameEditor.Game.GameManager.PhysicsEngineComponent.Enabled = gameEditor.Game.GameManager.IsRunningInGameEditorMode;

        buttonLaunch.Content = gameEditor.Game.GameManager.IsRunningInGameEditorMode ? "Running" : "Launch";
    }

    private void ButtonSave_OnClick(object sender, RoutedEventArgs e)
    {
        var world = gameEditor.Game.GameManager.CurrentWorld;
        AssetSaver.SaveAsset(world.AssetInfo.FileName, world);
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