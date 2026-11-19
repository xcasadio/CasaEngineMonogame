using System.Linq;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Scene;

/// <summary>
/// The orbit behaviour component (ADR-0148): a posed perspective camera it drives shows what an
/// <see cref="ArcBallCameraComponent"/> of the same values shows.
/// </summary>
public sealed class OrbitCameraComponentTests
{
    private const float Tolerance = 1e-4f;

    private static readonly Vector3[] Targets = [Vector3.Zero, new Vector3(3f, 1.5f, -2f), new Vector3(-10f, 0.5f, 8f)];
    private static readonly float[] Distances = [1f, 5f, 40f];
    private static readonly float[] Pitches = [-89f, -40f, 0f, 35f, 89f];

    private static void Size(CameraComponent camera)
    {
        // Without a world the viewport is empty: the far plane first, since the near plane is clamped on it.
        camera.FarPlane = 1000f;
        camera.NearPlane = 1f;
        camera.OnScreenResized(1920, 1080);
    }

    private static ArcBallCameraComponent CreateArcBall()
    {
        var camera = new ArcBallCameraComponent();
        Size(camera);
        return camera;
    }

    // The formulas of ArcBallCameraComponent, written here so that these proofs do not depend on that class (ADR-0149):
    // it looked along FormulaDirection from Target - Direction * Distance, at Target, with Vector3.Up for up.
    private static Vector3 FormulaDirection(float yaw, float pitch)
    {
        return new Vector3(MathF.Sin(yaw) * MathF.Cos(pitch), MathF.Sin(pitch), -MathF.Cos(yaw) * MathF.Cos(pitch));
    }

    private static Vector3 FormulaRight(float yaw, float pitch)
    {
        Quaternion orientation = Quaternion.CreateFromAxisAngle(Vector3.Up, -yaw) * Quaternion.CreateFromAxisAngle(Vector3.Right, pitch);
        return Vector3.Transform(Vector3.Right, orientation);
    }

    private static Vector3 FormulaPosition(Vector3 target, float distance, float yaw, float pitch)
    {
        return target - FormulaDirection(yaw, pitch) * distance;
    }

    private static Matrix FormulaView(Vector3 target, float distance, float yaw, float pitch)
    {
        return Matrix.CreateLookAt(FormulaPosition(target, distance, yaw, pitch), target, Vector3.Up);
    }

    private static (Entity Entity, PerspectiveCameraComponent Camera, OrbitCameraComponent Orbit) CreateOrbitCamera()
    {
        var camera = new PerspectiveCameraComponent();
        Size(camera);
        var orbit = new OrbitCameraComponent();
        var entity = new Entity { RootComponent = camera };
        entity.AddComponent(orbit);
        return (entity, camera, orbit);
    }

    private static void AssertMatrixEqual(Matrix expected, Matrix actual, float tolerance, string context = "")
    {
        float[] expectedTerms = Matrix.ToFloatArray(expected);
        float[] actualTerms = Matrix.ToFloatArray(actual);
        for (int index = 0; index < expectedTerms.Length; index++)
        {
            Assert.True(MathF.Abs(expectedTerms[index] - actualTerms[index]) <= tolerance,
                $"term {index}: expected {expectedTerms[index]}, actual {actualTerms[index]} {context}");
        }
    }

    private static void AssertVectorEqual(Vector3 expected, Vector3 actual, float tolerance, string context = "")
    {
        Assert.True((expected - actual).Length() <= tolerance, $"expected {expected}, actual {actual} {context}");
    }

