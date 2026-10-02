using System.Reflection;
using CasaEngine.Core.Time;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Physics;

/// <summary>
/// ADR-0047: the field stage of <see cref="CharacterControllerComponent"/> stops at the dynamic obstacles of a
/// <see cref="World.MovementObstacleProbe"/> with the exact contact of ADR-0045, and the contact report says which
/// obstacle shortened each axis (T-ENG-1 to T-ENG-12 of <c>ai-agent/tasks/field-movement-obstacles-tasks.md</c>).
/// The mover is a 20 x 14 x 32 box (half extents 10 x 7) on a flat field of 72 x 72 cells of 16 px, Z up
/// (h1 = X, h2 = Y); the test probe uses semi-open boxes [min ; max) on X, Y and Z.
/// </summary>
public class CharacterControllerMovementObstacleTests
{
    private const float HalfX = 10f;
    private const float HalfY = 7f;
    private const float Height = 32f;
    private const int FieldCells = 72;

    // The obstacle of T-ENG-1 and the pawn column that meets it.
    private static readonly Vector3 ObstacleMin = new(949.5f, 497f, 48f);
    private static readonly Vector3 ObstacleMax = new(969.5f, 511f, 80f);

    // T-ENG-1

    [Fact]
    public void Move_StopsExactlyAtTheContactOfAnObstacle_AndReportsIt()
    {
        var probe = new BoxProbe();
        var obstacle = probe.Add(ObstacleMin, ObstacleMax);
        var rig = CreateRig(new Vector3(968f, 552f, 48f), CreateField(48f), probe);

        rig.Controller.Move(new Vector3(0f, -0.5f, 0f));
        Assert.Equal(551.5f, rig.Root.Y);
        Assert.Equal(1, probe.Calls);

        for (var step = 2; step <= 34; step++)
        {
            probe.Calls = 0;
            rig.Controller.Move(new Vector3(0f, -1f, 0f));
            Assert.Equal(551.5f - (step - 1), rig.Root.Y);
            Assert.Equal(1, probe.Calls);
            Assert.Null(rig.Controller.LastContact.H2Obstacle);
        }

        Assert.Equal(518.5f, rig.Root.Y);

        // Step 35: the contact.
        probe.Calls = 0;
        rig.Controller.Move(new Vector3(0f, -1f, 0f));

        Assert.Equal(518.0f, rig.Root.Y);
        Assert.Equal(-0.5f, rig.Controller.LastContact.ActualH2Amount);
        Assert.True(rig.Controller.LastContact.H2Curtailed);
        Assert.False(rig.Controller.LastContact.H1Curtailed);
        Assert.Same(obstacle, rig.Controller.LastContact.H2Obstacle);
        Assert.Null(rig.Controller.LastContact.H1Obstacle);
        Assert.InRange(probe.Calls, 26, 30);
        Assert.Equal(27, probe.Calls);

        // Step 36: pushing the established contact.
        probe.Calls = 0;
        rig.Controller.Move(new Vector3(0f, -1f, 0f));

        Assert.Equal(518.0f, rig.Root.Y);
        Assert.Equal(0f, rig.Controller.LastContact.ActualH2Amount);
        Assert.Same(obstacle, rig.Controller.LastContact.H2Obstacle);
        Assert.Equal(2, probe.Calls);

        // The reached position is free and the next float toward the obstacle is blocked.
        Assert.False(probe.TryFindObstacle(rig.Pawn, new Vector3(968f, 518.0f, 48f), out _));
        Assert.True(probe.TryFindObstacle(rig.Pawn, new Vector3(968f, MathF.BitDecrement(518.0f), 48f), out _));
    }

    // T-ENG-2

    [Fact]
    public void Move_AnObstacleFlushWithTheMoverIsNotBlocking()
    {
        var probe = new BoxProbe();
        probe.Add(new Vector3(938f, 497f, 48f), new Vector3(958f, 511f, 80f));
        var rig = CreateRig(new Vector3(968f, 551.5f, 48f), CreateField(48f), probe);

        for (var i = 0; i < 80; i++)
        {
            rig.Controller.Move(new Vector3(0f, -1f, 0f));
            Assert.Null(rig.Controller.LastContact.H2Obstacle);
        }

        Assert.Equal(471.5f, rig.Root.Y);
        Assert.Equal(80, probe.Calls);
    }

    // T-ENG-3

