using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Scene.World;
using Xunit;

namespace CasaEngine.Tests.EditorServices.Cutscenes;

public sealed class CutscenePreviewTransportTests
{
    private const double OneStep = CutscenePreviewTransport.StepSeconds;

    private sealed class FakeStepper : ICutscenePreviewStepper
    {
        public bool IsPlaying { get; set; } = true;

        public double PlayElapsedSeconds { get; set; }

        public int StepCount { get; private set; }

        public List<float> Steps { get; } = new();

        /// <summary>When set, the play ends once it has reached this time.</summary>
        public double? EndsAtSeconds { get; set; }

        public void Step(float seconds)
        {
            StepCount++;
            Steps.Add(seconds);
            PlayElapsedSeconds += seconds;
            if (EndsAtSeconds is { } end && PlayElapsedSeconds >= end)
            {
                IsPlaying = false;
            }
        }
    }

    /// <summary>A world with a play of a single Wait, advanced by fixed steps like the preview session does.</summary>
    private sealed class WorldStepper : ICutscenePreviewStepper
    {
        private readonly World _world;

        public WorldStepper(World world)
        {
            _world = world;
        }

        public bool IsPlaying => _world.CutsceneDirector.IsPlaying;

        public double PlayElapsedSeconds => _world.CutsceneDirector.PlayElapsedSeconds;

        public void Step(float seconds) => _world.Update(seconds);
    }

    private static CutscenePreviewTransport NewTransport(Func<long> clock) => new(clock, 1000);

    private static CutscenePreviewTransport NewTransport() => NewTransport(() => 0);

    [Fact]
    public void NewTransport_IsRunning()
    {
        var transport = NewTransport();

        Assert.Equal(CutscenePreviewTransportMode.Running, transport.Mode);
        Assert.Equal(8.0, transport.BudgetMilliseconds);
    }

    [Fact]
    public void StepSeconds_IsOneFrameOfTheTimeline()
    {
        Assert.Equal((float)(1.0 / CutsceneTimelineProjection.FramesPerSecond), CutscenePreviewTransport.StepSeconds);
    }

    [Fact]
    public void Advance_WhenRunning_DoesOneStepOfTheFrameDuration()
    {
        var transport = NewTransport();
        var stepper = new FakeStepper();

        int steps = transport.Advance(stepper, 0.05f);

        Assert.Equal(1, steps);
        Assert.Equal(new[] { 0.05f }, stepper.Steps);
    }

    [Fact]
    public void Advance_WhenPausedWithoutRequest_DoesNoStep()
    {
        var transport = NewTransport();
        var stepper = new FakeStepper();
        transport.Pause();

        int steps = transport.Advance(stepper, 0.05f);

        Assert.Equal(0, steps);
        Assert.Equal(0, stepper.StepCount);
        Assert.Equal(CutscenePreviewTransportMode.Paused, transport.Mode);
    }

    [Fact]
    public void Resume_AfterPause_StepsAgain()
    {
        var transport = NewTransport();
        var stepper = new FakeStepper();
        transport.Pause();
        transport.Resume();

        transport.Advance(stepper, 0.05f);

        Assert.Equal(CutscenePreviewTransportMode.Running, transport.Mode);
        Assert.Equal(1, stepper.StepCount);
    }

    [Fact]
    public void StepFrames_PausesThenDoesExactlyThatManyFrameSteps()
    {
        var transport = NewTransport();
        var stepper = new FakeStepper();

        transport.StepFrames(3);

        Assert.Equal(CutscenePreviewTransportMode.Paused, transport.Mode);
        Assert.Equal(3, transport.PendingFrameSteps);
        Assert.Equal(0, stepper.StepCount);

        int steps = transport.Advance(stepper, 0.05f);

        Assert.Equal(3, steps);
        Assert.All(stepper.Steps, step => Assert.Equal(CutscenePreviewTransport.StepSeconds, step));
        Assert.Equal(CutscenePreviewTransportMode.Paused, transport.Mode);
        Assert.Equal(0, transport.PendingFrameSteps);

        Assert.Equal(0, transport.Advance(stepper, 0.05f));
        Assert.Equal(3, stepper.StepCount);
    }

