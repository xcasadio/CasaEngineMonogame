using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.EditorUI.Controls.Common;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;
using CasaEngine.EditorUI.Plugins.Tools;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.SceneManagement.Components;
using XNAGizmo;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public partial class EntityControl : UserControl
{
    public static readonly DependencyProperty SelectedComponentProperty = DependencyProperty.Register(nameof(SelectedComponent), typeof(ComponentViewModel), typeof(EntityControl));

    public ComponentViewModel? SelectedComponent
    {
        get => (ComponentViewModel?)GetValue(SelectedComponentProperty);
        set => SetValue(SelectedComponentProperty, value);
    }

    private CasaEngineGame? _game;
    private GizmoComponent? _gizmoComponent;

    public EntityControl()
    {
        InitializeComponent();
        treeViewComponents.ItemMoved = ItemMoved;
    }

    private void ItemMoved(object item, object newparent)
    {
        if (item is ComponentViewModel componentViewModel && newparent is ComponentViewModel parentComponentViewModel)
        {
            if (componentViewModel.Parent != null)
            {
                componentViewModel.Parent.RemoveComponent(componentViewModel);
            }

            parentComponentViewModel.AddComponent(componentViewModel);
        }
    }

    public void InitializeFromGameEditor(GameEditor gameEditor)
    {
        gameEditor.GameStarted += OnGameStarted;
    }

    private void OnGameStarted(object? sender, EventArgs e)
    {
        _game = (CasaEngineGame)sender;
        _gizmoComponent = _game.GetGameComponent<GizmoComponent>();
        _gizmoComponent.Gizmo.SelectionChanged -= OnEntitiesSelectionChanged;
        _gizmoComponent.Gizmo.SelectionChanged += OnEntitiesSelectionChanged;
    }

    private void OnEntitiesSelectionChanged(object? sender, List<ITransformable> entities)
    {
        //if (!_isSelectionTriggerFromGizmoActive)
        //{
        //    return;
        //}
        //_isSelectionTriggerActive = false;

        SceneComponent? selectedSceneComponent = null;
        if (_gizmoComponent.Gizmo.Selection.Count > 0)
        {
            selectedSceneComponent = (SceneComponent)_gizmoComponent.Gizmo.Selection[0];
        }

        if (selectedSceneComponent != null)
        {
            var entityViewModel = DataContext as EntityViewModel;
            SetSelectedItem(entityViewModel.GetFromComponent(selectedSceneComponent));
        }

        //_isSelectionTriggerActive = true;
    }


    private void SetSelectedItem(ComponentViewModel? componentViewModel)
    {
        if (componentViewModel != null && SelectedComponent != componentViewModel)
        {
            SelectedComponent = componentViewModel;
            SelectTreeViewItem(SelectedComponent);
        }
        else
        {
            foreach (var item in treeViewComponents.ItemContainerGenerator.Items)
            {
                SelectTreeViewItem(item as ComponentViewModel, false, false, true, false);
            }
        }
    }

    private void SelectTreeViewItem(ComponentViewModel componentViewModel, bool alterExpand = true, bool expand = true, bool alterSelect = true, bool select = true)
    {
        Stack<ComponentViewModel> parents = new();
        parents.Push(componentViewModel);
        var root = componentViewModel;

        while (root != null)
        {
            parents.Push(root);
            root = root.Parent;
        }

        ItemsControl itemsControl = treeViewComponents;

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

                treeViewItem.UpdateLayout();
            }
        }
    }

    private void ButtonRenameEntity_OnClick(object sender, RoutedEventArgs e)
    {
        var inputTextBox = new InputTextBox
        {
            Description = "Enter a new name",
            Title = "Rename"
        };
        var entity = (DataContext as EntityViewModel);
        inputTextBox.Text = entity.Name;
        inputTextBox.Predicate = ValidateEntityNewName;

        if (inputTextBox.ShowDialog() == true)
        {
            entity.Name = inputTextBox.Text;
            //AssetCatalog.Rename(entity.Entity.Id, inputTextBox.Text);
        }
    }

    private bool ValidateEntityNewName(string? newName)
    {
        return AssetCatalog.CanRename(newName);
    }

    private void OnComponentSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        var componentViewModel = treeViewComponents.SelectedItem as ComponentViewModel;
        SelectedComponent = componentViewModel;

        if (componentViewModel?.Component is SceneComponent sceneComponent)
        {
            SelectInGizmo(sceneComponent);
        }
    }

    private void ButtonAddComponentClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not EntityViewModel entityViewModel)
        {
            return;
        }

        var inputComboBox = new InputComboBox(Application.Current.MainWindow)
        {
            Title = "Add a new component",
            Description = "Choose the type of component to add",
            Items = ElementRegister.EntityComponentNames.Keys.ToList()
        };

        if (inputComboBox.ShowDialog() == true && inputComboBox.SelectedItem != null)
        {
            var componentType = ElementRegister.EntityComponentNames[inputComboBox.SelectedItem];
            var component = (EntityComponent)Activator.CreateInstance(componentType);
            var componentViewModel = new ComponentViewModel(component);

            if (treeViewComponents.SelectedItem is ComponentViewModel selectedComponentViewModel)
            {
                selectedComponentViewModel.AddComponent(componentViewModel);
            }
            else
            {
                entityViewModel.AddComponent(componentViewModel);
            }

            component.Initialize();
            component.InitializeWithWorld(_game.GameManager.CurrentWorld);

            if (componentViewModel.Component is SceneComponent sceneComponent)
            {
                SelectInGizmo(sceneComponent);
            }
        }
    }

    private void SelectInGizmo(SceneComponent sceneComponent)
    {
        _gizmoComponent.Gizmo.Clear();
        _gizmoComponent.Gizmo.Selection.Add(sceneComponent);
    }
    /*
    private void ButtonDeleteComponentOnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: EntityComponent component })
        {
            var entityViewModel = DataContext as EntityViewModel;
            entityViewModel.ComponentListViewModel.RemoveComponent(component);
        }
    }*/

    private void TreeView_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && e.IsToggled)
        {
            if (treeViewComponents.SelectedItem is ComponentViewModel componentViewModel)
            {
                componentViewModel.Parent.RemoveComponent(componentViewModel);

                if (componentViewModel.Component is SceneComponent sceneComponent)
                {
                    var gizmoComponent = _game.GetGameComponent<GizmoComponent>();
                    gizmoComponent.Gizmo.Selection.Remove(sceneComponent);
                }
            }
        }
    }

    private void ComboBoxGameplayProxyClassName_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { DataContext: ComponentViewModel componentViewModel } comboBox
            && !string.IsNullOrEmpty(comboBox.SelectedValue as string))
        {
            var gameplayScriptClassName = comboBox.SelectedValue as string;

            if (componentViewModel is RootNodeComponentViewModel rootNodeComponentViewModel
                && rootNodeComponentViewModel.EntityViewModel is RootNodeEntityViewModel rootNodeEntityViewModel)
            {
                rootNodeEntityViewModel.World.GameplayProxyClassName = gameplayScriptClassName;
            }
            else
            {
                componentViewModel.Owner.GameplayProxyClassName = gameplayScriptClassName;
            }
        }
    }

    public bool ValidateDefaultPawnAsset(object owner, Guid assetId, string assetFullName)
    {
        if (owner is RootNodeComponentViewModel rootNodeComponentViewModel
            && System.IO.Path.GetExtension(assetFullName) == Constants.FileNameExtensions.Entity)
        {
            var rootNodeEntityViewModel = rootNodeComponentViewModel.EntityViewModel as RootNodeEntityViewModel;
            rootNodeEntityViewModel.World.GameMode.DefaultPawnAssetId = assetId;
            return true;
        }

        return false;
    }

    public bool ValidateGameModeAsset(object owner, Guid assetId, string assetFullName)
    {
        if (owner is RootNodeComponentViewModel rootNodeComponentViewModel
            && System.IO.Path.GetExtension(assetFullName) == Constants.FileNameExtensions.GameMode)
        {
            var rootNodeEntityViewModel = rootNodeComponentViewModel.EntityViewModel as RootNodeEntityViewModel;
            rootNodeEntityViewModel.World.GameModeAssetId = assetId;
            return true;
        }

        return false;
    }
}