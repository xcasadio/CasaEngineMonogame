using System;
using CasaEngine.Editor.Runtime;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game.Components.DebugTools;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.World;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// A dockable world viewport panel backed by a hosted CasaEngine render view.
/// The editor displays the render target produced by the engine runtime instead of
/// trying to draw the world directly from the UI layer.
/// </summary>
public class WorldViewportPanel : IDisposable
{
    public event Action<Entity?>? SelectedEntityChanged;

    private sealed class ViewportHostPanel : MGDockPanel
    {
        public ViewportHostPanel(MGWindow window)
            : base(window)
        {
        }

        public override bool TryHandleNavigationAction(UINavigationAction action)
        {
            return action switch
            {
                UINavigationAction.MoveNext => true,
                UINavigationAction.MovePrevious => true,
                UINavigationAction.MoveUp => true,
                UINavigationAction.MoveDown => true,
                UINavigationAction.MoveLeft => true,
                UINavigationAction.MoveRight => true,
                _ => false,
            };
        }
    }

    private sealed class MguiViewportViewHost : IViewHost, IViewScreenBoundsHost
    {
        private readonly Func<Rectangle> _getScreenBounds;
        private bool _disposed;

        public MguiViewportViewHost(ViewId viewId, Func<Rectangle> getScreenBounds)
        {
            ViewId = viewId;
            _getScreenBounds = getScreenBounds;
        }

        public ViewId ViewId { get; }

        public int Width => ScreenBounds.Width;

        public int Height => ScreenBounds.Height;

        public bool IsVisible => ScreenBounds.Width > 0 && ScreenBounds.Height > 0;

        public Rectangle ScreenBounds => _getScreenBounds();

        public event Action<IViewHost, int, int>? Resized;

        public event Action<IViewHost>? Closed;

        public void NotifyResized(int newWidth, int newHeight)
        {
            Resized?.Invoke(this, newWidth, newHeight);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Closed?.Invoke(this);
        }
    }

    private readonly MGWindow _window;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly HostedEditorGameAdapter _editorRuntime;
    private readonly IWindowInputSource _windowInputSource;

    private MGDockPanel _viewportHost = null!;
    private MGImage _viewportImage = null!;

    private RenderTargetSurface? _surface;
    private RenderView? _renderView;
    private MguiViewportViewHost? _renderViewHost;
    private DebugGridComponent? _grid;
    private DebugAxisComponent? _axis;
    private Texture2D? _boundTexture;
    private World? _fallbackWorld;
    private World? _observedWorld;
    private Entity? _cameraEntity;
    private Entity? _selectedEntity;
    private ArcBallCameraComponent? _camera;
    private readonly EditorViewportCameraController _cameraController = new();
    private readonly EditorViewportGizmoController _gizmoController;
    private int _rtWidth = 16;
    private int _rtHeight = 16;

    internal WorldViewportPanel(MGWindow window, GraphicsDevice graphicsDevice, HostedEditorGameAdapter editorRuntime, IWindowInputSource windowInputSource)
    {
        _window = window;
        _graphicsDevice = graphicsDevice;
        _editorRuntime = editorRuntime;
        _windowInputSource = windowInputSource;
        _gizmoController = new EditorViewportGizmoController(editorRuntime);
        _gizmoController.SelectedEntityChanged += OnGizmoSelectedEntityChanged;
    }

    public MGElement CreateContent()
    {
        if (_viewportHost != null)
        {
            return _viewportHost;
        }

        EnsureRenderViewCreated();

        _viewportHost = new ViewportHostPanel(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsFocusable = true,
        };

        _viewportHost.OnLayoutBoundsChanged += OnViewportBoundsChanged;
        _viewportHost.MouseHandler.LMBPressedInside += (_, e) =>
        {
            ActivateThisView(captureInput: false);
        };
        _viewportHost.MouseHandler.LMBClickedInside += (_, e) =>
        {
            ActivateThisView(captureInput: false);
        };

        _viewportImage = new MGImage(_window, new MGTextureData(_surface!.Texture!), Stretch: Stretch.Fill)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = false,
        };

