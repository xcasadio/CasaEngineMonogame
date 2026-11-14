using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Framework.Scene.Cameras;

/// <summary>Mouse buttons held during a frame, as seen by <see cref="ViewportCameraNavigation"/>.</summary>
[Flags]
public enum ViewportMouseButtons
{
    None = 0,
    Left = 1,
    Right = 2,
    Middle = 4,
}

/// <summary>Navigation keys held (or, for <see cref="FocusPressed"/>, newly pressed) during a frame.</summary>
[Flags]
public enum ViewportNavigationKeys
{
    None = 0,
    Forward = 1 << 0,
    Backward = 1 << 1,
    Left = 1 << 2,
    Right = 1 << 3,
    WorldUp = 1 << 4,
    WorldDown = 1 << 5,
    LocalUp = 1 << 6,
    LocalDown = 1 << 7,
    FieldOfViewWider = 1 << 8,
    FieldOfViewNarrower = 1 << 9,
    FocusPressed = 1 << 10,
}

/// <summary>
/// Camera placement driven by <see cref="ViewportCameraNavigation"/>: the eye stands at
/// <c>Pivot - Forward * Distance</c>, with the same yaw and pitch convention as
/// <see cref="ArcBallCameraComponent"/>. The field of view is not part of it (ADR-0089).
/// </summary>
public struct ViewportCameraState
{
    public Vector3 Pivot;
    public float Distance;
    public float Yaw;
    public float Pitch;

    public ViewportCameraState(Vector3 pivot, float distance, float yaw, float pitch)
    {
        Pivot = pivot;
        Distance = distance;
        Yaw = yaw;
        Pitch = pitch;
    }

    public readonly Vector3 Forward => ViewportCameraNavigation.ComputeForward(Yaw, Pitch);

    public readonly Vector3 Eye => Pivot - Forward * Distance;

    public static ViewportCameraState From(ArcBallCameraComponent camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        return new ViewportCameraState(camera.Target, camera.Distance, camera.Yaw, camera.Pitch);
    }

    /// <summary>Writes the pivot, distance, yaw and pitch. The field of view is left untouched.</summary>
    public readonly void ApplyTo(ArcBallCameraComponent camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        camera.Target = Pivot;
        camera.Distance = Distance;
        camera.Yaw = Yaw;
        camera.Pitch = Pitch;
    }
}

/// <summary>Input of one frame for <see cref="ViewportCameraNavigation.Update"/>.</summary>
public struct ViewportNavigationInput
{
    /// <summary>Buttons held this frame.</summary>
    public ViewportMouseButtons Buttons;

    public bool Alt;

    /// <summary>Pointer movement since the previous frame, in pixels (Y grows downwards).</summary>
    public float MouseDeltaX;

    public float MouseDeltaY;

    /// <summary>Wheel movement in notches; positive moves forward.</summary>
    public float WheelNotches;

    public ViewportNavigationKeys Keys;

    /// <summary>
    /// True when the left button was pressed this frame over a gizmo handle: the press belongs to the gizmo
    /// and no camera gesture starts until the left button is released.
    /// </summary>
    public bool LeftPressedOnGizmoHandle;

    public float ElapsedSeconds;
}

/// <summary>What the caller has to do after <see cref="ViewportCameraNavigation.Update"/>.</summary>
public struct ViewportNavigationResult
{
    /// <summary>The left button was released without dragging: a selection click.</summary>
    public bool LeftClickReleased;

    /// <summary>F was pressed with the right button up: frame the selection.</summary>
    public bool FocusRequested;

    /// <summary>Change to apply to the camera's field of view, in radians.</summary>
    public float FieldOfViewDelta;

    /// <summary>The right button is held: letter keys fly the camera and must not trigger other shortcuts.</summary>
    public bool IsFlying;

    /// <summary>The camera state changed this frame.</summary>
    public bool CameraChanged;
}

/// <summary>
/// Unreal-style perspective viewport navigation (ADR-0089), shared by the editor viewport and
/// <c>ScriptArcBallCamera</c>. It holds only the gesture in progress and the fly speed; the caller owns the
/// <see cref="ViewportCameraState"/>. No allocation per frame.
/// </summary>
public sealed class ViewportCameraNavigation
{
    public const float MinPitch = -MathHelper.PiOver2 + 0.01f;
    public const float MaxPitch = MathHelper.PiOver2 - 0.01f;

