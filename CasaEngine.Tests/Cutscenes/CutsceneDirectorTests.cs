using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting.Coroutines;
using Xunit;

namespace CasaEngine.Tests.Cutscenes;

public sealed class CutsceneDirectorTests
{
    [Fact]
    public void Play_CompletesSequenceActionsThroughWorldCoroutineManager()
    {
        var world = new World();
        CutsceneAsset asset = CreateSequenceAsset();

        world.CutsceneDirector.Play(asset);

        Assert.True(world.CutsceneDirector.IsPlaying);

        int completedFrame = AdvanceUntilNotPlaying(world, 10);

        Assert.InRange(completedFrame, 1, 10);
        CutsceneDebugSnapshot snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Completed, snapshot.State);
        Assert.False(world.CutsceneDirector.IsPlaying);
    }

    [Fact]
    public void Play_CompletesParallelActionsThroughWorldCoroutineManager()
    {
        var world = new World();
        CutsceneAsset asset = CreateParallelAsset();

        world.CutsceneDirector.Play(asset);
        int completedFrame = AdvanceUntilNotPlaying(world, 12);

        Assert.InRange(completedFrame, 1, 12);
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void Stop_StopsActiveCutsceneAndOwnedParallelChildren()
    {
        var world = new World();
        CutsceneAsset asset = CreateParallelAsset(10f, 10f);

        world.CutsceneDirector.Play(asset);
        world.CoroutineManager.Update(Context(1));
        world.CoroutineManager.Update(Context(2));

        Assert.True(world.CutsceneDirector.GetDebugSnapshot().ActiveCoroutines.Count > 0);

        world.CutsceneDirector.Stop();
        world.CoroutineManager.Update(Context(3));

        CutsceneDebugSnapshot snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Stopped, snapshot.State);
        Assert.False(world.CutsceneDirector.IsPlaying);
        Assert.Empty(snapshot.ActiveCoroutines);
    }

    [Fact]
    public void Play_InvalidAssetDoesNotStartCoroutine()
    {
        var world = new World();
        var asset = new CutsceneAsset
        {
            Name = "Invalid",
            RootAction = new WaitCutsceneActionData { Seconds = -1f }
        };

        world.CutsceneDirector.Play(asset);

        CutsceneDebugSnapshot snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.False(world.CutsceneDirector.IsPlaying);
        Assert.Equal(CutsceneRuntimeState.Invalid, snapshot.State);
        Assert.Contains(snapshot.ValidationMessages, message => message.Severity == CutsceneValidationSeverity.Error);
    }

    [Fact]
    public void WorldClearStopsCutsceneDirector()
    {
        var world = new World();
        CutsceneAsset asset = CreateParallelAsset(10f, 10f);

        world.CutsceneDirector.Play(asset);
        world.CoroutineManager.Update(Context(1));

        world.Clear();

        Assert.False(world.CutsceneDirector.IsPlaying);
        Assert.Equal(CutsceneRuntimeState.Stopped, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    private static int AdvanceUntilNotPlaying(World world, int maxFrameCount)
    {
        for (int frameIndex = 1; frameIndex <= maxFrameCount; frameIndex++)
        {
            world.CoroutineManager.Update(Context(frameIndex));
            if (!world.CutsceneDirector.IsPlaying)
            {
                return frameIndex;
            }
        }

        return maxFrameCount;
    }

    private static CoroutineUpdateContext Context(long frameIndex)
    {
        return new CoroutineUpdateContext(0.1f, 0.1f, 1f, frameIndex);
    }

    private static CutsceneAsset CreateSequenceAsset()
    {
        return new CutsceneAsset
        {
            Name = "Sequence",
            RootAction = new SequenceCutsceneActionData
            {
                Actions =
                {
                    new WaitCutsceneActionData { Seconds = 0.2f },
                    new WaitCutsceneActionData { Seconds = 0.1f }
                }
            }
        };
    }

    private static CutsceneAsset CreateParallelAsset(float firstWait = 0.2f, float secondWait = 0.3f)
    {
        return new CutsceneAsset
        {
            Name = "Parallel",
            RootAction = new ParallelCutsceneActionData
            {
                Actions =
                {
                    new WaitCutsceneActionData { Seconds = firstWait },
                    new WaitCutsceneActionData { Seconds = secondWait }
                }
            }
        };
    }
}