    [Fact]
    public void StepFrames_WithZeroOrNegativeCount_ChangesNothing()
    {
        var transport = NewTransport();

        transport.StepFrames(0);
        transport.StepFrames(-2);

        Assert.Equal(CutscenePreviewTransportMode.Running, transport.Mode);
        Assert.Equal(0, transport.PendingFrameSteps);
    }

    [Fact]
    public void StepFrames_WhenNoPlayRuns_DoesNoStep()
    {
        var transport = NewTransport();
        var stepper = new FakeStepper { IsPlaying = false };

        transport.StepFrames(2);
        int steps = transport.Advance(stepper, 0.05f);

        Assert.Equal(0, steps);
        Assert.Equal(0, transport.PendingFrameSteps);
    }

    [Fact]
    public void Seek_TwoSecondsFromZero_Does120StepsThenPauses()
    {
        var transport = NewTransport();
        var stepper = new FakeStepper();

        transport.Seek(2.0, stepper.PlayElapsedSeconds);

        Assert.Equal(CutscenePreviewTransportMode.Seeking, transport.Mode);
        Assert.Equal(2.0, transport.TargetSeconds);

        int steps = transport.Advance(stepper, 0.05f);

        Assert.Equal(120, steps);
        Assert.Equal(120, stepper.StepCount);
        Assert.Equal(CutscenePreviewTransportMode.Paused, transport.Mode);
        Assert.InRange(stepper.PlayElapsedSeconds, 2.0 - OneStep, 2.0 + OneStep);
        Assert.All(stepper.Steps, step => Assert.Equal(CutscenePreviewTransport.StepSeconds, step));
    }

    [Fact]
    public void Seek_ToATimeAlreadyReached_PausesWithoutAnyStep()
    {
        var transport = NewTransport();
        var stepper = new FakeStepper { PlayElapsedSeconds = 1.0 };

        transport.Seek(1.0 + OneStep / 4.0, stepper.PlayElapsedSeconds);

        Assert.Equal(CutscenePreviewTransportMode.Paused, transport.Mode);
        Assert.Equal(0, transport.Advance(stepper, 0.05f));
        Assert.Equal(0, stepper.StepCount);
    }

    [Fact]
    public void Seek_ToANegativeTime_AimsAtZero()
    {
        var transport = NewTransport();

        transport.Seek(-3.0, 0.0);

        Assert.Equal(0.0, transport.TargetSeconds);
        Assert.Equal(CutscenePreviewTransportMode.Paused, transport.Mode);
    }

    [Fact]
    public void Advance_WhenSeekingWithAnExhaustedBudget_DoesExactlyOneStepAndKeepsSeeking()
    {
        long now = 0;
        var transport = NewTransport(() => now += 1000); // every read of the clock costs a whole second
        var stepper = new FakeStepper();
        transport.Seek(2.0, 0.0);

        int steps = transport.Advance(stepper, 0.05f);

        Assert.Equal(1, steps);
        Assert.Equal(CutscenePreviewTransportMode.Seeking, transport.Mode);
    }

    [Fact]
    public void Advance_WhenSeekingWithAnExhaustedBudget_ReachesTheTargetOverSeveralCalls()
    {
        long now = 0;
        var transport = NewTransport(() => now += 1000);
        var stepper = new FakeStepper();
        transport.Seek(0.1, 0.0);

        int calls = 0;
        while (transport.Mode == CutscenePreviewTransportMode.Seeking && calls < 100)
        {
            transport.Advance(stepper, 0.05f);
            calls++;
        }

        Assert.Equal(CutscenePreviewTransportMode.Paused, transport.Mode);
        Assert.Equal(6, stepper.StepCount);
        Assert.True(calls > 1);
    }

