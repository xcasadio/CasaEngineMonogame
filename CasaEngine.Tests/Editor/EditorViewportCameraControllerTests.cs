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

    /// <summary>D18: the letter keys move the camera, but not while Ctrl is held, so Ctrl+S (save) and Ctrl+D
    /// (duplicate) leave it where it is; the arrows still move it with Ctrl held.</summary>
    [Theory]
    [InlineData(true, Keys.S)]
    [InlineData(false, Keys.LeftControl, Keys.S)]
    [InlineData(false, Keys.RightControl, Keys.S)]
    [InlineData(false, Keys.LeftControl, Keys.D)]
    [InlineData(true, Keys.LeftControl, Keys.Up)]
    public void Update_LetterKeysMoveTheCamera_ButNotWhileControlIsHeld(bool moves, params Keys[] keys)
    {
        var controller = new EditorViewportCameraController();
        var camera = controller.CreateCameraComponent();

        controller.Update(
            CreateGameTime(),
            camera,
            CreateContext(new KeyboardState(keys), CreateMouseState(ButtonState.Released)),
            receivesInput: true,
            isKeyboardFocused: true,
            allowFreeCameraMovement: true,
            canHandleKeyboardInput: true,
            activateView: _ => { },
            releaseInput: () => { });

        Assert.Equal(moves, controller.Target != Vector3.Zero);
    }

    private static GameTime CreateGameTime()
        => new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

    private static ViewInputContext CreateContext(KeyboardState keyboardState, MouseState mouseState)
        => new(
            ViewId.Next(),
            1,
            keyboardState,
            mouseState,
            new Rectangle(0, 0, 640, 480),
            mouseState.Position,
            mouseState.Position,
            0,
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