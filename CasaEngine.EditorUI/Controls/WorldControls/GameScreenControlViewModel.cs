using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using XNAGizmo;

namespace CasaEngine.Editor.Controls.WorldControls;

public class GameScreenControlViewModel : INotifyPropertyChanged
{
    private readonly CasaEngineGame? _game;
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

    public GameScreenControlViewModel(GameEditorWorld gameEditorWorld)
    {
        _game = gameEditorWorld.Game;
        gameEditorWorld.GameStarted += OnGameGameStarted;
    }

    private void OnGameGameStarted(object? sender, EventArgs e)
    {
        CasaEngineGame game = (CasaEngineGame)sender;
        _gizmoComponent = game.GetGameComponent<GizmoComponent>();
        _gizmoComponent.Gizmo.GizmoModeChangedEvent += OnGizmoModeChangedEvent;
        _gizmoComponent.Gizmo.TransformSpaceChangedEvent += OnTransformSpaceChangedEvent;

        OnGizmoModeChangedEvent(this, EventArgs.Empty);
        OnTransformSpaceChangedEvent(this, EventArgs.Empty);
    }

    private void OnGizmoModeChangedEvent(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsTranslationMode));
        OnPropertyChanged(nameof(IsRotationMode));
        OnPropertyChanged(nameof(IsScaleMode));
    }

    private void OnTransformSpaceChangedEvent(object? sender, EventArgs e)
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