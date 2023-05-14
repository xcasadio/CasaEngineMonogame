using System.Windows.Data;
using System.Windows.Input;
using CasaEngine.Core.Logger;
using CasaEngine.Framework.Assets.Map2d;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.WpfControls;
using Microsoft.Xna.Framework;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout;
using Xceed.Wpf.AvalonDock.Layout.Serialization;

namespace EditorWpf.Controls.SpriteControls
{
    public partial class SpriteEditorControl : EditorControlBase
    {
        private string _spriteSheetFileName;

        protected override string LayoutFileName => "spriteEditorLayout.xml";
        protected override DockingManager DockingManager => dockingManagerSprite;

        public SpriteEditorControl()
        {
            InitializeComponent();
            SpriteListControl.InitializeFromGameEditor(GameEditorSpriteControl.GameEditor);

            dockingManagerSprite.ActiveContentChanged += DockingManagerSprite_ActiveContentChanged;
        }

        private void DockingManagerSprite_ActiveContentChanged(object? sender, System.EventArgs e)
        {
            var layout = dockingManagerSprite.ActiveContent as LayoutContent;
            var binding = new Binding("SelectedItem");

            if (layout.Content is SpriteListControl)
            {
                spriteDetailsControl.DataContext = SpriteListControl.SelectedItem;
                binding.Source = SpriteListControl;
            }
            else if (layout.Content is SpriteSocketsControl)
            {
                spriteDetailsControl.DataContext = SpriteSocketsControl.SelectedItem;
                binding.Source = SpriteSocketsControl;
            }
            else if (layout.Content is SpriteCollisionsControl)
            {
                spriteDetailsControl.DataContext = SpriteCollisionsControl.SelectedItem;
                binding.Source = SpriteCollisionsControl;
            }
            else
            {
                return;
            }

            spriteDetailsControl.SetBinding(SpriteEditorControl.DataContextProperty, binding);
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