    [Fact]
    public void OverAGridOfValues_TheView_EqualsTheArcBallView()
    {
        foreach (Vector3 target in Targets)
        {
            foreach (float distance in Distances)
            {
                for (int yawStep = 0; yawStep < 8; yawStep++)
                {
                    foreach (float pitchDegrees in Pitches)
                    {
                        float yaw = yawStep * MathHelper.TwoPi / 8f;
                        float pitch = MathHelper.ToRadians(pitchDegrees);
                        string context = $"(target {target}, distance {distance}, yaw {yaw}, pitch {pitch})";

                        var arcBall = CreateArcBall();
                        arcBall.Target = target;
                        arcBall.Distance = distance;
                        arcBall.Yaw = yaw;
                        arcBall.Pitch = pitch;

                        var (_, camera, orbit) = CreateOrbitCamera();
                        orbit.Target = target;
                        orbit.Distance = distance;
                        orbit.Yaw = yaw;
                        orbit.Pitch = pitch;

                        AssertMatrixEqual(arcBall.ViewMatrix, camera.ViewMatrix, Tolerance, context);
                        AssertVectorEqual(arcBall.Direction, orbit.Direction, Tolerance, context);
                        AssertVectorEqual(arcBall.Right, orbit.Right, Tolerance, context);
                        AssertVectorEqual(target - orbit.Direction * distance, camera.LocalPosition, Tolerance, context);
                        AssertVectorEqual(arcBall.Position, camera.LocalPosition, Tolerance, context);
                        AssertMatrixEqual(FormulaView(target, distance, yaw, pitch), camera.ViewMatrix, Tolerance, context);
                        AssertVectorEqual(FormulaDirection(yaw, pitch), orbit.Direction, Tolerance, context);
                        AssertVectorEqual(FormulaRight(yaw, pitch), orbit.Right, Tolerance, context);
                        AssertVectorEqual(FormulaPosition(target, distance, yaw, pitch), camera.LocalPosition, Tolerance, context);
                    }
                }
            }
        }
    }

    [Fact]
    public void WithANegativeDistance_TheCameraStillLooksAtTheTarget_LikeTheArcBall()
    {
        foreach (Vector3 target in Targets)
        {
            foreach (float distance in new[] { -1f, -5f, -40f })
            {
                for (int yawStep = 0; yawStep < 8; yawStep++)
                {
                    foreach (float pitchDegrees in Pitches)
                    {
                        float yaw = yawStep * MathHelper.TwoPi / 8f;
                        float pitch = MathHelper.ToRadians(pitchDegrees);
                        string context = $"(target {target}, distance {distance}, yaw {yaw}, pitch {pitch})";

                        var arcBall = CreateArcBall();
                        arcBall.Target = target;
                        arcBall.Distance = distance;
                        arcBall.Yaw = yaw;
                        arcBall.Pitch = pitch;

                        var (_, camera, orbit) = CreateOrbitCamera();
                        orbit.Target = target;
                        orbit.Distance = distance;
                        orbit.Yaw = yaw;
                        orbit.Pitch = pitch;

                        AssertMatrixEqual(arcBall.ViewMatrix, camera.ViewMatrix, Tolerance, context);
                        AssertVectorEqual(arcBall.Direction, orbit.Direction, Tolerance, context);
                        AssertVectorEqual(arcBall.Right, orbit.Right, Tolerance, context);
                        AssertVectorEqual(arcBall.Position, camera.LocalPosition, Tolerance, context);
                        AssertMatrixEqual(FormulaView(target, distance, yaw, pitch), camera.ViewMatrix, Tolerance, context);
                        AssertVectorEqual(FormulaDirection(yaw, pitch), orbit.Direction, Tolerance, context);
                        AssertVectorEqual(FormulaRight(yaw, pitch), orbit.Right, Tolerance, context);
                        AssertVectorEqual(FormulaPosition(target, distance, yaw, pitch), camera.LocalPosition, Tolerance, context);

                        // The camera faces the target: the target is straight ahead, on the -Z axis of the view.
                        Vector3 seen = Vector3.Transform(target, camera.ViewMatrix);
                        Assert.True(MathF.Abs(seen.X) <= 1e-3f && MathF.Abs(seen.Y) <= 1e-3f && seen.Z < 0f, $"target seen at {seen} {context}");
                    }
                }
            }
        }
    }

    [Fact]
    public void TheValuesADemoStartsWith_ANegativeDistanceIncluded_GiveTheArcBallView()
    {
        // The start values of the world-space UI demo: the camera stands in front of the screen, at (0, 7, 18).
        var target = new Vector3(0f, 4f, 0f);
        var arcBall = CreateArcBall();
        arcBall.Target = target;
        arcBall.Distance = -18.25f;
        arcBall.Yaw = MathHelper.Pi;
        arcBall.Pitch = 0.165f;
        var (_, camera, orbit) = CreateOrbitCamera();

        orbit.Target = target;
        orbit.Distance = -18.25f;
        orbit.Yaw = MathHelper.Pi;
        orbit.Pitch = 0.165f;

        AssertMatrixEqual(arcBall.ViewMatrix, camera.ViewMatrix, Tolerance);
        AssertMatrixEqual(FormulaView(target, -18.25f, MathHelper.Pi, 0.165f), camera.ViewMatrix, Tolerance);
        AssertVectorEqual(new Vector3(0f, 7f, 18f), camera.LocalPosition, 0.01f);
    }

