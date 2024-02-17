using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.Core.Helpers;
using CasaEngine.EditorUI.Controls.ContentBrowser;
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

        TreeViewEntities.ItemMoved = ItemMoved;
    }

    private void ItemMoved(object item, object newparent)
    {
        if (item is EntityViewModel entityViewModel && newparent is EntityViewModel parentEntityViewModel)
        {
            if (entityViewModel.Parent != null)
            {
                entityViewModel.Parent.RemoveActorChild(entityViewModel);
            }

            parentEntityViewModel.AddActorChild(entityViewModel);
        }
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


        _gizmoComponent.Gizmo.DeleteSelectionEvent -= OnDeleteSelectionEvent;
        _gizmoComponent.Gizmo.DeleteSelectionEvent += OnDeleteSelectionEvent;
    }

    private void OnDeleteSelectionEvent(object? sender, List<ITransformable> selectionPool)
    {
        var entitiesViewModel = DataContext as EntityListViewModel;

        var entities = selectionPool.Cast<SceneComponent>().Select(x => x.Owner).ToHashSet();

        foreach (var entity in entities)
        {
            entitiesViewModel.Remove(entity);
        }

        _gizmoComponent.Gizmo.Clear();
        SetSelectedItem(null);
    }

    private void OnCopyTriggered(object? sender, List<ITransformable> selectionPool)
    {
        if (!_isSelectionTriggerFromGizmoActive)
        {
            return;
        }

        _isSelectionTriggerActive = false;

        var entitiesViewModel = DataContext as EntityListViewModel;
        var newEntities = new List<EntityViewModel>();

        var entities = selectionPool.Cast<SceneComponent>().Select(x => x.Owner).ToHashSet();

        foreach (var entity in entities)
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

        SceneComponent? selectedSceneComponent = null;
        if (_gizmoComponent.Gizmo.Selection.Count > 0)
        {
            selectedSceneComponent = (SceneComponent)_gizmoComponent.Gizmo.Selection[0];
        }

        if (selectedSceneComponent != null)
        {
            var entitiesViewModel = DataContext as EntityListViewModel;
            SetSelectedItem(entitiesViewModel.GetFromEntity(selectedSceneComponent.Owner));
        }

        _isSelectionTriggerActive = true;
    }

    private void OnWorldChanged(object? sender, EventArgs e)
    {
        TreeViewEntities.ItemsSource = (DataContext as EntityListViewModel).Entities;
    }

    private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (!_isSelectionTriggerActive)
        {
            return;
        }

        _isSelectionTriggerActive = false;
        _isSelectionTriggerFromGizmoActive = false;

        var entityViewModel = e.NewValue as EntityViewModel;
        _gizmoComponent.Gizmo.Clear();

        if (entityViewModel != null)
        {
            if (entityViewModel?.Entity?.RootComponent != null)
            {
                _gizmoComponent.Gizmo.Selection.Add(entityViewModel.Entity.RootComponent);
            }

            SetSelectedItem(entityViewModel);
        }

        _isSelectionTriggerActive = true;
        _isSelectionTriggerFromGizmoActive = true;
    }

    private void SetSelectedItem(EntityViewModel? selectedEntity)
    {
        if (selectedEntity != null && SelectedItem != selectedEntity)
        {
            SelectedItem = selectedEntity;
            SelectTreeViewItem(SelectedItem);
        }
        else
        {
            foreach (var item in TreeViewEntities.ItemContainerGenerator.Items)
            {
                SelectTreeViewItem(item as EntityViewModel, false, false, true, false);
            }
        }
    }

    private void SelectTreeViewItem(EntityViewModel entityViewModel, bool alterExpand = true, bool expand = true, bool alterSelect = true, bool select = true)
    {
        Stack<EntityViewModel> parents = new();
        parents.Push(entityViewModel);
        var root = entityViewModel;

        while (root != null)
        {
            parents.Push(root);
            root = root.Parent;
        }

        ItemsControl itemsControl = TreeViewEntities;

        while (parents.TryPop(out var entity))
        {
            if (itemsControl.ItemContainerGenerator.ContainerFromItem(entity) is TreeViewItem treeViewItem)
            {
                itemsControl = treeViewItem;

                if (alterExpand)
                {
                    treeViewItem.IsExpanded = expand;
                }

                if (alterSelect)
                {
                    treeViewItem.IsSelected = select;

                    if (select)
                    {
                        treeViewItem.BringIntoView();
                    }
                }
            }
        }
    }

    private void TreeView_KeyDown(object sender, KeyEventArgs e)
    {
        if (e is { Key: Key.Delete, IsToggled: true })
        {
            if (TreeViewEntities.SelectedItem is EntityViewModel entityViewModel)
            {
                var entitiesViewModel = DataContext as EntityListViewModel;
                entitiesViewModel.Remove(entityViewModel);

                if (entityViewModel.ComponentListViewModel?.RootComponentViewModel?.Component is SceneComponent sceneComponent)
                {
                    _gizmoComponent.Gizmo.Selection.Remove(sceneComponent);
                }
            }
        }
    }

    private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (TreeViewEntities.SelectedValue is EntityViewModel entityViewModel
            && entityViewModel.Entity?.RootComponent != null)
        {
            //TODO : Camera in front of the Object
            var sceneComponent = entityViewModel.Entity.RootComponent;
            var boundingBox = sceneComponent.BoundingBox;
            //var offset = sceneComponent.Forward * boundingBox.GetRadius();
            var offset = Game.GameManager.ActiveCamera.Forward * boundingBox.GetRadius();
            Game.GameManager.ActiveCamera.SetPositionAndTarget(sceneComponent.Position + offset, sceneComponent.Position);
        }
    }
}