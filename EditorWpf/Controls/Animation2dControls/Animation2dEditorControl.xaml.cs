using System.IO;
using Microsoft.Xna.Framework;
using System.Windows.Controls;
using Xceed.Wpf.AvalonDock.Layout.Serialization;

namespace EditorWpf.Controls.Animation2dControls
{
    public partial class Animation2dEditorControl : UserControl, IEditorControl
    {
        private const string LayoutFileName = "animation2dEditorLayout.xml";

        public Animation2dEditorControl()
        {
            InitializeComponent();
            Animation2dListControl.InitializeFromGameEditor(GameEditorAnimation2dControl.GameEditor);
        }
        public void ShowControl(UserControl control, string panelTitle)
        {
            EditorControlHelper.ShowControl(control, dockingManagerAnimation2d, panelTitle);
        }

        public void LoadLayout(string path)
        {
            var fileName = Path.Combine(path, LayoutFileName);

            if (!File.Exists(fileName))
            {
                return;
            }

            EditorControlHelper.LoadLayout(dockingManagerAnimation2d, fileName, LayoutSerializationCallback);
        }

        private void LayoutSerializationCallback(object? sender, LayoutSerializationCallbackEventArgs e)
        {
            e.Content = e.Model.Title switch
            {
                "Animations 2d" => Animation2dListControl,
                "Animation 2d View" => GameEditorAnimation2dControl,
                "Details" => animation2dDetailsControl,
                "Logs" => this.FindParent<MainWindow>().LogsControl,
                "Content Browser" => this.FindParent<MainWindow>().ContentBrowserControl,
                _ => e.Content
            };
        }

        public void SaveLayout(string path)
        {
            var fileName = Path.Combine(path, LayoutFileName);
            EditorControlHelper.SaveLayout(dockingManagerAnimation2d, fileName);
        }

        public void LoadAnimations2d(string fileName)
        {
            Animation2dListControl.LoadAnimations2d(fileName);
        }
    }
}
