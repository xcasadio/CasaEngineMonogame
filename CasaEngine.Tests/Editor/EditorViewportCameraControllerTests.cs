using CasaEngine.Editor.Runtime;
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