    [Theory]
    [InlineData(240f, 255.5f, 247f)]
    [InlineData(240f, 255.75f, 247f)]
    [InlineData(240f, 255.9995f, 247f)]
    [InlineData(1008f, 1023.5f, 1015f)]
    [InlineData(1008f, 1023.75f, 1015f)]
    [InlineData(1008f, 1023.9995f, 1015f)]
    public void Move_Northward_IsExactAtPowerOfTwoWindows(float top, float start, float expected)
    {
        var probe = new BoxProbe();
        probe.Add(new Vector3(0f, top - 40f, 0f), new Vector3(5000f, top, 1000f));
        var rig = CreateRig(new Vector3(60f, start, 0f), CreateField(0f), probe);

        rig.Controller.Move(new Vector3(0f, -10f, 0f));

        Assert.Equal(expected, rig.Root.Y);
        Assert.False(probe.TryFindObstacle(rig.Pawn, new Vector3(60f, rig.Root.Y, 0f), out _));
        Assert.True(probe.TryFindObstacle(rig.Pawn, new Vector3(60f, MathF.BitDecrement(rig.Root.Y), 0f), out _));
    }

    [Theory]
    [InlineData(240f, 255.5f, 250f)]
    [InlineData(240f, 255.9995f, 250f)]
    [InlineData(1008f, 1023.5f, 1018f)]
    [InlineData(1008f, 1023.9995f, 1018f)]
    public void Move_Westward_IsExactAtPowerOfTwoWindows(float edge, float start, float expected)
    {
        var probe = new BoxProbe();
        probe.Add(new Vector3(edge - 40f, 0f, 0f), new Vector3(edge, 5000f, 1000f));
        var rig = CreateRig(new Vector3(start, 24f, 0f), CreateField(0f), probe);

        rig.Controller.Move(new Vector3(-10f, 0f, 0f));

        Assert.Equal(expected, rig.Root.X);
        Assert.False(probe.TryFindObstacle(rig.Pawn, new Vector3(rig.Root.X, 24f, 0f), out _));
        Assert.True(probe.TryFindObstacle(rig.Pawn, new Vector3(MathF.BitDecrement(rig.Root.X), 24f, 0f), out _));
    }

    // T-ENG-4

    [Fact]
    public void Move_StartingInsideAnObstacle_LeavesOnlyByAStepThatQuitsTheOverlap()
    {
        var probe = new BoxProbe();
        var obstacle = probe.Add(ObstacleMin, ObstacleMax);
        var rig = CreateRig(new Vector3(968f, 515f, 48f), CreateField(48f), probe);

        foreach (var amount in new[] { 1f, 2f })
        {
            probe.Calls = 0;
            rig.Controller.Move(new Vector3(0f, amount, 0f));

            Assert.Equal(515f, rig.Root.Y);
            Assert.Equal(0f, rig.Controller.LastContact.ActualH2Amount);
            Assert.Same(obstacle, rig.Controller.LastContact.H2Obstacle);
            Assert.Equal(2, probe.Calls);
        }

        rig.Controller.Move(new Vector3(0f, 3f, 0f));
        Assert.Equal(518f, rig.Root.Y);
        Assert.Equal(3f, rig.Controller.LastContact.ActualH2Amount);
        Assert.Null(rig.Controller.LastContact.H2Obstacle);

        rig.Pawn.RootComponent!.Position = new Vector3(968f, 515f, 48f);
        rig.Controller.Move(new Vector3(0f, 4f, 0f));
        Assert.Equal(519f, rig.Root.Y);

        rig.Pawn.RootComponent!.Position = new Vector3(968f, 515f, 48f);
        rig.Controller.Move(new Vector3(0f, -1f, 0f));
        Assert.Equal(515f, rig.Root.Y);
        Assert.NotNull(rig.Controller.LastContact.H2Obstacle);
    }

    // T-ENG-5

    [Fact]
    public void Move_Diagonal_FirstAxisBlockedByAnObstacle()
    {
        var probe = new BoxProbe();
        var obstacle = probe.Add(ObstacleMin, ObstacleMax);
        var rig = CreateRig(new Vector3(930f, 504f, 48f), CreateField(48f), probe);

        rig.Controller.Move(new Vector3(25f, -1f, 0f));

        Assert.Equal(939.5f, rig.Root.X);
        Assert.Equal(503f, rig.Root.Y);
        var contact = rig.Controller.LastContact;
        Assert.Equal(9.5f, contact.ActualH1Amount);
        Assert.Equal(-1f, contact.ActualH2Amount);
        Assert.True(contact.H1Curtailed);
        Assert.Same(obstacle, contact.H1Obstacle);
        Assert.False(contact.H2Curtailed);
        Assert.Null(contact.H2Obstacle);
        Assert.Equal(28, probe.Calls);
    }

