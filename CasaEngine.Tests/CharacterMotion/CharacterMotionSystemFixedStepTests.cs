using CasaEngine.Core.Time;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.CharacterMotion;

/// <summary>
/// Covers the M1 slice of docs/plan-moteur-character-motion.md (parent repository): the opt-in
/// fixed-step mode of <see cref="CasaEngine.Framework.Scene.CharacterMotion.CharacterMotionSystem"/>.
/// </summary>
public sealed class CharacterMotionSystemFixedStepTests
{
    private const float FixedTimeStep = 1f / 50f;

    [Fact]
    public void DefaultMode_IsDisabled_AndNeverAdvancesTheFixedStepCounter()
    {
        World world = CreateWorldWithControlledEntity(out _, out _);

        Assert.Equal(0f, world.CharacterMotion.FixedTimeStep);
        Assert.Equal(4, world.CharacterMotion.MaxStepsPerFrame);

        for (int frameIndex = 1; frameIndex <= 10; frameIndex++)
        {
            world.Update(FrameTime.FromElapsedTime(0.1f, frameIndex));
        }

        Assert.Equal(0, world.CharacterMotion.ExecutedFixedStepCount);
    }

    [Fact]
    public void FixedStep_AtExactCadence_RunsExactlyOneStepPerFrame()
    {
        World world = CreateWorldWithControlledEntity(out _, out _);
        world.CharacterMotion.FixedTimeStep = FixedTimeStep;

        for (int frameIndex = 1; frameIndex <= 10; frameIndex++)
        {
            world.Update(FrameTime.FromElapsedTime(FixedTimeStep, frameIndex));
            Assert.Equal(frameIndex, world.CharacterMotion.ExecutedFixedStepCount);
        }
    }

    [Fact]
    public void FixedStep_WithNonMultipleDeltaTime_CarriesRemainderAndNeverRunsMoreThanOneStepPerFrame()
    {
        World world = CreateWorldWithControlledEntity(out _, out _);
        world.CharacterMotion.FixedTimeStep = FixedTimeStep;
        const float dt = 1f / 123f;

        long previousCount = 0;
        for (int frameIndex = 1; frameIndex <= 123; frameIndex++)
        {
            world.Update(FrameTime.FromElapsedTime(dt, frameIndex));
            long count = world.CharacterMotion.ExecutedFixedStepCount;
            Assert.InRange(count - previousCount, 0, 1);
            previousCount = count;
        }

        Assert.InRange(world.CharacterMotion.ExecutedFixedStepCount, 49, 51);
    }

    [Fact]
    public void FixedStep_OnALongFrame_IsCappedAtMaxStepsPerFrame()
    {
        World world = CreateWorldWithControlledEntity(out _, out _);
        world.CharacterMotion.FixedTimeStep = FixedTimeStep;
        world.CharacterMotion.MaxStepsPerFrame = 4;

        world.Update(FrameTime.FromElapsedTime(1f, 1));

        Assert.Equal(4, world.CharacterMotion.ExecutedFixedStepCount);

        // The cap drops the unrun remainder (spiral-of-death guard) rather than carrying it: the
        // very next frame, even at the exact cadence, still advances by exactly one more step.
        world.Update(FrameTime.FromElapsedTime(FixedTimeStep, 2));
        Assert.Equal(5, world.CharacterMotion.ExecutedFixedStepCount);
    }

    [Fact]
    public void FixedStep_TrajectoryDistances_AreEqualWithinTolerance_AcrossCadences()
    {
        // Drives all three cadences to exactly 50 fixed steps (mirroring the theory above, kept as
        // its own assertion so the three distances can be compared directly against each other).
        float distance50 = RunToFixedStepCount(1f / 50f, targetSteps: 50);
        float distance123 = RunToFixedStepCount(1f / 123f, targetSteps: 50);
        float distance240 = RunToFixedStepCount(1f / 240f, targetSteps: 50);

        Assert.Equal(distance50, distance123, 1e-4f);
        Assert.Equal(distance50, distance240, 1e-4f);
        Assert.Equal(distance123, distance240, 1e-4f);
    }

