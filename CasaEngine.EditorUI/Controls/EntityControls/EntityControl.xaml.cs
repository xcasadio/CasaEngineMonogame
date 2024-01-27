using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.EditorUI.Controls.Common;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;
using CasaEngine.EditorUI.Plugins.Tools;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.SceneManagement.Components;
using CasaEngine.WpfControls;
using Microsoft.Xna.Framework;
using Xceed.Wpf.Toolkit.Primitives;

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

    public EntityControl()
    {
        InitializeComponent();
    }

    public void InitializeFromGameEditor(GameEditor gameEditor)
    {
        gameEditor.GameStarted += OnGameStarted;
    }

    private void OnGameStarted(object? sender, EventArgs e)
    {
        _game = (CasaEngineGame)sender;
        //entityComponentsControl.Initialize(_game);
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
            GameSettings.AssetCatalog.Rename(entity.Entity.Id, inputTextBox.Text);
        }
    }

    private bool ValidateEntityNewName(string? newName)
    {
        return GameSettings.AssetCatalog.CanRename(newName);
    }

    private void OnComponentSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        SelectedComponent = treeViewComponents.SelectedItem as ComponentViewModel;
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
            var component = (ActorComponent)Activator.CreateInstance(componentType);
            var componentViewModel = new ComponentViewModel(component);

            if (treeViewComponents.SelectedItem is ComponentViewModel selectedComponentViewModel)
            {
                selectedComponentViewModel.AddChildComponent(componentViewModel);

                if (componentViewModel.Component is SceneComponent sceneComponent)
                {
                    var gizmoComponent = _game.GetGameComponent<GizmoComponent>();
                    gizmoComponent.Gizmo.Clear();
                    gizmoComponent.Gizmo.Selection.Add(sceneComponent);
                }
            }
            else
            {
                entityViewModel.AddComponent(componentViewModel);
            }

            component.Initialize();
            component.InitializeWithWorld(_game.GameManager.CurrentWorld);
        }
    }

    private void ButtonDeleteComponentOnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ActorComponent component })
        {
            var entityViewModel = DataContext as EntityViewModel;
            entityViewModel.ComponentListViewModel.RemoveComponent(component);
        }
    }

    private void TreeView_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Delete && e.IsToggled)
        {
            if (treeViewComponents.SelectedItem is ComponentViewModel componentViewModel)
            {
                var entityViewModel = DataContext as EntityViewModel;
                entityViewModel.RemoveComponent(componentViewModel);

                if (componentViewModel.Component is SceneComponent sceneComponent)
                {
                    var gizmoComponent = _game.GetGameComponent<GizmoComponent>();
                    gizmoComponent.Gizmo.Selection.Remove(sceneComponent);
                }
            }
        }
    }
}