using System.Windows.Controls;
using CasaEngine.Framework.Game;

namespace EditorWpf.Controls
{
    /// <summary>
    /// Interaction logic for SettingsControl.xaml
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        public SettingsControl()
        {
            InitializeComponent();

            PropertyGridProjectSettings.SelectedObject = Engine.Instance.ProjectSettings;
            PropertyGridPhysicSettings.SelectedObject = Engine.Instance.PhysicsSettings;
        }
    }
}
