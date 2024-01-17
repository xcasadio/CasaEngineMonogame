using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Editor.Tools;
using CasaEngine.EditorUI.Controls.Common;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;

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
            var component = (Component)Activator.CreateInstance(componentType);
            component.Initialize(entityViewModel.Entity);
            entityViewModel.Entity.ComponentManager.Add(component);
        }
    }

    private void ButtonDeleteComponentOnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: Component component })
        {
            component.Owner.ComponentManager.Remove(component);
        }
    }
}