using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Core.Design;
using CasaEngine.Core.Logger;
using CasaEngine.Framework.Game;

namespace EditorWpf.Controls.WorldControls
{
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
            //Create the specific project settings =>
            //  launch in the project directory
            //  launch this world
            var gameExe = "CasaEngineLauncher.exe";
            var processStartInfo = new ProcessStartInfo(Path.Combine("Game", gameExe), "project path");
            var process = Process.Start(processStartInfo);

            //process.WaitForExit()
        }

        private void ButtonSave_OnClick(object sender, RoutedEventArgs e)
        {
            gameEditor.Game.GameManager.CurrentWorld.Save(GameSettings.ProjectSettings.ProjectPath, SaveOption.Editor);
            LogManager.Instance.WriteLineInfo($"World {gameEditor.Game.GameManager.CurrentWorld.AssetInfo.Name} saved ({gameEditor.Game.GameManager.CurrentWorld.AssetInfo.FileName})");
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
}
