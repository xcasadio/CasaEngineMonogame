using CasaEngine.Core.Time;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Rendering.ScreenEffects;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Cutscenes;

/// <summary>The play clock and the per-action notifications of the cutscene director (ADR-0098).</summary>
public sealed class CutsceneActionNotificationTests
{
    private const float Step = 0.1f;

    private long _frame;

    private readonly record struct Event(string Kind, CutsceneActionData Action, double Seconds, long Frames);

    [Fact]
    public void Wait_RaisesStartAtZeroAndEndAtItsDuration()
    {
        var world = new World();
        List<Event> events = Record(world);
        var wait = new WaitCutsceneActionData { Seconds = 1f };

        Play(world, wait);
        Advance(world, 20);

        Assert.Equal(2, events.Count);
        Assert.Equal(new Event("start", wait, 0d, 0), events[0]);
        Assert.Equal("end", events[1].Kind);
        Assert.Same(wait, events[1].Action);
        Assert.InRange(events[1].Seconds, 1.0 - Step, 1.0 + Step);
        Assert.Equal(10, events[1].Frames);
    }

    [Fact]
    public void Sequence_RaisesNestedNotificationsInOrder()
    {
        var world = new World();
        List<Event> events = Record(world);
        var first = new WaitCutsceneActionData { Seconds = 0.2f };
        var second = new WaitCutsceneActionData { Seconds = 0.2f };
        var sequence = new SequenceCutsceneActionData { Actions = { first, second } };

        Play(world, sequence);
        Advance(world, 20);

        Assert.Equal(
            ["start:Sequence", "start:Wait", "end:Wait", "start:Wait", "end:Wait", "end:Sequence"],
            Names(events));
        Assert.Same(first, events[1].Action);
        Assert.Same(second, events[3].Action);
        Assert.True(events[3].Seconds >= events[2].Seconds);
    }

    [Fact]
    public void Parallel_StartsBranchesTogetherAndEndsAfterTheLongest()
    {
        var world = new World();
        List<Event> events = Record(world);
        var shortWait = new WaitCutsceneActionData { Seconds = 0.2f };
        var longWait = new WaitCutsceneActionData { Seconds = 0.5f };
        var parallel = new ParallelCutsceneActionData { Actions = { shortWait, longWait } };

        Play(world, parallel);
        Advance(world, 20);

        Assert.Equal(
            ["start:Parallel", "start:Wait", "start:Wait", "end:Wait", "end:Wait", "end:Parallel"],
            Names(events));
        Assert.Equal(events[1].Seconds, events[2].Seconds);
        Assert.Same(shortWait, events[3].Action);
        Assert.Same(longWait, events[4].Action);
    }

    [Fact]
    public void Repeat_OfTwoLaps_RaisesTheChildrenTwiceAndTheRepeatOnce()
    {
        var world = new World();
        List<Event> events = Record(world);
        var wait = new WaitCutsceneActionData { Seconds = 0.2f };
        var repeat = new RepeatCutsceneActionData { Count = 2, Actions = { wait } };

        Play(world, repeat);
        Advance(world, 20);

        Assert.Equal(
            ["start:Repeat", "start:Wait", "end:Wait", "start:Wait", "end:Wait", "end:Repeat"],
            Names(events));
        Assert.Same(wait, events[1].Action);
        Assert.Same(wait, events[3].Action);
    }

    [Fact]
    public void MoveSprite_RaisesStartThenEndInTheSameUpdate()
    {
        var world = new World();
        world.Entities.Add(new Entity { Name = "Star", RootComponent = new StaticSpriteComponent() });
        List<Event> events = Record(world);
        var move = new MoveSpriteCutsceneActionData { EntityName = "Star", X = 10f, Y = 0f, Speed = 1f };

        Play(world, move);
        Advance(world, 3);

        Assert.Equal(["start:MoveSprite", "end:MoveSprite"], Names(events));
        Assert.Equal(events[0].Seconds, events[1].Seconds);
        Assert.Equal(events[0].Frames, events[1].Frames);
    }

    [Fact]
    public void StoppingAPlay_RaisesNoEndForTheRunningAction()
    {
        var world = new World();
        List<Event> events = Record(world);

        Play(world, new WaitCutsceneActionData { Seconds = 5f });
        Advance(world, 3);
        world.CutsceneDirector.Stop();
        Advance(world, 3);

        Assert.Equal(["start:Wait"], Names(events));
    }

    [Fact]
    public void ARequestRefusedByTheHost_RaisesStartAndEnd()
    {
        var world = new World();
        world.CutsceneDirector.EffectHost = new RefusingHost();
        List<Event> events = Record(world);

        Play(world, new ChangeWorldCutsceneActionData { Target = "elsewhere.world" });
        Advance(world, 3);

        Assert.Equal(["start:ChangeWorld", "end:ChangeWorld"], Names(events));
    }

