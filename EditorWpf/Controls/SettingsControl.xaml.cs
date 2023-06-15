using System.Windows;
using System.Windows.Controls;
using CasaEngine.Framework.Game;

namespace EditorWpf.Controls
{
    public partial class SettingsControl : UserControl
    {
        public SettingsControl()
        {
            InitializeComponent();
        }

        private void SettingsControl_OnGotFocus(object sender, RoutedEventArgs e)
        {
            PropertyGridProjectSettings.SelectedObject = GameSettings.ProjectSettings;
            PropertyGridPhysiscSettings.SelectedObject = GameSettings.PhysicsSettings;
            PropertyGridGraphicsSettings.SelectedObject = GameSettings.GraphicsSettings;
        }
    }
}
