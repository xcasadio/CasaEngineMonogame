using System;
using System.Windows;
using CasaEngine.Framework.Entities;
using Microsoft.Xna.Framework;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout.Serialization;

namespace CasaEngine.Editor.Controls.EntityControls
{
    public partial class EntityEditorControl : EditorControlBase
    {
        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(EntityViewModel), typeof(EntityEditorControl));

        public EntityViewModel SelectedItem
        {
            get => (EntityViewModel)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        protected override string LayoutFileName => "entityEditorLayout.xml";
        public override DockingManager DockingManager => dockingManagerWorld;

        public event EventHandler GameStarted;

        public GameEditorEntity GameEditorEntity => GameScreenControl.gameEntityEditor;

        public EntityEditorControl()
        {
            InitializeComponent();

            GameScreenControl.gameEntityEditor.GameStarted += OnGameStarted;
            EntityControl.InitializeFromGameEditor(GameScreenControl.gameEntityEditor);
        }

        private void OnGameStarted(object? sender, EventArgs e)
        {
            GameStarted?.Invoke(this, e);
        }

        protected override void LayoutSerializationCallback(object? sender, LayoutSerializationCallbackEventArgs e)
        {
            e.Content = e.Model.Title switch
            {
                "Details" => EntityControl,
                "Game Screen" => GameScreenControl,
                "Logs" => this.FindParent<MainWindow>().LogsControl,
                "Content Browser" => this.FindParent<MainWindow>().ContentBrowserControl,
                _ => e.Content
            };
        }

        public void LoadEntity(string fileName)
        {
            SelectedItem = new EntityViewModel(EntityLoader.Load(fileName));
        }
    }
}