    [Fact]
    public void ThePositiveDistanceOfTheWorldSpaceUiDemo_GivesTheViewOfItsFormerNegativeDistance()
    {
        // The demo started at a distance of -18.25 with a yaw of pi and a pitch of 0.165: the navigation, which
        // ignores the sign of the distance, then ran mirrored and the first wheel notch turned the camera around.
        // It now starts from the same pose written with a positive distance.
        var target = new Vector3(0f, 4f, 0f);
        var arcBall = CreateArcBall();
        arcBall.Target = target;
        arcBall.Distance = -18.25f;
        arcBall.Yaw = MathHelper.Pi;
        arcBall.Pitch = 0.165f;
        var (_, camera, orbit) = CreateOrbitCamera();

        orbit.Target = target;
        orbit.Distance = 18.25f;
        orbit.Yaw = 0f;
        orbit.Pitch = -0.165f;

        AssertMatrixEqual(arcBall.ViewMatrix, camera.ViewMatrix, Tolerance);
        AssertMatrixEqual(FormulaView(target, -18.25f, MathHelper.Pi, 0.165f), camera.ViewMatrix, Tolerance);
        // The view direction of the navigation is now the one of the camera.
        AssertVectorEqual(Vector3.Normalize(target - camera.LocalPosition), orbit.Direction, Tolerance);
    }

    public static TheoryData<float, float, float, float, float, float> CameraPlacements => new()
    {
        // The placements of ArcBallCameraComponentTests.
        { 0f, 12f, 15f, 0f, 0f, 0f },
        { 0f, 4.5f, 15.5f, 0f, 1.2f, 0f },
        { 34.686f, 42.36f, 50.701f, 0f, 8.8f, 0f },
        { -10f, 8f, 18f, -10f, 1.4f, 0f },
        { 6f, 3f, -12f, 0f, 0f, 0f },
    };

    [Theory]
    [MemberData(nameof(CameraPlacements))]
    public void SetCamera_GivesTheArcBallValues_AndALookAtView(
        float positionX, float positionY, float positionZ,
        float targetX, float targetY, float targetZ)
    {
        var position = new Vector3(positionX, positionY, positionZ);
        var target = new Vector3(targetX, targetY, targetZ);
        var arcBall = CreateArcBall();
        arcBall.SetCamera(position, target, Vector3.Up);
        var (_, camera, orbit) = CreateOrbitCamera();

        orbit.SetCamera(position, target);

        Assert.Equal(arcBall.Target, orbit.Target);
        Assert.Equal(arcBall.Distance, orbit.Distance, Tolerance);
        Assert.Equal(arcBall.Yaw, orbit.Yaw, Tolerance);
        Assert.Equal(arcBall.Pitch, orbit.Pitch, Tolerance);
        AssertMatrixEqual(Matrix.CreateLookAt(position, target, Vector3.Up), camera.ViewMatrix, Tolerance);
        AssertMatrixEqual(arcBall.ViewMatrix, camera.ViewMatrix, Tolerance);

        // What ArcBallCameraComponent.SetCamera deduced from the two points.
        float expectedDistance = (position - target).Length();
        Vector3 backward = (position - target) / expectedDistance;
        Assert.Equal(target, orbit.Target);
        Assert.Equal(expectedDistance, orbit.Distance, Tolerance);
        Assert.Equal(MathF.Atan2(-backward.X, backward.Z), orbit.Yaw, Tolerance);
        Assert.Equal(-MathF.Asin(backward.Y), orbit.Pitch, Tolerance);
    }

    [Fact]
    public void SetCamera_WithThePositionOnTheTarget_KeepsTheYawAndThePitch()
    {
        var arcBall = CreateArcBall();
        arcBall.Yaw = 1.1f;
        arcBall.Pitch = -0.4f;
        arcBall.SetCamera(new Vector3(2f, 3f, 4f), new Vector3(2f, 3f, 4f), Vector3.Up);
        var (_, _, orbit) = CreateOrbitCamera();
        orbit.Yaw = 1.1f;
        orbit.Pitch = -0.4f;

        orbit.SetCamera(new Vector3(2f, 3f, 4f), new Vector3(2f, 3f, 4f));

        Assert.Equal(arcBall.Yaw, orbit.Yaw);
        Assert.Equal(arcBall.Pitch, orbit.Pitch);
        Assert.Equal(arcBall.Distance, orbit.Distance);
        Assert.Equal(1.1f, orbit.Yaw);
        Assert.Equal(-0.4f, orbit.Pitch);
        Assert.Equal(0f, orbit.Distance);
        Assert.Equal(new Vector3(2f, 3f, 4f), orbit.Target);
    }

