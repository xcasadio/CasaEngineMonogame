using System;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.World;
using GizmoTools;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input.Mouse;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Thickness = MonoGame.Extended.Thickness;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// A dockable world viewport panel that renders the current <see cref="World"/>
/// into a <see cref="RenderTarget2D"/> and displays it via <see cref="MGImage"/>.
/// <para/>
/// Camera controls:
/// <list type="bullet">
/// <item>Middle-click drag — orbit</item>
/// <item>Shift + middle-click drag — pan</item>
/// <item>Scroll wheel — zoom</item>
/// </list>
/// Call <see cref="DrawViewport"/> from <c>Game1.Draw()</c> before MGUI renders.
/// </summary>
public class WorldViewportPanel : IDisposable
{
    // ─────────────────────────────────────────────────────────────────────────
    // Constants
    // ─────────────────────────────────────────────────────────────────────────

    private const float OrbitSensitivity = 0.005f;
    private const float PanSensitivity   = 0.01f;
    private const float ZoomSensitivity  = 1.1f;
    private const float MinDistance      = 0.5f;
    private const float MaxDistance      = 1000f;
    private const float DefaultDistance  = 10f;
    private const float DefaultFov       = MathHelper.PiOver4;
    private const float NearPlane        = 0.1f;
    private const float FarPlane         = 2000f;

    // ─────────────────────────────────────────────────────────────────────────
    // Fields — dependencies
    // ─────────────────────────────────────────────────────────────────────────

    private readonly MGWindow _window;
    private readonly GraphicsDevice _graphicsDevice;

    // ─────────────────────────────────────────────────────────────────────────
    // Fields — MGUI elements
    // ─────────────────────────────────────────────────────────────────────────

    private MGImage _viewportImage = null!;

    // ─────────────────────────────────────────────────────────────────────────
    // Fields — rendering
    // ─────────────────────────────────────────────────────────────────────────

    private RenderTarget2D? _renderTarget;
    private int _rtWidth  = 16;
    private int _rtHeight = 16;

    // ─────────────────────────────────────────────────────────────────────────
    // Fields — editor camera (orbit mode)
    // ─────────────────────────────────────────────────────────────────────────

    private float _yaw      = MathHelper.PiOver4;
    private float _pitch    = -MathHelper.Pi / 6f;
    private float _distance = DefaultDistance;
    private Vector3 _target = Vector3.Zero;

    // ─────────────────────────────────────────────────────────────────────────
    // Fields — drag tracking
    // ─────────────────────────────────────────────────────────────────────────

    private bool _isDragging;
    private Point _lastDragPos;
    private MouseButton _dragButton;

    // ─────────────────────────────────────────────────────────────────────────
    // Fields — gizmo
    // ─────────────────────────────────────────────────────────────────────────

    private readonly Gizmo _gizmo;

