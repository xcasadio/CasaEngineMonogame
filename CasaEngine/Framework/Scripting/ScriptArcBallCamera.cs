using CasaEngine.Framework.Application;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Framework.Scripting;

/// <summary>
/// Orbit camera script for an <see cref="ArcBallCameraComponent"/>, with the same conventions as the
/// editor viewport camera (<c>EditorViewportCameraController</c>):
/// right mouse drag orbits around the target (yaw += dx, pitch -= dy), arrows move along the camera
/// right/forward axes and PageUp/PageDown along its up axis at <see cref="InputDisplacementRate"/>
/// units per second. The wheel dollies: camera and target both move along the view direction by
/// <see cref="InputDistanceRate"/> × distance per notch, so the orbit distance never changes.
/// <para/>
/// Input is arbitrated with the UI like <see cref="PlayerInput"/> does: the keyboard is ignored while
/// the UI owns it (<see cref="InputRouter.IsKeyboardCapturedByUI"/>) or the pointer is over the UI,
/// the mouse is ignored while the pointer is over or captured by the UI
/// (<see cref="InputRouter.IsMouseHandledByUI"/>). A right drag that starts over the 3D view keeps
/// orbiting even when the cursor crosses a UI window.
/// </summary>
public class ScriptArcBallCamera : GameplayProxy
{
    private const float MinPitch = -MathHelper.PiOver2 + 0.01f;
    private const float MaxPitch = MathHelper.PiOver2 - 0.01f;

    private ArcBallCameraComponent _arcBallCameraComponent;
    private InputComponent _inputComponent;
    private CasaEngineGame _game;
    private bool _orbitLatched;

    /// <summary>Orbit sensitivity in radians per mouse pixel (frame-rate independent).</summary>
    public float InputTurnRate { get; set; }

    /// <summary>
    /// Dolly step per wheel notch, as a fraction of the current orbit distance (camera and target
    /// move together along the view direction; the distance itself is unchanged).
    /// </summary>
    public float InputDistanceRate { get; set; }

    /// <summary>Keyboard displacement speed in world units per second.</summary>
    public float InputDisplacementRate { get; set; }

    public ScriptArcBallCamera()
    {
        InputTurnRate = 0.005f;
        InputDistanceRate = 0.1f;
        InputDisplacementRate = 10.0f;
    }

    protected override void InitializePrivate()
    {
        base.InitializePrivate();

        _arcBallCameraComponent = Owner.GetComponent<ArcBallCameraComponent>();
    }

    public override void InitializeWithWorld(Scene.World.World world)
    {
        _game = world.Game;
        _inputComponent = world.Game.GetGameComponent<InputComponent>();
    }

    public override void Update(float elapsedTime)
    {
        if (_arcBallCameraComponent == null || _inputComponent == null)
        {
            return;
        }

        ResolveUiOwnership(out bool keyboardBlocked, out bool mouseBlocked);

        var mouse = _inputComponent.MouseManager;
        var keyboard = _inputComponent.KeyboardManager;

        if (mouse.RightButtonJustPressed)
        {
            _orbitLatched = !mouseBlocked;
        }
        else if (!mouse.RightButtonPressed)
        {
            _orbitLatched = false;
        }

        if (_orbitLatched && mouse.RightButtonPressed)
        {
            float dx = mouse.DeltaX;
            float dy = mouse.DeltaY;
            if (dx != 0f)
            {
                _arcBallCameraComponent.Yaw += dx * InputTurnRate;
            }

            if (dy != 0f)
            {
                _arcBallCameraComponent.Pitch = MathHelper.Clamp(_arcBallCameraComponent.Pitch - dy * InputTurnRate, MinPitch, MaxPitch);
            }
        }

        if (!mouseBlocked)
        {
            int wheelDelta = mouse.WheelDelta;
            if (wheelDelta != 0)
            {
                // One notch = 120 units. Wheel forward moves camera and target forward along the view.
                float notches = wheelDelta / 120f;
                float step = notches * InputDistanceRate * Math.Abs(_arcBallCameraComponent.Distance);
                _arcBallCameraComponent.Target += _arcBallCameraComponent.Direction * step;
            }
        }

        if (keyboardBlocked)
        {
            return;
        }

        var move = Vector3.Zero;

        if (keyboard.IsKeyPressed(Keys.Right))
        {
            move += _arcBallCameraComponent.Right;
        }
        else if (keyboard.IsKeyPressed(Keys.Left))
        {
            move -= _arcBallCameraComponent.Right;
        }

        if (keyboard.IsKeyPressed(Keys.Up))
        {
            move += _arcBallCameraComponent.Direction;
        }
        else if (keyboard.IsKeyPressed(Keys.Down))
        {
            move -= _arcBallCameraComponent.Direction;
        }

        if (keyboard.IsKeyPressed(Keys.PageUp))
        {
            move += _arcBallCameraComponent.Up;
        }
        else if (keyboard.IsKeyPressed(Keys.PageDown))
        {
            move -= _arcBallCameraComponent.Up;
        }

        if (move != Vector3.Zero)
        {
            move.Normalize();
            _arcBallCameraComponent.Target += move * InputDisplacementRate * elapsedTime;
        }
    }

    private void ResolveUiOwnership(out bool keyboardBlocked, out bool mouseBlocked)
    {
        keyboardBlocked = false;
        mouseBlocked = false;

        var view = _game?.GameManager.ViewManager.ActiveView;
        var router = _inputComponent.InputRouter;
        if (view == null)
        {
            return;
        }

        IUIViewRuntime uiView = view.UIView;
        bool pointerOverUi = router != null ? router.IsMouseHandledByUI(view) : uiView?.IsPointerOverUI == true;
        bool pointerCapturedByUi = uiView?.IsPointerCaptured == true;
        bool keyboardCapturedByUi = router != null ? router.IsKeyboardCapturedByUI(view) : uiView?.IsKeyboardCaptured == true;
        bool modalUi = uiView?.HasModalInput == true;

        mouseBlocked = pointerOverUi || pointerCapturedByUi || modalUi;
        keyboardBlocked = keyboardCapturedByUi || pointerOverUi || modalUi;
    }

    public override void Draw()
    {
    }

    public override void OnHit(Collision collision)
    {
    }

    public override void OnHitEnded(Collision collision)
    {
    }

    public override void OnBeginPlay(Scene.World.World world)
    {
    }

    public override void OnEndPlay(Scene.World.World world)
    {
    }

    public override IGameplayProxy Clone()
    {
        return new ScriptArcBallCamera
        {
            InputTurnRate = InputTurnRate,
            InputDistanceRate = InputDistanceRate,
            InputDisplacementRate = InputDisplacementRate,
        };
    }
}