    private enum Gesture
    {
        None,
        PendingLeftClick,
        LeftDrag,
        Look,
        Pan,
        AltOrbit,
        AltDolly,
        AltTrack,

        // The press belongs to the gizmo, or an Alt gesture lost its button: nothing moves until every button is up.
        WaitForRelease,
    }

    private Gesture _gesture;
    private ViewportMouseButtons _previousButtons;
    private float _pendingDragX;
    private float _pendingDragY;

    /// <summary>Radians per pixel for looking, turning and orbiting.</summary>
    public float RotationSensitivity { get; set; } = 0.005f;

    /// <summary>World units per pixel, as a fraction of the pivot distance, for pan, track, left drag and Alt dolly.</summary>
    public float PanSensitivity { get; set; } = 0.01f;

    /// <summary>Wheel notch length as a fraction of the pivot distance.</summary>
    public float WheelStepRatio { get; set; } = 0.1f;

    /// <summary>Shortest wheel notch, in world units.</summary>
    public float MinWheelStep { get; set; } = 0.5f;

    /// <summary>Fly speed in world units per second (right button + keys).</summary>
    public float FlySpeed { get; set; } = 10f;

    public float MinFlySpeed { get; set; } = 0.1f;

    public float MaxFlySpeed { get; set; } = 1000f;

    /// <summary>Fly speed factor applied per wheel notch while the right button is held.</summary>
    public float FlySpeedWheelFactor { get; set; } = 1.25f;

    public float MinDistance { get; set; } = 0.5f;

    public float MaxDistance { get; set; } = 1000f;

    /// <summary>Pointer travel, in pixels, past which a left press becomes a camera drag instead of a click.</summary>
    public float DragThresholdPixels { get; set; } = 4f;

    /// <summary>Field of view change per second while Z or C is held, in radians.</summary>
    public float FieldOfViewRate { get; set; } = MathHelper.PiOver4;

    /// <summary>Inverts the vertical mouse axis for looking around and Alt orbit (ADR-0091).</summary>
    public bool InvertY { get; set; }

    /// <summary>True while a camera gesture (or a gizmo press) owns the mouse.</summary>
    public bool IsGestureActive => _gesture != Gesture.None;

    /// <summary>Forgets the gesture in progress, for instance when the view loses the input.</summary>
    public void Reset()
    {
        _gesture = Gesture.None;
        _previousButtons = ViewportMouseButtons.None;
        _pendingDragX = 0f;
        _pendingDragY = 0f;
    }

    public ViewportNavigationResult Update(ref ViewportCameraState state, in ViewportNavigationInput input)
    {
        var result = new ViewportNavigationResult();
        ViewportMouseButtons buttons = input.Buttons;
        ViewportMouseButtons pressed = buttons & ~_previousButtons;
        bool leftHeld = (buttons & ViewportMouseButtons.Left) != 0;
        bool rightHeld = (buttons & ViewportMouseButtons.Right) != 0;
        bool middleHeld = (buttons & ViewportMouseButtons.Middle) != 0;
        float dx = input.MouseDeltaX;
        float dy = input.MouseDeltaY;

        UpdateGesture(input, pressed, leftHeld, rightHeld, middleHeld, ref result);

        switch (_gesture)
        {
            case Gesture.PendingLeftClick:
                _pendingDragX += dx;
                _pendingDragY += dy;
                if (_pendingDragX * _pendingDragX + _pendingDragY * _pendingDragY >= DragThresholdPixels * DragThresholdPixels)
                {
                    _gesture = Gesture.LeftDrag;
                    result.CameraChanged |= MoveAndTurn(ref state, _pendingDragX, _pendingDragY);
                }

                break;
            case Gesture.LeftDrag:
                result.CameraChanged |= MoveAndTurn(ref state, dx, dy);
                break;
            case Gesture.Look:
                result.CameraChanged |= LookAround(ref state, dx, dy);
                break;
            case Gesture.Pan:
            case Gesture.AltTrack:
                result.CameraChanged |= Pan(ref state, dx, dy);
                break;
            case Gesture.AltOrbit:
                result.CameraChanged |= Orbit(ref state, dx, dy);
                break;
            case Gesture.AltDolly:
                result.CameraChanged |= MoveTowardPivot(ref state, (dx - dy) * PanSensitivity * state.Distance);
                break;
        }

        if (input.WheelNotches != 0f)
        {
            if (rightHeld)
            {
                FlySpeed = Math.Clamp(FlySpeed * MathF.Pow(FlySpeedWheelFactor, input.WheelNotches), MinFlySpeed, MaxFlySpeed);
            }
            else
            {
                float step = Math.Max(MinWheelStep, WheelStepRatio * state.Distance);
                result.CameraChanged |= MoveTowardPivot(ref state, step * input.WheelNotches);
            }
        }

        result.IsFlying = rightHeld;
        if (rightHeld)
        {
            result.CameraChanged |= Fly(ref state, input.Keys, input.ElapsedSeconds);
            result.FieldOfViewDelta = ComputeFieldOfViewDelta(input.Keys, input.ElapsedSeconds);
        }
        else if ((input.Keys & ViewportNavigationKeys.FocusPressed) != 0)
        {
            result.FocusRequested = true;
        }

        _previousButtons = buttons;
        return result;
    }