    [Theory]
    [InlineData(1f / 50f, 9.600f)]
    [InlineData(1f / 123f, 9.540f)]
    [InlineData(1f / 240f, 9.5208f)]
    public void OffMode_TrajectoryDivergesAcrossCadences_ForAboutOneSecondOfRealTime(float dt, float expectedDistance)
    {
        World world = CreateWorldWithControlledEntity(out Entity entity, out CharacterControllerComponent controller);
        // FixedTimeStep defaults to 0 (disabled) - this is the off-mode control for the fixed-step
        // trajectory-invariance test above, proving (e) would be vacuous without fixed-step mode.
        Assert.Equal(0f, world.CharacterMotion.FixedTimeStep);
        controller.SetMoveIntent(Vector2.UnitX);

        int frameCount = (int)MathF.Round(1f / dt);
        for (int frameIndex = 1; frameIndex <= frameCount; frameIndex++)
        {
            world.Update(FrameTime.FromElapsedTime(dt, frameIndex));
        }

        Assert.Equal(0, world.CharacterMotion.ExecutedFixedStepCount);
        Assert.Equal(expectedDistance, entity.RootComponent!.Position.X, 1e-3f);
    }

    [Fact]
    public void Clear_ResetsTheFixedStepAccumulator_SoAPartialFrameAfterReloadDoesNotCarryOverTheRemainder()
    {
        // M1.a: Clear() (e.g. on a world reload) must reset the fixed-step accumulator added by
        // M1, or a pending remainder from the previous world survives into the next one and can
        // trigger an extra fixed step on its very first frame. Two partial frames (each below one
        // FixedTimeStep) sum to more than one FixedTimeStep, so without the fix the accumulator
        // carried across Clear() would make the second partial frame run a step.
        World world = CreateWorldWithControlledEntity(out _, out _);
        world.CharacterMotion.FixedTimeStep = FixedTimeStep;
        float partialDt = FixedTimeStep * 0.6f;

        world.Update(FrameTime.FromElapsedTime(partialDt, 1));
        Assert.Equal(0, world.CharacterMotion.ExecutedFixedStepCount);

        world.CharacterMotion.Clear();

        world.Update(FrameTime.FromElapsedTime(partialDt, 2));
        Assert.Equal(0, world.CharacterMotion.ExecutedFixedStepCount);
    }

    [Fact]
    public void OffMode_TrajectoryDistances_DivergeByAtLeastTheExpectedAmount()
    {
        float distance50 = RunVariableStepForAboutOneSecond(1f / 50f);
        float distance123 = RunVariableStepForAboutOneSecond(1f / 123f);
        float distance240 = RunVariableStepForAboutOneSecond(1f / 240f);

        float maxDistance = MathF.Max(distance50, MathF.Max(distance123, distance240));
        float minDistance = MathF.Min(distance50, MathF.Min(distance123, distance240));

        Assert.True(maxDistance - minDistance >= 0.079f,
            $"Expected off-mode distances to diverge by at least 0.079, got {maxDistance - minDistance} (50={distance50}, 123={distance123}, 240={distance240}).");
    }

    private static float RunToFixedStepCount(float dt, int targetSteps)
    {
        World world = CreateWorldWithControlledEntity(out Entity entity, out CharacterControllerComponent controller);
        world.CharacterMotion.FixedTimeStep = FixedTimeStep;
        controller.SetMoveIntent(Vector2.UnitX);

        int frameIndex = 0;
        while (world.CharacterMotion.ExecutedFixedStepCount < targetSteps)
        {
            frameIndex++;
            world.Update(FrameTime.FromElapsedTime(dt, frameIndex));
            Assert.True(frameIndex < 100_000, "Fixed step count never reached the target - accounting is broken.");
        }

        return entity.RootComponent!.Position.X;
    }

    private static float RunVariableStepForAboutOneSecond(float dt)
    {
        World world = CreateWorldWithControlledEntity(out Entity entity, out CharacterControllerComponent controller);
        controller.SetMoveIntent(Vector2.UnitX);

        int frameCount = (int)MathF.Round(1f / dt);
        for (int frameIndex = 1; frameIndex <= frameCount; frameIndex++)
        {
            world.Update(FrameTime.FromElapsedTime(dt, frameIndex));
        }

        return entity.RootComponent!.Position.X;
    }

    private static World CreateWorldWithControlledEntity(out Entity entity, out CharacterControllerComponent controller)
    {
        var world = new World();
        entity = new Entity
        {
            RootComponent = new TestSceneComponent(),
        };

        controller = new CharacterControllerComponent
        {
            Settings = new CharacterControllerSettings
            {
                MaxHorizontalSpeed = 10f,
                Acceleration = 100f,
                Deceleration = 100f,
                Gravity = 0f,
            }
        };
        entity.AddComponent(controller);

        world.AddEntity(entity);
        world.Update(FrameTime.FromElapsedTime(0f));
        return world;
    }

    private sealed class TestSceneComponent : SceneComponent
    {
        public override EntityComponent Clone()
        {
            return new TestSceneComponent();
        }
    }
}
