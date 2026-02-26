using System;
using CasaEngine.EditorUI.Controls;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.Rendering;
using XNAGizmo;

namespace CasaEngine.EditorUI.Controls.WorldControls.ViewModels;

public class WorldEditorViewModel : NotifyPropertyChangeBase
{
    private CasaEngineGame? _game;
    private GizmoComponent? _gizmoComponent;
    private EntityListViewModel _entitiesViewModel;

    public EntityListViewModel EntitiesViewModel
    {
        get => _entitiesViewModel;
        private set => SetField(ref _entitiesViewModel, value);
    }

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

    /// <summary>
    /// Multi-view-architecture constructor.  Uses <paramref name="host"/> and
    /// <paramref name="viewId"/> instead of an individual <see cref="GameEditorWorld"/>.
    /// </summary>
    public WorldEditorViewModel(EngineHost host, ViewId viewId)
    {
        void Wire()
        {
            _game = host.Game;
            _gizmoComponent = host.GetViewContext(viewId)?.Gizmo
                           ?? _game?.GetGameComponent<GizmoComponent>();

            if (_game != null)
                _game.GameManager.WorldChanged += OnWorldChanged;

            if (_gizmoComponent != null)
            {
                _gizmoComponent.Gizmo.GizmoModeChangedEvent   += OnGizmoModeChangedEvent;
                _gizmoComponent.Gizmo.TransformSpaceChangedEvent += OnTransformSpaceChangedEvent;
            }

            OnGizmoModeChangedEvent(this, EventArgs.Empty);
            OnTransformSpaceChangedEvent(this, EventArgs.Empty);
            OnWorldChanged(this, EventArgs.Empty);
        }

        if (host.IsStarted) Wire();
        else host.Started += (_, _) => Wire();
    }

    private void OnWorldChanged(object? sender, EventArgs e)
    {
        var worldViewModel = new RootNodeEntityViewModel(_game.GameManager.CurrentWorld);
        EntitiesViewModel = new EntityListViewModel(worldViewModel);
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
}