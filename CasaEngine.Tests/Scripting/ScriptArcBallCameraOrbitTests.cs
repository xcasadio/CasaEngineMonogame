using CasaEngine.Framework.Scene.Cameras;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Scripting;

/// <summary>
/// The viewport navigation drives an orbit component as it drives an arc ball camera (ADR-0145): the same input gives
/// the same state and the same view, and <see cref="ScriptArcBallCamera"/> picks the orbit of its entity when there is one.
/// </summary>
public sealed class ScriptArcBallCameraOrbitTests
{
    private const float Tolerance = 1e-4f;

    private static void Size(CameraComponent camera)
    {
        camera.FarPlane = 1000f;
        camera.NearPlane = 1f;
        camera.OnScreenResized(1920, 1080);
    }

    private sealed class ArcBallRig
    {
        public ArcBallRig()
        {
            Camera = new ArcBallCameraComponent();
            Size(Camera);
            Camera.SetCamera(new Vector3(0f, 12f, 15f), new Vector3(0f, 1f, 0f), Vector3.Up);
            var entity = new Entity { RootComponent = Camera };
            entity.SetGameplayProxy(Script);
            entity.Initialize();
        }

        public ArcBallCameraComponent Camera { get; }

        public ScriptArcBallCamera Script { get; } = new();
    }

    private sealed class OrbitRig
    {
        public OrbitRig()
        {
            Camera = new PerspectiveCameraComponent();
            Size(Camera);
            Orbit = new OrbitCameraComponent();
            var entity = new Entity { RootComponent = Camera };
            entity.AddComponent(Orbit);
            Orbit.SetCamera(new Vector3(0f, 12f, 15f), new Vector3(0f, 1f, 0f));
            entity.SetGameplayProxy(Script);
            entity.Initialize();
        }

        public PerspectiveCameraComponent Camera { get; }

        public OrbitCameraComponent Orbit { get; }

        public ScriptArcBallCamera Script { get; } = new();
    }

    // Each sequence is a gesture of the viewport navigation, one input per frame.
    public static TheoryData<string> Gestures => new() { "alt-left-orbit", "wheel", "right-look", "alt-right-dolly", "middle-pan", "fly", "field-of-view" };

    private static ViewportNavigationInput[] Frames(string gesture)
    {
        static ViewportNavigationInput Frame(ViewportMouseButtons buttons, float dx = 0f, float dy = 0f, bool alt = false,
            float wheel = 0f, ViewportNavigationKeys keys = ViewportNavigationKeys.None)
            => new()
            {
                Buttons = buttons,
                Alt = alt,
                MouseDeltaX = dx,
                MouseDeltaY = dy,
                WheelNotches = wheel,
                Keys = keys,
                ElapsedSeconds = 1f / 60f,
            };

        return gesture switch
        {
            "alt-left-orbit" => [Frame(ViewportMouseButtons.Left, alt: true), Frame(ViewportMouseButtons.Left, 20f, 10f, alt: true), Frame(ViewportMouseButtons.None)],
            "wheel" => [Frame(ViewportMouseButtons.None, wheel: 2f), Frame(ViewportMouseButtons.None, wheel: -1f)],
            "right-look" => [Frame(ViewportMouseButtons.Right), Frame(ViewportMouseButtons.Right, 15f, -8f), Frame(ViewportMouseButtons.None)],
            "alt-right-dolly" => [Frame(ViewportMouseButtons.Right, alt: true), Frame(ViewportMouseButtons.Right, 10f, alt: true)],
            "middle-pan" => [Frame(ViewportMouseButtons.Middle), Frame(ViewportMouseButtons.Middle, 12f, 7f)],
            "fly" => [Frame(ViewportMouseButtons.Right), Frame(ViewportMouseButtons.Right, keys: ViewportNavigationKeys.Forward | ViewportNavigationKeys.Right), Frame(ViewportMouseButtons.Right, keys: ViewportNavigationKeys.WorldUp)],
            _ => [Frame(ViewportMouseButtons.Right), Frame(ViewportMouseButtons.Right, keys: ViewportNavigationKeys.FieldOfViewWider), Frame(ViewportMouseButtons.Right, keys: ViewportNavigationKeys.FieldOfViewWider)],
        };
    }