    [Fact]
    public void Move_Diagonal_SecondAxisBlockedByAnObstacle()
    {
        var probe = new BoxProbe();
        var obstacle = probe.Add(ObstacleMin, ObstacleMax);
        var rig = CreateRig(new Vector3(945f, 520f, 48f), CreateField(48f), probe);

        rig.Controller.Move(new Vector3(2f, -5f, 0f));

        Assert.Equal(947f, rig.Root.X);
        Assert.Equal(518f, rig.Root.Y);
        var contact = rig.Controller.LastContact;
        Assert.Equal(2f, contact.ActualH1Amount);
        Assert.Equal(-2f, contact.ActualH2Amount);
        Assert.False(contact.H1Curtailed);
        Assert.Null(contact.H1Obstacle);
        Assert.True(contact.H2Curtailed);
        Assert.Same(obstacle, contact.H2Obstacle);
        Assert.Equal(28, probe.Calls);
    }

    [Fact]
    public void Move_TheFieldBlocksLast_NoObstacleIsReported()
    {
        // A field wall on the row [224 ; 240) and an obstacle farther on: the contact is the wall's.
        var probe = new BoxProbe();
        probe.Add(new Vector3(0f, 192f, 0f), new Vector3(5000f, 216f, 1000f));
        var rig = CreateRig(new Vector3(60f, 260f, 0f), CreateField(0f, wallRow: 14), probe);

        rig.Controller.Move(new Vector3(0f, -60f, 0f));

        Assert.Equal(247f, rig.Root.Y);
        var contact = rig.Controller.LastContact;
        Assert.Equal(-13f, contact.ActualH2Amount);
        Assert.True(contact.H2Curtailed);
        Assert.Null(contact.H2Obstacle);
    }

    [Fact]
    public void Move_TheProbeIsAskedBeforeTheField_AndBlocksLast()
    {
        var probe = new BoxProbe();
        var obstacle = probe.Add(new Vector3(0f, 232f, 0f), new Vector3(5000f, 244f, 1000f));
        var rig = CreateRig(new Vector3(60f, 260f, 0f), CreateField(0f, wallRow: 14), probe);

        rig.Controller.Move(new Vector3(0f, -20f, 0f));

        Assert.Equal(251f, rig.Root.Y);
        Assert.True(rig.Controller.LastContact.H2Curtailed);
        Assert.Same(obstacle, rig.Controller.LastContact.H2Obstacle);
    }

    [Fact]
    public void Move_OnlyTheArrivalPointIsTested_ASlimObstacleIsCrossed()
    {
        var probe = new BoxProbe();
        probe.Add(new Vector3(0f, 232f, 0f), new Vector3(5000f, 244f, 1000f));
        var rig = CreateRig(new Vector3(60f, 260f, 0f), CreateField(0f), probe);

        rig.Controller.Move(new Vector3(0f, -60f, 0f));

        Assert.Equal(200f, rig.Root.Y);
        Assert.False(rig.Controller.LastContact.H1Curtailed);
        Assert.False(rig.Controller.LastContact.H2Curtailed);
        Assert.Null(rig.Controller.LastContact.H2Obstacle);
    }

    // T-ENG-6

    [Fact]
    public void Move_ProbeCallBudget()
    {
        var probe = new BoxProbe();
        probe.Add(ObstacleMin, ObstacleMax);

        // A free axis: exactly one call.
        var rig = CreateRig(new Vector3(500f, 500f, 48f), CreateField(48f), probe);
        rig.Controller.Move(new Vector3(0f, -3f, 0f));
        Assert.Equal(1, probe.Calls);

        // Free x and y: two calls; the probe gets the mover and the candidate root, X advanced for h2, Z unchanged.
        probe.Calls = 0;
        probe.RecordedCount = 0;
        rig.Pawn.RootComponent!.Position = new Vector3(500f, 500f, 48f);
        rig.Controller.Move(new Vector3(3f, -3f, 0f));
        Assert.Equal(2, probe.Calls);
        Assert.Same(rig.Pawn, probe.LastMover);
        Assert.Equal(503f, probe.RecordedX[1]);
        Assert.Equal(497f, probe.RecordedY[1]);
        Assert.Equal(48f, probe.LastCandidate.Z);

        // The tick that establishes the contact.
        probe.Calls = 0;
        rig.Pawn.RootComponent!.Position = new Vector3(968f, 519f, 48f);
        rig.Controller.Move(new Vector3(0f, -3f, 0f));
        Assert.InRange(probe.Calls, 26, 30);

        // Pushing the established contact.
        probe.Calls = 0;
        rig.Controller.Move(new Vector3(0f, -3f, 0f));
        Assert.Equal(2, probe.Calls);
    }

