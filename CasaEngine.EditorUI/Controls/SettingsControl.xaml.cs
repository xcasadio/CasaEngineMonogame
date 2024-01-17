using System.Windows;
using System.Windows.Controls;
using CasaEngine.Framework.Game;

namespace CasaEngine.EditorUI.Controls;

public partial class SettingsControl : UserControl
{
    public SettingsControl()
    {
        InitializeComponent();
    }

    private void SettingsControl_OnGotFocus(object sender, RoutedEventArgs e)
    {
        PropertyGridProjectSettings.SelectedObject = GameSettings.ProjectSettings;
        PropertyGridPhysiscSettings.SelectedObject = GameSettings.PhysicsEngineSettings;
        PropertyGridGraphicsSettings.SelectedObject = GameSettings.GraphicsSettings;
    }
}