        if (_renderView != null && _renderView.Host != null)
        {
            _editorRuntime.GameManager.ViewManager.HookViewHost(_renderView);
        }

        RegisterViewportInput();

        _viewportHost.TryAddChild(_viewportImage, Dock.Top);

        return _viewportHost;
    }

    public void DrawViewport(GameTime gameTime)
    {
        SynchronizeRenderViewWorld();
        SynchronizeCamera();
        SynchronizeGizmo();
        RefreshTextureBinding();
    }

    public void UpdateInput(GameTime gameTime)
    {
        if (_viewportHost == null)
        {
            return;
        }

        var router = _editorRuntime.InputComponent.InputRouter;
        bool isKeyboardFocused = _renderView != null && (router?.KeyboardFocusViewId ?? ViewId.Empty) == _renderView.Id;

        var inputContext = _editorRuntime.InputComponent.CurrentViewInputContext;
        bool receivesInput = _renderView != null && IsPointerInputRoutedToView(inputContext, _renderView.Id);
        _gizmoController.Deactivate();

        if (_camera != null)
        {
            _cameraController.Update(
                gameTime,
                _camera,
                inputContext,
                receivesInput,
                isKeyboardFocused,
                ActivateThisView,
                () => _editorRuntime.GameManager.ViewManager.ReleaseInput());
        }

        UpdateGizmoInput(gameTime, inputContext, receivesInput, isKeyboardFocused);
    }

    private static bool IsPointerInputRoutedToView(ViewInputContext inputContext, ViewId viewId)
    {
        if (viewId.IsEmpty || inputContext.ViewId != viewId)
        {
            return false;
        }

        return inputContext.RoutingState.Reason is InputRoutingReason.Pointer
            or InputRoutingReason.Capture
            or InputRoutingReason.UIPointerCapture
            or InputRoutingReason.Modal;
    }

    public void SetSelectedEntity(Entity? entity)
    {
        _selectedEntity = entity;
        _gizmoController.SetSelectedEntity(entity);
    }

    public void FocusEntity(Entity? entity)
    {
        if (entity?.RootComponent == null || _camera == null)
        {
            return;
        }

        var bounds = entity.GetBoundingBox();
        var diagonal = Vector3.Distance(bounds.Min, bounds.Max);
        var distance = Math.Max(5f, diagonal <= 0f ? 10f : diagonal * 1.5f);

        _cameraController.Focus(entity.RootComponent.Position, distance);
        _cameraController.ApplyTo(_camera);
    }

    private void OnViewportBoundsChanged(object? sender, EventArgs<Rectangle> e)
    {
        var newBounds = e.NewValue;
        var width = Math.Max(16, newBounds.Width);
        var height = Math.Max(16, newBounds.Height);

        if (width == _rtWidth && height == _rtHeight)
        {
            return;
        }

        _rtWidth = width;
        _rtHeight = height;

        _renderViewHost?.NotifyResized(width, height);

        _surface?.EnsureSize(width, height);
        _camera?.OnScreenResized(width, height);
        RefreshTextureBinding();
    }

    private void EnsureRenderViewCreated()
    {
        if (_renderView != null)
        {
            return;
        }

        var world = GetRenderWorld();

        _cameraEntity = new Entity
        {
            Name = "EditorViewportCamera",
            IsVisible = false,
        };

        _camera = _cameraController.CreateCameraComponent();

        _cameraEntity.AddComponent(_camera);
        _cameraEntity.Initialize();
        _cameraEntity.InitializeWithWorld(world);
        _camera.OnScreenResized(_rtWidth, _rtHeight);

        _surface = new RenderTargetSurface(
            _graphicsDevice,
            _rtWidth,
            _rtHeight,
            renderTargetPool: _editorRuntime.RenderTargetPool);

        var viewId = _editorRuntime.GameManager.ViewManager.CreateView(new ViewDefinition
        {
            Name = "World Viewport",
            World = world,
            Camera = _camera,
            Surface = _surface,
            ClearColor = Color.DimGray,
            UpdateMode = ViewUpdateMode.RealTime,
        });

        if (!_editorRuntime.GameManager.ViewManager.TryGetView(viewId, out var renderView))
        {
            throw new InvalidOperationException("The world viewport could not create its render view.");
        }

        _renderView = renderView;
        // Only report real bounds when the element is in the visual tree (active dock tab).
        // When the viewport is a background tab, SetParent(null) detaches it but LayoutBounds
        // retains its last value → ScreenToView would incorrectly route input to this view.
        _renderViewHost = new MguiViewportViewHost(renderView.Id,
            () => _viewportHost?.Parent != null ? _viewportHost.LayoutBounds : Rectangle.Empty);
        _renderView.Host = _renderViewHost;
        AttachWorld(world);
        EnsureEditorOverlays(world);
        EnsureEditorGizmo(world);
        _gizmoController.SetSelectedEntity(_selectedEntity);
    }

    private void RegisterViewportInput()
    {
        if (_renderView == null)
        {
            return;
        }

        _editorRuntime.InputComponent.InputRouter?.RegisterViewInput(_renderView.Id, _windowInputSource);
    }

    private void ActivateThisView(bool captureInput)
    {
        if (_renderView == null)
        {
            return;
        }

        _editorRuntime.GameManager.ViewManager.SetActive(_renderView);
        _editorRuntime.InputComponent.InputRouter?.SetKeyboardFocus(_renderView.Id);
        _viewportHost?.Focus(MGUI.Core.UI.KeyboardFocusSource.Pointer);

        if (captureInput)
        {
            _editorRuntime.GameManager.ViewManager.CaptureInput(_renderView);
        }
    }

    public void ReleaseInputIfOutside(Point screenPosition)
    {
        if (_renderView == null || _viewportHost == null)
        {
            return;
        }

        // Guard: treat detached (background-tab) viewport as if it has no bounds,
        // so clicks in the document area correctly release input / clear keyboard focus.
        if (_viewportHost.Parent != null && _viewportHost.LayoutBounds.Contains(screenPosition))
        {
            return;
        }

        _editorRuntime.GameManager.ViewManager.ReleaseInput();
        _editorRuntime.InputComponent.InputRouter?.ClearKeyboardFocus(_renderView.Id);
    }

    private World GetRenderWorld()
    {
        var currentWorld = _editorRuntime.GameManager.CurrentWorld;
        if (currentWorld != null)
        {
            return currentWorld;
        }

        _fallbackWorld ??= CreateFallbackWorld();
        return _fallbackWorld;
    }

    private World CreateFallbackWorld()
    {
        var world = new World
        {
            Name = "EditorPreviewWorld",
        };

        world.LoadContent(_editorRuntime);
        return world;
    }

    private void SynchronizeRenderViewWorld()
    {
        if (_renderView == null)
        {
            return;
        }

        var desiredWorld = _editorRuntime.GameManager.CurrentWorld ?? _fallbackWorld ?? CreateFallbackWorld();
        AttachWorld(desiredWorld);
        if (ReferenceEquals(_renderView.World, desiredWorld))
        {
            return;
        }

        _renderView.World = desiredWorld;
        _cameraEntity?.InitializeWithWorld(desiredWorld);
        _camera?.OnScreenResized(_rtWidth, _rtHeight);

        _gizmoController.ResetWorld(desiredWorld);
        if (_selectedEntity?.World != desiredWorld)
        {
            _selectedEntity = null;
            SelectedEntityChanged?.Invoke(null);
        }

        _gizmoController.SetSelectedEntity(_selectedEntity);
    }

    private void AttachWorld(World world)
    {
        if (ReferenceEquals(_observedWorld, world))
        {
            return;
        }

        DetachWorld();
        _observedWorld = world;
        _observedWorld.EntityAdded += OnWorldEntityAdded;
        _observedWorld.EntityRemoved += OnWorldEntityRemoved;
        _observedWorld.EntitiesCleared += OnWorldEntitiesCleared;
    }

    private void DetachWorld()
    {
        if (_observedWorld == null)
        {
            return;
        }

        _observedWorld.EntityAdded -= OnWorldEntityAdded;
        _observedWorld.EntityRemoved -= OnWorldEntityRemoved;
        _observedWorld.EntitiesCleared -= OnWorldEntitiesCleared;
        _observedWorld = null;
    }

    private void OnWorldEntityAdded(object? sender, Entity entity)
    {
        if (sender is World world)
        {
            _gizmoController.RefreshWorldSelection(world, _selectedEntity);
        }
    }

    private void OnWorldEntityRemoved(object? sender, Entity entity)
    {
        if (ReferenceEquals(_selectedEntity, entity))
        {
            _selectedEntity = null;
            SelectedEntityChanged?.Invoke(null);
        }

        if (sender is World world)
        {
            _gizmoController.RefreshWorldSelection(world, _selectedEntity);
        }
    }

    private void OnWorldEntitiesCleared(object? sender, EventArgs e)
    {
        _selectedEntity = null;
        SelectedEntityChanged?.Invoke(null);

        if (sender is World world)
        {
            _gizmoController.RefreshWorldSelection(world, null);
        }
    }

    private void OnGizmoSelectedEntityChanged(Entity? entity)
    {
        _selectedEntity = entity;
        SelectedEntityChanged?.Invoke(entity);
    }

    private void SynchronizeCamera()
    {
        if (_camera == null)
        {
            return;
        }

        _cameraController.ApplyTo(_camera);
    }

    private void RefreshTextureBinding()
    {
        var texture = _surface?.Texture;
        if (texture == null || ReferenceEquals(texture, _boundTexture))
        {
            return;
        }

        _boundTexture = texture;

        if (_viewportImage != null)
        {
            _viewportImage.Source = new MGTextureData(texture);
        }
    }

    private void EnsureEditorGizmo(World world)
    {
        _gizmoController.EnsureInitialized(_renderView, _camera, _surface, world);
    }

    private void EnsureEditorOverlays(World world)
    {
        if (_renderView == null)
        {
            return;
        }

        _grid ??= CreateGridComponent();
        _axis ??= CreateAxisComponent();

        var overlayPipeline = _renderView.Pipeline as OverlayViewPipeline ?? new OverlayViewPipeline();
        overlayPipeline.RenderGridAction = (graphicsDevice, _, frame) => _grid?.DrawForView(graphicsDevice, in frame);
        overlayPipeline.RenderAxisAction = (graphicsDevice, _, frame) => _axis?.DrawForView(graphicsDevice, in frame);
        _renderView.Pipeline = overlayPipeline;
    }

    private DebugGridComponent CreateGridComponent()
    {
        var grid = new DebugGridComponent(_editorRuntime);
        grid.Initialize();
        return grid;
    }

    private DebugAxisComponent CreateAxisComponent()
    {
        var axis = new DebugAxisComponent(_editorRuntime);
        axis.Initialize();
        return axis;
    }

    private void SynchronizeGizmo()
    {
        _gizmoController.Synchronize(_camera, _surface, _renderView?.World);
    }

    private void UpdateGizmoInput(GameTime gameTime, ViewInputContext inputContext, bool receivesInput, bool isKeyboardFocused)
    {
        _gizmoController.Update(gameTime, inputContext, receivesInput, isKeyboardFocused, _camera, _surface, _renderView?.World);
    }

    public void Dispose()
    {
        DetachWorld();

        if (_renderView != null)
        {
            _editorRuntime.InputComponent.InputRouter?.UnregisterViewInput(_renderView.Id);
            if (_renderView.Host != null)
            {
                _editorRuntime.GameManager.ViewManager.UnhookViewHost(_renderView);
                _renderView.Host = null;
            }
        }

        _renderViewHost?.Dispose();
        _renderViewHost = null;

        _gizmoController.Dispose();
        DisposeOverlayComponent(_grid);
        DisposeOverlayComponent(_axis);
        _grid = null;
        _axis = null;

        if (_renderView != null)
        {
            _editorRuntime.GameManager.ViewManager.Remove(_renderView);
            _renderView = null;
        }

        _surface?.Dispose();
    }

    private void DisposeOverlayComponent(DrawableGameComponent? component)
    {
        if (component == null)
        {
            return;
        }

        _editorRuntime.Components.Remove(component);
        component.Dispose();
    }
}
