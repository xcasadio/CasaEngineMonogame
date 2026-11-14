using CasaEngine.Framework.Scene.Cameras;
using CasaEngine.Framework.Scene.Entities.Components;
using System;
using CasaEngine.Framework.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Editor.Runtime;

internal readonly struct EditorViewportCameraState
{
    public EditorViewportCameraState(float yaw, float pitch, float distance, Vector3 target)
    {
        Yaw = yaw;
        Pitch = pitch;
        Distance = distance;
        Target = target;
    }

    public float Yaw { get; }

    public float Pitch { get; }

    public float Distance { get; }

    public Vector3 Target { get; }
}

/// <summary>
/// Perspective camera of the editor viewport. The gestures are those of Unreal's perspective viewport, computed by
/// the shared <see cref="ViewportCameraNavigation"/> (ADR-0089). This controller owns the pivot, distance, yaw and
/// pitch (focus, play-mode capture and restore) and writes them to the <see cref="ArcBallCameraComponent"/>; the
/// field of view stays on the component.
/// </summary>
internal sealed class EditorViewportCameraController
{
    private const float MinDistance = 0.5f;
    private const float MaxDistance = 1000f;
    private const float DefaultDistance = 10f;

    private readonly ViewportCameraNavigation _navigation = new()
    {
        MinDistance = MinDistance,
        MaxDistance = MaxDistance,
    };

    private MouseState _previousMouseState;
    private KeyboardState _previousKeyboardState;
    private bool _isCapturing;
    private bool _hasPreviousMouseState;
    private bool _ignoreHeldButtonsOnNextUpdate;
    private ViewportMouseButtons _ignoredButtons;

    public float Yaw { get; private set; } = MathHelper.PiOver4;
    public float Pitch { get; private set; } = -MathHelper.Pi / 6f;
    public float Distance { get; private set; } = DefaultDistance;
    public Vector3 Target { get; private set; } = Vector3.Zero;

    /// <summary>True while a mouse gesture of the navigation (or a gizmo press it waits on) is in progress.</summary>
    public bool IsGestureActive => _navigation.IsGestureActive;

    /// <summary>Current fly speed; right button + wheel changes it.</summary>
    public float FlySpeed => _navigation.FlySpeed;

    /// <summary>Applies the user's 3D camera preferences (ADR-0091).</summary>
    public void ApplyPreferences(Camera3dPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        _navigation.RotationSensitivity = preferences.RotationSensitivity;
        _navigation.PanSensitivity = preferences.PanSensitivity;
        _navigation.WheelStepRatio = preferences.WheelStepRatio;
        _navigation.FlySpeed = preferences.FlySpeed;
        _navigation.FlySpeedWheelFactor = preferences.FlySpeedWheelFactor;
        _navigation.FieldOfViewRate = MathHelper.ToRadians(preferences.FieldOfViewRateDegrees);
        _navigation.DragThresholdPixels = preferences.DragThresholdPixels;
        _navigation.InvertY = preferences.InvertY;
    }

    /// <summary>
    /// Forgets the input history, for instance after play mode, during which this controller did not run: the next
    /// update moves nothing, and the buttons held at that moment start no gesture until they are released.
    /// </summary>
    public void ResetInput()
    {
        _navigation.Reset();
        _hasPreviousMouseState = false;
        _ignoreHeldButtonsOnNextUpdate = true;
    }

    public ArcBallCameraComponent CreateCameraComponent()
    {
        return new ArcBallCameraComponent
        {
            Target = Target,
            Distance = Distance,
            Yaw = Yaw,
            Pitch = Pitch,
        };
    }

    public void ApplyTo(ArcBallCameraComponent camera)
    {
        camera.Target = Target;
        camera.Distance = Distance;
        camera.Yaw = Yaw;
        camera.Pitch = Pitch;
    }

    public EditorViewportCameraState CaptureState()
    {
        return new EditorViewportCameraState(Yaw, Pitch, Distance, Target);
    }

    public void RestoreState(in EditorViewportCameraState state)
    {
        SetState(state.Yaw, state.Pitch, state.Distance, state.Target);
    }

    public void SetState(float yaw, float pitch, float distance, Vector3 target)
    {
        Yaw = yaw;
        Pitch = Math.Clamp(pitch, ViewportCameraNavigation.MinPitch, ViewportCameraNavigation.MaxPitch);
        Distance = Math.Clamp(distance, MinDistance, MaxDistance);
        Target = target;
    }

    public void Focus(Vector3 target, float? distance = null)
    {
        Target = target;

        if (distance.HasValue)
        {
            Distance = Math.Clamp(distance.Value, MinDistance, MaxDistance);
        }
    }