    // T-ENG-7

    [Fact]
    public void Move_WithANeverBlockingProbe_IsBitIdenticalToNoProbe()
    {
        var start = new Vector3(620f, 400f, 48f);
        var withoutProbe = CreateRig(start, CreateField(48f, wallColumn: 44), null);
        var withProbe = CreateRig(start, CreateField(48f, wallColumn: 44), new BoxProbe());
        var random = new Random(1234);
        var curtailedSteps = 0;

        for (var i = 0; i < 100; i++)
        {
            var step = new Vector3(
                (float)(random.NextDouble() * 4.0 - 1.0),
                (float)(random.NextDouble() * 6.0 - 3.0),
                0f);
            withoutProbe.Controller.Move(step);
            withProbe.Controller.Move(step);

            Assert.Equal(withoutProbe.Root, withProbe.Root);
            Assert.Equal(withoutProbe.Controller.LastContact.H1Curtailed, withProbe.Controller.LastContact.H1Curtailed);
            Assert.Equal(withoutProbe.Controller.LastContact.H2Curtailed, withProbe.Controller.LastContact.H2Curtailed);
            if (withoutProbe.Controller.LastContact.H1Curtailed)
            {
                curtailedSteps++;
            }
        }

        Assert.True(curtailedSteps > 0, "the series must meet the field wall");
    }

    // T-ENG-8

    [Fact]
    public void Move_ProbeIntroducesNoAllocation()
    {
        var obstacleProbe = new BoxProbe();
        obstacleProbe.Add(ObstacleMin, ObstacleMax);
        // From 551.5 the series of 100 steps of -1 includes the tick that establishes the contact (step 34) and pushes.
        var start = new Vector3(968f, 551.5f, 48f);
        var without = CreateRig(start, CreateField(48f), null);
        var with = CreateRig(start, CreateField(48f), obstacleProbe);
        var neverBlocking = CreateRig(start, CreateField(48f), new BoxProbe());

        // Warm-up: JIT and first-call work.
        MeasureHundredSteps(without, start);
        MeasureHundredSteps(with, start);
        MeasureHundredSteps(neverBlocking, start);

        var baseline = MeasureHundredSteps(without, start);
        var withObstacle = MeasureHundredSteps(with, start);
        var withNeverBlocking = MeasureHundredSteps(neverBlocking, start);

        Assert.Same(obstacleProbe.FirstObstacle, with.Controller.LastContact.H2Obstacle);
        Assert.True(obstacleProbe.Calls >= 2 * (33 + 27 + 66 * 2), "the series must include a contact tick and pushes against the probe");
        if (baseline == 0)
        {
            Assert.Equal(0, withObstacle);
        }
        else
        {
            Assert.True(withObstacle <= baseline, $"with probe {withObstacle} bytes, without {baseline} bytes");
        }

        Assert.Equal(baseline, withNeverBlocking);
    }

