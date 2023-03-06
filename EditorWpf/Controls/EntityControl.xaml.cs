using System;
using System.Linq;
using System.Net.Mime;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Editor.Tools;
using CasaEngine.Framework.Entities;
using EditorWpf.Controls.Common;

namespace EditorWpf.Controls
{
    /// <summary>
    /// Interaction logic for EntityControl.xaml
    /// </summary>
    public partial class EntityControl : UserControl
    {
        public EntityControl()
        {
            InitializeComponent();
        }

        private void ButtonAddComponentClick(object sender, RoutedEventArgs e)
        {
            var inputComboBox = new InputComboBox(Application.Current.MainWindow);
            inputComboBox.Title = "Add a new component";
            inputComboBox.Description = "Choose the type of component to add";
            inputComboBox.Items = ElementRegister.EntityComponentNames.Keys.ToList();

            if (inputComboBox.ShowDialog() == true
                && inputComboBox.SelectedItem != null)
            {
                var componentType = ElementRegister.EntityComponentNames[inputComboBox.SelectedItem];
                var entity = DataContext as Entity;
                var component = (Component)Activator.CreateInstance(componentType, entity);
                entity.ComponentManager.Components.Add(component);
            }
        }
    }
}
