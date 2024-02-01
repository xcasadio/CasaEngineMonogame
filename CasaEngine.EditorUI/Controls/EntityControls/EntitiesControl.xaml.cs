using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;
using CasaEngine.EditorUI.Controls.WorldControls;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.SceneManagement.Components;
using XNAGizmo;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public partial class EntitiesControl : UserControl
{
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(EntityViewModel), typeof(EntitiesControl));

    private CasaEngineGame Game { get; set; }

    private bool _isSelectionTriggerActive = true;
    private bool _isSelectionTriggerFromGizmoActive = true;
    private GizmoComponent? _gizmoComponent;

    public EntityViewModel SelectedItem
    {
        get => (EntityViewModel)GetValue(SelectedItemProperty);
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

        _gizmoComponent = Game.GetGameComponent<GizmoComponent>();

        _gizmoComponent.Gizmo.SelectionChanged -= OnEntitiesSelectionChanged;
        _gizmoComponent.Gizmo.SelectionChanged += OnEntitiesSelectionChanged;

        _gizmoComponent.Gizmo.CopyTriggered -= OnCopyTriggered;
        _gizmoComponent.Gizmo.CopyTriggered += OnCopyTriggered;
    }

    private void OnCopyTriggered(object? sender, List<ITransformable> entities)
    {
        if (!_isSelectionTriggerFromGizmoActive)
        {
            return;
        }

        _isSelectionTriggerActive = false;

        var entitiesViewModel = DataContext as EntityListViewModel;
        var newEntities = new List<EntityViewModel>();

        var actors = entities.Cast<SceneComponent>().Select(x => x.Owner).ToHashSet();

        foreach (var entity in actors)
        {
            var newEntity = entity.Clone();
            newEntity.Initialize();
            newEntity.InitializeWithWorld(Game.GameManager.CurrentWorld);
            newEntities.Add(entitiesViewModel.Add(newEntity));
        }

        _gizmoComponent.Gizmo.Clear();
        foreach (var entityViewModel in newEntities)
        {
            if (entityViewModel.Entity.RootComponent != null)
            {
                _gizmoComponent.Gizmo.Selection.Add(entityViewModel.Entity.RootComponent);
            }
        }

        SetSelectedItem(newEntities[0]);

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
        SceneComponent? selectedEntity = null;
        if (gizmoComponent.Gizmo.Selection.Count > 0)
        {
            selectedEntity = (SceneComponent)gizmoComponent.Gizmo.Selection[0];
        }

        if (selectedEntity != null)
        {
            var entitiesViewModel = DataContext as EntityListViewModel;
            SetSelectedItem(entitiesViewModel.GetFromEntity(selectedEntity.Owner));
        }

        _isSelectionTriggerActive = true;
    }

    private void OnWorldChanged(object? sender, EventArgs e)
    {
        var entitiesViewModel = new EntityListViewModel(Game.GameManager.CurrentWorld);
        DataContext = entitiesViewModel;
        TreeViewEntities.ItemsSource = entitiesViewModel.Entities;
    }

    private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (!_isSelectionTriggerActive)
        {
            return;
        }

        _isSelectionTriggerFromGizmoActive = false;

        var entityViewModel = e.NewValue as EntityViewModel;

        if (e.NewValue == null)
        {
            _gizmoComponent.Gizmo.Clear();
        }
        else
        {
            _gizmoComponent.Gizmo.Clear();
            if (entityViewModel.Entity.RootComponent != null)
            {
                _gizmoComponent.Gizmo.Selection.Add(entityViewModel.Entity.RootComponent);
            }
        }

        SetSelectedItem(entityViewModel);

        _isSelectionTriggerFromGizmoActive = true;
    }

    private void SetSelectedItem(EntityViewModel? selectedEntity)
    {
        if (selectedEntity != null)
        {
            SelectedItem = selectedEntity;

            if (TreeViewEntities.ItemContainerGenerator.ContainerFromItem(selectedEntity) is TreeViewItem treeViewItem)
            {
                treeViewItem.IsSelected = true;
            }
        }
        else
        {
            foreach (var item in TreeViewEntities.ItemContainerGenerator.Items)
            {
                if (TreeViewEntities.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem treeViewItem)
                {
                    treeViewItem.IsSelected = false;
                }
            }
        }
    }

    private void TreeView_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e is { Key: Key.Delete, IsToggled: true })
        {
            if (TreeViewEntities.ItemContainerGenerator.ContainerFromItem(SelectedItem) is TreeViewItem { DataContext: EntityViewModel entityViewModel })
            {
                var entitiesViewModel = DataContext as EntityListViewModel;
                entitiesViewModel.Remove(entityViewModel);
            }
        }
    }

    private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem treeViewItem)
        {
            //TODO : Camera in front of the Object
        }
    }
}