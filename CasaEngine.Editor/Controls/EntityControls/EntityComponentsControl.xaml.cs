using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Editor.Controls.Common;
using CasaEngine.Editor.Tools;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;

namespace CasaEngine.Editor.Controls.EntityControls
{
    public partial class EntityComponentsControl : UserControl
    {
        public EntityComponentsControl()
        {
            InitializeComponent();
        }

        public CasaEngineGame Game { get; set; }

        private void OnComponentsChanged(object? sender, EventArgs e)
        {
            if (sender is Entity entity)
            {
                RefreshComponentsList(entity);
            }
        }

        private void RefreshComponentsList(Entity entity)
        {
            ListBoxComponents.DataContext = null;
            ListBoxComponents.DataContext = entity;
        }


        private void ButtonAddComponentClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is not Entity entity)
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
                component.Initialize(entity, Game);
                entity.ComponentManager.Components.Add(component);

                RefreshComponentsList(entity);
            }
        }

        private void ButtonDeleteComponentOnClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: Component component })
            {
                component.Owner.ComponentManager.Components.Remove(component);
                RefreshComponentsList(component.Owner);
            }
        }
    }
}
