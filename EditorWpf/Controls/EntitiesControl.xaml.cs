using System;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using XNAGizmo;

namespace EditorWpf.Controls
{
    public partial class EntitiesControl : UserControl
    {
        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(Entity), typeof(EntitiesControl));

        private CasaEngineGame Game { get; set; }

        private bool _isSelectionTriggerActive = true;
        private bool _isSelectionTriggerFromGizmoActive = true;

        public Entity SelectedItem
        {
            get => (Entity)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public EntitiesControl()
        {
            InitializeComponent();
        }

        public void InitializeFromGameEditor(GameEditorWorld gameEditorWorld)
        {
            gameEditorWorld.GameStarted += OnGameGameStarted;
        }

        private void OnGameGameStarted(object? sender, EventArgs e)
        {
            Game = (CasaEngineGame)sender;
            Game.GameManager.WorldChanged += OnWorldChanged;
            var gizmoComponent = Game.GetGameComponent<GizmoComponent>();
            gizmoComponent.Gizmo.SelectionChanged -= OnEntitiesSelectionChanged;
            gizmoComponent.Gizmo.SelectionChanged += OnEntitiesSelectionChanged;
        }

        private void OnEntitiesSelectionChanged(object? sender, System.Collections.Generic.List<ITransformable> e)
        {
            if (!_isSelectionTriggerFromGizmoActive)
            {
                return;
            }
            _isSelectionTriggerActive = false;

            var gizmoComponent = Game.GetGameComponent<GizmoComponent>();
            Entity? selectedEntity = null;
            if (gizmoComponent.Gizmo.Selection.Count > 0)
            {
                selectedEntity = (Entity)gizmoComponent.Gizmo.Selection[0];
            }
            SetSelectedItem(selectedEntity);

            _isSelectionTriggerActive = true;
        }

        private void OnWorldChanged(object? sender, EventArgs e)
        {
            Game.GameManager.CurrentWorld.EntitiesChanged += OnEntitiesChanged;
            TreeView.ItemsSource = Game.GameManager.CurrentWorld.Entities;
        }

        private void OnEntitiesChanged(object? sender, EventArgs e)
        {
            TreeView.ItemsSource = null;
            TreeView.ItemsSource = Game.GameManager.CurrentWorld.Entities;
        }

        private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (!_isSelectionTriggerActive)
            {
                return;
            }

            _isSelectionTriggerFromGizmoActive = false;

            var gizmoComponent = Game.GetGameComponent<GizmoComponent>();

            if (e.NewValue == null)
            {
                gizmoComponent.Gizmo.Clear();
            }
            else
            {
                gizmoComponent.Gizmo.Clear(); // TODO
                gizmoComponent.Gizmo.Selection.Add((ITransformable)e.NewValue);

            }

            var selectedEntity = (Entity)e.NewValue;
            SetSelectedItem(selectedEntity);

            _isSelectionTriggerFromGizmoActive = true;
        }

        private void SetSelectedItem(Entity? selectedEntity)
        {
            SelectedItem = selectedEntity;

            if (selectedEntity != null)
            {
                var treeViewItem = TreeView.ItemContainerGenerator.ContainerFromItem(selectedEntity) as TreeViewItem;
                if (treeViewItem != null)
                {
                    treeViewItem.IsSelected = true;
                }
            }
            else
            {
                foreach (var item in TreeView.ItemContainerGenerator.Items)
                {
                    var treeViewItem = TreeView.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                    if (treeViewItem != null)
                    {
                        treeViewItem.IsSelected = false;
                    }
                }
            }
        }
    }
}
