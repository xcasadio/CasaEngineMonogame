using System.Linq;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Scene;

/// <summary>
/// The 2d follow behaviour component (ADR-0145): dead zone, limits, missing target, data.
/// </summary>
public sealed class Follow2dCameraComponentTests
{
    private const float CameraZ = 290f;

    private sealed class Rig
    {
        public Rig()
        {
            Camera = new PerspectiveCameraComponent { LocalPosition = new Vector3(0f, 0f, CameraZ) };
            Follow = new Follow2dCameraComponent { DeadZone = new Vector2(100f, 60f) };
            CameraEntity = new Entity { Name = "camera", RootComponent = Camera };
            CameraEntity.AddComponent(Follow);
            TargetRoot = new TransformComponent();
            Target = new Entity { Name = "hero", RootComponent = TargetRoot };
            Follow.Target = Target;
        }

        public PerspectiveCameraComponent Camera { get; }

        public Follow2dCameraComponent Follow { get; }

        public Entity CameraEntity { get; }

        public TransformComponent TargetRoot { get; }

        public Entity Target { get; }

        public Vector3 MoveTargetAndUpdate(float x, float y)
        {
            TargetRoot.LocalPosition = new Vector3(x, y, 0f);
            Follow.Update(1f / 60f);
            return Camera.LocalPosition;
        }
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(50f, 30f)]
    [InlineData(-50f, -30f)]
    [InlineData(49f, -29f)]
    public void ATargetInsideTheDeadZone_LeavesTheCameraStill(float x, float y)
    {
        var rig = new Rig();

        Assert.Equal(new Vector3(0f, 0f, CameraZ), rig.MoveTargetAndUpdate(x, y));
    }

    [Theory]
    [InlineData(80f, 0f, 30f, 0f)]
    [InlineData(-80f, 0f, -30f, 0f)]
    [InlineData(0f, 45f, 0f, 15f)]
    [InlineData(0f, -45f, 0f, -15f)]
    [InlineData(120f, -100f, 70f, -70f)]
    public void ATargetPastAnEdge_PullsTheCameraByTheExcess(float x, float y, float expectedX, float expectedY)
    {
        var rig = new Rig();

        Vector3 position = rig.MoveTargetAndUpdate(x, y);

        Assert.Equal(expectedX, position.X, 1e-4f);
        Assert.Equal(expectedY, position.Y, 1e-4f);
        Assert.Equal(CameraZ, position.Z);
    }

    [Fact]
    public void AfterAPull_TheTargetSitsOnTheEdgeOfTheDeadZone_AndASecondUpdateChangesNothing()
    {
        var rig = new Rig();

        Vector3 first = rig.MoveTargetAndUpdate(300f, 0f);
        rig.Follow.Update(1f / 60f);

        Assert.Equal(250f, first.X, 1e-4f);
        Assert.Equal(first, rig.Camera.LocalPosition);
    }

    [Fact]
    public void TheLimits_KeepTheCameraInsideTheirRectangle()
    {
        var rig = new Rig();
        rig.Follow.Limits = new Rectangle(-100, -50, 300, 120);

        Vector3 farRight = rig.MoveTargetAndUpdate(1000f, 1000f);
        Vector3 farLeft = rig.MoveTargetAndUpdate(-1000f, -1000f);

        Assert.Equal(new Vector3(200f, 70f, CameraZ), farRight);
        Assert.Equal(new Vector3(-100f, -50f, CameraZ), farLeft);
    }

    [Fact]
    public void AZeroWidthOrHeight_LeavesThatAxisFree()
    {
        var rig = new Rig();
        rig.Follow.Limits = new Rectangle(-100, -50, 0, 120);

        Vector3 position = rig.MoveTargetAndUpdate(1000f, 1000f);

        Assert.Equal(950f, position.X, 1e-3f);
        Assert.Equal(70f, position.Y, 1e-4f);
    }

