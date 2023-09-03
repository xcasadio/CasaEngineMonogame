using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.World;
using EditorWpf.Controls.Common;
using EditorWpf.Controls.EntityControls;
using XNAGizmo;

namespace EditorWpf.Controls.WorldControls
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

            gizmoComponent.Gizmo.CopyTriggered -= OnCopyTriggered;
            gizmoComponent.Gizmo.CopyTriggered += OnCopyTriggered;
        }

        private void OnCopyTriggered(object? sender, List<ITransformable> entities)
        {
            if (!_isSelectionTriggerFromGizmoActive)
            {
                return;
            }

            _isSelectionTriggerActive = false;

            var newEntities = new List<Entity>();

            foreach (var entity in entities.Cast<Entity>())
            {
                var newEntity = entity.Clone();
                newEntity.Initialize(Game);
                Game.GameManager.CurrentWorld.AddEntityImmediately(newEntity);
                newEntities.Add(newEntity);
            }

            var gizmoComponent = Game.GetGameComponent<GizmoComponent>();
            gizmoComponent.Gizmo.Clear();
            foreach (var entity in newEntities)
            {
                gizmoComponent.Gizmo.Selection.Add(entity);
            }

            var entitiesViewModel = DataContext as EntitiesViewModel;
            SetSelectedItem(entitiesViewModel.GetFromEntity(newEntities[0]));

            _isSelectionTriggerActive = true;
        }

        private void OnEntitiesSelectionChanged(object? sender, List<ITransformable> entities)
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

            var entitiesViewModel = DataContext as EntitiesViewModel;
            SetSelectedItem(entitiesViewModel.GetFromEntity(selectedEntity));

            _isSelectionTriggerActive = true;
        }

        private void OnWorldChanged(object? sender, EventArgs e)
        {
            var entitiesViewModel = new EntitiesViewModel(Game.GameManager.CurrentWorld);
            DataContext = entitiesViewModel;
            TreeView.ItemsSource = entitiesViewModel.Entities;
        }

        private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (!_isSelectionTriggerActive)
            {
                return;
            }

            _isSelectionTriggerFromGizmoActive = false;

            var gizmoComponent = Game.GetGameComponent<GizmoComponent>();

            var entityViewModel = e.NewValue as EntityViewModel;

            if (e.NewValue == null)
            {
                gizmoComponent.Gizmo.Clear();
            }
            else
            {
                gizmoComponent.Gizmo.Clear(); // TODO
                gizmoComponent.Gizmo.Selection.Add(entityViewModel.Entity);
            }

            SetSelectedItem(entityViewModel);

            _isSelectionTriggerFromGizmoActive = true;
        }

        private void SetSelectedItem(EntityViewModel? selectedEntity)
        {
            if (selectedEntity != null)
            {
                SelectedItem = selectedEntity.Entity;

                if (TreeView.ItemContainerGenerator.ContainerFromItem(selectedEntity) is TreeViewItem treeViewItem)
                {
                    treeViewItem.IsSelected = true;
                }
            }
            else
            {
                foreach (var item in TreeView.ItemContainerGenerator.Items)
                {
                    if (TreeView.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem treeViewItem)
                    {
                        treeViewItem.IsSelected = false;
                    }
                }
            }
        }

        private void TreeView_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Delete && e.IsToggled)
            {
                if (TreeView.ItemContainerGenerator.ContainerFromItem(SelectedItem) is TreeViewItem treeViewItem)
                {
                    Game.GameManager.CurrentWorld.RemoveEntity(treeViewItem.DataContext as Entity);
                    //OnEntitiesChanged(this, EventArgs.Empty);
                }
            }
        }

        private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TreeViewItem treeViewItem)
            {
                var inputTextBox = new InputTextBox();
                inputTextBox.Description = "Enter a new name";
                inputTextBox.Title = "Rename";
                var entity = (treeViewItem.DataContext as EntityViewModel);
                inputTextBox.Text = entity.Name;

                if (inputTextBox.ShowDialog() == true)
                {
                    //_gameEditor.Game.GameManager.AssetContentManager.Rename(animation2dDataViewModel.Name, inputTextBox.Text);
                    entity.Name = inputTextBox.Text;
                }
            }
        }
    }

    internal class EntitiesViewModel
    {
        public ObservableCollection<EntityViewModel> Entities { get; } = new();

        public EntitiesViewModel(World world)
        {
            world.EntitiesAdded += OnEntitiesAdded;
            world.EntitiesRemoved += OnEntitiesRemoved;
            world.EntitiesClear += OnEntitiesClear;

            foreach (var worldEntity in world.Entities)
            {
                Entities.Add(new EntityViewModel(worldEntity));
            }
        }

        private void OnEntitiesClear(object? sender, EventArgs e)
        {
            Entities.Clear();
        }

        private void OnEntitiesRemoved(object? sender, Entity entity)
        {
            foreach (var entityViewModel in Entities)
            {
                if (entityViewModel.Entity == entity)
                {
                    Entities.Remove(entityViewModel);
                    break;
                }
            }
        }

        private void OnEntitiesAdded(object? sender, Entity entity)
        {
            Entities.Add(new EntityViewModel(entity));
        }

        public EntityViewModel? GetFromEntity(Entity entity)
        {
            foreach (var entityViewModel in Entities)
            {
                if (entityViewModel.Entity == entity)
                {
                    return entityViewModel;
                }
            }

            return null;
        }
    }

    internal class EntityViewModel : NotifyPropertyChangeBase
    {
        public Entity Entity { get; }

        public string Name
        {
            get => Entity.Name;
            set
            {
                if (Entity.Name != value)
                {
                    Entity.Name = value;
                    OnPropertyChanged();
                }
            }
        }

        public EntityViewModel(Entity entity)
        {
            Entity = entity;
        }
    }
}