    // ─────────────────────────────────────────────────────────────────────────
    // Properties
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>The world to render, or <c>null</c> if no project is open.</summary>
    public World? ActiveWorld { get; set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Constructor
    // ─────────────────────────────────────────────────────────────────────────

    public WorldViewportPanel(MGWindow window, GraphicsDevice graphicsDevice)
    {
        _window        = window;
        _graphicsDevice = graphicsDevice;
        _gizmo         = new Gizmo(graphicsDevice);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds and returns the root MGUI element for this panel.
    /// Call once and use as a <c>DockPanelNode.ContentFactory</c> result.
    /// </summary>
    public MGElement CreateContent()
    {
        // ── Create initial render target ──────────────────────────────────
        RecreateRenderTarget(_rtWidth, _rtHeight);

        // ── Viewport image ────────────────────────────────────────────────
        _viewportImage = new MGImage(_window, new MGTextureData(_renderTarget!), Stretch: Stretch.Fill)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment   = VerticalAlignment.Stretch,
        };

        // Resize render target when the panel changes size
        _viewportImage.OnLayoutBoundsChanged += OnViewportBoundsChanged;

        // Mouse events for camera control
        _viewportImage.MouseHandler.DragStart += OnDragStart;
        _viewportImage.MouseHandler.Dragged   += OnDragged;
        _viewportImage.MouseHandler.DragEnd   += (_, _) => _isDragging = false;
        _viewportImage.MouseHandler.Scrolled  += OnScrolled;

        // ── Gizmo mode toolbar ────────────────────────────────────────────
        var toolbar = BuildToolbar();

        // ── Outer layout ──────────────────────────────────────────────────
        var panel = new MGDockPanel(_window);
        panel.TryAddChild(toolbar, Dock.Top);
        panel.TryAddChild(_viewportImage, Dock.Top); // fill

        return panel;
    }

    /// <summary>
    /// Renders the current <see cref="ActiveWorld"/> (and gizmo) into
    /// <see cref="_renderTarget"/>. Call this from <c>Game1.Draw()</c> before
    /// MGUI draws, so the <see cref="MGImage"/> shows the latest content.
    /// </summary>
    public void DrawViewport(GameTime gameTime)
    {
        if (_renderTarget == null) return;

        var previousTargets = _graphicsDevice.GetRenderTargets();

        _graphicsDevice.SetRenderTarget(_renderTarget);
        _graphicsDevice.Clear(Color.DimGray);

        if (ActiveWorld != null)
        {
            var frame = BuildRenderFrame();
            _gizmo.UpdateCameraProperties(frame.View, frame.Projection, frame.CameraPosition);

            // World rendering — entities draw via their components
            // Note: full rendering requires CasaEngineGame integration (future task)
            ActiveWorld.Draw(in frame);

            // Draw gizmo on top
            _gizmo.Draw();
        }
        else
        {
            DrawPlaceholder();
        }

        _graphicsDevice.SetRenderTargets(previousTargets);
    }

    /// <summary>
    /// Returns the current editor camera as a <see cref="RenderFrame"/>
    /// suitable for passing to <see cref="World.Draw"/>.
    /// </summary>
    public RenderFrame BuildRenderFrame()
    {
        var cameraPosition = ComputeCameraPosition();
        var view = Matrix.CreateLookAt(cameraPosition, _target, Vector3.Up);
        var aspect = _rtHeight > 0 ? (float)_rtWidth / _rtHeight : 1f;
        var projection = Matrix.CreatePerspectiveFieldOfView(DefaultFov, aspect, NearPlane, FarPlane);
        var viewProjection = view * projection;
        var viewportRect = new Rectangle(0, 0, _rtWidth, _rtHeight);
        return new RenderFrame(view, projection, cameraPosition, viewportRect);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Toolbar
    // ─────────────────────────────────────────────────────────────────────────

    private MGStackPanel BuildToolbar()
    {
        var toolbar = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Margin  = new Thickness(4, 2, 4, 2),
            Spacing = 4,
        };

        toolbar.TryAddChild(new MGTextBlock(_window, "[b]Viewport[/b]")
        {
            VerticalAlignment = VerticalAlignment.Center,
        });

        var btnTranslate = MakeGizmoModeButton("✥ Move",   () => SetGizmoMode(GizmoMode.Translate));
        var btnRotate    = MakeGizmoModeButton("↻ Rotate", () => SetGizmoMode(GizmoMode.Rotate));
        var btnScale     = MakeGizmoModeButton("↲ Scale",  () => SetGizmoMode(GizmoMode.UniformScale));

        toolbar.TryAddChild(btnTranslate);
        toolbar.TryAddChild(btnRotate);
        toolbar.TryAddChild(btnScale);

        // World/local space toggle
        var btnSpace = new MGButton(_window, _ => ToggleGizmoSpace())
        {
            Padding = new Thickness(4, 1, 4, 1),
        };
        btnSpace.SetContent(new MGTextBlock(_window, "Local ↔ World"));
        toolbar.TryAddChild(btnSpace);

        return toolbar;
    }

    private MGButton MakeGizmoModeButton(string label, Action action)
    {
        var btn = new MGButton(_window, _ => action())
        {
            Padding = new Thickness(4, 1, 4, 1),
        };
        btn.SetContent(new MGTextBlock(_window, label));
        return btn;
    }

    private void SetGizmoMode(GizmoMode mode)
    {
        _gizmo.ActiveMode = mode;
    }

    private void ToggleGizmoSpace()
    {
        _gizmo.ActiveSpace = _gizmo.ActiveSpace == TransformSpace.Local
            ? TransformSpace.World
            : TransformSpace.Local;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Bounds change → resize render target
    // ─────────────────────────────────────────────────────────────────────────

    private void OnViewportBoundsChanged(object? sender, EventArgs<Rectangle> e)
    {
        var newBounds = e.NewValue;
        var w = Math.Max(16, newBounds.Width);
        var h = Math.Max(16, newBounds.Height);

        if (w == _rtWidth && h == _rtHeight)
            return;

        RecreateRenderTarget(w, h);

        // Update MGImage source to the new render target
        _viewportImage.Source = new MGTextureData(_renderTarget!);
    }

    private void RecreateRenderTarget(int width, int height)
    {
        _renderTarget?.Dispose();
        _rtWidth  = width;
        _rtHeight = height;
        _renderTarget = new RenderTarget2D(
            _graphicsDevice,
            _rtWidth, _rtHeight,
            false,
            SurfaceFormat.Color,
            DepthFormat.Depth24Stencil8);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Camera controls
    // ─────────────────────────────────────────────────────────────────────────

    private void OnDragStart(object? sender, BaseMouseDragStartEventArgs e)
    {
        _isDragging   = true;
        _dragButton   = e.Button;
        _lastDragPos  = e.Position;
    }

    private void OnDragged(object? sender, BaseMouseDraggedEventArgs e)
    {
        if (!_isDragging || e.Button != _dragButton) return;

        var dx = e.Position.X - _lastDragPos.X;
        var dy = e.Position.Y - _lastDragPos.Y;
        _lastDragPos = e.Position;

        if (_dragButton == MouseButton.Middle)
        {
            bool isShift = Keyboard.GetState().IsKeyDown(Keys.LeftShift) ||
                           Keyboard.GetState().IsKeyDown(Keys.RightShift);

            if (isShift)
            {
                // Pan — move target in the camera's right/up plane
                var right = Vector3.Cross(Vector3.Up, ComputeCameraDirection());
                right.Normalize();
                var up = Vector3.Up;
                _target -= right * dx * PanSensitivity * _distance;
                _target += up    * dy * PanSensitivity * _distance;
            }
            else
            {
                // Orbit
                _yaw   -= dx * OrbitSensitivity;
                _pitch  = Math.Clamp(_pitch - dy * OrbitSensitivity,
                                     -MathHelper.PiOver2 + 0.01f,
                                      MathHelper.PiOver2 - 0.01f);
            }
        }
    }

    private void OnScrolled(object? sender, BaseMouseScrolledEventArgs e)
    {
        if (e.ScrollWheelDelta > 0)
            _distance = Math.Max(MinDistance, _distance / ZoomSensitivity);
        else if (e.ScrollWheelDelta < 0)
            _distance = Math.Min(MaxDistance, _distance * ZoomSensitivity);
    }

    private Vector3 ComputeCameraDirection()
    {
        return new Vector3(
            (float)(Math.Cos(_pitch) * Math.Sin(_yaw)),
            (float)Math.Sin(_pitch),
            (float)(Math.Cos(_pitch) * Math.Cos(_yaw)));
    }

    private Vector3 ComputeCameraPosition()
    {
        return _target + ComputeCameraDirection() * _distance;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Placeholder rendering (no world loaded)
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawPlaceholder()
    {
        // Viewport is already cleared to DimGray; nothing more needed here.
        // A future task can draw a "No world loaded" text via SpriteBatch.
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IDisposable
    // ─────────────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _renderTarget?.Dispose();
        _gizmo.Dispose();
    }
}
