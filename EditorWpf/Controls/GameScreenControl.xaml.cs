using System.IO;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Framework;
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

        }

        private void ButtonSave_OnClick(object sender, RoutedEventArgs e)
        {
            //EngineComponents.ProjectManager.Load(fileName_);
            GameInfo.Instance.CurrentWorld.Save(Path.Combine(EngineComponents.ProjectManager.ProjectPath, "Worlds"));
        }
    }
}
