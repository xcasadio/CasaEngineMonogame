using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;

namespace CasaEngine.Tests.Animation;

/// <summary>
/// The logical clock of the animation ends of <see cref="AnimatedSpriteComponent"/> (ADR-0046): optional,
/// in whole ticks, driven by the game layer. The expected values were written before the code, from a model
/// of the rules and of the float32 sums of the real-time sampler.
/// </summary>
public class AnimatedSpriteLogicalEndClockTests
{
    private const int Rate = 50;

    private static readonly Guid SpriteId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

    private static Animation2d Make(string name, AnimationType type, params float[] keyTimes)
    {
        var data = new Animation2dData { Name = name, AnimationType = type };
        data.Parts.Add(new Animation2dPartData { Id = "sprite", DefaultSpriteId = SpriteId });
        var track = new Animation2dTrackData { TargetPartId = "sprite", Property = Animation2dTrackProperty.Sprite };
        foreach (var keyTime in keyTimes)
        {
            track.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(keyTime, SpriteId));
        }

        data.Tracks.Add(track);
        return new Animation2d(data);
    }

    /// <summary>Animation 6 of Ronan: 24 ticks at 50 Hz.</summary>
    private static Animation2d RonanOnce() => Make("ronan_6", AnimationType.Once, 0f, 0.12f, 0.24f, 0.35999998f, 0.48f);

    private static Animation2d Once(string name, float durationSeconds) => Make(name, AnimationType.Once, 0f, durationSeconds);

    private static Animation2d Loop(string name, float durationSeconds) => Make(name, AnimationType.Loop, 0f, durationSeconds);

    /// <summary>Animation 0 of the hero: 54 ticks at 50 Hz.</summary>
    private static Animation2d HeroLoop() => Make("hero_0", AnimationType.Loop, 0f, 0.79999995f, 0.99999994f, 1.06f, 1.0799999f);

    private static AnimatedSpriteComponent CreateWith(params Animation2d[] animations)
    {
        var component = AnimatedSpriteClockTestHost.Create(out _, out _);
        foreach (var animation in animations)
        {
            component.AddAnimation(animation);
        }

        component.SetCurrentAnimation(0, true);
        component.SetLogicalTickRate(Rate);
        return component;
    }

    private sealed class Counters
    {
        public int Finished;
        public int Looped;

        public Counters(AnimatedSpriteComponent component)
        {
            component.AnimationFinished += (_, _) => Finished++;
            component.AnimationLooped += (_, _) => Looped++;
        }
    }

    [Fact]
    public void L1_Once_FinishesOnItsDurationTick_ExactlyOnce()
    {
        var component = CreateWith(RonanOnce());
        var counters = new Counters(component);

        Assert.Equal(24, component.LogicalDurationTicks);

        for (int advance = 1; advance <= 23; advance++)
        {
            component.AdvanceLogicalTicks(1);
            Assert.Equal(0, counters.Finished);
            Assert.Equal(advance, component.LogicalTick);
        }

        component.AdvanceLogicalTicks(1);
        Assert.Equal(1, counters.Finished);
        Assert.Equal(24, component.LogicalTick);
        Assert.True(component.IsLogicalEndReached);

        for (int advance = 25; advance <= 30; advance++)
        {
            component.AdvanceLogicalTicks(1);
        }

        Assert.Equal(1, counters.Finished);
    }

    [Theory]
    [InlineData(0.64f, 32)]
    [InlineData(0.79999995f, 40)]
    [InlineData(0.48f, 24)]
    [InlineData(0.59999996f, 30)]
    public void L2_Once_FinishesOnTheExpectedAdvance(float durationSeconds, int expectedAdvance)
    {
        var component = CreateWith(Once("once", durationSeconds));
        var counters = new Counters(component);

        for (int advance = 1; advance <= expectedAdvance + 5; advance++)
        {
            component.AdvanceLogicalTicks(1);
            Assert.Equal(advance >= expectedAdvance ? 1 : 0, counters.Finished);
        }

        Assert.Equal(expectedAdvance, component.LogicalDurationTicks);
    }

    [Fact]
    public void L3_Loop_TurnsEveryDurationTick_AndNeverFinishes()
    {
        var component = CreateWith(HeroLoop());
        var turnAdvances = new List<int>();
        var turnCounts = new List<int>();
        int advance = 0;
        int finished = 0;
        component.AnimationLooped += (_, _) =>
        {
            turnAdvances.Add(advance);
            turnCounts.Add(component.CompletedLoopCount);
        };
        component.AnimationFinished += (_, _) => finished++;

        Assert.Equal(54, component.LogicalDurationTicks);

        for (advance = 1; advance <= 162; advance++)
        {
            component.AdvanceLogicalTicks(1);
            if (advance == 53)
            {
                Assert.Equal(53, component.LogicalTick);
            }

            if (advance == 54)
            {
                Assert.Equal(0, component.LogicalTick);
            }
        }

        Assert.Equal(new[] { 54, 108, 162 }, turnAdvances);
        Assert.Equal(new[] { 1, 2, 3 }, turnCounts);
        Assert.Equal(3, component.CompletedLoopCount);
        Assert.Equal(0, finished);
    }

    [Fact]
    public void L4_MultiTickAdvance_AndInvalidArguments()
    {
        var loop = CreateWith(Loop("loop", 0.48f));
        var loopCounters = new Counters(loop);
        Assert.Equal(50, loop.AdvanceLogicalTicks(50));
        Assert.Equal(2, loopCounters.Looped);
        Assert.Equal(2, loop.LogicalTick);

        var once = CreateWith(Once("once", 0.48f));
        var onceCounters = new Counters(once);
        Assert.Equal(100, once.AdvanceLogicalTicks(100));
        Assert.Equal(1, onceCounters.Finished);
        Assert.Equal(24, once.LogicalTick);

        Assert.Equal(0, once.AdvanceLogicalTicks(0));
        Assert.Equal(24, once.LogicalTick);

        Assert.Throws<ArgumentOutOfRangeException>(() => once.AdvanceLogicalTicks(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => once.SetLogicalTickRate(-1));
    }

    [Fact]
    public void L5_Resets()
    {
        var component = CreateWith(RonanOnce(), HeroLoop());
        var counters = new Counters(component);

        component.AdvanceLogicalTicks(24);
        Assert.Equal(1, counters.Finished);

        component.SetCurrentAnimation(0, true);
        Assert.Equal(0, component.LogicalTick);
        Assert.False(component.IsLogicalEndReached);
        component.AdvanceLogicalTicks(23);
        Assert.Equal(1, counters.Finished);
        component.AdvanceLogicalTicks(1);
        Assert.Equal(2, counters.Finished);

        component.SetCurrentAnimation(0, true);
        component.AdvanceLogicalTicks(10);
        component.SetCurrentAnimation(0, false);
        Assert.Equal(10, component.LogicalTick);

        component.SetLogicalTickRate(Rate);
        Assert.Equal(10, component.LogicalTick);

        component.SetLogicalTickRate(25);
        Assert.Equal(0, component.LogicalTick);
        Assert.Equal(12, component.LogicalDurationTicks);
        component.SetLogicalTickRate(Rate);

        component.SetCurrentAnimation(0, true);
        component.AdvanceLogicalTicks(10);
        component.SetCurrentAnimation(1, false);
        Assert.Equal(0, component.LogicalTick);
        Assert.Equal(54, component.LogicalDurationTicks);
        Assert.Equal(0, component.CompletedLoopCount);
    }

    [Fact]
    public void L6_BothClocksCoexist_AndEachOneMovesItsOwnState()
    {
        // Ronan: the rendering ends on the 24th update without raising anything; the logical end comes with the advance.
        var ronan = CreateWith(RonanOnce());
        var ronanCounters = new Counters(ronan);
        for (int frame = 1; frame <= 30; frame++)
        {
            ronan.Update(0.02f);
            if (frame == 23)
            {
                Assert.Equal(0.46000007f, ronan.CurrentAnimationTimeSeconds);
            }

            if (frame == 24)
            {
                Assert.Equal(0.48f, ronan.CurrentAnimationTimeSeconds);
                Assert.Equal(0, ronanCounters.Finished);
            }

            Assert.Equal(Math.Min(frame - 1, 24), ronan.LogicalTick);
            ronan.AdvanceLogicalTicks(1);
            if (frame == 24)
            {
                Assert.Equal(1, ronanCounters.Finished);
            }
        }

        Assert.Equal(1, ronanCounters.Finished);

        // Hero 83: the logical end of the 32nd frame, the rendering ends on the 33rd update, no second event.
        var hero = CreateWith(Once("hero_83", 0.64f));
        var heroCounters = new Counters(hero);
        for (int frame = 1; frame <= 40; frame++)
        {
            hero.Update(0.02f);
            if (frame == 32)
            {
                Assert.True(hero.CurrentAnimationTimeSeconds < 0.64f);
            }

            if (frame == 33)
            {
                Assert.Equal(0.64f, hero.CurrentAnimationTimeSeconds);
            }

            hero.AdvanceLogicalTicks(1);
            Assert.Equal(frame >= 32 ? 1 : 0, heroCounters.Finished);
        }

        // A long real-time update first: nothing is raised, the 24th advance raises the end.
        var late = CreateWith(RonanOnce());
        var lateCounters = new Counters(late);
        late.Update(1f);
        Assert.Equal(0, lateCounters.Finished);
        Assert.Equal(0, late.LogicalTick);
        for (int advance = 1; advance <= 24; advance++)
        {
            late.AdvanceLogicalTicks(1);
            Assert.Equal(advance == 24 ? 1 : 0, lateCounters.Finished);
        }

        // Advances do not move the rendering time.
        var untouched = CreateWith(RonanOnce());
        untouched.AdvanceLogicalTicks(10);
        Assert.Equal(0f, untouched.CurrentAnimationTimeSeconds);
    }

    [Fact]
    public void L7_DefaultMode_KeepsTheRealTimeEnds_AndRejectsAdvances()
    {
        var component = AnimatedSpriteClockTestHost.Create(out _, out _);
        component.AddAnimation(RonanOnce());
        component.SetCurrentAnimation(0, true);
        var counters = new Counters(component);

        component.Update(1f);

        Assert.Equal(1, counters.Finished);
        Assert.Equal(0, component.LogicalTickRate);
        Assert.Throws<InvalidOperationException>(() => component.AdvanceLogicalTicks(1));
    }

    [Fact]
    public void L8_ZeroAndSmallDurations()
    {
        var zeroLoop = CreateWith(Make("zero_loop", AnimationType.Loop, 0f));
        var zeroLoopCounters = new Counters(zeroLoop);
        for (int advance = 0; advance < 100; advance++)
        {
            zeroLoop.AdvanceLogicalTicks(1);
        }

        Assert.Equal(0, zeroLoopCounters.Looped);
        Assert.Equal(0, zeroLoop.LogicalTick);

        var zeroOnce = CreateWith(Make("zero_once", AnimationType.Once, 0f));
        var zeroOnceCounters = new Counters(zeroOnce);
        zeroOnce.AdvanceLogicalTicks(1);
        Assert.Equal(1, zeroOnceCounters.Finished);
        Assert.Equal(0, zeroOnce.LogicalTick);
        zeroOnce.AdvanceLogicalTicks(10);
        Assert.Equal(1, zeroOnceCounters.Finished);

        var tiny = CreateWith(Loop("tiny", 0.005f));
        var tinyCounters = new Counters(tiny);
        Assert.Equal(1, tiny.LogicalDurationTicks);
        for (int advance = 1; advance <= 7; advance++)
        {
            tiny.AdvanceLogicalTicks(1);
            Assert.Equal(advance, tinyCounters.Looped);
        }
    }

    [Theory]
    [InlineData(0.25f, 13)]
    [InlineData(0.75f, 38)]
    [InlineData(0.12f, 6)]
    public void L9_DurationInTicks_RoundsHalfAwayFromZero(float durationSeconds, int expectedTicks)
    {
        var component = CreateWith(Once("rounding", durationSeconds));

        Assert.Equal(expectedTicks, component.LogicalDurationTicks);
    }

    [Fact]
    public void L10_Seek_ReAlignsTheLogicalTickWithoutEvents()
    {
        var once = CreateWith(Once("once", 0.48f));
        var onceCounters = new Counters(once);

        once.SeekCurrentAnimation(0.12f);
        Assert.Equal(6, once.LogicalTick);
        Assert.Equal(0, onceCounters.Finished);
        for (int advance = 1; advance <= 18; advance++)
        {
            once.AdvanceLogicalTicks(1);
            Assert.Equal(advance == 18 ? 1 : 0, onceCounters.Finished);
        }

        var seekedToEnd = CreateWith(Once("once", 0.48f));
        var seekedCounters = new Counters(seekedToEnd);
        seekedToEnd.SeekCurrentAnimation(0.48f);
        Assert.Equal(24, seekedToEnd.LogicalTick);
        Assert.True(seekedToEnd.IsLogicalEndReached);
        Assert.Equal(0, seekedCounters.Finished);
        seekedToEnd.AdvanceLogicalTicks(5);
        Assert.Equal(0, seekedCounters.Finished);

        var loop = CreateWith(HeroLoop());
        var loopCounters = new Counters(loop);
        loop.SeekCurrentAnimation(0.5f);
        Assert.Equal(25, loop.LogicalTick);
        loop.SeekCurrentAnimation(1.0799999f);
        Assert.Equal(0, loop.LogicalTick);
        Assert.Equal(0, loop.CompletedLoopCount);
        Assert.Equal(0, loopCounters.Looped);
    }

    [Fact]
    public void L11_InitializeWithWorld_ResetsTheClockAndKeepsTheRate()
    {
        var component = AnimatedSpriteClockTestHost.Create(out var world, out _);
        component.AddAnimation(RonanOnce());
        component.SetCurrentAnimation(0, true);
        component.SetLogicalTickRate(Rate);
        component.AdvanceLogicalTicks(5);
        Assert.Equal(5, component.LogicalTick);

        component.InitializeWithWorld(world);

        Assert.Equal(Rate, component.LogicalTickRate);
        Assert.Equal(0, component.LogicalTick);
        Assert.Equal(24, component.LogicalDurationTicks);

        component.InitializeWithWorld(world);
        Assert.Equal(Rate, component.LogicalTickRate);
    }

    [Fact]
    public void L12_Copy_DoesNotCopyTheRate()
    {
        var original = AnimatedSpriteClockTestHost.Create(out var world, out _);
        original.AddAnimation(RonanOnce());
        original.SetCurrentAnimation(0, true);
        original.SetLogicalTickRate(Rate);

        var copy = new AnimatedSpriteComponent(original);
        AnimatedSpriteClockTestHost.PlaceInWorld(copy, world);

        Assert.Equal(0, copy.LogicalTickRate);

        copy.InitializeWithWorld(world);
        var counters = new Counters(copy);
        copy.Update(1f);

        Assert.Equal(1, counters.Finished);
    }

    [Fact]
    public void L13_ConditionalUpdateGate_DoesNotDependOnTheClock()
    {
        var component = AnimatedSpriteClockTestHost.Create(out _, out var game);
        component.AddAnimation(RonanOnce());
        component.SetCurrentAnimation(0, true);
        component.SetLogicalTickRate(Rate);
        component.Update(1f);
        component.AdvanceLogicalTicks(24);
        Assert.True(component.IsLogicalEndReached);

        Assert.True(component.ShouldUpdateWhenConditional(component.Owner));

        game.ExecutionPolicy = GameplayExecutionPolicies.EditorPreview;
        Assert.False(component.ShouldUpdateWhenConditional(component.Owner));
    }

    private static void IgnoreEvent(object? sender, Animation2d animation)
    {
    }

    [Fact]
    public void L14_AdvancingAllocatesNothing()
    {
        var component = CreateWith(Loop("loop", 0.48f));
        component.AnimationLooped += IgnoreEvent;
        component.AnimationFinished += IgnoreEvent;

        for (int warmUp = 0; warmUp < 10; warmUp++)
        {
            component.AdvanceLogicalTicks(1);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int advance = 0; advance < 1000; advance++)
        {
            component.AdvanceLogicalTicks(1);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void L15_AHandlerChangingTheAnimation_StopsTheAdvance()
    {
        var component = CreateWith(Loop("loop_a", 0.48f), Loop("loop_b", 0.48f));
        int looped = 0;
        component.AnimationLooped += (_, _) =>
        {
            looped++;
            component.SetCurrentAnimation(1, false);
        };

        int applied = component.AdvanceLogicalTicks(50);

        Assert.Equal(1, looped);
        Assert.Equal("loop_b", component.CurrentAnimation.Animation2dData.Name);
        Assert.Equal(0, component.LogicalTick);
        Assert.Equal(24, applied);
    }

    [Fact]
    public void L16_PausedPlayback_DoesNotBlockTheLogicalClock()
    {
        var component = CreateWith(RonanOnce());
        var counters = new Counters(component);
        component.IsPlaybackPaused = true;

        for (int frame = 1; frame <= 30; frame++)
        {
            component.Update(0.02f);
            component.AdvanceLogicalTicks(1);
            Assert.Equal(frame >= 24 ? 1 : 0, counters.Finished);
        }

        Assert.Equal(0f, component.CurrentAnimationTimeSeconds);
    }

    [Fact]
    public void L17_NoCurrentAnimation_KeepsTheRateAndDoesNothing()
    {
        var component = AnimatedSpriteClockTestHost.Create(out var world, out _);
        component.AddAnimation(RonanOnce());
        var counters = new Counters(component);
        Assert.Null(component.CurrentAnimation);

        component.SetLogicalTickRate(Rate);
        for (int advance = 0; advance < 10; advance++)
        {
            Assert.Equal(0, component.AdvanceLogicalTicks(1));
        }

        Assert.Equal(0, counters.Finished);
        Assert.Equal(0, counters.Looped);
        Assert.Equal(0, component.LogicalTick);
        Assert.Equal(0, component.LogicalDurationTicks);

        component.InitializeWithWorld(world);

        Assert.Equal(Rate, component.LogicalTickRate);
        Assert.Equal(24, component.LogicalDurationTicks);

        var empty = AnimatedSpriteClockTestHost.Create(out _, out _);
        var emptyCounters = new Counters(empty);
        empty.SetLogicalTickRate(Rate);
        for (int advance = 0; advance < 10; advance++)
        {
            Assert.Equal(0, empty.AdvanceLogicalTicks(1));
        }

        Assert.Equal(0, emptyCounters.Finished);
        Assert.Equal(0, emptyCounters.Looped);
    }
}