    private static long MeasureHundredSteps(Rig rig, Vector3 start)
    {
        rig.Pawn.RootComponent!.Position = start;
        var step = new Vector3(0f, -1f, 0f);
        var before = AllocationWindow.Start();
        for (var i = 0; i < 100; i++)
        {
            rig.Controller.Move(step);
        }

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    // T-ENG-10

    [Fact]
    public void Move_WithAProbeAndNoField_OnlyTheObstacleBlocks()
    {
        var probe = new BoxProbe();
        var obstacle = probe.Add(ObstacleMin, ObstacleMax);
        var rig = CreateRig(new Vector3(968f, 552f, 48f), null, probe);

        rig.Controller.Move(new Vector3(0f, -0.5f, 0f));
        for (var step = 2; step <= 35; step++)
        {
            rig.Controller.Move(new Vector3(0f, -1f, 0f));
        }

        Assert.Equal(518.0f, rig.Root.Y);
        Assert.Same(obstacle, rig.Controller.LastContact.H2Obstacle);
    }

    // T-ENG-11

    [Fact]
    public void Update_StopsAtTheContactAndReportsTheObstacle()
    {
        var probe = new BoxProbe();
        var obstacle = probe.Add(ObstacleMin, ObstacleMax);
        var rig = CreateRig(new Vector3(968f, 520f, 48f), CreateField(48f), probe);
        ConfigureForUpdate(rig.Controller);
        rig.Controller.SetMoveIntent(new Vector2(0f, 1f));

        rig.Controller.Update(0.02f);

        Assert.Equal(-10f, rig.Controller.LastContact.RequestedH2Amount, precision: 3);
        Assert.Equal(518.0f, rig.Root.Y);
        Assert.True(rig.Controller.LastContact.H2Curtailed);
        Assert.Same(obstacle, rig.Controller.LastContact.H2Obstacle);
        Assert.Equal(-100f, rig.Controller.Velocity.Y, precision: 1);
    }

    [Fact]
    public void FixedStep_TheContactIsPaidOnceThenTheTwoCallsOfAPush()
    {
        var probe = new BoxProbe();
        var obstacle = probe.Add(ObstacleMin, ObstacleMax);
        var world = CreateWorld();
        world.CollisionField = CreateField(48f);
        world.MovementObstacleProbe = probe;
        var rig = CreateRig(new Vector3(968f, 520f, 48f), world);
        ConfigureForUpdate(rig.Controller);
        rig.Controller.SetMoveIntent(new Vector2(0f, 1f));
        world.Entities.Add(rig.Pawn);
        world.CharacterMotion.FixedTimeStep = 0.02f;

        world.CharacterMotion.Update(FrameTime.FromElapsedTime(0.04f, 1));

        Assert.Equal(2, world.CharacterMotion.ExecutedFixedStepCount);
        Assert.Equal(518.0f, rig.Root.Y);
        Assert.Same(obstacle, rig.Controller.LastContact.H2Obstacle);

        // The second sub-step starts with the whole step from 518: Y 508.
        var secondSubStepStart = -1;
        for (var i = 1; i < probe.RecordedCount; i++)
        {
            if (probe.RecordedY[i] == 508f)
            {
                secondSubStepStart = i;
                break;
            }
        }

        Assert.True(secondSubStepStart > 0, "the second sub-step must start with the whole step from 518");
        Assert.InRange(secondSubStepStart, 26, 30);
        Assert.Equal(2, probe.RecordedCount - secondSubStepStart);
    }

    // T-ENG-12

    [Theory]
    [InlineData("Stop")]
    [InlineData("Teleport")]
    [InlineData("RestoreStateSnapshot")]
    [InlineData("UpdateZero")]
    [InlineData("TinyMove")]
    public void ClearingThePublishedObstacles(string clearingCall)
    {
        var probe = new BoxProbe();
        probe.Add(ObstacleMin, ObstacleMax);
        var rig = CreateRig(new Vector3(968f, 520f, 48f), CreateField(48f), probe);
        rig.Controller.Move(new Vector3(0f, -10f, 0f));
        Assert.NotNull(rig.Controller.LastContact.H2Obstacle);

        switch (clearingCall)
        {
            case "Stop":
                rig.Controller.Stop();
                break;
            case "Teleport":
                rig.Controller.Teleport(new Vector3(100f, 100f, 48f));
                break;
            case "RestoreStateSnapshot":
                rig.Controller.RestoreStateSnapshot(rig.Controller.CaptureStateSnapshot());
                break;
            case "UpdateZero":
                rig.Controller.Update(0f);
                break;
            case "TinyMove":
                rig.Controller.Move(new Vector3(0f, -0.0005f, 0f));
                break;
        }

        Assert.Null(rig.Controller.LastContact.H1Obstacle);
        Assert.Null(rig.Controller.LastContact.H2Obstacle);
    }

    private static void ConfigureForUpdate(CharacterControllerComponent controller)
    {
        controller.Settings.Gravity = 0f;
        controller.Settings.MaxHorizontalSpeed = 500f;
        controller.Settings.Acceleration = 100000f;
    }

    // Montage

    private static HeightGridCollisionField CreateField(float height, int wallRow = -1, int wallColumn = -1)
    {
        var heights = new float[FieldCells * FieldCells];
        Array.Fill(heights, height);
        for (var cell = 0; cell < FieldCells; cell++)
        {
            if (wallRow >= 0)
            {
                heights[wallRow * FieldCells + cell] = height + 100f;
            }

            if (wallColumn >= 0)
            {
                heights[cell * FieldCells + wallColumn] = height + 100f;
            }
        }

        return new HeightGridCollisionField(Vector3.Zero, 16f, FieldCells, FieldCells, heights, up: Vector3.UnitZ);
    }

    private static Rig CreateRig(Vector3 root, ICollisionField field, BoxProbe probe)
    {
        var world = CreateWorld();
        world.CollisionField = field;
        world.MovementObstacleProbe = probe;
        return CreateRig(root, world);
    }

    private static Rig CreateRig(Vector3 root, World world)
    {
        var entity = new Entity { RootComponent = new TestSceneComponent() };
        typeof(Entity)
            .GetProperty(nameof(Entity.World), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(entity, world);

        var collision = new CollisionComponent();
        collision.Fixtures.Add(new ColliderFixture(new Box { Size = new Vector3(2f * HalfX, 2f * HalfY, Height) })
        {
            LocalPosition = new Vector3(0f, 0f, Height * 0.5f),
        });
        entity.AddComponent(collision);
        collision.Parent = entity.RootComponent;
        entity.RootComponent!.Children.Add(collision);

        var controller = new CharacterControllerComponent();
        controller.Settings.StepHeight = 3f;
        controller.Settings.GroundSnapDistance = 4f;
        controller.Settings.Gravity = 1250f;
        controller.Settings.MaxFallSpeed = 800f;
        controller.Settings.SkinWidth = 0.5f;
        controller.Settings.WalkabilityMask = 0u;
        controller.Settings.Radius = 8f;
        controller.Settings.Height = Height;
        entity.AddComponent(controller);

        entity.RootComponent.Position = root;
        return new Rig(entity, controller, world);
    }

    private static World CreateWorld()
    {
        var world = new World();
        typeof(World)
            .GetProperty(nameof(World.PhysicsWorld), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(world, new PhysicsWorld(useExternalViewManagement: false, spacePolicy: new TopDownElevationSimulationSpacePolicy()));
        return world;
    }

    private sealed class Rig
    {
        public Rig(Entity pawn, CharacterControllerComponent controller, World world)
        {
            Pawn = pawn;
            Controller = controller;
            World = world;
        }

        public Entity Pawn { get; }

        public CharacterControllerComponent Controller { get; }

        public World World { get; }

        public Vector3 Root => Pawn.RootComponent!.Position;
    }

    /// <summary>
    /// Test probe with semi-open boxes [min ; max): counts its calls, keeps the mover and the candidate roots it
    /// receives in preallocated storage, never allocates in <see cref="TryFindObstacle"/>.
    /// </summary>
    private sealed class BoxProbe : IMovementObstacleProbe
    {
        private readonly Entity[] _entities = new Entity[8];
        private readonly Vector3[] _mins = new Vector3[8];
        private readonly Vector3[] _maxs = new Vector3[8];
        private int _count;

        public float[] RecordedX { get; } = new float[4096];

        public float[] RecordedY { get; } = new float[4096];

        public int RecordedCount { get; set; }

        public int Calls { get; set; }

        public Entity FirstObstacle => _entities[0];

        public Entity LastMover { get; private set; }

        public Vector3 LastCandidate { get; private set; }

        public Entity Add(Vector3 min, Vector3 max)
        {
            var entity = new Entity();
            _entities[_count] = entity;
            _mins[_count] = min;
            _maxs[_count] = max;
            _count++;
            return entity;
        }

        public bool TryFindObstacle(Entity mover, in Vector3 candidateRootPosition, out Entity obstacle)
        {
            Calls++;
            LastMover = mover;
            LastCandidate = candidateRootPosition;
            if (RecordedCount < RecordedX.Length)
            {
                RecordedX[RecordedCount] = candidateRootPosition.X;
                RecordedY[RecordedCount] = candidateRootPosition.Y;
                RecordedCount++;
            }

            var minX = candidateRootPosition.X - HalfX;
            var maxX = candidateRootPosition.X + HalfX;
            var minY = candidateRootPosition.Y - HalfY;
            var maxY = candidateRootPosition.Y + HalfY;
            var minZ = candidateRootPosition.Z;
            var maxZ = candidateRootPosition.Z + Height;

            for (var i = 0; i < _count; i++)
            {
                if (minX < _maxs[i].X && _mins[i].X < maxX
                    && minY < _maxs[i].Y && _mins[i].Y < maxY
                    && minZ < _maxs[i].Z && _mins[i].Z < maxZ)
                {
                    obstacle = _entities[i];
                    return true;
                }
            }

            obstacle = null;
            return false;
        }
    }

    private sealed class TestSceneComponent : SceneComponent
    {
        public override EntityComponent Clone()
        {
            return new TestSceneComponent();
        }
    }
}