    [Fact]
    public void AWriteOfEachValue_IsSeenAtTheNextReadOfTheView_WithoutUpdate()
    {
        var (_, camera, orbit) = CreateOrbitCamera();
        var views = new List<Matrix> { camera.ViewMatrix };

        orbit.Target = new Vector3(1f, 2f, 3f);
        views.Add(camera.ViewMatrix);
        orbit.Distance = 9f;
        views.Add(camera.ViewMatrix);
        orbit.Yaw = 0.7f;
        views.Add(camera.ViewMatrix);
        orbit.Pitch = -0.3f;
        views.Add(camera.ViewMatrix);

        Assert.Equal(views.Count, views.Distinct().Count());
    }

    private static JObject SavedOrbitEntity(bool cameraIsRoot)
    {
        var camera = new PerspectiveCameraComponent();
        Size(camera);
        var entity = new Entity();
        if (cameraIsRoot)
        {
            entity.RootComponent = camera;
        }
        else
        {
            var root = new TransformComponent();
            entity.RootComponent = root;
            root.AddChildComponent(camera);
        }

        var orbit = new OrbitCameraComponent { Target = new Vector3(4f, 1f, -3f), Distance = 12f, Yaw = 2.2f, Pitch = -0.6f };
        entity.AddComponent(orbit);

        var node = new JObject();
        EditorEntityJsonSerializer.SaveEntity(entity, node);
        return node;
    }

