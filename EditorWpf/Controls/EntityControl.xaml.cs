using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
            var entity = sender as Entity;
            Vector3ControlPosition.X = entity.Position.X;
            Vector3ControlPosition.Y = entity.Position.Y;
            Vector3ControlPosition.Z = entity.Position.Z;
            _doNotUpdateEntityPosition = false;
        }

        private void OnEntityScaleChanged(object? sender, EventArgs e)
        {
            _doNotUpdateEntityScale = true;
            var entity = sender as Entity;
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

            var vector3Control = sender as Vector3Control;

            if (vector3Control == null)
            {
                return;
            }

            var entity = vector3Control.DataContext as Entity;

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

            var vector3Control = sender as Vector3Control;

            if (vector3Control?.DataContext is not Entity entity)
            {
                return;
            }

            entity.Coordinates.LocalPosition = new Vector3(vector3Control.X, vector3Control.Y, vector3Control.Z);
        }
    }
}
