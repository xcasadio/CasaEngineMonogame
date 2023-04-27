using System.IO;
using System.Windows.Controls;
using Microsoft.Xna.Framework;
using Xceed.Wpf.AvalonDock.Layout.Serialization;

namespace EditorWpf.Controls
{
    public partial class SpriteEditorControl : UserControl, IEditorControl
    {
        private const string LayoutFileName = "spriteEditorLayout.xml";

        public SpriteEditorControl()
        {
            InitializeComponent();
            SpriteListControl.InitializeFromGameEditor(GameEditorSprite);

        }

        public void ShowControl(UserControl control, string panelTitle)
        {
            EditorControlHelper.ShowControl(control, dockingManagerSprite, panelTitle);
        }

        public void LoadLayout(string path)
        {
            var fileName = Path.Combine(path, LayoutFileName);

            if (!File.Exists(fileName))
            {
                return;
            }

            EditorControlHelper.LoadLayout(dockingManagerSprite, fileName, LayoutSerializationCallback);
        }

        private void LayoutSerializationCallback(object? sender, LayoutSerializationCallbackEventArgs e)
        {
            e.Content = e.Model.Title switch
            {
                "Sprites" => SpriteListControl,
                "Collisions" => SpriteCollisionsControl,
                "Sprite View" => GameEditorSprite,
                "Sockets" => SpriteSocketsControl,
                "Details" => spriteDetailsControl,
                "Logs" => this.FindParent<MainWindow>().LogsControl,
                "Content Browser" => this.FindParent<MainWindow>().ContentBrowserControl,
                _ => e.Content
            };
        }

        public void SaveLayout(string path)
        {
            var fileName = Path.Combine(path, LayoutFileName);
            EditorControlHelper.SaveLayout(dockingManagerSprite, fileName);
        }
    }
}
