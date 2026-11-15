using CasaEngine.Engine.Input;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Scene.Cameras;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Framework.Scripting;

/// <summary>
/// Camera script for an <see cref="ArcBallCameraComponent"/> with the controls of Unreal's perspective viewport,
/// shared with the editor viewport through <see cref="ViewportCameraNavigation"/> (ADR-0089): left drag moves
/// forward/back and turns, right drag looks around, middle drag or left + right drag pans, the wheel moves toward the
/// pivot without moving it (the pivot is pushed ahead once the minimum distance is reached), right button + W/A/S/D
/// (arrows, numpad), E/Q, R/F and Z/C fly and change the field of view, right button + wheel changes the fly speed,
/// Alt + left/right/middle drag orbits, dollies and tracks around the pivot, and F puts the pivot on
/// <see cref="FocusPoint"/>.
/// <para/>
/// The component owns the camera state: the script reads it at the start of each update and writes it back, so
/// values set on the component (a demo's initial camera) are kept. Z/C change the component's field of view, which
/// the component recomputes from the aspect ratio on resize.
/// <para/>
/// Input is arbitrated with the UI like <see cref="PlayerInput"/> does: the keyboard is ignored while the UI owns it
/// (<see cref="InputRouter.IsKeyboardCapturedByUI"/>) or the pointer is over the UI; a mouse button pressed over the
/// UI (<see cref="InputRouter.IsMouseHandledByUI"/>) starts no gesture, and a gesture started over the 3D view goes on
/// when the cursor crosses a UI window.
/// </summary>
public class ScriptArcBallCamera : GameplayProxy
{
    private readonly ViewportCameraNavigation _navigation = new();

    private ArcBallCameraComponent _arcBallCameraComponent;
    private InputComponent _inputComponent;
    private CasaEngineGame _game;
    private ViewportMouseButtons _latchedButtons;
    private KeyboardState _previousKeyboardState;

    /// <summary>Look, turn and orbit sensitivity in radians per mouse pixel.</summary>
    public float InputTurnRate
    {
        get => _navigation.RotationSensitivity;
        set => _navigation.RotationSensitivity = value;
    }

    /// <summary>Wheel notch length, as a fraction of the distance to the pivot.</summary>
    public float InputDistanceRate
    {
        get => _navigation.WheelStepRatio;
        set => _navigation.WheelStepRatio = value;
    }

    /// <summary>Fly speed (right button + keys) in world units per second; the right button + wheel changes it.</summary>
    public float InputDisplacementRate
    {
        get => _navigation.FlySpeed;
        set => _navigation.FlySpeed = value;
    }

    /// <summary>Point the pivot goes to when F is pressed (there is no selection at runtime).</summary>
    public Vector3 FocusPoint { get; set; }

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
        KeyboardState keyboardState = _inputComponent.KeyboardManager.State;

        // A button joins the gesture only when it is pressed over the 3D view.
        ViewportMouseButtons held = ReadHeldButtons(mouse);
        if (!mouseBlocked)
        {
            _latchedButtons |= ReadJustPressedButtons(mouse);
        }

        _latchedButtons &= held;
        bool mouseActive = _latchedButtons != ViewportMouseButtons.None || !mouseBlocked;

        var input = new ViewportNavigationInput
        {
            Buttons = _latchedButtons,
            Alt = keyboardState.IsKeyDown(Keys.LeftAlt) || keyboardState.IsKeyDown(Keys.RightAlt),
            MouseDeltaX = _latchedButtons != ViewportMouseButtons.None ? mouse.DeltaX : 0f,
            MouseDeltaY = _latchedButtons != ViewportMouseButtons.None ? mouse.DeltaY : 0f,
            WheelNotches = mouseActive ? mouse.WheelDelta / 120f : 0f,
            Keys = keyboardBlocked
                ? ViewportNavigationKeys.None
                : ViewportCameraNavigation.ReadKeys(keyboardState, _previousKeyboardState, includeLetterKeys: true),
            ElapsedSeconds = elapsedTime,
        };
        _previousKeyboardState = keyboardState;

        var state = ViewportCameraState.From(_arcBallCameraComponent);
        ViewportNavigationResult result = _navigation.Update(ref state, input);

        if (result.FocusRequested)
        {
            state.Pivot = FocusPoint;
            result.CameraChanged = true;
        }

        if (result.CameraChanged)
        {
            state.ApplyTo(_arcBallCameraComponent);
        }

        if (result.FieldOfViewDelta != 0f)
        {
            _arcBallCameraComponent.FieldOfView += result.FieldOfViewDelta;
        }
    }

    private static ViewportMouseButtons ReadHeldButtons(MouseManager mouse)
    {
        var buttons = ViewportMouseButtons.None;
        if (mouse.LeftButtonPressed)
        {
            buttons |= ViewportMouseButtons.Left;
        }

        if (mouse.RightButtonPressed)
        {
            buttons |= ViewportMouseButtons.Right;
        }

        if (mouse.MiddleButtonPressed)
        {
            buttons |= ViewportMouseButtons.Middle;
        }

        return buttons;
    }

    private static ViewportMouseButtons ReadJustPressedButtons(MouseManager mouse)
    {
        var buttons = ViewportMouseButtons.None;
        if (mouse.LeftButtonJustPressed)
        {
            buttons |= ViewportMouseButtons.Left;
        }

        if (mouse.RightButtonJustPressed)
        {
            buttons |= ViewportMouseButtons.Right;
        }

        if (mouse.MiddleButtonJustPressed)
        {
            buttons |= ViewportMouseButtons.Middle;
        }

        return buttons;
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
            FocusPoint = FocusPoint,
        };
    }
}
