using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Xna.Framework;
using Xceed.Wpf.AvalonDock.Layout.Serialization;

namespace EditorWpf.Controls
{
    public partial class WorldEditorControl : UserControl, IEditorControl
    {
        private const string LayoutFileName = "worldEditorLayout.xml";

        public event EventHandler GameStarted;

        public GameEditor GameEditor => GameScreenControl.gameEditor;

        public WorldEditorControl()
        {
            InitializeComponent();

            GameScreenControl.gameEditor.GameStarted += OnGameStarted;
            EntitiesControl.InitializeFromGameEditor(GameScreenControl.gameEditor);
            EntityControl.InitializeFromGameEditor(GameScreenControl.gameEditor.Game);
        }


        private void OnGameStarted(object? sender, EventArgs e)
        {
            GameStarted?.Invoke(this, e);
        }

        public void ShowControl(UserControl control, string panelTitle)
        {
            EditorControlHelper.ShowControl(control, dockingManagerWorld, panelTitle);
        }

        public void LoadLayout(string path)
        {
            var fileName = Path.Combine(path, LayoutFileName);

            if (!File.Exists(fileName))
            {
                return;
            }

            EditorControlHelper.LoadLayout(dockingManagerWorld, fileName, LayoutSerializationCallback);
        }

        private void LayoutSerializationCallback(object? sender, LayoutSerializationCallbackEventArgs e)
        {
            e.Content = e.Model.Title switch
            {
                "Entities" => EntitiesControl,
                "Details" => EntityControl,
                "Game Screen" => GameScreenControl,
                "Place Actors" => PlaceActorsControl,
                "Logs" => this.FindParent<MainWindow>().LogsControl,
                "Content Browser" => this.FindParent<MainWindow>().ContentBrowserControl,
                _ => e.Content
            };
        }

        public void SaveLayout(string path)
        {
            var fileName = Path.Combine(path, LayoutFileName);
            EditorControlHelper.SaveLayout(dockingManagerWorld, fileName);
        }
    }
}
