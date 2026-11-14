using CasaEngine.Editor.Runtime;
using CasaEngine.Framework.Scene.Cameras;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace CasaEngine.Tests.Editor;

public sealed class EditorViewportCameraControllerTests
{
    [Fact]
    public void Update_AllowsPointerActivationWhileKeyboardIsCapturedButDoesNotMoveFromKeyboard()
    {
        var controller = new EditorViewportCameraController();
        var camera = controller.CreateCameraComponent();
        bool activated = false;

        var inputContext = CreateContext(
            new KeyboardState(Keys.Up),
            CreateMouseState(ButtonState.Pressed));

        controller.Update(
            CreateGameTime(),
            camera,
            inputContext,
            receivesInput: true,
            isKeyboardFocused: false,
            allowFreeCameraMovement: true,
            canHandleKeyboardInput: false,
            activateView: _ => activated = true,
            releaseInput: () => { });

        Assert.True(activated);
        Assert.Equal(Vector3.Zero, controller.Target);
    }

    [Fact]
    public void Update_UsesKeyboardAfterPointerActivationAndKeyboardCaptureIsReleased()
    {
        var controller = new EditorViewportCameraController();
        var camera = controller.CreateCameraComponent();

        var inputContext = CreateContext(
            new KeyboardState(Keys.Up),
            CreateMouseState(ButtonState.Pressed));

        controller.Update(
            CreateGameTime(),
            camera,
            inputContext,
            receivesInput: true,
            isKeyboardFocused: false,
            allowFreeCameraMovement: true,
            canHandleKeyboardInput: false,
            activateView: _ => { },
            releaseInput: () => { });

        controller.Update(
            CreateGameTime(),
            camera,
            inputContext,
            receivesInput: true,
            isKeyboardFocused: true,
            allowFreeCameraMovement: true,
            canHandleKeyboardInput: true,
            activateView: _ => { },
            releaseInput: () => { });

        Assert.NotEqual(Vector3.Zero, controller.Target);
    }

    /// <summary>D18 and ADR-0089: with the right button held the letter keys fly the camera, but not while Ctrl is
    /// held, so Ctrl+S (save) and Ctrl+D (duplicate) leave it where it is; the arrows still fly with Ctrl held.</summary>
    [Theory]
    [InlineData(true, Keys.S)]
    [InlineData(false, Keys.LeftControl, Keys.S)]
    [InlineData(false, Keys.RightControl, Keys.S)]
    [InlineData(false, Keys.LeftControl, Keys.D)]
    [InlineData(true, Keys.LeftControl, Keys.Up)]
    public void Update_RightHeldLetterKeysFlyTheCamera_ButNotWhileControlIsHeld(bool moves, params Keys[] keys)
    {
        var controller = new EditorViewportCameraController();
        var camera = controller.CreateCameraComponent();

        controller.Update(
            CreateGameTime(),
            camera,
            CreateContext(new KeyboardState(keys), CreateMouseState(ButtonState.Pressed)),
            receivesInput: true,
            isKeyboardFocused: true,
            allowFreeCameraMovement: true,
            canHandleKeyboardInput: true,
            activateView: _ => { },
            releaseInput: () => { });

        Assert.Equal(moves, controller.Target != Vector3.Zero);
    }

    /// <summary>ADR-0089: without the right button the keys do not move the camera.</summary>
    [Fact]
    public void Update_KeysWithoutTheRightButton_DoNotMoveTheCamera()
    {
        var controller = new EditorViewportCameraController();
        var camera = controller.CreateCameraComponent();

        controller.Update(
            CreateGameTime(),
            camera,
            CreateContext(new KeyboardState(Keys.W, Keys.Up), CreateMouseState(ButtonState.Released)),
            receivesInput: true,
            isKeyboardFocused: true,
            allowFreeCameraMovement: true,
            canHandleKeyboardInput: true,
            activateView: _ => { },
            releaseInput: () => { });

        Assert.Equal(Vector3.Zero, controller.Target);
    }

    /// <summary>ADR-0089: the wheel moves toward the pivot without moving it.</summary>
    [Fact]
    public void Update_Wheel_KeepsThePivot()
    {
        var controller = new EditorViewportCameraController();
        var camera = controller.CreateCameraComponent();
        float distance = controller.Distance;

        controller.Update(
            CreateGameTime(),
            camera,
            CreateContext(new KeyboardState(), CreateMouseState(ButtonState.Released), verticalWheelDelta: 120),
            receivesInput: true,
            isKeyboardFocused: true,
            allowFreeCameraMovement: true,
            canHandleKeyboardInput: true,
            activateView: _ => { },
            releaseInput: () => { });

        Assert.Equal(Vector3.Zero, controller.Target);
        Assert.True(controller.Distance < distance);
        Assert.Equal(controller.Distance, camera.Distance);
    }

    /// <summary>Plan T1.2 (D9): with free movement off (front-2D view) the navigation does not run.</summary>
    [Fact]
    public void Update_WithoutFreeCameraMovement_ReturnsNullAndMovesNothing()
    {
        var controller = new EditorViewportCameraController();
        var camera = controller.CreateCameraComponent();
        var before = controller.CaptureState();

        var result = controller.Update(
            CreateGameTime(),
            camera,
            CreateContext(new KeyboardState(Keys.W), CreateMouseState(ButtonState.Pressed), verticalWheelDelta: 120),
            receivesInput: true,
            isKeyboardFocused: true,
            allowFreeCameraMovement: false,
            canHandleKeyboardInput: true,
            activateView: _ => { },
            releaseInput: () => { });

        Assert.Null(result);
        Assert.Equal(before.Target, controller.Target);
        Assert.Equal(before.Distance, controller.Distance);
        Assert.Equal(before.Yaw, controller.Yaw);
        Assert.Equal(before.Pitch, controller.Pitch);
    }

