using System.Collections;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting.Coroutines;
using Xunit;

namespace CasaEngine.Tests.Cutscenes;

/// <summary>
/// An exception thrown by a <see cref="CutsceneDirector.Ended"/> handler is logged with its context, like a coroutine
/// exception, and never interrupts the director: a world clear still stops every coroutine, a replacing play still
/// starts, and a play that ends normally still completes (ADR-0088). Uses the engine's log seam
/// (<see cref="Logs.AddLogger"/>/<see cref="Logs.Close"/>), so it runs in <see cref="ProjectEnvironmentCollection"/>.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class CutsceneEndedHandlerExceptionTests
{
    [Fact]
    public void AThrowingStoppedHandler_DoesNotInterruptTheWorldClear()
    {
        var world = new World();
        CoroutineHandle other = world.CoroutineManager.StartCoroutine(Forever(), new object(), "Other");
        world.CutsceneDirector.Ended += (_, _) => throw new InvalidOperationException("handler failure");
        world.CutsceneDirector.Play(CreateWaitAsset("Long", 10f));
        world.CoroutineManager.Update(Context(1));

        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            world.RuntimeSystems.Clear();
        }
        finally
        {
            Logs.Close();
        }

        Assert.False(world.CoroutineManager.IsRunning(other));
        Assert.Equal(CutsceneRuntimeState.Stopped, world.CutsceneDirector.GetDebugSnapshot().State);
        string error = Assert.Single(logger.Errors);
        Assert.Contains("Long", error);
        Assert.Contains("handler failure", error);
    }

    [Fact]
    public void AThrowingStoppedHandler_DoesNotPreventTheReplacingPlay()
    {
        var world = new World();
        CutsceneAsset first = CreateWaitAsset("First", 10f);
        CutsceneAsset second = CreateWaitAsset("Second", 0.1f);
        var ended = new List<CutsceneEndedEventArgs>();
        world.CutsceneDirector.Ended += (_, args) =>
        {
            ended.Add(args);
            if (ReferenceEquals(args.Asset, first))
            {
                throw new InvalidOperationException("handler failure");
            }
        };
        world.CutsceneDirector.Play(first);
        world.CoroutineManager.Update(Context(1));

        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            world.CutsceneDirector.Play(second);
            for (int frameIndex = 2; frameIndex <= 5; frameIndex++)
            {
                world.CoroutineManager.Update(Context(frameIndex));
            }
        }
        finally
        {
            Logs.Close();
        }

        Assert.Equal(2, ended.Count);
        Assert.Equal(CutsceneRuntimeState.Stopped, ended[0].State);
        Assert.Equal(CutsceneRuntimeState.Completed, ended[1].State);
        Assert.Same(second, ended[1].Asset);
        Assert.Single(logger.Errors);
    }

    [Fact]
    public void AThrowingCompletedHandler_IsLoggedOnce_AndThePlayStaysCompleted()
    {
        var world = new World();
        world.CutsceneDirector.Ended += (_, _) => throw new InvalidOperationException("handler failure");
        world.CutsceneDirector.Play(CreateWaitAsset("Short", 0.1f));

        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            for (int frameIndex = 1; frameIndex <= 4; frameIndex++)
            {
                world.CoroutineManager.Update(Context(frameIndex));
            }
        }
        finally
        {
            Logs.Close();
        }

        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
        string error = Assert.Single(logger.Errors);
        Assert.Contains("Short", error);
        Assert.Contains("handler failure", error);
    }

    private static CutsceneAsset CreateWaitAsset(string name, float seconds)
    {
        return new CutsceneAsset
        {
            Name = name,
            RootAction = new WaitCutsceneActionData { Seconds = seconds }
        };
    }

    private static IEnumerator Forever()
    {
        while (true)
        {
            yield return null;
        }
    }

    private static CoroutineUpdateContext Context(long frameIndex)
    {
        return new CoroutineUpdateContext(0.1f, 0.1f, 1f, frameIndex);
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Errors { get; } = new();

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) { }
        public void WriteError(string msg) => Errors.Add(msg);
    }
}