    private static void AssertMatrixEqual(Matrix expected, Matrix actual)
    {
        float[] expectedTerms = Matrix.ToFloatArray(expected);
        float[] actualTerms = Matrix.ToFloatArray(actual);
        for (int index = 0; index < expectedTerms.Length; index++)
        {
            Assert.True(MathF.Abs(expectedTerms[index] - actualTerms[index]) <= Tolerance,
                $"term {index}: expected {expectedTerms[index]}, actual {actualTerms[index]}");
        }
    }

    [Fact]
    public void FromThenApplyTo_OnAnOrbit_GivesTheSameState()
    {
        var orbit = new OrbitCameraComponent { Target = new Vector3(1f, 2f, 3f), Distance = 7f, Yaw = 0.4f, Pitch = -0.2f };
        var other = new OrbitCameraComponent();

        ViewportCameraState state = ViewportCameraState.From(orbit);
        state.ApplyTo(other);

        Assert.Equal(new Vector3(1f, 2f, 3f), state.Pivot);
        Assert.Equal(orbit.Target, other.Target);
        Assert.Equal(orbit.Distance, other.Distance);
        Assert.Equal(orbit.Yaw, other.Yaw);
        Assert.Equal(orbit.Pitch, other.Pitch);
    }

    [Theory]
    [MemberData(nameof(Gestures))]
    public void TheScript_GivesAnOrbitedCameraThePoseItGivesAnArcBall(string gesture)
    {
        var arcBall = new ArcBallRig();
        var orbit = new OrbitRig();
        AssertMatrixEqual(arcBall.Camera.ViewMatrix, orbit.Camera.ViewMatrix);
        Matrix start = orbit.Camera.ViewMatrix;
        float startFieldOfView = orbit.Camera.FieldOfView;

        foreach (ViewportNavigationInput input in Frames(gesture))
        {
            arcBall.Script.ApplyNavigation(input);
            orbit.Script.ApplyNavigation(input);
        }

        Assert.Equal(arcBall.Camera.Target.X, orbit.Orbit.Target.X, Tolerance);
        Assert.Equal(arcBall.Camera.Target.Y, orbit.Orbit.Target.Y, Tolerance);
        Assert.Equal(arcBall.Camera.Target.Z, orbit.Orbit.Target.Z, Tolerance);
        Assert.Equal(arcBall.Camera.Distance, orbit.Orbit.Distance, Tolerance);
        Assert.Equal(arcBall.Camera.Yaw, orbit.Orbit.Yaw, Tolerance);
        Assert.Equal(arcBall.Camera.Pitch, orbit.Orbit.Pitch, Tolerance);
        Assert.Equal(arcBall.Camera.FieldOfView, orbit.Camera.FieldOfView, Tolerance);
        AssertMatrixEqual(arcBall.Camera.ViewMatrix, orbit.Camera.ViewMatrix);

        // The gesture did something: otherwise the equalities above prove nothing.
        bool moved = start != orbit.Camera.ViewMatrix || startFieldOfView != orbit.Camera.FieldOfView;
        Assert.True(moved, $"gesture {gesture} changed nothing");
    }

    [Fact]
    public void TheScript_LeavesAnArcBallAloneWhenItsEntityHasAnOrbit()
    {
        // An entity that carries both: the orbit wins and poses the camera, the arc ball state is not navigated.
        var camera = new ArcBallCameraComponent();
        Size(camera);
        var orbit = new OrbitCameraComponent();
        var entity = new Entity { RootComponent = camera };
        entity.AddComponent(orbit);
        var script = new ScriptArcBallCamera();
        entity.SetGameplayProxy(script);
        entity.Initialize();
        float arcBallYaw = camera.Yaw;
        float orbitYaw = orbit.Yaw;

        foreach (ViewportNavigationInput input in Frames("alt-left-orbit"))
        {
            script.ApplyNavigation(input);
        }

        Assert.Equal(arcBallYaw, camera.Yaw);
        Assert.NotEqual(orbitYaw, orbit.Yaw);
    }

    [Fact]
    public void TheScript_OnAnEntityWithNoCamera_DoesNothing()
    {
        var entity = new Entity { RootComponent = new TransformComponent() };
        var script = new ScriptArcBallCamera();
        entity.SetGameplayProxy(script);
        entity.Initialize();

        script.ApplyNavigation(Frames("wheel")[0]);
    }
}
