using System.Collections;
using CasaEngine.Framework.Scripting.Coroutines;
using Xunit;

namespace CasaEngine.Tests.Scripting.Coroutines;

public sealed class CoroutineManagerCoreTests
{
    [Fact]
    public void CompletedCoroutineIsNoLongerRunningAfterUpdate()
    {
        var manager = new CoroutineManager();
        bool executed = false;

        CoroutineHandle handle = manager.StartCoroutine(CompleteImmediately(() => executed = true));

        manager.Update(Context(1));

        Assert.True(executed);
        Assert.False(manager.IsRunning(handle));
    }

    [Fact]
    public void NullYieldResumesOnNextFrame()
    {
        var manager = new CoroutineManager();
        int step = 0;

        CoroutineHandle handle = manager.StartCoroutine(WaitOneFrame(() => step = 1, () => step = 2));

        manager.Update(Context(10));

        Assert.Equal(1, step);
        Assert.True(manager.IsRunning(handle));

        manager.Update(Context(10));
        Assert.Equal(1, step);
        Assert.True(manager.IsRunning(handle));

        manager.Update(Context(11));
        Assert.Equal(2, step);
        Assert.False(manager.IsRunning(handle));
    }

    [Fact]
    public void StopCoroutineStopsTargetCoroutine()
    {
        var manager = new CoroutineManager();
        int updates = 0;

        CoroutineHandle handle = manager.StartCoroutine(LoopForever(() => updates++));
        manager.StopCoroutine(handle);
        manager.Update(Context(1));

        Assert.Equal(0, updates);
        Assert.False(manager.IsRunning(handle));
    }

    [Fact]
    public void StaleHandleDoesNotStopReusedSlot()
    {
        var manager = new CoroutineManager();

        CoroutineHandle oldHandle = manager.StartCoroutine(CompleteImmediately(() => { }));
        manager.Update(Context(1));

        CoroutineHandle newHandle = manager.StartCoroutine(LoopForever(() => { }));
        manager.Update(Context(2));

        Assert.Equal(oldHandle.Slot, newHandle.Slot);
        Assert.NotEqual(oldHandle.Generation, newHandle.Generation);

        manager.StopCoroutine(oldHandle);

        Assert.False(manager.IsRunning(oldHandle));
        Assert.True(manager.IsRunning(newHandle));
    }

    [Fact]
    public void StopAllCoroutinesWithOwnerOnlyStopsMatchingOwner()
    {
        var manager = new CoroutineManager();
        var owner = new object();
        var otherOwner = new object();

        CoroutineHandle ownedHandle = manager.StartCoroutine(LoopForever(() => { }), owner);
        CoroutineHandle otherHandle = manager.StartCoroutine(LoopForever(() => { }), otherOwner);

        manager.Update(Context(1));
        manager.StopAllCoroutines(owner);

        Assert.False(manager.IsRunning(ownedHandle));
        Assert.True(manager.IsRunning(otherHandle));
    }

    private static CoroutineUpdateContext Context(long frameIndex)
    {
        return new CoroutineUpdateContext(0.1f, 0.1f, 1f, frameIndex);
    }

    private static IEnumerator CompleteImmediately(Action action)
    {
        action();
        yield break;
    }

    private static IEnumerator WaitOneFrame(Action beforeWait, Action afterWait)
    {
        beforeWait();
        yield return null;
        afterWait();
    }

    private static IEnumerator LoopForever(Action action)
    {
        while (true)
        {
            action();
            yield return null;
        }
    }
}