    [Fact]
    public void WithNoTarget_NoCamera_OrADestroyedTarget_NothingMoves()
    {
        var rig = new Rig();
        rig.Follow.Target = null;
        Assert.Equal(new Vector3(0f, 0f, CameraZ), rig.MoveTargetAndUpdate(500f, 500f));

        rig.Follow.Target = rig.Target;
        rig.Target.Destroy();
        Assert.Equal(new Vector3(0f, 0f, CameraZ), rig.MoveTargetAndUpdate(500f, 500f));
        Assert.Null(rig.Follow.Target);

        var cameraless = new Entity { RootComponent = new TransformComponent() };
        var follow = new Follow2dCameraComponent { Target = new Entity { RootComponent = new TransformComponent { LocalPosition = new Vector3(9f, 9f, 0f) } } };
        cameraless.AddComponent(follow);
        follow.Update(1f / 60f);
        Assert.Equal(Vector3.Zero, cameraless.RootComponent.LocalPosition);
    }

    [Fact]
    public void TheTarget_IsFoundByItsNameInTheWorld()
    {
        var rig = new Rig();
        rig.Follow.Target = null;
        rig.Follow.TargetEntityName = "hero";
        var world = new World();
        world.Entities.Add(new Entity { Name = "other", RootComponent = new TransformComponent() });
        world.Entities.Add(rig.Target);

        rig.Follow.ResolveTarget(world);

        Assert.Same(rig.Target, rig.Follow.Target);
        Assert.Equal(30f, rig.MoveTargetAndUpdate(80f, 0f).X, 1e-4f);
    }

    [Fact]
    public void ANameNoEntityBears_LeavesTheTargetEmpty_AndTheCameraStill()
    {
        var rig = new Rig();
        rig.Follow.Target = null;
        rig.Follow.TargetEntityName = "nobody";
        var world = new World();
        world.Entities.Add(rig.Target);

        rig.Follow.ResolveTarget(world);
        rig.Follow.ResolveTarget(world);

        Assert.Null(rig.Follow.Target);
        Assert.Equal(new Vector3(0f, 0f, CameraZ), rig.MoveTargetAndUpdate(500f, 0f));
    }

    [Fact]
    public void TheSettings_RoundTripThroughTheEditorWriter()
    {
        var rig = new Rig();
        rig.Follow.TargetEntityName = "hero";
        rig.Follow.Limits = new Rectangle(-100, -50, 300, 120);
        var node = new JObject();
        EditorEntityJsonSerializer.SaveEntity(rig.CameraEntity, node);
        var followNode = (JObject)node["components"]![0]!;

        Assert.Equal(nameof(Follow2dCameraComponent), (string)followNode["type"]!);
        Assert.Equal("hero", (string)followNode["target_entity_name"]!);
        Assert.Equal(100f, (float)followNode["dead_zone"]!["x"]!);
        Assert.Equal(300, (int)followNode["limits"]!["w"]!);

        var loaded = new Entity();
        loaded.Load(node);
        var follow = Assert.Single(loaded.Components.OfType<Follow2dCameraComponent>());

        Assert.Equal("hero", follow.TargetEntityName);
        Assert.Equal(new Vector2(100f, 60f), follow.DeadZone);
        Assert.Equal(new Rectangle(-100, -50, 300, 120), follow.Limits);
        Assert.Null(follow.Target);
    }

    [Fact]
    public void AComponentSavedWithoutLimits_LoadsWithNone()
    {
        var rig = new Rig();
        var node = new JObject();
        EditorEntityJsonSerializer.SaveEntity(rig.CameraEntity, node);
        ((JObject)node["components"]![0]!).Remove("limits");

        var loaded = new Entity();
        loaded.Load(node);

        Assert.Equal(Rectangle.Empty, loaded.Components.OfType<Follow2dCameraComponent>().Single().Limits);
    }

    [Fact]
    public void Clone_KeepsTheSettings()
    {
        var rig = new Rig();
        rig.Follow.TargetEntityName = "hero";
        rig.Follow.Limits = new Rectangle(1, 2, 3, 4);

        Follow2dCameraComponent clone = rig.Follow.Clone();

        Assert.Equal("hero", clone.TargetEntityName);
        Assert.Equal(rig.Follow.DeadZone, clone.DeadZone);
        Assert.Equal(rig.Follow.Limits, clone.Limits);
        Assert.Same(rig.Target, clone.Target);
    }

    [Fact]
    public void Update_AllocatesNothing()
    {
        var rig = new Rig();
        rig.MoveTargetAndUpdate(10f, 10f);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 200; index++)
        {
            rig.TargetRoot.LocalPosition = new Vector3(index * 3f, -index * 2f, 0f);
            rig.Follow.Update(1f / 60f);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }
}
