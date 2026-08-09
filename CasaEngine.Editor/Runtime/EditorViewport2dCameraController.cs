using System;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Editor.Runtime;

internal readonly struct EditorViewport2dCameraState
{
    public EditorViewport2dCameraState(Vector3 target, int zoomStep, bool pixelSnap)
    {
        Target = target;
        ZoomStep = zoomStep;
        PixelSnap = pixelSnap;
    }

    public Vector3 Target { get; }

    public int ZoomStep { get; }

    public bool PixelSnap { get; }
}

/// <summary>
/// Drives the orthographic <see cref="Camera2dComponent"/> of a viewport running in 2d mode.
/// Middle mouse drag pans, the wheel zooms by integer steps centred on the cursor.
/// This is the 2d counterpart of <see cref="EditorViewportCameraController"/>; both are
/// independent so that a viewport can switch modes without losing the other mode's state.
/// </summary>
internal sealed class EditorViewport2dCameraController
{
    /// <summary>Smallest zoom step, mapping to a zoom factor of 1/8.</summary>
    public const int MinimumZoomStep = -7;

    /// <summary>Largest zoom step, mapping to a zoom factor of 32.</summary>
    public const int MaximumZoomStep = 31;

    private const int WheelDeltaPerStep = 120;

    private MouseState _previousMouseState;
    private bool _isMiddleDragCapturing;

    public Vector3 Target { get; private set; } = Vector3.Zero;

    public int ZoomStep { get; private set; }

    public bool PixelSnap { get; set; } = true;

    /// <summary>Magnification applied by the camera, derived from <see cref="ZoomStep"/>.</summary>
    public float Zoom => ZoomFromStep(ZoomStep);

    /// <summary>
    /// Maps a zoom step to a magnification factor: 0 is 1x, positive steps are 2x, 3x, 4x…
    /// and negative steps are 1/2, 1/3, 1/4…
    /// </summary>
    public static float ZoomFromStep(int step)
    {
        step = Math.Clamp(step, MinimumZoomStep, MaximumZoomStep);
        return step >= 0 ? step + 1 : 1.0f / (1 - step);
    }

    public Camera2dComponent CreateCameraComponent()
    {
        return new Camera2dComponent
        {
            Target = Target,
            Zoom = Zoom,
            PixelSnap = PixelSnap,
        };
    }

    public void ApplyTo(Camera2dComponent camera)
    {
        camera.Target = Target;
        camera.Zoom = Zoom;
        camera.PixelSnap = PixelSnap;
    }

    public EditorViewport2dCameraState CaptureState()
    {
        return new EditorViewport2dCameraState(Target, ZoomStep, PixelSnap);
    }

    public void RestoreState(in EditorViewport2dCameraState state)
    {
        SetState(state.Target, state.ZoomStep);
        PixelSnap = state.PixelSnap;
    }

    public void SetState(Vector3 target, int zoomStep)
    {
        Target = target;
        ZoomStep = Math.Clamp(zoomStep, MinimumZoomStep, MaximumZoomStep);
    }

    public void Focus(Vector3 target, int? zoomStep = null)
    {
        Target = target;

        if (zoomStep.HasValue)
        {
            ZoomStep = Math.Clamp(zoomStep.Value, MinimumZoomStep, MaximumZoomStep);
        }
    }

    /// <summary>
    /// Pans the camera by a mouse drag expressed in view pixels.
    /// </summary>
    public void Pan(int dx, int dy)
    {
        if (dx == 0 && dy == 0)
        {
            return;
        }

        float zoom = Zoom;
        Target = new Vector3(Target.X - dx / zoom, Target.Y + dy / zoom, Target.Z);
    }

    /// <summary>
    /// Applies <paramref name="steps"/> zoom notches while keeping the world point under
    /// <paramref name="localPosition"/> (view-local pixels) at the same place on screen.
    /// </summary>
    public void ZoomAtCursor(int steps, Point localPosition, Rectangle viewBounds)
    {
        if (steps == 0)
        {
            return;
        }

        int newStep = Math.Clamp(ZoomStep + steps, MinimumZoomStep, MaximumZoomStep);
        if (newStep == ZoomStep)
        {
            return;
        }

        float previousZoom = Zoom;
        float newZoom = ZoomFromStep(newStep);

        if (viewBounds.Width > 0 && viewBounds.Height > 0)
        {
            float offsetX = localPosition.X - viewBounds.Width * 0.5f;
            float offsetY = -(localPosition.Y - viewBounds.Height * 0.5f);
            float factor = 1.0f / previousZoom - 1.0f / newZoom;
            Target = new Vector3(Target.X + offsetX * factor, Target.Y + offsetY * factor, Target.Z);
        }

        ZoomStep = newStep;
    }

    /// <summary>
    /// Converts a raw wheel delta to a signed number of zoom notches.
    /// Any non zero delta yields at least one notch so that platforms reporting
    /// small deltas still zoom.
    /// </summary>
    public static int WheelDeltaToSteps(int wheelDelta)
    {
        if (wheelDelta == 0)
        {
            return 0;
        }

        int steps = wheelDelta / WheelDeltaPerStep;
        return steps != 0 ? steps : Math.Sign(wheelDelta);
    }

    public void Update(
        GameTime gameTime,
        Camera2dComponent camera,
        ViewInputContext inputContext,
        bool receivesInput,
        bool isKeyboardFocused,
        bool allowFreeCameraMovement,
        bool canHandleKeyboardInput,
        Action<bool> activateView,
        Action releaseInput)
    {
        if (!receivesInput && !isKeyboardFocused)
        {
            if (_isMiddleDragCapturing && inputContext.MouseState.MiddleButton == ButtonState.Released)
            {
                _isMiddleDragCapturing = false;
                ReleaseInputIfIdle(inputContext.MouseState, releaseInput);
            }

            _previousMouseState = inputContext.MouseState;
            return;
        }

        var mouseState = inputContext.MouseState;

        if (allowFreeCameraMovement && receivesInput
            && mouseState.MiddleButton == ButtonState.Pressed
            && _previousMouseState.MiddleButton == ButtonState.Released)
        {
            activateView(true);
            _isMiddleDragCapturing = true;
        }

        // The drag delta is only meaningful once the button was already down on the previous
        // frame, otherwise the first frame would pan by the whole cursor position.
        if (allowFreeCameraMovement && _isMiddleDragCapturing
            && mouseState.MiddleButton == ButtonState.Pressed
            && _previousMouseState.MiddleButton == ButtonState.Pressed)
        {
            Pan(mouseState.X - _previousMouseState.X, mouseState.Y - _previousMouseState.Y);
            ApplyTo(camera);
        }

        if (_isMiddleDragCapturing && mouseState.MiddleButton == ButtonState.Released)
        {
            _isMiddleDragCapturing = false;
            ReleaseInputIfIdle(mouseState, releaseInput);
        }

        if (allowFreeCameraMovement && receivesInput && inputContext.VerticalWheelDelta != 0)
        {
            activateView(false);
            ZoomAtCursor(
                WheelDeltaToSteps(inputContext.VerticalWheelDelta),
                inputContext.LocalPosition,
                inputContext.ScreenBounds);
            ApplyTo(camera);
        }

        _previousMouseState = mouseState;
    }

    private static void ReleaseInputIfIdle(MouseState mouseState, Action releaseInput)
    {
        if (mouseState.MiddleButton == ButtonState.Released && mouseState.RightButton == ButtonState.Released)
        {
            releaseInput();
        }
    }
}
