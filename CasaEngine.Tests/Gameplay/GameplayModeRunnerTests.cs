using CasaEngine.Core.Time;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Scene.World;
using Xunit;

namespace CasaEngine.Tests.Gameplay;

public sealed class GameplayModeRunnerTests
{
    [Fact]
    public void StartInitializesModeAndState()
    {
        var world = new World();
        var mode = new TestGameplayMode();
        var runner = new GameplayModeRunner();

        runner.Start(mode, new GameplayContext(world));

        Assert.Same(mode, runner.CurrentMode);
        Assert.NotNull(runner.CurrentState);
        Assert.Equal(GameplayPhase.Playing, runner.CurrentState.Phase);
        Assert.Equal(GameplayResult.Running, runner.CurrentState.Result);
        Assert.True(mode.InitializeCalled);
        Assert.True(mode.StartCalled);
        Assert.Same(world, mode.ContextWorld);
        Assert.Same(runner.CurrentState, mode.AssignedState);
    }

    [Theory]
    [InlineData(GameplayResult.Success, GameplayPhase.Success)]
    [InlineData(GameplayResult.Failure, GameplayPhase.Failure)]
    [InlineData(GameplayResult.Cancelled, GameplayPhase.Stopped)]
    public void UpdateTransitionsToTerminalResult(GameplayResult result, GameplayPhase expectedPhase)
    {
        var runner = new GameplayModeRunner();
        var mode = new TestGameplayMode
        {
            Result = result
        };

        runner.Start(mode, new GameplayContext(new World()));
        runner.Update(FrameTime.FromElapsedTime(0.25f, 1));

        Assert.Equal(1, mode.UpdateCount);
        Assert.Equal(0.25f, runner.CurrentState.ElapsedTime);
        Assert.Equal(result, runner.CurrentState.Result);
        Assert.Equal(expectedPhase, runner.CurrentState.Phase);
    }

    [Fact]
    public void PauseStopsUpdateUntilResume()
    {
        var runner = new GameplayModeRunner();
        var mode = new TestGameplayMode();

        runner.Start(mode, new GameplayContext(new World()));
        runner.Pause();
        runner.Update(FrameTime.FromElapsedTime(0.25f, 1));

        Assert.True(mode.PauseCalled);
        Assert.Equal(GameplayPhase.Paused, runner.CurrentState.Phase);
        Assert.Equal(0, mode.UpdateCount);
        Assert.Equal(0f, runner.CurrentState.ElapsedTime);

        runner.Resume();
        runner.Update(FrameTime.FromElapsedTime(0.25f, 2));

        Assert.True(mode.ResumeCalled);
        Assert.Equal(GameplayPhase.Playing, runner.CurrentState.Phase);
        Assert.Equal(1, mode.UpdateCount);
        Assert.Equal(0.25f, runner.CurrentState.ElapsedTime);
    }

    [Fact]
    public void StopClearsCurrentModeAndState()
    {
        var runner = new GameplayModeRunner();
        var mode = new TestGameplayMode();

        runner.Start(mode, new GameplayContext(new World()));
        runner.Stop();

        Assert.True(mode.StopCalled);
        Assert.Null(runner.CurrentMode);
        Assert.Null(runner.CurrentState);
    }

    [Fact]
    public void RestartResetsBaseStateAndCallsMode()
    {
        var runner = new GameplayModeRunner();
        var mode = new TestGameplayMode();

        runner.Start(mode, new GameplayContext(new World()));
        runner.Update(FrameTime.FromElapsedTime(0.25f, 1));
        runner.Restart();

        Assert.True(mode.RestartCalled);
        Assert.Equal(GameplayPhase.Playing, runner.CurrentState.Phase);
        Assert.Equal(GameplayResult.Running, runner.CurrentState.Result);
        Assert.Equal(0f, runner.CurrentState.ElapsedTime);
    }

    [Fact]
    public void WorldSetGameplayModeUpdatesRunner()
    {
        var world = new World();
        var mode = new TestGameplayMode();

        world.SetGameplayMode(mode);
        world.Update(FrameTime.FromElapsedTime(0.25f, 1));

        Assert.Same(mode, world.GameplayModeRunner.CurrentMode);
        Assert.Equal(1, mode.UpdateCount);
        Assert.Equal(0.25f, world.GameplayModeRunner.CurrentState.ElapsedTime);
    }

    private sealed class TestGameplayMode : GameplayMode
    {
        public bool InitializeCalled { get; private set; }
        public bool StartCalled { get; private set; }
        public bool PauseCalled { get; private set; }
        public bool ResumeCalled { get; private set; }
        public bool StopCalled { get; private set; }
        public bool RestartCalled { get; private set; }
        public int UpdateCount { get; private set; }
        public GameplayResult Result { get; set; } = GameplayResult.Running;
        public World ContextWorld => Context.World;
        public GameplayState AssignedState => State;

        protected override void OnInitialize()
        {
            InitializeCalled = true;
        }

        public override void Start()
        {
            StartCalled = true;
        }

        public override void Update(FrameTime frameTime)
        {
            UpdateCount++;
        }

        public override void Pause()
        {
            PauseCalled = true;
        }

        public override void Resume()
        {
            ResumeCalled = true;
        }

        public override void Stop()
        {
            StopCalled = true;
        }

        public override void Restart()
        {
            RestartCalled = true;
        }

        public override GameplayResult EvaluateResult()
        {
            return Result;
        }
    }
}