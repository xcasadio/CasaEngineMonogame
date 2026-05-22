using System.Collections;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting.Coroutines;
using Xunit;

namespace CasaEngine.Tests.Scripting.Coroutines;

public sealed class CoroutineWorldOwnershipTests
{
    [Fact]
    public void WorldClearStopsAllCoroutinesOwnedByWorldManager()
    {
        var world = new World();

        CoroutineHandle handle = world.CoroutineManager.StartCoroutine(LoopForever());
        world.CoroutineManager.Update(Context(1));

        world.Clear();

        Assert.False(world.CoroutineManager.IsRunning(handle));
    }

    [Fact]
    public void EntityDestroyStopsEntityAndComponentOwnedCoroutines()
    {
        var world = new World();
        var entity = new Entity();
        var component = new TestComponent();
        entity.AddComponent(component);
        entity.InitializeWithWorld(world);

        CoroutineHandle entityHandle = world.CoroutineManager.StartCoroutine(LoopForever(), entity);
        CoroutineHandle componentHandle = world.CoroutineManager.StartCoroutine(LoopForever(), component);
        world.CoroutineManager.Update(Context(1));

        entity.Destroy();

        Assert.False(world.CoroutineManager.IsRunning(entityHandle));
        Assert.False(world.CoroutineManager.IsRunning(componentHandle));
    }

    [Fact]
    public void ComponentDetachStopsComponentOwnedCoroutines()
    {
        var world = new World();
        var entity = new Entity();
        var component = new TestComponent();
        entity.AddComponent(component);
        entity.InitializeWithWorld(world);

        CoroutineHandle handle = world.CoroutineManager.StartCoroutine(LoopForever(), component);
        world.CoroutineManager.Update(Context(1));

        entity.RemoveComponent(component);

        Assert.False(world.CoroutineManager.IsRunning(handle));
    }

    private static CoroutineUpdateContext Context(long frameIndex)
    {
        return new CoroutineUpdateContext(0.1f, 0.1f, 1f, frameIndex);
    }

    private static IEnumerator LoopForever()
    {
        while (true)
        {
            yield return null;
        }
    }

    private sealed class TestComponent : EntityComponent
    {
        public override EntityComponent Clone()
        {
            return new TestComponent();
        }
    }
}