    /// <summary>Unit view direction for a yaw and a pitch, with the <see cref="ArcBallCameraComponent"/> convention.</summary>
    public static Vector3 ComputeForward(float yaw, float pitch)
    {
        float cosPitch = MathF.Cos(pitch);
        return new Vector3(MathF.Sin(yaw) * cosPitch, MathF.Sin(pitch), -MathF.Cos(yaw) * cosPitch);
    }

    /// <summary>Horizontal right vector for a yaw.</summary>
    public static Vector3 ComputeRight(float yaw)
    {
        return new Vector3(MathF.Cos(yaw), 0f, MathF.Sin(yaw));
    }

    /// <summary>
    /// Reads the navigation keys from two keyboard states. Letter keys (W, A, S, D, E, Q, R, F, Z, C) are skipped
    /// when <paramref name="includeLetterKeys"/> is false, so they stay free for Ctrl shortcuts.
    /// </summary>
    public static ViewportNavigationKeys ReadKeys(in KeyboardState current, in KeyboardState previous, bool includeLetterKeys)
    {
        var keys = ViewportNavigationKeys.None;
        keys |= Read(current, Keys.Up, Keys.NumPad8, includeLetterKeys ? Keys.W : Keys.None, ViewportNavigationKeys.Forward);
        keys |= Read(current, Keys.Down, Keys.NumPad2, includeLetterKeys ? Keys.S : Keys.None, ViewportNavigationKeys.Backward);
        keys |= Read(current, Keys.Left, Keys.NumPad4, includeLetterKeys ? Keys.A : Keys.None, ViewportNavigationKeys.Left);
        keys |= Read(current, Keys.Right, Keys.NumPad6, includeLetterKeys ? Keys.D : Keys.None, ViewportNavigationKeys.Right);
        keys |= Read(current, Keys.PageUp, Keys.NumPad9, includeLetterKeys ? Keys.E : Keys.None, ViewportNavigationKeys.WorldUp);
        keys |= Read(current, Keys.PageDown, Keys.NumPad7, includeLetterKeys ? Keys.Q : Keys.None, ViewportNavigationKeys.WorldDown);
        keys |= Read(current, Keys.NumPad1, Keys.None, includeLetterKeys ? Keys.Z : Keys.None, ViewportNavigationKeys.FieldOfViewWider);
        keys |= Read(current, Keys.NumPad3, Keys.None, includeLetterKeys ? Keys.C : Keys.None, ViewportNavigationKeys.FieldOfViewNarrower);

        if (includeLetterKeys)
        {
            if (current.IsKeyDown(Keys.R))
            {
                keys |= ViewportNavigationKeys.LocalUp;
            }

            if (current.IsKeyDown(Keys.F))
            {
                keys |= ViewportNavigationKeys.LocalDown;
                if (previous.IsKeyUp(Keys.F))
                {
                    keys |= ViewportNavigationKeys.FocusPressed;
                }
            }
        }

        return keys;
    }