    private static JObject FindCameraNode(JObject entityNode, bool cameraIsRoot)
    {
        var root = (JObject)entityNode["root_component"]!;
        return cameraIsRoot ? root : (JObject)root["children_component"]![0]!;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnEntityLoadedFromData_IsPosedAtItsInitialisation_WithNoPropertyWrite(bool cameraIsRoot)
    {
        JObject node = SavedOrbitEntity(cameraIsRoot);
        // The saved camera transform is replaced by another one: only the orbit can put the pose back.
        JObject cameraNode = FindCameraNode(node, cameraIsRoot);
        string transformKey = cameraNode.ContainsKey("coordinates") ? "coordinates" : "local_transform";
        cameraNode[transformKey]!["position"] = new JObject { ["x"] = 100f, ["y"] = 200f, ["z"] = 300f };
        cameraNode[transformKey]!["rotation"] = new JObject { ["x"] = 0f, ["y"] = 0f, ["z"] = 0f, ["w"] = 1f };

        var arcBall = CreateArcBall();
        arcBall.Target = new Vector3(4f, 1f, -3f);
        arcBall.Distance = 12f;
        arcBall.Yaw = 2.2f;
        arcBall.Pitch = -0.6f;

        var loaded = new Entity();
        loaded.Load(node);
        var camera = loaded.GetComponent<PerspectiveCameraComponent>();
        Assert.Equal(new Vector3(100f, 200f, 300f), camera.LocalPosition);

        loaded.Initialize();

        AssertMatrixEqual(arcBall.ViewMatrix, camera.ViewMatrix, Tolerance);
        AssertMatrixEqual(FormulaView(new Vector3(4f, 1f, -3f), 12f, 2.2f, -0.6f), camera.ViewMatrix, Tolerance);
    }

    [Fact]
    public void AnOrbitAddedToAnInitialisedEntity_PosesTheCamera()
    {
        var camera = new PerspectiveCameraComponent();
        Size(camera);
        var entity = new Entity { RootComponent = camera };
        entity.Initialize();
        var orbit = new OrbitCameraComponent();
        var detached = new OrbitCameraComponent();
        var expected = Vector3.Zero - detached.Direction * detached.Distance;

        entity.AddComponent(orbit);

        AssertVectorEqual(expected, camera.LocalPosition, Tolerance);
        Assert.NotEqual(Vector3.Zero, camera.LocalPosition);
    }

    [Fact]
    public void AClonedEntity_HasAnOrbitThatPosesItsOwnCamera()
    {
        var (entity, camera, orbit) = CreateOrbitCamera();
        orbit.SetCamera(new Vector3(0f, 12f, 15f), Vector3.Zero);
        Vector3 originalPosition = camera.LocalPosition;

        Entity clone = entity.Clone();
        var cloneCamera = clone.GetComponent<PerspectiveCameraComponent>();
        var cloneOrbit = clone.GetComponent<OrbitCameraComponent>();
        cloneOrbit.Target = new Vector3(50f, 0f, 0f);

        Assert.NotSame(camera, cloneCamera);
        Assert.NotSame(orbit, cloneOrbit);
        Assert.Equal(originalPosition, camera.LocalPosition);
        AssertVectorEqual(originalPosition + new Vector3(50f, 0f, 0f), cloneCamera.LocalPosition, Tolerance);
        Assert.Equal(orbit.Distance, cloneOrbit.Distance);
        Assert.Equal(orbit.Yaw, cloneOrbit.Yaw);
        Assert.Equal(orbit.Pitch, cloneOrbit.Pitch);
    }

    [Fact]
    public void APoseWrittenDirectlyOnTheCamera_IsOverwrittenAtTheNextOrbitWrite()
    {
        var (_, camera, orbit) = CreateOrbitCamera();
        orbit.SetCamera(new Vector3(0f, 12f, 15f), Vector3.Zero);
        Matrix framed = camera.ViewMatrix;

        camera.SetPositionAndTarget(new Vector3(30f, 2f, 1f), new Vector3(5f, 5f, 5f));
        Assert.NotEqual(framed, camera.ViewMatrix);
        orbit.Distance = orbit.Distance;

        AssertMatrixEqual(framed, camera.ViewMatrix, Tolerance);
    }

    [Fact]
    public void AnOrbitOnAnEntityWithNoCamera_DoesNothing()
    {
        var root = new TransformComponent();
        var entity = new Entity { RootComponent = root };
        var orbit = new OrbitCameraComponent();
        entity.AddComponent(orbit);

        orbit.Target = new Vector3(1f, 2f, 3f);
        orbit.SetCamera(new Vector3(5f, 5f, 5f), Vector3.Zero);
        entity.Initialize();

        Assert.Equal(Vector3.Zero, root.LocalPosition);
        Assert.Equal(Quaternion.Identity, root.LocalOrientation);

        var alone = new OrbitCameraComponent { Yaw = 0.5f };
        Assert.Equal(0.5f, alone.Yaw);
    }

    [Fact]
    public void TheOrbit_RoundTripsThroughTheEditorWriter_UnderTheArcBallKeys()
    {
        JObject node = SavedOrbitEntity(cameraIsRoot: true);
        var orbitNode = (JObject)node["components"]![0]!;

        Assert.Equal(nameof(OrbitCameraComponent), (string)orbitNode["type"]!);
        Assert.Equal(12f, (float)orbitNode["distance"]!);
        Assert.Equal(2.2f, (float)orbitNode["yaw"]!);
        Assert.Equal(-0.6f, (float)orbitNode["pitch"]!);
        Assert.Equal(4f, (float)orbitNode["target"]!["x"]!);

        var loaded = new Entity();
        loaded.Load(node);
        var orbit = Assert.Single(loaded.Components.OfType<OrbitCameraComponent>());

        Assert.Equal(new Vector3(4f, 1f, -3f), orbit.Target);
        Assert.Equal(12f, orbit.Distance);
        Assert.Equal(2.2f, orbit.Yaw);
        Assert.Equal(-0.6f, orbit.Pitch);
    }

    [Fact]
    public void WritingTheValues_AllocatesNothing()
    {
        var (_, camera, orbit) = CreateOrbitCamera();
        // Warm up: the first calls compile the generic lookup.
        orbit.Yaw = 0.1f;
        orbit.SetCamera(new Vector3(1f, 2f, 3f), Vector3.Zero);
        _ = camera.ViewMatrix;

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 100; index++)
        {
            orbit.Target = new Vector3(index, 0f, 0f);
            orbit.Distance = 5f + index;
            orbit.Yaw = index * 0.01f;
            orbit.Pitch = index * 0.005f;
            orbit.SetCamera(new Vector3(index, 4f, 9f), Vector3.Zero);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }
}
