using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Editor.Tools;
using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using EditorWpf.Controls.Common;
using EditorWpf.Windows;
using Microsoft.Xna.Framework;

namespace EditorWpf.Controls
{
    /// <summary>
    /// Interaction logic for EntityControl.xaml
    /// </summary>
    public partial class EntityControl : UserControl
    {
        private bool _doNotUpdateEntityPosition = false;
        private bool _doNotUpdateEntityScale = false;

        public EntityControl()
        {
            DataContextChanged += OnDataContextChanged;

            InitializeComponent();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is Entity oldEntity)
            {
                oldEntity.PositionChanged -= OnEntityPositionChanged;
                oldEntity.ScaleChanged -= OnEntityScaleChanged;

                Vector3ControlPosition.PropertyChanged -= OnPositionPropertyChanged;
                Vector3ControlScale.PropertyChanged -= OnScalePropertyChanged;
            }

            if (e.NewValue is Entity entity)
            {
                entity.PositionChanged += OnEntityPositionChanged;
                entity.ScaleChanged += OnEntityScaleChanged;
                OnEntityPositionChanged(entity, EventArgs.Empty);
                OnEntityScaleChanged(entity, EventArgs.Empty);

                Vector3ControlPosition.PropertyChanged += OnPositionPropertyChanged;
                Vector3ControlScale.PropertyChanged += OnScalePropertyChanged;
            }
        }

        private void OnEntityPositionChanged(object? sender, EventArgs e)
        {
            _doNotUpdateEntityPosition = true;
            var entity = (sender as Entity);
            Vector3ControlPosition.X = entity.Position.X;
            Vector3ControlPosition.Y = entity.Position.Y;
            Vector3ControlPosition.Z = entity.Position.Z;
            _doNotUpdateEntityPosition = false;
        }

        private void OnEntityScaleChanged(object? sender, EventArgs e)
        {
            _doNotUpdateEntityScale = true;
            var entity = (sender as Entity);
            Vector3ControlScale.X = entity.Scale.X;
            Vector3ControlScale.Y = entity.Scale.Y;
            Vector3ControlScale.Z = entity.Scale.Z;
            _doNotUpdateEntityScale = false;
        }

        private void OnScalePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_doNotUpdateEntityScale)
            {
                return;
            }

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
            if (_doNotUpdateEntityPosition)
            {
                return;
            }

            var vector3Control = (sender as Vector3Control);

            if (vector3Control?.DataContext is not Entity entity)
            {
                return;
            }

            entity.Coordinates.LocalPosition = new Vector3(vector3Control.X, vector3Control.Y, vector3Control.Z);
        }

        private void ButtonAddComponentClick(object sender, RoutedEventArgs e)
        {
            var inputComboBox = new InputComboBox(Application.Current.MainWindow)
            {
                Title = "Add a new component",
                Description = "Choose the type of component to add",
                Items = ElementRegister.EntityComponentNames.Keys.ToList()
            };

            if (inputComboBox.ShowDialog() == true && inputComboBox.SelectedItem != null)
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
            if (sender is FrameworkElement { DataContext: Component component })
            {
                component.Owner.ComponentManager.Components.Remove(component);
            }
        }

        private void StaticMeshComponent_SelectMesh_OnClick(object sender, RoutedEventArgs e)
        {
            var selectStaticMeshWindow = new SelectStaticMeshWindow();
            if (selectStaticMeshWindow.ShowDialog() == true)
            {
                var staticMeshComponent = (sender as FrameworkElement).DataContext as StaticMeshComponent;
                var graphicsDevice = CasaEngine.Framework.Game.Engine.Instance.Game.GraphicsDevice;

                staticMeshComponent.Mesh = ((GeometricPrimitive)Activator.CreateInstance(selectStaticMeshWindow.SelectedType, graphicsDevice)).CreateMesh();
            }
        }
    }
}
