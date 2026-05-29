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

internal sealed class EditorViewportCameraController
{
    private const float OrbitSensitivity = 0.005f;
    private const float PanSensitivity = 0.01f;
    private const float ZoomSensitivity = 1.1f;
    private const float MinDistance = 0.5f;
    private const float MaxDistance = 1000f;
    private const float DefaultDistance = 10f;
    private const float KeyboardMoveSpeed = 10f;

    private MouseState _previousMouseState;
    private bool _isMiddleDragCapturing;
    private bool _isRightDragCapturing;

    public float Yaw { get; private set; } = MathHelper.PiOver4;
    public float Pitch { get; private set; } = -MathHelper.Pi / 6f;
    public float Distance { get; private set; } = DefaultDistance;
    public Vector3 Target { get; private set; } = Vector3.Zero;

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
        Pitch = Math.Clamp(
            pitch,
            -MathHelper.PiOver2 + 0.01f,
            MathHelper.PiOver2 - 0.01f);
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

    public void Update(
        GameTime gameTime,
        ArcBallCameraComponent camera,
        ViewInputContext inputContext,
        bool receivesInput,
        bool isKeyboardFocused,
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

            if (_isRightDragCapturing && inputContext.MouseState.RightButton == ButtonState.Released)
            {
                _isRightDragCapturing = false;
                ReleaseInputIfIdle(inputContext.MouseState, releaseInput);
            }

            _previousMouseState = inputContext.MouseState;
            return;
        }

        var mouseState = inputContext.MouseState;
        var keyboardState = inputContext.KeyboardState;

        if (receivesInput && mouseState.MiddleButton == ButtonState.Pressed && _previousMouseState.MiddleButton == ButtonState.Released)
        {
            activateView(true);
            _isMiddleDragCapturing = true;
        }

        if (receivesInput && mouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released)
        {
            activateView(true);
            _isRightDragCapturing = true;
        }

        if (_isMiddleDragCapturing && mouseState.MiddleButton == ButtonState.Pressed)
        {
            var dx = mouseState.X - _previousMouseState.X;
            var dy = mouseState.Y - _previousMouseState.Y;
            if (dx != 0 || dy != 0)
            {
                ApplyMiddleMouseDrag(camera, keyboardState, dx, dy);
            }
        }

        if (_isRightDragCapturing && mouseState.RightButton == ButtonState.Pressed)
        {
            var dx = mouseState.X - _previousMouseState.X;
            var dy = mouseState.Y - _previousMouseState.Y;
            if (dx != 0 || dy != 0)
            {
                ApplyLookDrag(camera, dx, dy);
            }
        }

        if (_isMiddleDragCapturing && mouseState.MiddleButton == ButtonState.Released)
        {
            _isMiddleDragCapturing = false;
            ReleaseInputIfIdle(mouseState, releaseInput);
        }

        if (_isRightDragCapturing && mouseState.RightButton == ButtonState.Released)
        {
            _isRightDragCapturing = false;
            ReleaseInputIfIdle(mouseState, releaseInput);
        }

        if (receivesInput && inputContext.VerticalWheelDelta != 0)
        {
            activateView(false);
            ApplyScroll(inputContext.VerticalWheelDelta);
        }

        if (canHandleKeyboardInput && (receivesInput || isKeyboardFocused))
        {
            HandleKeyboardCameraInput(camera, keyboardState, (float)gameTime.ElapsedGameTime.TotalSeconds, _isRightDragCapturing || isKeyboardFocused);
        }

        _previousMouseState = mouseState;
    }

    private void ApplyMiddleMouseDrag(ArcBallCameraComponent camera, KeyboardState keyboardState, int dx, int dy)
    {
        bool isShift = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);

        if (isShift)
        {
            var right = Vector3.Cross(Vector3.Up, ComputeCameraDirection());
            right.Normalize();
            Target -= right * dx * PanSensitivity * Distance;
            Target += Vector3.Up * dy * PanSensitivity * Distance;
        }
        else
        {
            Yaw += dx * OrbitSensitivity;
            Pitch = Math.Clamp(
                Pitch - dy * OrbitSensitivity,
                -MathHelper.PiOver2 + 0.01f,
                MathHelper.PiOver2 - 0.01f);
        }

        ApplyTo(camera);
    }

    private void ApplyLookDrag(ArcBallCameraComponent camera, int dx, int dy)
    {
        Yaw += dx * OrbitSensitivity;
        Pitch = Math.Clamp(
            Pitch - dy * OrbitSensitivity,
            -MathHelper.PiOver2 + 0.01f,
            MathHelper.PiOver2 - 0.01f);

        ApplyTo(camera);
    }

    private void ApplyScroll(int scrollDelta)
    {
        if (scrollDelta > 0)
        {
            Distance = Math.Max(MinDistance, Distance / ZoomSensitivity);
        }
        else if (scrollDelta < 0)
        {
            Distance = Math.Min(MaxDistance, Distance * ZoomSensitivity);
        }
    }

    private void HandleKeyboardCameraInput(ArcBallCameraComponent camera, KeyboardState keyboardState, float elapsedSeconds, bool allowClassicKeys)
    {
        if (elapsedSeconds <= 0f)
        {
            return;
        }

        var move = Vector3.Zero;

        if (keyboardState.IsKeyDown(Keys.Right))
        {
            move += camera.Right;
        }
        else if (keyboardState.IsKeyDown(Keys.Left))
        {
            move -= camera.Right;
        }

        if (keyboardState.IsKeyDown(Keys.Up))
        {
            move += camera.Direction;
        }
        else if (keyboardState.IsKeyDown(Keys.Down))
        {
            move -= camera.Direction;
        }

        if (keyboardState.IsKeyDown(Keys.PageUp))
        {
            move += camera.Up;
        }
        else if (keyboardState.IsKeyDown(Keys.PageDown))
        {
            move -= camera.Up;
        }

        if (allowClassicKeys)
        {
            if (keyboardState.IsKeyDown(Keys.D))
            {
                move += camera.Right;
            }
            else if (keyboardState.IsKeyDown(Keys.A))
            {
                move -= camera.Right;
            }

            if (keyboardState.IsKeyDown(Keys.W))
            {
                move += camera.Direction;
            }
            else if (keyboardState.IsKeyDown(Keys.S))
            {
                move -= camera.Direction;
            }

            if (keyboardState.IsKeyDown(Keys.E))
            {
                move += camera.Up;
            }
            else if (keyboardState.IsKeyDown(Keys.Q))
            {
                move -= camera.Up;
            }
        }

        if (move == Vector3.Zero)
        {
            return;
        }

        move.Normalize();
        Target += move * KeyboardMoveSpeed * elapsedSeconds;
        ApplyTo(camera);
    }

    private static void ReleaseInputIfIdle(MouseState mouseState, Action releaseInput)
    {
        if (mouseState.MiddleButton == ButtonState.Released && mouseState.RightButton == ButtonState.Released)
        {
            releaseInput();
        }
    }

    private Vector3 ComputeCameraDirection()
    {
        return new Vector3(
            (float)(Math.Cos(Pitch) * Math.Sin(Yaw)),
            (float)Math.Sin(Pitch),
            (float)(Math.Cos(Pitch) * Math.Cos(Yaw)));
    }
}
