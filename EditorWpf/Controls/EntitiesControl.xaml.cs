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
        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register("SelectedItem", typeof(Entity), typeof(EntitiesControl));

        private bool isSelectionTriggerActive = true;

        public Entity SelectedItem
        {
            get => (Entity)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public EntitiesControl()
        {
            InitializeComponent();
            GameInfo.Instance.ReadyToStart += OnGameReadyToStart;
            GameInfo.Instance.WorldChanged += OnWorldChanged;

        }

        private void OnGameReadyToStart(object? sender, EventArgs e)
        {
            var gizmoComponent = Engine.Instance.Game.GetGameComponent<GizmoComponent>();
            gizmoComponent.Gizmo.SelectionChanged -= OnEntitiesSelectionChanged;
            gizmoComponent.Gizmo.SelectionChanged += OnEntitiesSelectionChanged;
        }

        private void OnEntitiesSelectionChanged(object? sender, System.Collections.Generic.List<ITransformable> e)
        {
            isSelectionTriggerActive = false;

            var gizmoComponent = Engine.Instance.Game.GetGameComponent<GizmoComponent>();
            Entity? selectedEntity = null;
            if (gizmoComponent.Gizmo.Selection.Count > 0)
            {
                selectedEntity = (Entity)gizmoComponent.Gizmo.Selection[0];
            }
            SetSelectedItem(selectedEntity);

            isSelectionTriggerActive = true;
        }

        private void OnWorldChanged(object? sender, EventArgs e)
        {
            GameInfo.Instance.CurrentWorld.EntitiesChanged += OnEntitiesChanged;
            TreeView.ItemsSource = GameInfo.Instance.CurrentWorld.Entities;
        }

        private void OnEntitiesChanged(object? sender, EventArgs e)
        {
            TreeView.ItemsSource = null;
            TreeView.ItemsSource = GameInfo.Instance.CurrentWorld.Entities;
        }

        private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (!isSelectionTriggerActive)
            {
                return;
            }

            var gizmoComponent = Engine.Instance.Game.GetGameComponent<GizmoComponent>();

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
