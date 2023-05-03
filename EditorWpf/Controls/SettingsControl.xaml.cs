using System;
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

        private void OnGameGameStarted(object? sender, EventArgs e)
        {
            var game = sender as CasaEngineGame;
            PropertyGridProjectSettings.SelectedObject = GameSettings.ProjectSettings;
            PropertyGridPhysicSettings.SelectedObject = GameSettings.Physics2dSettings;
        }
    }
}