    [Fact]
    public void Advance_WhenSeekingWithRoomInTheBudget_StopsAtTheBudget()
    {
        long now = 0;
        var transport = NewTransport(() => now += 3); // 8 ms of budget at 1000 ticks per second is 8 ticks
        var stepper = new FakeStepper();
        transport.Seek(2.0, 0.0);

        int steps = transport.Advance(stepper, 0.05f);

        Assert.InRange(steps, 2, 5);
        Assert.Equal(CutscenePreviewTransportMode.Seeking, transport.Mode);
    }

    [Fact]
    public void Advance_WhenThePlayEndsDuringASeek_StopsStepping()
    {
        var transport = NewTransport();
        var stepper = new FakeStepper { EndsAtSeconds = 1.0 };
        transport.Seek(5.0, 0.0);

        int steps = transport.Advance(stepper, 0.05f);

        Assert.Equal(60, steps);
        Assert.False(stepper.IsPlaying);
        Assert.Equal(CutscenePreviewTransportMode.Paused, transport.Mode);
        Assert.Equal(0, transport.Advance(stepper, 0.05f));
        Assert.Equal(60, stepper.StepCount);
    }

    [Fact]
    public void Advance_WhenSeekingAndNoPlayRuns_PausesWithoutAStep()
    {
        var transport = NewTransport();
        var stepper = new FakeStepper { IsPlaying = false };
        transport.Seek(1.0, 0.0);

        int steps = transport.Advance(stepper, 0.05f);

        Assert.Equal(0, steps);
        Assert.Equal(CutscenePreviewTransportMode.Paused, transport.Mode);
    }

    [Fact]
    public void Pause_DuringASeek_AbandonsItAndKeepsTheReachedTime()
    {
        long now = 0;
        var transport = NewTransport(() => now += 1000);
        var stepper = new FakeStepper();
        transport.Seek(2.0, 0.0);
        transport.Advance(stepper, 0.05f);

        transport.Pause();
        int steps = transport.Advance(stepper, 0.05f);

        Assert.Equal(0, steps);
        Assert.Equal(1, stepper.StepCount);
        Assert.Equal(CutscenePreviewTransportMode.Paused, transport.Mode);
    }

    [Fact]
    public void Seek_DuringASeek_ReplacesTheTarget()
    {
        long now = 0;
        var transport = NewTransport(() => now += 1000);
        var stepper = new FakeStepper();
        transport.Seek(2.0, 0.0);
        transport.Advance(stepper, 0.05f);

        transport.Seek(0.5, stepper.PlayElapsedSeconds);

        Assert.Equal(0.5, transport.TargetSeconds);
        Assert.Equal(CutscenePreviewTransportMode.Seeking, transport.Mode);
    }

    [Fact]
    public void Start_AfterASeek_GoesBackToRunning()
    {
        var transport = NewTransport();
        transport.Seek(2.0, 0.0);

        transport.Start();

        Assert.Equal(CutscenePreviewTransportMode.Running, transport.Mode);
        Assert.Equal(0.0, transport.TargetSeconds);
    }

