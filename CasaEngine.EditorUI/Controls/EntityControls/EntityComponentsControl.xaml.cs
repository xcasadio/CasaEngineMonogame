using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.EditorUI.Controls.Common;
using CasaEngine.EditorUI.Plugins.Tools;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.SceneManagement.Components;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public partial class EntityComponentsControl : UserControl
{
    public CasaEngineGame Game { get; private set; }

    public EntityComponentsControl()
    {
        InitializeComponent();
    }

    public void Initialize(CasaEngineGame game)
    {
        Game = game;
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
}