    private static ViewportNavigationKeys Read(in KeyboardState state, Keys first, Keys second, Keys third, ViewportNavigationKeys flag)
    {
        bool down = state.IsKeyDown(first)
            || (second != Keys.None && state.IsKeyDown(second))
            || (third != Keys.None && state.IsKeyDown(third));
        return down ? flag : ViewportNavigationKeys.None;
    }

    private void UpdateGesture(in ViewportNavigationInput input, ViewportMouseButtons pressed, bool leftHeld, bool rightHeld, bool middleHeld, ref ViewportNavigationResult result)
    {
        if (!leftHeld && !rightHeld && !middleHeld)
        {
            if (_gesture == Gesture.PendingLeftClick)
            {
                result.LeftClickReleased = true;
            }

            _gesture = Gesture.None;
            _pendingDragX = 0f;
            _pendingDragY = 0f;
            return;
        }

        bool leftPressed = (pressed & ViewportMouseButtons.Left) != 0;
        bool rightPressed = (pressed & ViewportMouseButtons.Right) != 0;
        bool middlePressed = (pressed & ViewportMouseButtons.Middle) != 0;

        switch (_gesture)
        {
            case Gesture.None:
                if (input.Alt && (leftPressed || rightPressed || middlePressed) && !(leftPressed && input.LeftPressedOnGizmoHandle))
                {
                    _gesture = leftPressed ? Gesture.AltOrbit : rightPressed ? Gesture.AltDolly : Gesture.AltTrack;
                }
                else if (leftPressed)
                {
                    if (input.LeftPressedOnGizmoHandle)
                    {
                        _gesture = Gesture.WaitForRelease;
                    }
                    else if (rightHeld)
                    {
                        _gesture = Gesture.Pan;
                    }
                    else
                    {
                        _gesture = Gesture.PendingLeftClick;
                        _pendingDragX = 0f;
                        _pendingDragY = 0f;
                    }
                }
                else if (rightPressed)
                {
                    _gesture = leftHeld ? Gesture.Pan : Gesture.Look;
                }
                else if (middlePressed)
                {
                    _gesture = Gesture.Pan;
                }

                break;
            case Gesture.PendingLeftClick:
            case Gesture.LeftDrag:
                if (rightPressed)
                {
                    _gesture = Gesture.Pan;
                }
                else if (!leftHeld)
                {
                    _gesture = rightHeld ? Gesture.Look : Gesture.Pan;
                }

                break;
            case Gesture.Look:
                if (leftPressed)
                {
                    _gesture = Gesture.Pan;
                }
                else if (!rightHeld)
                {
                    _gesture = Gesture.Pan;
                }

                break;
            case Gesture.Pan:
                if (!middleHeld)
                {
                    if (leftHeld && !rightHeld)
                    {
                        _gesture = Gesture.LeftDrag;
                    }
                    else if (rightHeld && !leftHeld)
                    {
                        _gesture = Gesture.Look;
                    }
                }

                break;
            case Gesture.AltOrbit:
                if (!leftHeld)
                {
                    _gesture = Gesture.WaitForRelease;
                }

                break;
            case Gesture.AltDolly:
                if (!rightHeld)
                {
                    _gesture = Gesture.WaitForRelease;
                }

                break;
            case Gesture.AltTrack:
                if (!middleHeld)
                {
                    _gesture = Gesture.WaitForRelease;
                }

                break;
        }
    }

    private bool MoveAndTurn(ref ViewportCameraState state, float dx, float dy)
    {
        if (dx == 0f && dy == 0f)
        {
            return false;
        }

        // Mouse up (negative dy) moves the eye forward along the horizontal view direction, then the camera
        // turns around the eye.
        Vector3 move = ComputeForward(state.Yaw, 0f) * (-dy * PanSensitivity * state.Distance);
        Vector3 eye = state.Eye + move;
        state.Yaw = WrapYaw(state.Yaw + dx * RotationSensitivity);
        state.Pivot = eye + ComputeForward(state.Yaw, state.Pitch) * state.Distance;
        return true;
    }

