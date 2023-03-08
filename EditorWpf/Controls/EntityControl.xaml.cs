using System;
using System.Linq;
using System.Net.Mime;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CasaEngine.Editor.Tools;
using CasaEngine.Framework.Entities;
using EditorWpf.Controls.Common;
using Microsoft.Xna.Framework;

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

            Vector3ControlPosition.PropertyChanged += OnPositionPropertyChanged;
            Vector3ControlScale.PropertyChanged += OnScalePropertyChanged;

            //var timer = new Timer(1);
            //timer.Elapsed += Timer_Elapsed;
            //timer.Start();
        }

        private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (DataContext is Entity entity)
                {
                    Vector3ControlPosition.Value = entity.Coordinates.LocalPosition;
                }
            });
        }

        private void OnScalePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var vector3Control = (sender as Vector3Control);

            if (vector3Control == null)
            {
                return;
            }

            var entity = (vector3Control.DataContext as Entity);

            if (entity == null)
            {
                return;
            }

            entity.Coordinates.LocalScale = new Vector3(vector3Control.X, vector3Control.Y, vector3Control.Z);
        }

        private void OnPositionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var vector3Control = (sender as Vector3Control);

            if (vector3Control == null)
            {
                return;
            }

            var entity = (vector3Control.DataContext as Entity);

            if (entity == null)
            {
                return;
            }

            entity.Coordinates.LocalPosition = new Vector3(vector3Control.X, vector3Control.Y, vector3Control.Z);
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
                component.Initialize();
                entity.ComponentManager.Components.Add(component);
            }
        }

        private void ButtonDeleteComponentOnClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement frameworkElement)
            {
                if (frameworkElement.DataContext is Component component)
                {
                    component.Owner.ComponentManager.Components.Remove(component);
                }
            }
        }
    }
}