    [Fact]
    public void Constructor_RejectsAnInvalidClock()
    {
        Assert.Throws<ArgumentNullException>(() => new CutscenePreviewTransport(null!, 1000));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CutscenePreviewTransport(() => 0, 0));
    }

    [Fact]
    public void Advance_WithARealWorld_SeekToOneAndAHalfSecondsLeavesThePlayRunning()
    {
        var world = new World();
        world.CutsceneDirector.Play(new CutsceneAsset
        {
            Name = "Waits",
            RootAction = new SequenceCutsceneActionData
            {
                Actions =
                {
                    new WaitCutsceneActionData { Seconds = 4f },
                    new WaitCutsceneActionData { Seconds = 1f }
                }
            }
        });
        var stepper = new WorldStepper(world);
        var transport = new CutscenePreviewTransport();
        transport.Seek(1.5, stepper.PlayElapsedSeconds);

        int calls = 0;
        while (transport.Mode == CutscenePreviewTransportMode.Seeking && calls < 1000)
        {
            transport.Advance(stepper, 0.016f);
            calls++;
        }

        Assert.Equal(CutscenePreviewTransportMode.Paused, transport.Mode);
        Assert.InRange(world.CutsceneDirector.PlayElapsedSeconds, 1.5 - OneStep, 1.5 + OneStep);
        Assert.True(world.CutsceneDirector.IsPlaying);
        Assert.Equal(CutsceneRuntimeState.Playing, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void Advance_WithARealWorld_PausedFreezesThePlayAndFrameStepsAdvanceItByOneFrame()
    {
        var world = new World();
        world.CutsceneDirector.Play(new CutsceneAsset
        {
            Name = "Wait",
            RootAction = new WaitCutsceneActionData { Seconds = 4f }
        });
        var stepper = new WorldStepper(world);
        var transport = new CutscenePreviewTransport();

        transport.Advance(stepper, 0.5f);
        double before = world.CutsceneDirector.PlayElapsedSeconds;
        transport.Pause();
        transport.Advance(stepper, 0.5f);

        Assert.Equal(before, world.CutsceneDirector.PlayElapsedSeconds);

        transport.StepFrames(2);
        transport.Advance(stepper, 0.5f);

        Assert.Equal(before + 2 * OneStep, world.CutsceneDirector.PlayElapsedSeconds, 5);
        Assert.Equal(2, world.CutsceneDirector.PlayElapsedFrames - 1);
    }

    public static TheoryData<bool, CutscenePreviewTransportMode, CutsceneRuntimeState, bool, CutscenePreviewStatus> StatusRows => new()
    {
        { true, CutscenePreviewTransportMode.Seeking, CutsceneRuntimeState.Playing, false, CutscenePreviewStatus.Seeking },
        { true, CutscenePreviewTransportMode.Paused, CutsceneRuntimeState.Playing, false, CutscenePreviewStatus.Paused },
        { true, CutscenePreviewTransportMode.Running, CutsceneRuntimeState.Playing, false, CutscenePreviewStatus.Playing },
        { false, CutscenePreviewTransportMode.Running, CutsceneRuntimeState.Completed, false, CutscenePreviewStatus.Ended },
        { false, CutscenePreviewTransportMode.Running, CutsceneRuntimeState.Failed, false, CutscenePreviewStatus.Ended },
        { false, CutscenePreviewTransportMode.Running, CutsceneRuntimeState.Stopped, true, CutscenePreviewStatus.Ended },
        { false, CutscenePreviewTransportMode.Running, CutsceneRuntimeState.Stopped, false, CutscenePreviewStatus.Stopped },
        { false, CutscenePreviewTransportMode.Running, CutsceneRuntimeState.Idle, false, CutscenePreviewStatus.Stopped },
        { false, CutscenePreviewTransportMode.Running, CutsceneRuntimeState.Invalid, false, CutscenePreviewStatus.Stopped },
        { false, CutscenePreviewTransportMode.Paused, CutsceneRuntimeState.Completed, false, CutscenePreviewStatus.Ended },
        { false, CutscenePreviewTransportMode.Seeking, CutsceneRuntimeState.Completed, false, CutscenePreviewStatus.Ended },
        { false, CutscenePreviewTransportMode.Seeking, CutsceneRuntimeState.Failed, false, CutscenePreviewStatus.Ended },
        { false, CutscenePreviewTransportMode.Paused, CutsceneRuntimeState.Stopped, true, CutscenePreviewStatus.Ended },
        { false, CutscenePreviewTransportMode.Paused, CutsceneRuntimeState.Idle, false, CutscenePreviewStatus.Stopped },
    };

    [Theory]
    [MemberData(nameof(StatusRows))]
    public void GetStatus_FollowsTheTableOfTheTransportRow(
        bool isPlaying,
        CutscenePreviewTransportMode mode,
        CutsceneRuntimeState state,
        bool endedByWorldChange,
        CutscenePreviewStatus expected)
    {
        Assert.Equal(expected, CutscenePreviewTransport.GetStatus(isPlaying, mode, state, endedByWorldChange));
    }
}
