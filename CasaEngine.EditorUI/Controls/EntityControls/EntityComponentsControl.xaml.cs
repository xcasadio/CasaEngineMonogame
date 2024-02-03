using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.EditorUI.Controls.Common;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;
using CasaEngine.EditorUI.Plugins.Tools;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.SceneManagement.Components;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public partial class EntityComponentsControl : UserControl
{
    public CasaEngineGame Game { get; private set; }

    private GizmoComponent? _gizmoComponent;

    public EntityComponentsControl()
    {
        InitializeComponent();
    }

    public void Initialize(CasaEngineGame game)
    {
        Game = game;
        _gizmoComponent = Game.GetGameComponent<GizmoComponent>();
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
            entityViewModel.Entity.AddComponent(component);
            component.Initialize();
            component.InitializeWithWorld(Game.GameManager.CurrentWorld);
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

    private void OnComponentSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0)
        {
            _gizmoComponent.Gizmo.Clear();
        }
        else if (e.AddedItems[0] is ComponentViewModel componentViewModel) // TODO : bug : combox is selected ???
        {
            _gizmoComponent.Gizmo.Clear(); // TODO : select in one function

            if (componentViewModel.Component is SceneComponent sceneComponent)
            {
                _gizmoComponent.Gizmo.Selection.Add(sceneComponent);
            }
        }
    }
}