    /// <summary>ADR-0089: Z widens the field of view while the right button is held; the pivot state is untouched.</summary>
    [Fact]
    public void Update_RightHeldZ_WidensTheFieldOfView()
    {
        var controller = new EditorViewportCameraController();
        var camera = controller.CreateCameraComponent();
        camera.FieldOfView = 1f;

        controller.Update(
            new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(0.1)),
            camera,
            CreateContext(new KeyboardState(Keys.Z), CreateMouseState(ButtonState.Pressed)),
            receivesInput: true,
            isKeyboardFocused: true,
            allowFreeCameraMovement: true,
            canHandleKeyboardInput: true,
            activateView: _ => { },
            releaseInput: () => { });

        Assert.True(camera.FieldOfView > 1f);
        Assert.Equal(Vector3.Zero, controller.Target);
    }

    /// <summary>ADR-0091: the user's rotation sensitivity and Y inversion drive the look gesture.</summary>
    [Theory]
    [InlineData(false, 1f)]
    [InlineData(true, -1f)]
    public void ApplyPreferences_SetsTheRotationSensitivityAndInvertY(bool invertY, float pitchSign)
    {
        var controller = new EditorViewportCameraController();
        var camera = controller.CreateCameraComponent();
        controller.ApplyPreferences(new Camera3dPreferences { RotationSensitivity = 0.01f, InvertY = invertY, FlySpeed = 25f });
        float pitch = controller.Pitch;

        void Frame(int y) => controller.Update(
            CreateGameTime(),
            camera,
            CreateContext(new KeyboardState(), new MouseState(20, y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, 0)),
            receivesInput: true,
            isKeyboardFocused: true,
            allowFreeCameraMovement: true,
            canHandleKeyboardInput: true,
            activateView: _ => { },
            releaseInput: () => { });

        Frame(30);
        Frame(20);

        // 10 pixels up at 0.01 rad per pixel.
        Assert.Equal(pitch + pitchSign * 0.1f, controller.Pitch, 1e-4f);
        Assert.Equal(25f, controller.FlySpeed);
    }

    /// <summary>Notice A3: a left press whose pointer input goes away, the view keeping the keyboard focus, is not a click.</summary>
    [Fact]
    public void Update_PointerInputLostDuringALeftPress_ReportsNoClick()
    {
        var controller = new EditorViewportCameraController();
        var camera = controller.CreateCameraComponent();

        ViewportNavigationResult? Frame(ButtonState left, bool receivesInput)
            => controller.Update(
                CreateGameTime(),
                camera,
                CreateContext(new KeyboardState(), CreateMouseState(20, 30, left, ButtonState.Released)),
                receivesInput,
                isKeyboardFocused: true,
                allowFreeCameraMovement: true,
                canHandleKeyboardInput: true,
                activateView: _ => { },
                releaseInput: () => { });

        Frame(ButtonState.Released, receivesInput: true);
        Frame(ButtonState.Pressed, receivesInput: true);
        ViewportNavigationResult? lost = Frame(ButtonState.Released, receivesInput: false);
        Frame(ButtonState.Pressed, receivesInput: true);
        ViewportNavigationResult? click = Frame(ButtonState.Released, receivesInput: true);

        Assert.False(lost.Value.LeftClickReleased);
        Assert.True(click.Value.LeftClickReleased);
    }

    /// <summary>Notice A4: after play mode, a button still held and a pointer that moved do not jump the camera.</summary>
    [Fact]
    public void ResetInput_HeldButtonsWaitForRelease_AndThePointerDoesNotJump()
    {
        var controller = new EditorViewportCameraController();
        var camera = controller.CreateCameraComponent();

        void Frame(int x, int y, ButtonState right)
            => controller.Update(
                CreateGameTime(),
                camera,
                CreateContext(new KeyboardState(), CreateMouseState(x, y, ButtonState.Released, right)),
                receivesInput: true,
                isKeyboardFocused: true,
                allowFreeCameraMovement: true,
                canHandleKeyboardInput: true,
                activateView: _ => { },
                releaseInput: () => { });

        Frame(20, 30, ButtonState.Released);
        float yaw = controller.Yaw;
        controller.ResetInput();

        Frame(300, 200, ButtonState.Pressed);
        Frame(320, 200, ButtonState.Pressed);
        Assert.Equal(yaw, controller.Yaw);

        Frame(320, 200, ButtonState.Released);
        Frame(320, 200, ButtonState.Pressed);
        Frame(330, 200, ButtonState.Pressed);
        Assert.NotEqual(yaw, controller.Yaw);
    }

    private static MouseState CreateMouseState(int x, int y, ButtonState left, ButtonState right)
        => new(x, y, 0, left, ButtonState.Released, right, ButtonState.Released, ButtonState.Released, 0);

    private static GameTime CreateGameTime()
        => new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

    private static ViewInputContext CreateContext(KeyboardState keyboardState, MouseState mouseState, int verticalWheelDelta = 0)
        => new(
            ViewId.Next(),
            1,
            keyboardState,
            mouseState,
            new Rectangle(0, 0, 640, 480),
            mouseState.Position,
            mouseState.Position,
            verticalWheelDelta,
            0,
            InputRoutingState.Empty);

    private static MouseState CreateMouseState(ButtonState rightButton)
        => new(
            20,
            30,
            0,
            ButtonState.Released,
            ButtonState.Released,
            rightButton,
            ButtonState.Released,
            ButtonState.Released,
            0);
}