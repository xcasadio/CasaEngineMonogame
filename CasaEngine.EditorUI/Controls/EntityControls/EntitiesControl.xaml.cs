﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.Editor.Controls.Common;
using CasaEngine.Editor.Controls.WorldControls;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using XNAGizmo;

namespace CasaEngine.Editor.Controls.EntityControls;

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

        var entitiesViewModel = DataContext as EntitiesViewModel;
        var newEntities = new List<EntityViewModel>();

        foreach (var entity in entities.Cast<Entity>())
        {
            var newEntity = entity.Clone();
            newEntity.Initialize(Game);
            newEntities.Add(entitiesViewModel.Add(newEntity));
        }

        _gizmoComponent.Gizmo.Clear();
        foreach (var entityViewModel in newEntities)
        {
            _gizmoComponent.Gizmo.Selection.Add(entityViewModel.Entity);
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
            SelectedItem = selectedEntity;

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
            //TODO : Camera in front of the Object
        }
    }
}