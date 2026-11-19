using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Editor.Runtime;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Editor;

/// <summary>
/// The pose of the 3d viewport camera of the editor, built by <see cref="EditorViewportCameraController.CreateCameraEntity"/>,
/// the method the viewport panel calls. The expected views and positions were recorded on 2026-11-19 with the
/// arc ball camera the viewport used before ADR-0148 (a class since removed, ADR-0149), through the same construction and the same
/// re-initialization as a change of world: the camera driven by an orbit component must show the same thing.
/// </summary>
public sealed class EditorViewportCameraPoseTests
{
    private const float Tolerance = 1e-4f;

    // A world over a game built without its constructor, in an editor policy: the screen size comes from two fields,
    // so that a camera can join the world with no graphics device.
    private static World CreateWorld(int width, int height)
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        game.ExecutionPolicy = GameplayExecutionPolicies.EditorPreview;
        typeof(CasaEngineGame).GetField("_screenSizeWidth", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, width);
        typeof(CasaEngineGame).GetField("_screenSizeHeight", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, height);
        var world = new World { Name = "EditorViewportCameraPoseTests" };
        typeof(World).GetProperty(nameof(World.Game), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(world, game);
        return world;
    }

    private static readonly (float Yaw, float Pitch, float Distance, Vector3 Target)[] States =
    [
        (0.3f, -0.2f, 10f, new Vector3(1f, 2f, 3f)),
        (2.5f, 0.6f, 4f, new Vector3(-5f, 0.5f, 2f)),
        (-1.2f, -1.1f, 250f, new Vector3(0f, 0f, 0f)),
        (4.0f, 0.05f, 35f, new Vector3(12f, -3f, -40f)),
    ];

    private static readonly float[] InitialView =
        [0.70710677f, 0.35355347f, -0.6123725f, 0f, -2.9802322E-08f, 0.86602557f, 0.5f, 0f, 0.7071069f, -0.35355335f, 0.61237246f, 0f, -4.7683716E-07f, -0f, -10.000001f, 1f];

    private static readonly Vector3 InitialPosition = new(-6.123725f, 5f, 6.1237245f);

    private static readonly float[][] Views =
    [
        [0.9553365f, 0.058710806f, -0.2896295f, 0f, -3.7252903E-09f, 0.9800666f, 0.19866933f, 0f, 0.29552022f, -0.18979606f, 0.93629336f, 0f, -1.841897f, -1.449456f, -12.91659f, 1f],
        [-0.8011435f, -0.33792284f, -0.4939404f, 0f, -0f, 0.8253356f, -0.56464255f, 0f, 0.5984722f, -0.45235974f, -0.6612124f, 0f, -5.202662f, -1.1975625f, -4.8649564f, 1f],
        [0.36235774f, -0.83064014f, 0.42276922f, 0f, 4.470349E-08f, 0.45359606f, 0.89120734f, 0f, -0.93203914f, -0.32293585f, 0.16436407f, 0f, -0f, 2.861023E-06f, -249.99998f, 1f],
        [-0.6536436f, 0.037824366f, 0.7558567f, 0f, -1.8626451E-09f, 0.99875027f, -0.049979176f, 0f, -0.7568025f, -0.03266857f, -0.6528267f, 0f, -22.428375f, 1.235616f, -70.33329f, 1f],
    ];

    private static readonly Vector3[] Positions =
    [
        new(-1.8962951f, 3.9866934f, 12.362934f),
        new(-6.9757614f, -1.75857f, -0.6448493f),
        new(105.692314f, 222.80185f, 41.09102f),
        new(38.454987f, -4.7492714f, -62.848938f),
    ];

    private static void AssertView(float[] expected, Matrix actual, string context)
    {
        float[] actualTerms = Matrix.ToFloatArray(actual);
        for (int index = 0; index < expected.Length; index++)
        {
            // The translation terms grow with the distance: the tolerance is relative above one unit.
            float tolerance = Tolerance * MathF.Max(1f, MathF.Abs(expected[index]));
            Assert.True(MathF.Abs(expected[index] - actualTerms[index]) <= tolerance,
                $"{context}, term {index}: expected {expected[index]}, actual {actualTerms[index]}");
        }
    }

    private static void AssertPosition(Vector3 expected, Vector3 actual, string context)
    {
        float tolerance = Tolerance * MathF.Max(1f, expected.Length());
        Assert.True((expected - actual).Length() <= tolerance, $"{context}: expected {expected}, actual {actual}");
    }

    [Fact]
    public void TheViewCamera_ShowsWhatTheArcBallCameraShowed_ThroughAChangeOfWorld()
    {
        var controller = new EditorViewportCameraController();
        Entity entity = controller.CreateCameraEntity("EditorViewportCamera", CreateWorld(1280, 720),
            out PerspectiveCameraComponent camera, out OrbitCameraComponent orbit);

        AssertView(InitialView, camera.ViewMatrix, "initial");
        AssertPosition(InitialPosition, camera.Position, "initial");

        for (int index = 0; index < States.Length; index++)
        {
            var state = States[index];
            controller.SetState(state.Yaw, state.Pitch, state.Distance, state.Target);
            controller.ApplyTo(orbit);

            if (index == 2)
            {
                // The viewport panel re-initializes the camera entity when the rendered world changes.
                EditorCameraFactory.InitializeWithWorld(entity, camera, CreateWorld(800, 600));
            }

            AssertView(Views[index], camera.ViewMatrix, $"state {index}");
            AssertPosition(Positions[index], camera.Position, $"state {index}");
        }

        Assert.Equal(float.PositiveInfinity, camera.FarPlane);
        Assert.Equal(800, camera.Viewport.Width);
        Assert.Equal(600, camera.Viewport.Height);
    }

    [Fact]
    public void TheConstruction_PutsTheOrbitOnTheEntityOfTheCamera()
    {
        var controller = new EditorViewportCameraController();
        controller.SetState(1f, -0.5f, 20f, new Vector3(3f, 0f, 0f));

        Entity entity = controller.CreateCameraEntity("EditorViewportCamera", CreateWorld(1280, 720),
            out PerspectiveCameraComponent camera, out OrbitCameraComponent orbit);

        Assert.Same(entity, camera.Owner);
        Assert.Same(entity, orbit.Owner);
        Assert.Equal(20f, orbit.Distance);
        Assert.Equal(new Vector3(3f, 0f, 0f), orbit.Target);
        AssertPosition(orbit.Target - orbit.Direction * orbit.Distance, camera.Position, "construction");
    }

    [Fact]
    public void TheViewCamera_KeepsAnInfiniteFarPlane_WithTheProjectionOfThe3dCameras()
    {
        var controller = new EditorViewportCameraController();
        controller.CreateCameraEntity("EditorViewportCamera", CreateWorld(1280, 720),
            out PerspectiveCameraComponent camera, out _);

        Assert.Equal(float.PositiveInfinity, camera.FarPlane);
        Assert.Equal(1f, camera.NearPlane);
        Assert.Equal(-1f, camera.ProjectionMatrix.M33);
        Assert.Equal(-1f, camera.ProjectionMatrix.M43);
    }
}
