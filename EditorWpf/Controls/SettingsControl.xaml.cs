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
            GameEditor.GameStarted += OnGameGameStarted;
        }

        private void OnGameGameStarted(object? sender, EventArgs e)
        {
            CasaEngineGame game = (CasaEngineGame)sender;
            PropertyGridProjectSettings.SelectedObject = game.GameManager.ProjectSettings;
            PropertyGridPhysicSettings.SelectedObject = game.GameManager.Physics2dSettings;
        }
    }
}
