using System.IO;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Core.Logger;
using CasaEngine.Framework.Game;

namespace EditorWpf.Controls
{
    public partial class GameScreenControl : UserControl
    {
        public GameScreenControl()
        {
            InitializeComponent();
        }

        private void ButtonLaunchGame_Click(object sender, RoutedEventArgs e)
        {
            //Create the specific project settings =>
            //  launch in the project directory
            //  launch this world
            var game = new CasaEngineGame();
            game.Run();
        }

        private void ButtonSave_OnClick(object sender, RoutedEventArgs e)
        {
            GameInfo.Instance.CurrentWorld.Save(Path.GetDirectoryName(GameInfo.Instance.CurrentWorld.FileName));
            LogManager.Instance.WriteLine($"World {GameInfo.Instance.CurrentWorld.Name} saved '{Path.Combine(GameEditor.Game.GameManager.ProjectManager.ProjectPath, GameInfo.Instance.CurrentWorld.Name + ".json")}'");
        }

        private void ButtonTranslate_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as GameScreenControlViewModel).IsTranslationMode = true;
        }

        private void ButtonRotate_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as GameScreenControlViewModel).IsRotationMode = true;
        }

        private void ButtonScale_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as GameScreenControlViewModel).IsScaleMode = true;
        }

        private void ButtonLocalSpace_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as GameScreenControlViewModel).IsTransformSpaceLocal = true;
        }

        private void ButtonWorldSpace_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as GameScreenControlViewModel).IsTransformSpaceWorld = true;
        }
    }
}