    /// <summary>
    /// Runs one frame of navigation. Returns <c>null</c> when free camera movement is off (front-2D view): the
    /// navigation does not run and the caller keeps its previous selection behaviour.
    /// </summary>
    /// <param name="leftPressedOnGizmoHandle">The gizmo hovers a handle (previous frame's hover): a left press
    /// there belongs to the gizmo and moves no camera.</param>
    public ViewportNavigationResult? Update(
        GameTime gameTime,
        ArcBallCameraComponent camera,
        ViewInputContext inputContext,
        bool receivesInput,
        bool isKeyboardFocused,
        bool allowFreeCameraMovement,
        bool canHandleKeyboardInput,
        Action<bool> activateView,
        Action releaseInput,
        bool leftPressedOnGizmoHandle = false)
    {
        var mouseState = inputContext.MouseState;
        var keyboardState = inputContext.KeyboardState;

        if (_ignoreHeldButtonsOnNextUpdate)
        {
            // Notice A4: the previous states are stale; held buttons and keys must not read as fresh presses.
            _ignoreHeldButtonsOnNextUpdate = false;
            _ignoredButtons = ReadButtons(mouseState);
            _previousMouseState = mouseState;
            _previousKeyboardState = keyboardState;
        }

        _ignoredButtons &= ReadButtons(mouseState);

        if (!allowFreeCameraMovement || (!receivesInput && !isKeyboardFocused))
        {
            if (!allowFreeCameraMovement || ReadButtons(mouseState) == ViewportMouseButtons.None)
            {
                // A gesture whose buttons came up outside the view is forgotten, not reported as a click.
                _navigation.Reset();
            }

            ReleaseCaptureIfIdle(mouseState, releaseInput);
            _previousMouseState = mouseState;
            _previousKeyboardState = keyboardState;
            _hasPreviousMouseState = true;
            return allowFreeCameraMovement ? new ViewportNavigationResult() : null;
        }

        var buttons = ViewportMouseButtons.None;
        float dx = 0f;
        float dy = 0f;
        float wheelNotches = 0f;

        if (receivesInput)
        {
            buttons = ReadButtons(mouseState) & ~_ignoredButtons;
            if ((buttons & ~ReadButtons(_previousMouseState)) != 0)
            {
                activateView(true);
                _isCapturing = true;
            }

            // The first frame has no previous pointer position: no movement rather than a jump from (0, 0).
            if (_hasPreviousMouseState)
            {
                dx = mouseState.X - _previousMouseState.X;
                dy = mouseState.Y - _previousMouseState.Y;
            }

            if (inputContext.VerticalWheelDelta != 0)
            {
                activateView(false);
                wheelNotches = EditorViewport2dCameraController.WheelDeltaToSteps(inputContext.VerticalWheelDelta);
            }
        }

        var keys = ViewportNavigationKeys.None;
        if (canHandleKeyboardInput && (receivesInput || isKeyboardFocused))
        {
            // The letter keys stay free for the editor's Ctrl shortcuts (Ctrl+S saves, Ctrl+D duplicates): while Ctrl
            // is held they neither fly the camera nor frame the selection.
            bool controlHeld = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);
            keys = ViewportCameraNavigation.ReadKeys(keyboardState, _previousKeyboardState, !controlHeld);
            if (!isKeyboardFocused)
            {
                keys &= ~ViewportNavigationKeys.FocusPressed;
            }
        }

        var input = new ViewportNavigationInput
        {
            Buttons = buttons,
            Alt = keyboardState.IsKeyDown(Keys.LeftAlt) || keyboardState.IsKeyDown(Keys.RightAlt),
            MouseDeltaX = dx,
            MouseDeltaY = dy,
            WheelNotches = wheelNotches,
            Keys = keys,
            LeftPressedOnGizmoHandle = leftPressedOnGizmoHandle,
            ElapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds,
        };

        if (!receivesInput && _navigation.IsGestureActive)
        {
            // Notice A3: keyboard focus without pointer input reads every button as up. A gesture whose pointer input
            // went away is forgotten rather than ended as a click that the view never received.
            _navigation.Reset();
        }

        var state = new ViewportCameraState(Target, Distance, Yaw, Pitch);
        ViewportNavigationResult result = _navigation.Update(ref state, input);

        if (result.CameraChanged)
        {
            SetState(state.Yaw, state.Pitch, state.Distance, state.Pivot);
            ApplyTo(camera);
        }

        if (result.FieldOfViewDelta != 0f)
        {
            camera.FieldOfView += result.FieldOfViewDelta;
        }

        ReleaseCaptureIfIdle(mouseState, releaseInput);
        _previousMouseState = mouseState;
        _previousKeyboardState = keyboardState;
        _hasPreviousMouseState = true;
        return result;
    }

    private void ReleaseCaptureIfIdle(MouseState mouseState, Action releaseInput)
    {
        if (_isCapturing && ReadButtons(mouseState) == ViewportMouseButtons.None)
        {
            _isCapturing = false;
            releaseInput();
        }
    }

    private static ViewportMouseButtons ReadButtons(MouseState mouseState)
    {
        var buttons = ViewportMouseButtons.None;
        if (mouseState.LeftButton == ButtonState.Pressed)
        {
            buttons |= ViewportMouseButtons.Left;
        }

        if (mouseState.RightButton == ButtonState.Pressed)
        {
            buttons |= ViewportMouseButtons.Right;
        }

        if (mouseState.MiddleButton == ButtonState.Pressed)
        {
            buttons |= ViewportMouseButtons.Middle;
        }

        return buttons;
    }
}
