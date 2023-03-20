using System.Windows.Controls;
using CasaEngine.Framework;

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

            PropertyGridProjectSettings.SelectedObject = EngineComponents.ProjectSettings;
            PropertyGridPhysicSettings.SelectedObject = EngineComponents.Physics2dSettings;
        }
    }
}