    private bool LookAround(ref ViewportCameraState state, float dx, float dy)
    {
        if (dx == 0f && dy == 0f)
        {
            return false;
        }

        Vector3 eye = state.Eye;
        state.Yaw = WrapYaw(state.Yaw + dx * RotationSensitivity);
        state.Pitch = Math.Clamp(state.Pitch - PitchDelta(dy), MinPitch, MaxPitch);
        state.Pivot = eye + ComputeForward(state.Yaw, state.Pitch) * state.Distance;
        return true;
    }

    private bool Orbit(ref ViewportCameraState state, float dx, float dy)
    {
        if (dx == 0f && dy == 0f)
        {
            return false;
        }

        state.Yaw = WrapYaw(state.Yaw + dx * RotationSensitivity);
        state.Pitch = Math.Clamp(state.Pitch - PitchDelta(dy), MinPitch, MaxPitch);
        return true;
    }

    private bool Pan(ref ViewportCameraState state, float dx, float dy)
    {
        if (dx == 0f && dy == 0f)
        {
            return false;
        }

        Vector3 forward = ComputeForward(state.Yaw, state.Pitch);
        Vector3 right = ComputeRight(state.Yaw);
        Vector3 up = Vector3.Cross(right, forward);
        float scale = PanSensitivity * state.Distance;

        // The camera follows the mouse: drag right moves right, drag up moves up.
        state.Pivot += right * (dx * scale) - up * (dy * scale);
        return true;
    }

    /// <summary>
    /// Moves the eye toward the pivot by <paramref name="amount"/> (negative moves away). The pivot stays put
    /// until the distance reaches <see cref="MinDistance"/>; past it the pivot is pushed ahead of the eye.
    /// </summary>
    private bool MoveTowardPivot(ref ViewportCameraState state, float amount)
    {
        if (amount == 0f)
        {
            return false;
        }

        float distance = state.Distance - amount;
        if (distance < MinDistance)
        {
            state.Pivot += ComputeForward(state.Yaw, state.Pitch) * (MinDistance - distance);
            distance = MinDistance;
        }
        else if (distance > MaxDistance)
        {
            distance = MaxDistance;
        }

        bool changed = distance != state.Distance || amount > 0f;
        state.Distance = distance;
        return changed;
    }

    private bool Fly(ref ViewportCameraState state, ViewportNavigationKeys keys, float elapsedSeconds)
    {
        if (elapsedSeconds <= 0f)
        {
            return false;
        }

        Vector3 forward = ComputeForward(state.Yaw, state.Pitch);
        Vector3 right = ComputeRight(state.Yaw);
        Vector3 localUp = Vector3.Cross(right, forward);
        Vector3 move = forward * Axis(keys, ViewportNavigationKeys.Forward, ViewportNavigationKeys.Backward)
            + right * Axis(keys, ViewportNavigationKeys.Right, ViewportNavigationKeys.Left)
            + Vector3.Up * Axis(keys, ViewportNavigationKeys.WorldUp, ViewportNavigationKeys.WorldDown)
            + localUp * Axis(keys, ViewportNavigationKeys.LocalUp, ViewportNavigationKeys.LocalDown);

        if (move.LengthSquared() < 1e-8f)
        {
            return false;
        }

        move.Normalize();
        state.Pivot += move * (FlySpeed * elapsedSeconds);
        return true;
    }

    private static float Axis(ViewportNavigationKeys keys, ViewportNavigationKeys positive, ViewportNavigationKeys negative)
    {
        float value = 0f;
        if ((keys & positive) != 0)
        {
            value += 1f;
        }

        if ((keys & negative) != 0)
        {
            value -= 1f;
        }

        return value;
    }

    private float ComputeFieldOfViewDelta(ViewportNavigationKeys keys, float elapsedSeconds)
    {
        float delta = 0f;
        if ((keys & ViewportNavigationKeys.FieldOfViewWider) != 0)
        {
            delta += FieldOfViewRate * elapsedSeconds;
        }

        if ((keys & ViewportNavigationKeys.FieldOfViewNarrower) != 0)
        {
            delta -= FieldOfViewRate * elapsedSeconds;
        }

        return delta;
    }

    private float PitchDelta(float dy)
    {
        float delta = dy * RotationSensitivity;
        return InvertY ? -delta : delta;
    }

    private static float WrapYaw(float yaw)
    {
        return yaw % MathHelper.TwoPi;
    }
}
