using CasaEngine.Framework.Scene.Cameras;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace CasaEngine.Tests.Scene;

/// <summary>
/// Pins the Unreal-style viewport navigation of ADR-0089 (one test per control of decision D7 of
/// ai-agent/tasks/unreal-viewport-camera-tasks.md, plus the click/drag and gizmo-handle rules).
/// </summary>
public class ViewportCameraNavigationTests
{
    private const float Tolerance = 1e-3f;

    private static ViewportCameraState CreateState()
    {
        return new ViewportCameraState(new Vector3(1f, 2f, 3f), 10f, 0.3f, -0.2f);
    }

    private static ViewportNavigationResult Step(
        ViewportCameraNavigation navigation,
        ref ViewportCameraState state,
        ViewportMouseButtons buttons,
        float dx = 0f,
        float dy = 0f,
        float wheel = 0f,
        bool alt = false,
        ViewportNavigationKeys keys = ViewportNavigationKeys.None,
        bool onHandle = false,
        float elapsed = 0.1f)
    {
        var input = new ViewportNavigationInput
        {
            Buttons = buttons,
            Alt = alt,
            MouseDeltaX = dx,
            MouseDeltaY = dy,
            WheelNotches = wheel,
            Keys = keys,
            LeftPressedOnGizmoHandle = onHandle,
            ElapsedSeconds = elapsed,
        };
        return navigation.Update(ref state, input);
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, Tolerance);
        Assert.Equal(expected.Y, actual.Y, Tolerance);
        Assert.Equal(expected.Z, actual.Z, Tolerance);
    }

    [Fact]
    public void Eye_MatchesTheArcBallComponentPosition()
    {
        var state = CreateState();
        var camera = new ArcBallCameraComponent();
        camera.OnScreenResized(1920, 1080);
        camera.FarPlane = 1000f;
        camera.NearPlane = 1f;

        state.ApplyTo(camera);
        _ = camera.ViewMatrix;

        AssertVector(state.Eye, camera.Position);
        AssertVector(state.Forward, camera.Direction);
    }

    [Fact]
    public void ApplyTo_LeavesTheFieldOfViewUntouched()
    {
        var camera = new ArcBallCameraComponent();
        camera.FieldOfView = 1.1f;

        CreateState().ApplyTo(camera);

        Assert.Equal(1.1f, camera.FieldOfView, Tolerance);
    }

    [Fact]
    public void Wheel_MovesTheEyeTowardThePivot_WithoutMovingThePivot()
    {
        var navigation = new ViewportCameraNavigation();
        var state = CreateState();
        Vector3 pivot = state.Pivot;

        var result = Step(navigation, ref state, ViewportMouseButtons.None, wheel: 1f);

        Assert.True(result.CameraChanged);
        AssertVector(pivot, state.Pivot);
        Assert.Equal(9f, state.Distance, Tolerance);

        Step(navigation, ref state, ViewportMouseButtons.None, wheel: -1f);
        AssertVector(pivot, state.Pivot);
        Assert.Equal(9.9f, state.Distance, Tolerance);
    }

    [Fact]
    public void Wheel_AtTheMinimumDistance_PushesThePivotAhead()
    {
        var navigation = new ViewportCameraNavigation();
        var state = new ViewportCameraState(Vector3.Zero, 0.6f, 0f, 0f);
        Vector3 eye = state.Eye;

        Step(navigation, ref state, ViewportMouseButtons.None, wheel: 1f);

        Assert.Equal(navigation.MinDistance, state.Distance, Tolerance);
        // The minimum step is 0.5: the eye moved 0.5 forward, so the pivot moved 0.4 ahead.
        AssertVector(eye + state.Forward * 0.5f, state.Eye);
        AssertVector(new Vector3(0f, 0f, -0.4f), state.Pivot);
    }

    [Fact]
    public void RightDrag_LooksAroundTheEye()
    {
        var navigation = new ViewportCameraNavigation();
        var state = CreateState();
        Vector3 eye = state.Eye;

        Step(navigation, ref state, ViewportMouseButtons.Right);
        Step(navigation, ref state, ViewportMouseButtons.Right, dx: 40f, dy: -20f);

        AssertVector(eye, state.Eye);
        Assert.Equal(0.3f + 40f * 0.005f, state.Yaw, Tolerance);
        Assert.Equal(-0.2f + 20f * 0.005f, state.Pitch, Tolerance);
        Assert.Equal(10f, state.Distance, Tolerance);
    }

    [Fact]
    public void LeftDrag_MovesForwardAndTurns_AfterTheThreshold()
    {
        var navigation = new ViewportCameraNavigation();
        var state = CreateState();
        Vector3 eye = state.Eye;

        Step(navigation, ref state, ViewportMouseButtons.Left);
        var below = Step(navigation, ref state, ViewportMouseButtons.Left, dy: -3f);
        Assert.False(below.CameraChanged);
        AssertVector(eye, state.Eye);

        Step(navigation, ref state, ViewportMouseButtons.Left, dy: -2f);

        // 5 pixels up (past the 4-pixel threshold) = 5 * 0.01 * 10 = 0.5 forward on the horizontal plane.
        Vector3 expectedEye = eye + ViewportCameraNavigation.ComputeForward(0.3f, 0f) * 0.5f;
        AssertVector(expectedEye, state.Eye);
        Assert.Equal(-0.2f, state.Pitch, Tolerance);

        Step(navigation, ref state, ViewportMouseButtons.Left, dx: 10f);
        Assert.Equal(0.3f + 10f * 0.005f, state.Yaw, Tolerance);
        AssertVector(expectedEye, state.Eye);

        var release = Step(navigation, ref state, ViewportMouseButtons.None);
        Assert.False(release.LeftClickReleased);
    }

    [Fact]
    public void LeftClick_BelowTheThreshold_IsAClick()
    {
        var navigation = new ViewportCameraNavigation();
        var state = CreateState();
        ViewportCameraState before = state;

        Step(navigation, ref state, ViewportMouseButtons.Left);
        Step(navigation, ref state, ViewportMouseButtons.Left, dx: 2f, dy: 2f);
        var result = Step(navigation, ref state, ViewportMouseButtons.None);

        Assert.True(result.LeftClickReleased);
        Assert.Equal(before, state);
    }

    [Fact]
    public void LeftPressOnGizmoHandle_MovesNothingAndIsNotAClick()
    {
        var navigation = new ViewportCameraNavigation();
        var state = CreateState();
        ViewportCameraState before = state;

        Step(navigation, ref state, ViewportMouseButtons.Left, onHandle: true);
        Step(navigation, ref state, ViewportMouseButtons.Left, dx: 50f, dy: 30f);
        var result = Step(navigation, ref state, ViewportMouseButtons.None);

        Assert.False(result.LeftClickReleased);
        Assert.Equal(before, state);
    }

    [Theory]
    [InlineData(ViewportMouseButtons.Middle)]
    [InlineData(ViewportMouseButtons.Left | ViewportMouseButtons.Right)]
    public void PanGestures_MoveTheCameraWithTheMouse(ViewportMouseButtons buttons)
    {
        var navigation = new ViewportCameraNavigation();
        var state = new ViewportCameraState(Vector3.Zero, 10f, 0f, 0f);

        Step(navigation, ref state, buttons);
        Step(navigation, ref state, buttons, dx: 10f, dy: -10f);

        // Facing -Z: right is +X and up is +Y; 10 pixels = 10 * 0.01 * 10 = 1 unit.
        AssertVector(new Vector3(1f, 1f, 0f), state.Pivot);
        Assert.Equal(0f, state.Yaw, Tolerance);
        Assert.Equal(10f, state.Distance, Tolerance);
    }

    [Fact]
    public void AltLeftDrag_OrbitsAroundThePivot()
    {
        var navigation = new ViewportCameraNavigation();
        var state = CreateState();
        Vector3 pivot = state.Pivot;

        Step(navigation, ref state, ViewportMouseButtons.Left, alt: true);
        Step(navigation, ref state, ViewportMouseButtons.Left, dx: 20f, dy: 10f, alt: true);
        var result = Step(navigation, ref state, ViewportMouseButtons.None);

        AssertVector(pivot, state.Pivot);
        Assert.Equal(0.3f + 20f * 0.005f, state.Yaw, Tolerance);
        Assert.Equal(-0.2f - 10f * 0.005f, state.Pitch, Tolerance);
        Assert.False(result.LeftClickReleased);
    }

    [Fact]
    public void AltRightDrag_DolliesTowardThePivot()
    {
        var navigation = new ViewportCameraNavigation();
        var state = CreateState();
        Vector3 pivot = state.Pivot;

        Step(navigation, ref state, ViewportMouseButtons.Right, alt: true);
        Step(navigation, ref state, ViewportMouseButtons.Right, dx: 10f, alt: true);

        AssertVector(pivot, state.Pivot);
        Assert.Equal(9f, state.Distance, Tolerance);
        Assert.Equal(0.3f, state.Yaw, Tolerance);
    }

    [Fact]
    public void AltMiddleDrag_Tracks()
    {
        var navigation = new ViewportCameraNavigation();
        var state = new ViewportCameraState(Vector3.Zero, 10f, 0f, 0f);

        Step(navigation, ref state, ViewportMouseButtons.Middle, alt: true);
        Step(navigation, ref state, ViewportMouseButtons.Middle, dx: 10f, alt: true);

        AssertVector(new Vector3(1f, 0f, 0f), state.Pivot);
    }

    [Theory]
    [InlineData(ViewportNavigationKeys.Forward, 0f, 0f, -1f)]
    [InlineData(ViewportNavigationKeys.Backward, 0f, 0f, 1f)]
    [InlineData(ViewportNavigationKeys.Left, -1f, 0f, 0f)]
    [InlineData(ViewportNavigationKeys.Right, 1f, 0f, 0f)]
    [InlineData(ViewportNavigationKeys.WorldUp, 0f, 1f, 0f)]
    [InlineData(ViewportNavigationKeys.WorldDown, 0f, -1f, 0f)]
    [InlineData(ViewportNavigationKeys.LocalUp, 0f, 1f, 0f)]
    [InlineData(ViewportNavigationKeys.LocalDown, 0f, -1f, 0f)]
    public void RightHeldKeys_FlyTheCamera(ViewportNavigationKeys key, float x, float y, float z)
    {
        var navigation = new ViewportCameraNavigation();
        var state = new ViewportCameraState(Vector3.Zero, 10f, 0f, 0f);

        var result = Step(navigation, ref state, ViewportMouseButtons.Right, keys: key, elapsed: 0.1f);

        // 10 units per second for 0.1 second.
        AssertVector(new Vector3(x, y, z), state.Pivot);
        Assert.True(result.IsFlying);
        Assert.False(result.FocusRequested);
    }

    [Fact]
    public void LocalUp_FollowsThePitch_WorldUpDoesNot()
    {
        var navigation = new ViewportCameraNavigation();
        var pitched = new ViewportCameraState(Vector3.Zero, 10f, 0f, -MathHelper.PiOver4);
        var world = pitched;

        Step(navigation, ref pitched, ViewportMouseButtons.Right, keys: ViewportNavigationKeys.LocalUp);
        Step(navigation, ref world, ViewportMouseButtons.Right, keys: ViewportNavigationKeys.WorldUp);

        AssertVector(new Vector3(0f, 1f, 0f), world.Pivot);
        Assert.True(pitched.Pivot.Z < -0.5f);
        Assert.True(pitched.Pivot.Y > 0.5f);
    }

    [Fact]
    public void KeysWithoutTheRightButton_DoNotFly()
    {
        var navigation = new ViewportCameraNavigation();
        var state = CreateState();
        ViewportCameraState before = state;

        var result = Step(navigation, ref state, ViewportMouseButtons.None, keys: ViewportNavigationKeys.Forward | ViewportNavigationKeys.FieldOfViewWider);

        Assert.Equal(before, state);
        Assert.False(result.IsFlying);
        Assert.Equal(0f, result.FieldOfViewDelta);
    }

    [Fact]
    public void RightHeldWheel_ChangesTheFlySpeed_NotTheCamera()
    {
        var navigation = new ViewportCameraNavigation();
        var state = CreateState();
        ViewportCameraState before = state;

        Step(navigation, ref state, ViewportMouseButtons.Right, wheel: 2f);

        Assert.Equal(before, state);
        Assert.Equal(10f * 1.25f * 1.25f, navigation.FlySpeed, Tolerance);
    }

    [Fact]
    public void RightHeldZAndC_ReturnAFieldOfViewDelta()
    {
        var navigation = new ViewportCameraNavigation();
        var state = CreateState();

        var wider = Step(navigation, ref state, ViewportMouseButtons.Right, keys: ViewportNavigationKeys.FieldOfViewWider, elapsed: 0.5f);
        var narrower = Step(navigation, ref state, ViewportMouseButtons.Right, keys: ViewportNavigationKeys.FieldOfViewNarrower, elapsed: 0.5f);

        Assert.Equal(MathHelper.PiOver4 * 0.5f, wider.FieldOfViewDelta, Tolerance);
        Assert.Equal(-MathHelper.PiOver4 * 0.5f, narrower.FieldOfViewDelta, Tolerance);
    }

    [Fact]
    public void F_Alone_RequestsFocus()
    {
        var navigation = new ViewportCameraNavigation();
        var state = CreateState();
        ViewportCameraState before = state;

        var result = Step(navigation, ref state, ViewportMouseButtons.None, keys: ViewportNavigationKeys.FocusPressed | ViewportNavigationKeys.LocalDown);

        Assert.True(result.FocusRequested);
        Assert.Equal(before, state);
    }

    [Fact]
    public void RightHeldF_MovesDownLocally_WithoutFocus()
    {
        var navigation = new ViewportCameraNavigation();
        var state = new ViewportCameraState(Vector3.Zero, 10f, 0f, 0f);

        var result = Step(navigation, ref state, ViewportMouseButtons.Right, keys: ViewportNavigationKeys.FocusPressed | ViewportNavigationKeys.LocalDown);

        Assert.False(result.FocusRequested);
        AssertVector(new Vector3(0f, -1f, 0f), state.Pivot);
    }

    [Theory]
    [InlineData(ViewportMouseButtons.Right, false)]
    [InlineData(ViewportMouseButtons.Left, true)]
    public void InvertY_InvertsLookAndOrbit(ViewportMouseButtons button, bool alt)
    {
        var navigation = new ViewportCameraNavigation { InvertY = true };
        var state = CreateState();

        Step(navigation, ref state, button, alt: alt);
        Step(navigation, ref state, button, dy: -20f, alt: alt);

        // Mouse up looks down when Y is inverted.
        Assert.Equal(-0.2f - 20f * 0.005f, state.Pitch, Tolerance);
    }

    [Fact]
    public void Pitch_IsClamped()
    {
        var navigation = new ViewportCameraNavigation();
        var state = CreateState();

        Step(navigation, ref state, ViewportMouseButtons.Right);
        Step(navigation, ref state, ViewportMouseButtons.Right, dy: -100000f);

        Assert.Equal(ViewportCameraNavigation.MaxPitch, state.Pitch, Tolerance);
    }

    [Fact]
    public void ReadKeys_MapsUnrealKeys_AndSkipsLettersOnRequest()
    {
        var current = new KeyboardState(Keys.W, Keys.F, Keys.PageUp, Keys.Z);
        var previous = new KeyboardState();

        var keys = ViewportCameraNavigation.ReadKeys(current, previous, includeLetterKeys: true);
        Assert.Equal(
            ViewportNavigationKeys.Forward | ViewportNavigationKeys.LocalDown | ViewportNavigationKeys.FocusPressed
            | ViewportNavigationKeys.WorldUp | ViewportNavigationKeys.FieldOfViewWider,
            keys);

        var held = ViewportCameraNavigation.ReadKeys(current, current, includeLetterKeys: true);
        Assert.Equal(0, (int)(held & ViewportNavigationKeys.FocusPressed));

        var noLetters = ViewportCameraNavigation.ReadKeys(current, previous, includeLetterKeys: false);
        Assert.Equal(ViewportNavigationKeys.WorldUp, noLetters);
    }

    /// <summary>Notice A2: right released and left pressed in the same frame is a fresh left press, not a frame of pan.</summary>
    [Fact]
    public void RightReleasedAndLeftPressedTogether_StartsAClick_WithoutPanning()
    {
        var navigation = new ViewportCameraNavigation();
        var state = CreateState();
        Step(navigation, ref state, ViewportMouseButtons.Right);
        ViewportCameraState before = state;

        var moved = Step(navigation, ref state, ViewportMouseButtons.Left, dx: 2f, dy: 1f);
        var released = Step(navigation, ref state, ViewportMouseButtons.None);

        Assert.False(moved.CameraChanged);
        AssertVector(before.Pivot, state.Pivot);
        Assert.True(released.LeftClickReleased);
    }

    /// <summary>Notice A2, other way round: left released and right pressed in the same frame looks at once.</summary>
    [Fact]
    public void LeftReleasedAndRightPressedTogether_LooksAround_WithoutPanning()
    {
        var reference = new ViewportCameraNavigation();
        var expected = CreateState();
        Step(reference, ref expected, ViewportMouseButtons.Right);
        Step(reference, ref expected, ViewportMouseButtons.Right, dx: 2f, dy: 1f);

        var navigation = new ViewportCameraNavigation();
        var state = CreateState();
        Step(navigation, ref state, ViewportMouseButtons.Left);
        Step(navigation, ref state, ViewportMouseButtons.Right, dx: 2f, dy: 1f);

        AssertVector(expected.Pivot, state.Pivot);
        Assert.Equal(expected.Yaw, state.Yaw, Tolerance);
        Assert.Equal(expected.Pitch, state.Pitch, Tolerance);
    }

    /// <summary>A left press handed over from a look still belongs to the gizmo when it lands on a handle.</summary>
    [Fact]
    public void RightReleasedAndLeftPressedOnAHandle_MovesNothingUntilRelease()
    {
        var navigation = new ViewportCameraNavigation();
        var state = CreateState();
        Step(navigation, ref state, ViewportMouseButtons.Right);
        ViewportCameraState before = state;

        Step(navigation, ref state, ViewportMouseButtons.Left, onHandle: true);
        Step(navigation, ref state, ViewportMouseButtons.Left, dx: 50f, dy: 20f);
        var released = Step(navigation, ref state, ViewportMouseButtons.None);

        AssertVector(before.Pivot, state.Pivot);
        Assert.Equal(before.Yaw, state.Yaw, Tolerance);
        Assert.False(released.LeftClickReleased);
    }
}
