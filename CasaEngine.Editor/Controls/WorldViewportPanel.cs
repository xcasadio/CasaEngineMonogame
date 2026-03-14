using System;
using System.Collections.Generic;
using CasaEngine.Engine.Input.InputDeviceStateProviders;
using CasaEngine.Editor.Runtime;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game.Components.DebugTools;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Transform;
using CasaEngine.Framework.World;
using GizmoTools;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input.Mouse;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Diagnostics;
using System.Runtime.InteropServices;
using WinFormsMessage = System.Windows.Forms.Message;
using WinFormsNativeWindow = System.Windows.Forms.NativeWindow;
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
    private sealed class MouseWheelMessageHook : WinFormsNativeWindow, IDisposable
    {
        private const int WM_MOUSEWHEEL = 0x020A;

        public event Action<int, Point>? MouseWheelScrolled;

        public MouseWheelMessageHook(IntPtr handle)
        {
            AssignHandle(handle);
        }

        protected override void WndProc(ref WinFormsMessage m)
        {
            if (m.Msg == WM_MOUSEWHEEL)
            {
                int delta = (short)(((uint)m.WParam.ToInt64()) >> 16);
                int screenX = (short)((uint)m.LParam.ToInt64() & 0xFFFF);
                int screenY = (short)(((uint)m.LParam.ToInt64() >> 16) & 0xFFFF);

                if (GetClientPoint(Handle, screenX, screenY, out var clientPoint))
                {
                    MouseWheelScrolled?.Invoke(delta, clientPoint);
                }
            }

            base.WndProc(ref m);
        }

        public void Dispose()
        {
            ReleaseHandle();
        }
    }

    private sealed class ViewportMouseStateProvider : IMouseStateProvider
    {
        private const int VK_LBUTTON = 0x01;
        private const int VK_RBUTTON = 0x02;
        private const int VK_MBUTTON = 0x04;
        private const int VK_XBUTTON1 = 0x05;
        private const int VK_XBUTTON2 = 0x06;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private readonly Func<Rectangle> _getBounds;
        private readonly Func<IntPtr> _getWindowHandle;
        private MouseState? _lastLoggedMouseState;

        public ViewportMouseStateProvider(Func<Rectangle> getBounds, Func<IntPtr> getWindowHandle)
        {
            _getBounds = getBounds;
            _getWindowHandle = getWindowHandle;
        }

        public MouseState GetState()
        {
            var bounds = _getBounds();
            var state = GetWindowMouseState(Mouse.GetState());
            LogState(bounds, state);

            return new MouseState(
                state.X - bounds.Left,
                state.Y - bounds.Top,
                state.ScrollWheelValue,
                state.LeftButton,
                state.MiddleButton,
                state.RightButton,
                state.XButton1,
                state.XButton2,
                state.HorizontalScrollWheelValue);
        }

        private MouseState GetWindowMouseState(MouseState fallbackState)
        {
            var handle = _getWindowHandle();
            if (handle == IntPtr.Zero || !GetCursorPos(out var point) || !ScreenToClient(handle, ref point))
            {
                return fallbackState;
            }

            var left = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0 ? ButtonState.Pressed : ButtonState.Released;
            var right = (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0 ? ButtonState.Pressed : ButtonState.Released;
            var middle = (GetAsyncKeyState(VK_MBUTTON) & 0x8000) != 0 ? ButtonState.Pressed : ButtonState.Released;
            var xButton1 = (GetAsyncKeyState(VK_XBUTTON1) & 0x8000) != 0 ? ButtonState.Pressed : ButtonState.Released;
            var xButton2 = (GetAsyncKeyState(VK_XBUTTON2) & 0x8000) != 0 ? ButtonState.Pressed : ButtonState.Released;

            return new MouseState(
                point.X,
                point.Y,
                fallbackState.ScrollWheelValue,
                left,
                middle,
                right,
                xButton1,
                xButton2,
                fallbackState.HorizontalScrollWheelValue);
        }

        private void LogState(Rectangle bounds, MouseState currentState)
        {
            if (_lastLoggedMouseState is MouseState previousState
                && previousState.LeftButton == currentState.LeftButton
                && previousState.MiddleButton == currentState.MiddleButton
                && previousState.RightButton == currentState.RightButton)
            {
                return;
            }

            Debug.WriteLine(
                $"[ViewportMouseProvider] screen=({currentState.X},{currentState.Y}) bounds={bounds} local=({currentState.X - bounds.Left},{currentState.Y - bounds.Top}) " +
                $"l={currentState.LeftButton} m={currentState.MiddleButton} r={currentState.RightButton}");
            _lastLoggedMouseState = currentState;
        }
    }

    private const int VK_LBUTTON = 0x01;
    private const int VK_MBUTTON = 0x04;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private readonly MGWindow _window;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly HostedEditorGameAdapter _editorRuntime;
    private readonly Func<IntPtr> _getWindowHandle;
    private MouseWheelMessageHook? _mouseWheelHook;

    private MGDockPanel _viewportHost = null!;
    private MGImage _viewportImage = null!;

    private RenderTargetSurface? _surface;
    private RenderView? _renderView;
    private Texture2D? _boundTexture;
    private World? _fallbackWorld;
    private Entity? _cameraEntity;
    private ArcBallCameraComponent? _camera;
    private readonly EditorViewportCameraController _cameraController = new();
    private TransformGizmoComponent? _gizmo;
    private KeyboardStateProvider? _keyboardProvider;
    private IMouseStateProvider? _mouseProvider;
    private int _rtWidth = 16;
    private int _rtHeight = 16;

    private MouseState _previousLocalMouseState;
    private KeyboardState _previousKeyboardState;
    private int _pendingScrollDelta;

    internal WorldViewportPanel(MGWindow window, GraphicsDevice graphicsDevice, HostedEditorGameAdapter editorRuntime, Func<IntPtr> getWindowHandle)
    {
        _window = window;
        _graphicsDevice = graphicsDevice;
        _editorRuntime = editorRuntime;
        _getWindowHandle = getWindowHandle;
    }

    public MGElement CreateContent()
    {
        EnsureRenderViewCreated();
        EnsureMouseWheelHook();

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
        _viewportHost.MouseHandler.Scrolled += OnScrolled;

        _keyboardProvider ??= new KeyboardStateProvider();
        _mouseProvider ??= new ViewportMouseStateProvider(() => _viewportHost?.LayoutBounds ?? Rectangle.Empty, _getWindowHandle);

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
        var localMouseState = receivesInput ? inputContext.MouseState : (_mouseProvider?.GetState() ?? new MouseState());
        var keyboardState = receivesInput || isKeyboardFocused ? inputContext.KeyboardState : new KeyboardState();
        bool isViewportInteractive = receivesInput || isKeyboardFocused;

        if (_gizmo != null)
        {
            _gizmo.IsActiveViewport = false;
        }

        if (receivesInput && _pendingScrollDelta != 0)
        {
            inputContext = inputContext with { VerticalWheelDelta = inputContext.VerticalWheelDelta + _pendingScrollDelta };
        }

        _pendingScrollDelta = 0;

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

        UpdateGizmoInput(gameTime, keyboardState, localMouseState, isViewportInteractive);

        _previousLocalMouseState = localMouseState;
        _previousKeyboardState = keyboardState;
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

    private void EnsureMouseWheelHook()
    {
        if (_mouseWheelHook != null)
        {
            return;
        }

        var handle = _getWindowHandle();
        if (handle == IntPtr.Zero)
        {
            return;
        }

        _mouseWheelHook = new MouseWheelMessageHook(handle);
        _mouseWheelHook.MouseWheelScrolled += OnNativeMouseWheelScrolled;
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
            _mouseProvider,
            IsPointerInsideViewport);

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

    private void OnNativeMouseWheelScrolled(int delta, Point clientPoint)
    {
        if (delta == 0 || !IsPointInsideViewport(clientPoint))
        {
            return;
        }

        _pendingScrollDelta += delta;
        ActivateThisView(captureInput: false);
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

        if (_gizmo != null)
        {
            _gizmo.SelectionWorld = desiredWorld;
            _gizmo.ClearSelection();
            _gizmo.SetSelectionPool(GetViewportSelectableObjects(desiredWorld));
        }
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
        if (_renderView == null || _camera == null || _surface == null)
        {
            return;
        }

        if (_gizmo == null)
        {
            _gizmo = new TransformGizmoComponent(_editorRuntime);
            _gizmo.Initialize();
            _gizmo.SelectionChanged += (_, selection) =>
                Debug.WriteLine($"[WorldViewportPanel] GizmoSelectionChanged count={selection.Count}");
        }

        _gizmo.ActiveCamera = _camera;
        _gizmo.ActiveSurface = _surface;
        _gizmo.SelectionWorld = world;
        _gizmo.IsActiveViewport = true;
        _gizmo.SetSelectionPool(GetViewportSelectableObjects(world));

        var overlayPipeline = _renderView.Pipeline as OverlayViewPipeline ?? new OverlayViewPipeline();
        overlayPipeline.RenderGizmosAction = (_, _, frame) => _gizmo.DrawForView(in frame);
        _renderView.Pipeline = overlayPipeline;
    }

    private void SynchronizeGizmo()
    {
        if (_gizmo == null)
        {
            return;
        }

        _gizmo.ActiveCamera = _camera;
        _gizmo.ActiveSurface = _surface;
    }

    private void TrySelectAt(Point localPosition)
    {
        if (_gizmo == null || _camera == null || _surface == null)
        {
            return;
        }

        _gizmo.ActiveCamera = _camera;
        _gizmo.ActiveSurface = _surface;
        _gizmo.SelectionWorld = _renderView?.World;
        _gizmo.Gizmo.ActiveViewport = new Viewport(0, 0, _surface.ViewportRect.Width, _surface.ViewportRect.Height);
        _gizmo.Gizmo.UpdateCameraProperties(_camera.ViewMatrix, _camera.ProjectionMatrix, _camera.Position);

        var keyboard = Keyboard.GetState();
        bool addToSelection = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
        bool removeFromSelection = keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt);

        Debug.WriteLine($"[WorldViewportPanelPoll] LMBSelect local=({localPosition.X},{localPosition.Y}) ctrl={addToSelection} alt={removeFromSelection}");
        _gizmo.Gizmo.SelectEntities(new Vector2(localPosition.X, localPosition.Y), addToSelection, removeFromSelection);
        _gizmo.Gizmo.RefreshPresentation();
        Debug.WriteLine($"[WorldViewportPanelPoll] SelectionCount={_gizmo.CurrentSelection.Count}");

        foreach (var selected in _gizmo.CurrentSelection)
        {
            Debug.WriteLine($"[WorldViewportPanelPoll] Selected {DescribeSelection(selected)}");
        }
    }

    private static IEnumerable<ITransformableObject> GetViewportSelectableObjects(World world)
    {
        var selectables = new List<ITransformableObject>();

        foreach (var entity in world.Entities)
        {
            AddSelectableRoots(entity, selectables);
        }

        return selectables;
    }

    private static void AddSelectableRoots(Entity entity, List<ITransformableObject> selectables)
    {
        if (entity.RootComponent != null)
        {
            selectables.Add(entity.RootComponent);
        }

        foreach (var child in entity.Children)
        {
            AddSelectableRoots(child, selectables);
        }
    }

    private static string DescribeSelection(ITransformableObject transformable)
    {
        if (transformable is SceneComponent sceneComponent)
        {
            var ownerName = sceneComponent.Owner?.Name ?? "<no-owner>";
            return $"owner={ownerName} component={sceneComponent.GetType().Name} bounds={sceneComponent.BoundingBox}";
        }

        return $"type={transformable.GetType().Name} bounds={transformable.BoundingBox}";
    }

    private bool IsPointerInsideViewport()
    {
        if (_viewportHost == null)
        {
            return false;
        }

        var bounds = _viewportHost.LayoutBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return false;
        }

        return bounds.Contains(GetWindowMouseState().X, GetWindowMouseState().Y);
    }

    private bool IsPointInsideViewport(Point point)
    {
        if (_viewportHost == null)
        {
            return false;
        }

        var bounds = _viewportHost.LayoutBounds;
        return bounds.Width > 0 && bounds.Height > 0 && bounds.Contains(point);
    }

    private Point GetViewportLocalPoint(Point point)
    {
        var bounds = _viewportHost?.LayoutBounds ?? Rectangle.Empty;
        return new Point(point.X - bounds.Left, point.Y - bounds.Top);
    }

    private MouseState GetWindowMouseState()
    {
        var fallbackState = Mouse.GetState();
        var handle = _getWindowHandle();
        if (handle == IntPtr.Zero || !GetCursorPos(out var point) || !ScreenToClient(handle, ref point))
        {
            return fallbackState;
        }

        var left = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0 ? ButtonState.Pressed : ButtonState.Released;
        var middle = (GetAsyncKeyState(VK_MBUTTON) & 0x8000) != 0 ? ButtonState.Pressed : ButtonState.Released;

        return new MouseState(
            point.X,
            point.Y,
            fallbackState.ScrollWheelValue,
            left,
            middle,
            fallbackState.RightButton,
            fallbackState.XButton1,
            fallbackState.XButton2,
            fallbackState.HorizontalScrollWheelValue);
    }

    private static bool GetClientPoint(IntPtr handle, int screenX, int screenY, out Point clientPoint)
    {
        clientPoint = Point.Zero;
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        var point = new POINT
        {
            X = screenX,
            Y = screenY,
        };

        if (!ScreenToClient(handle, ref point))
        {
            return false;
        }

        clientPoint = new Point(point.X, point.Y);
        return true;
    }

    private void OnScrolled(object? sender, BaseMouseScrolledEventArgs e)
    {
        _pendingScrollDelta += e.ScrollWheelDelta;
    }

    private void UpdateGizmoInput(GameTime gameTime, KeyboardState keyboardState, MouseState localMouseState, bool isViewportInteractive)
    {
        if (!isViewportInteractive || _gizmo == null || _camera == null || _surface == null)
        {
            _gizmo?.Gizmo.RefreshPresentation();
            return;
        }

        _gizmo.ActiveCamera = _camera;
        _gizmo.ActiveSurface = _surface;
        _gizmo.SelectionWorld = _renderView?.World;
        _gizmo.Gizmo.ActiveViewport = new Viewport(0, 0, _surface.ViewportRect.Width, _surface.ViewportRect.Height);
        _gizmo.Gizmo.UpdateCameraProperties(_camera.ViewMatrix, _camera.ProjectionMatrix, _camera.Position);

        if (IsNewKeyPress(keyboardState, Keys.D1))
        {
            _gizmo.Gizmo.ActiveMode = GizmoMode.Translate;
        }

        if (IsNewKeyPress(keyboardState, Keys.D2))
        {
            _gizmo.Gizmo.ActiveMode = GizmoMode.Rotate;
        }

        if (IsNewKeyPress(keyboardState, Keys.D3))
        {
            _gizmo.Gizmo.ActiveMode = GizmoMode.NonUniformScale;
        }

        if (IsNewKeyPress(keyboardState, Keys.D4))
        {
            _gizmo.Gizmo.ActiveMode = GizmoMode.UniformScale;
        }

        _gizmo.Gizmo.PrecisionModeEnabled = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);

        if (IsNewKeyPress(keyboardState, Keys.O))
        {
            _gizmo.Gizmo.ToggleActiveSpace();
        }

        if (IsNewKeyPress(keyboardState, Keys.I))
        {
            _gizmo.Gizmo.SnapEnabled = !_gizmo.Gizmo.SnapEnabled;
        }

        if (IsNewKeyPress(keyboardState, Keys.P))
        {
            _gizmo.Gizmo.NextPivotType();
        }

        if (IsNewKeyPress(keyboardState, Keys.Escape))
        {
            _gizmo.Gizmo.Clear();
        }

        bool leftJustPressed = localMouseState.LeftButton == ButtonState.Pressed
            && _previousLocalMouseState.LeftButton == ButtonState.Released;

        if (leftJustPressed)
        {
            bool addToSelection = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);
            bool removeFromSelection = keyboardState.IsKeyDown(Keys.LeftAlt) || keyboardState.IsKeyDown(Keys.RightAlt);
            _gizmo.Gizmo.SelectEntities(new Vector2(localMouseState.X, localMouseState.Y), addToSelection, removeFromSelection);
        }

        _gizmo.Gizmo.Update(gameTime, keyboardState, localMouseState);
    }

    private bool IsNewKeyPress(KeyboardState keyboardState, Keys key)
    {
        return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
    }

    public void Dispose()
    {
        if (_mouseWheelHook != null)
        {
            _mouseWheelHook.MouseWheelScrolled -= OnNativeMouseWheelScrolled;
            _mouseWheelHook.Dispose();
            _mouseWheelHook = null;
        }

        if (_renderView != null)
        {
            _editorRuntime.InputComponent.InputRouter?.UnregisterViewInput(_renderView.Id);
        }

        _gizmo?.ClearSelection();
        if (_gizmo != null)
        {
            _editorRuntime.Components.Remove(_gizmo);
            _gizmo.Dispose();
            _gizmo = null;
        }

        if (_renderView != null)
        {
            _editorRuntime.GameManager.ViewManager.Remove(_renderView);
            _renderView = null;
        }

        _surface?.Dispose();
    }
}
