using System;
using CasaEngine.Engine.Input.InputDeviceStateProviders;
using CasaEngine.Editor.Runtime;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.World;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Diagnostics;
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
    private readonly MGWindow _window;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly HostedEditorGameAdapter _editorRuntime;
    private readonly IWindowInputSource _windowInputSource;

    private MGDockPanel _viewportHost = null!;
    private MGImage _viewportImage = null!;

    private RenderTargetSurface? _surface;
    private RenderView? _renderView;
    private Texture2D? _boundTexture;
    private World? _fallbackWorld;
    private Entity? _cameraEntity;
    private ArcBallCameraComponent? _camera;
    private readonly EditorViewportCameraController _cameraController = new();
    private readonly EditorViewportGizmoController _gizmoController;
    private KeyboardStateProvider? _keyboardProvider;
    private IMouseStateProvider? _mouseProvider;
    private int _rtWidth = 16;
    private int _rtHeight = 16;

    internal WorldViewportPanel(MGWindow window, GraphicsDevice graphicsDevice, HostedEditorGameAdapter editorRuntime, IWindowInputSource windowInputSource)
    {
        _window = window;
        _graphicsDevice = graphicsDevice;
        _editorRuntime = editorRuntime;
        _windowInputSource = windowInputSource;
        _gizmoController = new EditorViewportGizmoController(editorRuntime);
    }

    public MGElement CreateContent()
    {
        EnsureRenderViewCreated();

        _viewportHost = new MGDockPanel(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsFocusable = true,
            Name = "WorldViewportHost",
        };

        _viewportHost.OnLayoutBoundsChanged += OnViewportBoundsChanged;
        _viewportHost.MouseHandler.PressedInside += (_, e) =>
            Debug.WriteLine($"[WorldViewportPanel] PressedInside button={e.Button} pos={e.Position} hovered={_viewportHost.IsHovered} bounds={_viewportHost.LayoutBounds}");
        _viewportHost.MouseHandler.LMBPressedInside += (_, e) =>
        {
            Debug.WriteLine($"[WorldViewportPanel] LMBPressedInside pos={e.Position} hovered={_viewportHost.IsHovered} bounds={_viewportHost.LayoutBounds}");
            ActivateThisView(captureInput: false);
        };
        _viewportHost.MouseHandler.LMBReleasedInside += (_, e) =>
            Debug.WriteLine($"[WorldViewportPanel] LMBReleasedInside pos={e.Position} hovered={_viewportHost.IsHovered} bounds={_viewportHost.LayoutBounds}");
        _viewportHost.MouseHandler.LMBClickedInside += (_, e) =>
        {
            Debug.WriteLine($"[WorldViewportPanel] LMBClickedInside pos={e.Position} hovered={_viewportHost.IsHovered} double={e.IsDoubleClick}");
            ActivateThisView(captureInput: false);
        };

        _keyboardProvider ??= new KeyboardStateProvider();
        _mouseProvider ??= new ViewportRelativeMouseStateProvider(_windowInputSource, () => _viewportHost?.LayoutBounds ?? Rectangle.Empty);

        _viewportImage = new MGImage(_window, new MGTextureData(_surface!.Texture!), Stretch: Stretch.Fill)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = false,
            Name = "WorldViewportImage",
        };

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
        bool receivesInput = _renderView != null && inputContext.ViewId == _renderView.Id;
        bool isViewportInteractive = receivesInput || isKeyboardFocused;

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

        UpdateGizmoInput(gameTime, inputContext, isViewportInteractive);
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
        EnsureEditorGizmo(world);
    }

    private void RegisterViewportInput()
    {
        if (_renderView == null || _keyboardProvider == null || _mouseProvider == null)
        {
            return;
        }

        _editorRuntime.InputComponent.InputRouter?.RegisterViewInput(
            _renderView.Id,
            _keyboardProvider,
            _mouseProvider);

        Debug.WriteLine($"[WorldViewportPanel] RegisterViewInput view={_renderView.Id} bounds={_viewportHost.LayoutBounds}");
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
        Debug.WriteLine($"[WorldViewportPanel] ActivateThisView view={_renderView.Id} capture={captureInput}");

        if (captureInput)
        {
            _editorRuntime.GameManager.ViewManager.CaptureInput(_renderView);
            Debug.WriteLine($"[WorldViewportPanel] CaptureInput view={_renderView.Id}");
        }
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
        if (ReferenceEquals(_renderView.World, desiredWorld))
        {
            return;
        }

        _renderView.World = desiredWorld;
        _cameraEntity?.InitializeWithWorld(desiredWorld);
        _camera?.OnScreenResized(_rtWidth, _rtHeight);

        _gizmoController.ResetWorld(desiredWorld);
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

    private void SynchronizeGizmo()
    {
        _gizmoController.Synchronize(_camera, _surface, _renderView?.World);
    }

    private void UpdateGizmoInput(GameTime gameTime, ViewInputContext inputContext, bool isViewportInteractive)
    {
        _gizmoController.Update(gameTime, inputContext, isViewportInteractive, _camera, _surface, _renderView?.World);
    }

    public void Dispose()
    {
        if (_renderView != null)
        {
            _editorRuntime.InputComponent.InputRouter?.UnregisterViewInput(_renderView.Id);
        }

        _gizmoController.Dispose();

        if (_renderView != null)
        {
            _editorRuntime.GameManager.ViewManager.Remove(_renderView);
            _renderView = null;
        }

        _surface?.Dispose();
    }
}
