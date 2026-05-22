using System.Collections;
using CasaEngine.Framework.Scripting.Coroutines;
using Xunit;

namespace CasaEngine.Tests.Scripting.Coroutines;

public sealed class CoroutineManagerInstructionTests
{
    [Fact]
    public void WaitForSecondsUsesScaledDeltaTime()
    {
        var manager = new CoroutineManager();
        bool completed = false;

        CoroutineHandle handle = manager.StartCoroutine(WaitThenRun(new WaitForSeconds(0.3f), () => completed = true));

        manager.Update(Context(1, 0.1f, 0.5f, 0.2f));
        manager.Update(Context(2, 0.1f, 0.5f, 0.2f));
        Assert.False(completed);
        Assert.True(manager.IsRunning(handle));

        manager.Update(Context(3, 0.1f, 0.5f, 0.2f));
        manager.Update(Context(4, 0.1f, 0.5f, 0.2f));

        Assert.True(completed);
        Assert.False(manager.IsRunning(handle));
    }

    [Fact]
    public void WaitForSecondsRealtimeUsesUnscaledDeltaTime()
    {
        var manager = new CoroutineManager();
        bool completed = false;

        CoroutineHandle handle = manager.StartCoroutine(WaitThenRun(new WaitForSecondsRealtime(1f), () => completed = true));

        manager.Update(Context(1, 0f, 0.5f, 0f));
        manager.Update(Context(2, 0f, 0.5f, 0f));
        Assert.False(completed);
        Assert.True(manager.IsRunning(handle));

        manager.Update(Context(3, 0f, 0.5f, 0f));

        Assert.True(completed);
        Assert.False(manager.IsRunning(handle));
    }

    [Fact]
    public void WaitForFramesWaitsRequestedUpdates()
    {
        var manager = new CoroutineManager();
        bool completed = false;

        CoroutineHandle handle = manager.StartCoroutine(WaitThenRun(new WaitForFrames(2), () => completed = true));

        manager.Update(Context(1));
        manager.Update(Context(2));
        Assert.False(completed);
        Assert.True(manager.IsRunning(handle));

        manager.Update(Context(3));

        Assert.True(completed);
        Assert.False(manager.IsRunning(handle));
    }

    [Fact]
    public void WaitUntilAndWaitWhileUsePredicateState()
    {
        var manager = new CoroutineManager();
        bool gate = false;
        int step = 0;

        CoroutineHandle handle = manager.StartCoroutine(Routine());

        manager.Update(Context(1));
        Assert.Equal(1, step);

        manager.Update(Context(2));
        Assert.Equal(1, step);
        gate = true;
        manager.Update(Context(3));
        Assert.Equal(2, step);

        manager.Update(Context(4));
        Assert.Equal(2, step);
        gate = false;
        manager.Update(Context(5));

        Assert.Equal(3, step);
        Assert.False(manager.IsRunning(handle));

        IEnumerator Routine()
        {
            step = 1;
            yield return new WaitUntil(() => gate);
            step = 2;
            yield return new WaitWhile(() => gate);
            step = 3;
        }
    }

    [Fact]
    public void NestedEnumeratorBlocksParentUntilCompleted()
    {
        var manager = new CoroutineManager();
        int step = 0;

        CoroutineHandle handle = manager.StartCoroutine(Parent());
        manager.Update(Context(1));
        Assert.Equal(2, step);
        Assert.True(manager.IsRunning(handle));

        manager.Update(Context(2));
        Assert.Equal(3, step);
        Assert.False(manager.IsRunning(handle));

        IEnumerator Parent()
        {
            step = 1;
            yield return Child();
            step = 3;
        }

        IEnumerator Child()
        {
            step = 2;
            yield return null;
        }
    }

    [Fact]
    public void CoroutineHandleYieldWaitsForTargetCoroutine()
    {
        var manager = new CoroutineManager();
        bool parentCompleted = false;

        CoroutineHandle childHandle = manager.StartCoroutine(WaitThenRun(new WaitForFrames(2), () => { }));
        CoroutineHandle parentHandle = manager.StartCoroutine(WaitForHandleThenRun(childHandle, () => parentCompleted = true));

        manager.Update(Context(1));
        manager.Update(Context(2));
        Assert.False(parentCompleted);
        Assert.True(manager.IsRunning(parentHandle));

        manager.Update(Context(3));
        manager.Update(Context(4));

        Assert.True(parentCompleted);
        Assert.False(manager.IsRunning(parentHandle));
    }

    [Fact]
    public void CompletedCoroutineHandleYieldResumesImmediately()
    {
        var manager = new CoroutineManager();
        bool completed = false;

        CoroutineHandle completedHandle = manager.StartCoroutine(CompleteImmediately());
        manager.Update(Context(1));

        CoroutineHandle parentHandle = manager.StartCoroutine(WaitForHandleThenRun(completedHandle, () => completed = true));
        manager.Update(Context(2));

        Assert.True(completed);
        Assert.False(manager.IsRunning(parentHandle));
    }

    [Fact]
    public void InvalidCoroutineHandleYieldResumesImmediatelyByDefault()
    {
        var manager = new CoroutineManager();
        bool completed = false;

        CoroutineHandle handle = manager.StartCoroutine(WaitForHandleThenRun(CoroutineHandle.Invalid, () => completed = true));
        manager.Update(Context(1));

        Assert.True(completed);
        Assert.False(manager.IsRunning(handle));
    }

    [Fact]
    public void CoroutineCannotWaitForItself()
    {
        var manager = new CoroutineManager();
        bool completed = false;
        CoroutineHandle handle = CoroutineHandle.Invalid;

        handle = manager.StartCoroutine(SelfWait());
        manager.Update(Context(1));

        Assert.False(completed);
        Assert.False(manager.IsRunning(handle));

        IEnumerator SelfWait()
        {
            yield return handle;
            completed = true;
        }
    }

    private static CoroutineUpdateContext Context(long frameIndex, float deltaTime = 0.1f, float unscaledDeltaTime = 0.1f, float timeScale = 1f)
    {
        return new CoroutineUpdateContext(deltaTime, unscaledDeltaTime, timeScale, frameIndex);
    }

    private static IEnumerator WaitThenRun(ICoroutineInstruction instruction, Action action)
    {
        yield return instruction;
        action();
    }

    private static IEnumerator WaitForHandleThenRun(CoroutineHandle handle, Action action)
    {
        yield return handle;
        action();
    }

    private static IEnumerator CompleteImmediately()
    {
        yield break;
    }
}