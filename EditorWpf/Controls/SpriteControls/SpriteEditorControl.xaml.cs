using System.Windows.Input;
using CasaEngine.Framework.Assets.Map2d;
using Microsoft.Xna.Framework;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout.Serialization;

namespace EditorWpf.Controls.SpriteControls
{
    public partial class SpriteEditorControl : EditorControlBase
    {
        private string _spriteSheetFileName;

        protected override string LayoutFileName { get; } = "spriteEditorLayout.xml";
        protected override DockingManager DockingManager => dockingManagerSprite;

        public SpriteEditorControl()
        {
            InitializeComponent();
            SpriteListControl.InitializeFromGameEditor(GameEditorSpriteControl.GameEditor);
        }

        protected override void LayoutSerializationCallback(object? sender, LayoutSerializationCallbackEventArgs e)
        {
            e.Content = e.Model.Title switch
            {
                "Sprites" => SpriteListControl,
                "Collisions" => SpriteCollisionsControl,
                "Sprite View" => GameEditorSpriteControl,
                "Sockets" => SpriteSocketsControl,
                "Details" => spriteDetailsControl,
                "Logs" => this.FindParent<MainWindow>().LogsControl,
                "Content Browser" => this.FindParent<MainWindow>().ContentBrowserControl,
                _ => e.Content
            };
        }

        public void LoadSpriteSheet(string fileName)
        {
            _spriteSheetFileName = fileName;
            SpriteListControl.LoadSpriteSheet(fileName);
        }

        private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var spriteDatas = GameEditorSpriteControl.GameEditor.Game.GameManager.AssetContentManager.GetAssets<SpriteData>();
            SpriteLoader.SaveToFile(_spriteSheetFileName, spriteDatas);
            e.Handled = true;
        }
    }
}