    [Fact]
    public void AFailureInABranch_RaisesNothingMoreForTheOtherBranches()
    {
        var world = new World();
        List<Event> events = Record(world);
        var failing = new ShowSpriteCutsceneActionData { EntityName = "Missing" };
        var other = new WaitCutsceneActionData { Seconds = 0.3f };
        var parallel = new ParallelCutsceneActionData { Actions = { failing, other } };

        Play(world, parallel);
        Advance(world, 10);

        Assert.Equal(CutsceneRuntimeState.Failed, world.CutsceneDirector.GetDebugSnapshot().State);
        Assert.Equal(["start:Parallel", "start:ShowSprite"], Names(events));
    }

    [Fact]
    public void AnEmptyParallel_RaisesStartAndEnd()
    {
        var world = new World();
        List<Event> events = Record(world);

        Play(world, new ParallelCutsceneActionData());
        Advance(world, 3);

        Assert.Equal(["start:Parallel", "end:Parallel"], Names(events));
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void APlayStartedByAnEndedHandler_SeesItsFirstStepAtTimeZero()
    {
        var world = new World();
        var secondWait = new WaitCutsceneActionData { Seconds = 0.3f };
        var second = new CutsceneAsset { Name = "Second", RootAction = secondWait };
        bool chained = false;
        world.CutsceneDirector.Ended += (_, _) =>
        {
            if (!chained)
            {
                chained = true;
                world.CutsceneDirector.Play(second);
            }
        };
        var seconds = new List<double>();
        world.CutsceneDirector.ActionStarted += (in CutsceneActionNotification n) =>
        {
            if (ReferenceEquals(n.Action, secondWait))
            {
                seconds.Add(n.ElapsedSeconds);
            }
        };

        Play(world, new WaitCutsceneActionData { Seconds = 0.2f });
        Advance(world, 12);

        Assert.True(chained);
        Assert.Equal([0d], seconds);
    }

    [Fact]
    public void TheClock_RestartsAtASecondPlay_AndStaysStillOutsideAPlay()
    {
        var world = new World();

        Assert.Equal(0d, world.CutsceneDirector.PlayElapsedSeconds);
        Advance(world, 3);
        Assert.Equal(0d, world.CutsceneDirector.PlayElapsedSeconds);
        Assert.Equal(0, world.CutsceneDirector.PlayElapsedFrames);

        Play(world, new WaitCutsceneActionData { Seconds = 5f });
        Advance(world, 4);
        Assert.Equal(4, world.CutsceneDirector.PlayElapsedFrames);
        Assert.InRange(world.CutsceneDirector.PlayElapsedSeconds, 0.39, 0.41);

        Play(world, new WaitCutsceneActionData { Seconds = 0.2f });
        Assert.Equal(0d, world.CutsceneDirector.PlayElapsedSeconds);
        Assert.Equal(0, world.CutsceneDirector.PlayElapsedFrames);

        Advance(world, 20);
        double afterEnd = world.CutsceneDirector.PlayElapsedSeconds;
        Advance(world, 5);
        Assert.Equal(afterEnd, world.CutsceneDirector.PlayElapsedSeconds);
    }

    [Fact]
    public void AThrowingHandler_DoesNotInterruptThePlay()
    {
        var world = new World();
        world.CutsceneDirector.ActionStarted += (in CutsceneActionNotification _) => throw new InvalidOperationException("handler failure");

        Play(world, new WaitCutsceneActionData { Seconds = 0.2f });
        Advance(world, 10);

        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    private static List<Event> Record(World world)
    {
        var events = new List<Event>();
        world.CutsceneDirector.ActionStarted += (in CutsceneActionNotification n) =>
            events.Add(new Event("start", n.Action, n.ElapsedSeconds, n.ElapsedFrames));
        world.CutsceneDirector.ActionEnded += (in CutsceneActionNotification n) =>
            events.Add(new Event("end", n.Action, n.ElapsedSeconds, n.ElapsedFrames));
        return events;
    }

    private static string[] Names(List<Event> events)
        => events.Select(e => e.Kind + ":" + e.Action.Type).ToArray();

    private static void Play(World world, CutsceneActionData root)
        => world.CutsceneDirector.Play(new CutsceneAsset { Name = "Clock", RootAction = root });

    private void Advance(World world, int updates)
    {
        for (int index = 0; index < updates; index++)
        {
            world.Update(FrameTime.FromElapsedTime(Step, ++_frame));
        }
    }

    private sealed class RefusingHost : ICutsceneEffectHost
    {
        public AudioService Audio => null;

        public ScreenEffectService ScreenEffects => null;

        public bool AllowGameRequest(CutsceneActionData action) => false;
    }
}
