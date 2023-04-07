using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Core.Logger;
using CasaEngine.Framework;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using XNAGizmo;

namespace EditorWpf.Controls
{
    public partial class GameScreenControl : UserControl
    {
        public GameScreenControl()
        {
            InitializeComponent();
        }

        private void ButtonLaunchGame_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ButtonSave_OnClick(object sender, RoutedEventArgs e)
        {
            GameInfo.Instance.CurrentWorld.Save();
            LogManager.Instance.WriteLine($"World {GameInfo.Instance.CurrentWorld.Name} saved '{Path.Combine(EngineComponents.ProjectManager.ProjectPath, GameInfo.Instance.CurrentWorld.Name + ".json")}'");
        }

        private void ButtonTranslate_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as GameScreenControlViewModel).IsTranslationMode = true;
        }

        private void ButtonRotate_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as GameScreenControlViewModel).IsRotationMode = true;
        }

        private void ButtonScale_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as GameScreenControlViewModel).IsScaleMode = true;
        }

        private void ButtonLocalSpace_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as GameScreenControlViewModel).IsTransformSpaceLocal = true;
        }

        private void ButtonWorldSpace_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as GameScreenControlViewModel).IsTransformSpaceWorld = true;
        }
    }

    public class GameScreenControlViewModel : INotifyPropertyChanged
    {
        private GizmoComponent? _gizmoComponent;

        public bool IsTranslationMode
        {
            get => _gizmoComponent?.Gizmo.ActiveMode == GizmoMode.Translate;
            set
            {
                _gizmoComponent.Gizmo.ActiveMode = GizmoMode.Translate;
                OnGizmoModeChangedEvent(this, EventArgs.Empty);
            }
        }

        public bool IsRotationMode
        {
            get => _gizmoComponent?.Gizmo.ActiveMode == GizmoMode.Rotate;
            set
            {
                _gizmoComponent.Gizmo.ActiveMode = GizmoMode.Rotate;
                OnGizmoModeChangedEvent(this, EventArgs.Empty);
            }
        }

        public bool IsScaleMode
        {
            get => _gizmoComponent?.Gizmo.ActiveMode == GizmoMode.NonUniformScale;
            set
            {
                _gizmoComponent.Gizmo.ActiveMode = GizmoMode.NonUniformScale;
                OnGizmoModeChangedEvent(this, EventArgs.Empty);
            }
        }

        public bool IsTransformSpaceLocal
        {
            get => _gizmoComponent?.Gizmo.ActiveSpace == TransformSpace.Local;
            set
            {
                _gizmoComponent.Gizmo.ActiveSpace = TransformSpace.Local;
                OnTransformSpaceChangedEvent(this, EventArgs.Empty);
            }
        }

        public bool IsTransformSpaceWorld
        {
            get => _gizmoComponent?.Gizmo.ActiveSpace == TransformSpace.World;
            set
            {
                _gizmoComponent.Gizmo.ActiveSpace = TransformSpace.World;
                OnTransformSpaceChangedEvent(this, EventArgs.Empty);
            }
        }

        public GameScreenControlViewModel()
        {
            GameInfo.Instance.ReadyToStart += OnGameReadyToStart;
        }

        private void OnGameReadyToStart(object? sender, System.EventArgs e)
        {
            _gizmoComponent = EngineComponents.Game.GetGameComponent<GizmoComponent>();
            _gizmoComponent.Gizmo.GizmoModeChangedEvent += OnGizmoModeChangedEvent;
            _gizmoComponent.Gizmo.TransformSpaceChangedEvent += OnTransformSpaceChangedEvent;

            OnGizmoModeChangedEvent(this, EventArgs.Empty);
            OnTransformSpaceChangedEvent(this, EventArgs.Empty);
        }

        private void OnGizmoModeChangedEvent(object? sender, System.EventArgs e)
        {
            OnPropertyChanged(nameof(IsTranslationMode));
            OnPropertyChanged(nameof(IsRotationMode));
            OnPropertyChanged(nameof(IsScaleMode));
        }

        private void OnTransformSpaceChangedEvent(object? sender, System.EventArgs e)
        {
            OnPropertyChanged(nameof(IsTransformSpaceLocal));
            OnPropertyChanged(nameof(IsTransformSpaceWorld));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
