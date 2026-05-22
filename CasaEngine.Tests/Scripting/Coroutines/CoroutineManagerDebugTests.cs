using System.Collections;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting.Coroutines;
using Xunit;

namespace CasaEngine.Tests.Scripting.Coroutines;

public sealed class CoroutineManagerDebugTests
{
    [Fact]
    public void DebugInfoExposesNameOwnerInstructionAndRemainingTime()
    {
        var manager = new CoroutineManager();
        var owner = new Entity { Name = "Door" };

        CoroutineHandle handle = manager.StartCoroutine(WaitThenRun(new WaitForSeconds(1f), () => { }), owner, "OpenDoor");
        manager.Update(Context(1, 0.25f));
        manager.Update(Context(2, 0.25f));

        IReadOnlyList<CoroutineDebugInfo> debugInfos = manager.GetActiveCoroutines();

        Assert.Single(debugInfos);
        Assert.Equal(handle, debugInfos[0].Handle);
        Assert.Equal("OpenDoor", debugInfos[0].Name);
        Assert.Equal("Door", debugInfos[0].OwnerName);
        Assert.Equal(nameof(WaitForSeconds), debugInfos[0].CurrentInstruction);
        Assert.Equal("Waiting", debugInfos[0].State);
        Assert.True(debugInfos[0].IsPaused);
        Assert.Equal(0.75f, debugInfos[0].RemainingTime);
    }

    [Fact]
    public void SetCoroutineNameUpdatesDebugInfo()
    {
        var manager = new CoroutineManager();

        CoroutineHandle handle = manager.StartCoroutine(LoopForever());
        manager.SetCoroutineName(handle, "Pulse");
        manager.Update(Context(1));

        IReadOnlyList<CoroutineDebugInfo> debugInfos = manager.GetActiveCoroutines();

        Assert.Single(debugInfos);
        Assert.Equal("Pulse", debugInfos[0].Name);
    }

    [Fact]
    public void CoroutineExceptionStopsOnlyFaultedCoroutineByDefault()
    {
        var manager = new CoroutineManager();

        CoroutineHandle faultedHandle = manager.StartCoroutine(ThrowOnMoveNext());
        CoroutineHandle runningHandle = manager.StartCoroutine(LoopForever());

        manager.Update(Context(1));

        Assert.False(manager.IsRunning(faultedHandle));
        Assert.True(manager.IsRunning(runningHandle));
    }

    [Fact]
    public void StrictModeRethrowsCoroutineExceptionAfterStoppingCoroutine()
    {
        var manager = new CoroutineManager
        {
            ThrowCoroutineExceptionsInDebug = true
        };

        CoroutineHandle handle = manager.StartCoroutine(ThrowOnMoveNext());

        Assert.Throws<InvalidOperationException>(() => manager.Update(Context(1)));
        Assert.False(manager.IsRunning(handle));
    }

    [Fact]
    public void EntityHelperStartsNamedCoroutineWithEntityOwner()
    {
        var world = new World();
        var entity = new Entity { Name = "Actor" };
        entity.InitializeWithWorld(world);

        CoroutineHandle handle = entity.StartCoroutine(LoopForever(), "ActorRoutine");
        world.CoroutineManager.Update(Context(1));

        IReadOnlyList<CoroutineDebugInfo> debugInfos = world.CoroutineManager.GetActiveCoroutines();

        Assert.Single(debugInfos);
        Assert.Equal(handle, debugInfos[0].Handle);
        Assert.Equal("ActorRoutine", debugInfos[0].Name);
        Assert.Equal("Actor", debugInfos[0].OwnerName);

        entity.StopAllCoroutines();
        Assert.False(world.CoroutineManager.IsRunning(handle));
    }

    [Fact]
    public void ComponentHelperStartsNamedCoroutineWithComponentOwner()
    {
        var world = new World();
        var entity = new Entity();
        var component = new TestComponent { Name = "Mover" };
        entity.AddComponent(component);
        entity.InitializeWithWorld(world);

        CoroutineHandle handle = component.StartTestCoroutine(LoopForever(), "MoveRoutine");
        world.CoroutineManager.Update(Context(1));

        IReadOnlyList<CoroutineDebugInfo> debugInfos = world.CoroutineManager.GetActiveCoroutines();

        Assert.Single(debugInfos);
        Assert.Equal(handle, debugInfos[0].Handle);
        Assert.Equal("MoveRoutine", debugInfos[0].Name);
        Assert.Equal("Mover", debugInfos[0].OwnerName);

        component.StopAllTestCoroutines();
        Assert.False(world.CoroutineManager.IsRunning(handle));
    }

    private static CoroutineUpdateContext Context(long frameIndex, float deltaTime = 0.1f)
    {
        return new CoroutineUpdateContext(deltaTime, deltaTime, 1f, frameIndex);
    }

    private static IEnumerator WaitThenRun(ICoroutineInstruction instruction, Action action)
    {
        yield return instruction;
        action();
    }

    private static IEnumerator LoopForever()
    {
        while (true)
        {
            yield return null;
        }
    }

    private static IEnumerator ThrowOnMoveNext()
    {
        throw new InvalidOperationException("Broken coroutine.");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    private sealed class TestComponent : EntityComponent
    {
        public CoroutineHandle StartTestCoroutine(IEnumerator routine, string? name)
        {
            return StartCoroutine(routine, name);
        }

        public void StopAllTestCoroutines()
        {
            StopAllCoroutines();
        }

        public override EntityComponent Clone()
        {
            return new TestComponent();
        }
    }
}