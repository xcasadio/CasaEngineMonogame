using System.Windows.Controls;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.GameFramework;

namespace CasaEngine.EditorUI.Controls.GameModeControls
{
    public partial class GameModeEditorControl : UserControl
    {
        private readonly AssetLoader<GameMode> _assetLoader;

        public GameModeEditorControl()
        {
            InitializeComponent();
            _assetLoader = new AssetLoader<GameMode>();
        }

        public void OpenGameMode(string fileName)
        {
            DataContext = _assetLoader.LoadAsset(fileName, null);
        }
    }
}
