using System;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.WpfControls;

namespace CasaEngine.Editor.Controls.EntityControls
{
    public partial class EntityControl : UserControl
    {
        public EntityControl()
        {
            DataContextChanged += OnDataContextChanged;

            InitializeComponent();
        }

        public void InitializeFromGameEditor(GameEditor gameEditor)
        {
            gameEditor.GameStarted += OnGameStarted;
        }

        private void OnGameStarted(object? sender, EventArgs e)
        {
            var casaEngineGame = (CasaEngineGame)sender;
            entityComponentsControl.Game = casaEngineGame;

            casaEngineGame.GameManager.FrameComputed += OnFrameComputed;
        }

        private void OnFrameComputed(object? sender, EventArgs e)
        {
            if (sender is GameManager gameManager && gameManager.IsRunningInGameEditorMode)
            {
                var expression = Vector3ControlPosition.GetBindingExpression(Vector3Editor.ValueProperty);
                expression?.UpdateTarget();

                expression = RotationControl.GetBindingExpression(RotationEditor.ValueProperty);
                expression?.UpdateTarget();

                expression = Vector3ControlScale.GetBindingExpression(Vector3Editor.ValueProperty);
                expression?.UpdateTarget();
            }
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is Entity oldEntity)
            {
                oldEntity.PositionChanged -= OnEntityPositionChanged;
                oldEntity.OrientationChanged -= OnEntityOrientationChanged;
                oldEntity.ScaleChanged -= OnEntityScaleChanged;
            }

            //Attach event because we can modify coordinate with the mouse with the game screen
            if (e.NewValue is Entity entity)
            {
                entity.PositionChanged += OnEntityPositionChanged;
                entity.OrientationChanged += OnEntityOrientationChanged;
                entity.ScaleChanged += OnEntityScaleChanged;
            }
        }

        private void OnEntityOrientationChanged(object? sender, EventArgs e)
        {
            var entity = sender as Entity;
            RotationControl.Value = entity.Coordinates.LocalRotation;
        }

        private void OnEntityPositionChanged(object? sender, EventArgs e)
        {
            var entity = sender as Entity;
            Vector3ControlPosition.Value = entity.Coordinates.LocalPosition;
        }

        private void OnEntityScaleChanged(object? sender, EventArgs e)
        {
            var entity = sender as Entity;
            Vector3ControlScale.Value = entity.Scale;
        }
    }
}
