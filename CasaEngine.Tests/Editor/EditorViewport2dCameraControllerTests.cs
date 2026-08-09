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
    public void PixelSnap_IsDisabledByDefaultSoThatNavigationStaysExact()
    {
        Assert.False(new EditorViewport2dCameraController().PixelSnap);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(-1)]
    [InlineData(-3)]
    public void ZoomAtCursor_KeepsTheWorldPointUnderTheCursorFixed_MeasuredOnTheCameraMatrices(int steps)
    {
        var controller = new EditorViewport2dCameraController();
        var bounds = new Rectangle(0, 0, 640, 480);
        var cursor = new Point(500, 100);

        controller.SetState(new Vector3(123.5f, -47.25f, 0f), 0);
        var camera = CreateCameraFor(controller, bounds);

        // World point currently under the cursor, obtained from the real view/projection matrices.
        var worldUnderCursor = UnprojectCursor(camera, cursor);

        controller.ZoomAtCursor(steps, cursor, bounds);
        controller.ApplyTo(camera);

        var reprojected = ProjectToScreen(camera, worldUnderCursor);
        Assert.Equal(cursor.X, reprojected.X, 2);
        Assert.Equal(cursor.Y, reprojected.Y, 2);
    }

    [Fact]
    public void ZoomAtCursor_WithPixelSnap_DriftsByAtMostOneTexel()
    {
        // Documented behaviour, not a bug: texel snapping quantizes the camera, so the point under
        // the cursor can move by up to one texel (1 / Zoom world units) per zoom notch.
        var controller = new EditorViewport2dCameraController { PixelSnap = true };
        var bounds = new Rectangle(0, 0, 640, 480);
        var cursor = new Point(500, 100);

        controller.SetState(new Vector3(123.5f, -47.25f, 0f), 0);
        var camera = CreateCameraFor(controller, bounds);

        var worldUnderCursor = UnprojectCursor(camera, cursor);

        controller.ZoomAtCursor(2, cursor, bounds);
        controller.ApplyTo(camera);

        var reprojected = ProjectToScreen(camera, worldUnderCursor);
        // Snapping rounds the camera to half a texel before and after the change, so the drift in
        // screen pixels is bounded by 0.5 * newZoom / oldZoom + 0.5 — here 2 pixels for 1x -> 3x.
        const float toleranceInPixels = 3f;
        Assert.InRange(reprojected.X, cursor.X - toleranceInPixels, cursor.X + toleranceInPixels);
        Assert.InRange(reprojected.Y, cursor.Y - toleranceInPixels, cursor.Y + toleranceInPixels);
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

    private static Framework.Scene.Entities.Components.Camera2dComponent CreateCameraFor(
        EditorViewport2dCameraController controller,
        Rectangle bounds)
    {
        var camera = controller.CreateCameraComponent();
        camera.OnScreenResized(bounds.Width, bounds.Height);
        camera.FarPlane = 1000.0f;
        camera.NearPlane = 1.0f;
        controller.ApplyTo(camera);
        return camera;
    }

    private static Vector3 UnprojectCursor(
        Framework.Scene.Entities.Components.Camera2dComponent camera,
        Point cursor)
    {
        // Depth of the target plane, so that the unprojected point lies on the plane being edited.
        float depth = camera.Viewport.Project(
            camera.Target,
            camera.ProjectionMatrix,
            camera.ViewMatrix,
            Matrix.Identity).Z;

        return camera.Viewport.Unproject(
            new Vector3(cursor.X, cursor.Y, depth),
            camera.ProjectionMatrix,
            camera.ViewMatrix,
            Matrix.Identity);
    }

    private static Vector3 ProjectToScreen(
        Framework.Scene.Entities.Components.Camera2dComponent camera,
        Vector3 worldPosition)
        => camera.Viewport.Project(
            worldPosition,
            camera.ProjectionMatrix,
            camera.ViewMatrix,
            Matrix.Identity);

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
