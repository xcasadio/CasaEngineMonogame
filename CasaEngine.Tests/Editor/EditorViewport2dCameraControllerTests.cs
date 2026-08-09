using CasaEngine.Editor.Runtime;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace CasaEngine.Tests.Editor;

public sealed class EditorViewport2dCameraControllerTests
{
    [Theory]
    [InlineData(0, 1f)]
    [InlineData(1, 2f)]
    [InlineData(2, 3f)]
    [InlineData(-1, 0.5f)]
    [InlineData(-3, 0.25f)]
    public void ZoomFromStep_MapsStepsToIntegerFactorsAndFractions(int step, float expected)
    {
        Assert.Equal(expected, EditorViewport2dCameraController.ZoomFromStep(step), 5);
    }

    [Fact]
    public void ZoomAtCursor_IsClampedToTheSupportedStepRange()
    {
        var controller = new EditorViewport2dCameraController();

        controller.ZoomAtCursor(1000, new Point(0, 0), Rectangle.Empty);
        Assert.Equal(EditorViewport2dCameraController.MaximumZoomStep, controller.ZoomStep);

        controller.ZoomAtCursor(-1000, new Point(0, 0), Rectangle.Empty);
        Assert.Equal(EditorViewport2dCameraController.MinimumZoomStep, controller.ZoomStep);
    }

    [Fact]
    public void ZoomAtCursor_KeepsTheWorldPointUnderTheCursorFixed()
    {
        var controller = new EditorViewport2dCameraController();
        var bounds = new Rectangle(0, 0, 640, 480);
        var cursor = new Point(500, 100);

        controller.SetState(new Vector3(120f, -40f, 0f), 0);
        var before = ProjectCursorToWorld(controller, cursor, bounds);

        controller.ZoomAtCursor(2, cursor, bounds);

        var after = ProjectCursorToWorld(controller, cursor, bounds);
        Assert.Equal(3f, controller.Zoom, 5);
        Assert.Equal(before.X, after.X, 3);
        Assert.Equal(before.Y, after.Y, 3);
    }

    [Fact]
    public void Pan_MovesTheTargetOppositeToTheDragScaledByZoom()
    {
        var controller = new EditorViewport2dCameraController();
        controller.SetState(Vector3.Zero, 1);

        controller.Pan(10, 6);

        Assert.Equal(-5f, controller.Target.X, 5);
        Assert.Equal(3f, controller.Target.Y, 5);
    }

    [Fact]
    public void Update_PansOnMiddleButtonDrag()
    {
        var controller = new EditorViewport2dCameraController();
        var camera = controller.CreateCameraComponent();

        Update(controller, camera, CreateMouseState(20, 30, ButtonState.Pressed));
        Update(controller, camera, CreateMouseState(30, 30, ButtonState.Pressed));

        Assert.Equal(-10f, controller.Target.X, 5);
        Assert.Equal(controller.Target, camera.Target);
    }

    [Fact]
    public void Update_ZoomsOnWheelAndCentersOnTheCursor()
    {
        var controller = new EditorViewport2dCameraController();
        var camera = controller.CreateCameraComponent();
        var bounds = new Rectangle(0, 0, 640, 480);
        var mouseState = CreateMouseState(600, 400, ButtonState.Released);
        var context = CreateContext(mouseState, bounds, new Point(600, 400), verticalWheelDelta: 120);

        controller.Update(
            CreateGameTime(),
            camera,
            context,
            receivesInput: true,
            isKeyboardFocused: false,
            allowFreeCameraMovement: true,
            canHandleKeyboardInput: false,
            activateView: _ => { },
            releaseInput: () => { });

        Assert.Equal(1, controller.ZoomStep);
        Assert.Equal(2f, camera.Zoom, 5);
        Assert.NotEqual(0f, controller.Target.X);
    }

    [Fact]
    public void CaptureState_RoundTripsThroughRestoreState()
    {
        var controller = new EditorViewport2dCameraController();
        controller.SetState(new Vector3(42f, -17f, 3f), 2);
        controller.PixelSnap = false;
        var state = controller.CaptureState();

        var restored = new EditorViewport2dCameraController();
        restored.RestoreState(state);

        Assert.Equal(state.Target, restored.Target);
        Assert.Equal(2, restored.ZoomStep);
        Assert.False(restored.PixelSnap);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(120, 1)]
    [InlineData(-240, -2)]
    [InlineData(30, 1)]
    [InlineData(-30, -1)]
    public void WheelDeltaToSteps_ConvertsRawDeltasToNotches(int delta, int expectedSteps)
    {
        Assert.Equal(expectedSteps, EditorViewport2dCameraController.WheelDeltaToSteps(delta));
    }

    private static Vector2 ProjectCursorToWorld(EditorViewport2dCameraController controller, Point cursor, Rectangle bounds)
    {
        float zoom = controller.Zoom;
        return new Vector2(
            controller.Target.X + (cursor.X - bounds.Width * 0.5f) / zoom,
            controller.Target.Y - (cursor.Y - bounds.Height * 0.5f) / zoom);
    }

    private static void Update(
        EditorViewport2dCameraController controller,
        Framework.Scene.Entities.Components.Camera2dComponent camera,
        MouseState mouseState)
    {
        controller.Update(
            CreateGameTime(),
            camera,
            CreateContext(mouseState, new Rectangle(0, 0, 640, 480), mouseState.Position, 0),
            receivesInput: true,
            isKeyboardFocused: false,
            allowFreeCameraMovement: true,
            canHandleKeyboardInput: false,
            activateView: _ => { },
            releaseInput: () => { });
    }

    private static GameTime CreateGameTime()
        => new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

    private static ViewInputContext CreateContext(
        MouseState mouseState,
        Rectangle bounds,
        Point localPosition,
        int verticalWheelDelta)
        => new(
            ViewId.Next(),
            1,
            new KeyboardState(),
            mouseState,
            bounds,
            mouseState.Position,
            localPosition,
            verticalWheelDelta,
            0,
            InputRoutingState.Empty);

    private static MouseState CreateMouseState(int x, int y, ButtonState middleButton)
        => new(
            x,
            y,
            0,
            ButtonState.Released,
            middleButton,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released,
            0);
}
