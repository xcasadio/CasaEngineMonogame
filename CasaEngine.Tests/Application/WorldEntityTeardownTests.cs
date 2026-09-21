using System;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Application;

/// <summary>
/// Pins that discarding an entity releases what its components own.
///
/// Entity.Destroy only raises flags, so before this a component holding GPU or engine resources kept
/// them for the lifetime of the process. TileMapComponent is the case that surfaced it: its per-chunk
/// vertex and index buffers are built lazily on the first draw of each world and released only from
/// InitializeWithWorld or Detach, and a world switch builds fresh components - so every map change
/// stranded the previous map's buffers, still registered on the graphics device. Several changes in,
/// the tile draw started abandoning whole chunks, silently, and the map showed growing black patches.
/// </summary>
public class WorldEntityTeardownTests
{
    private sealed class DetachRecordingComponent : EntityComponent
    {
        public int DetachCount { get; private set; }

        public override void Detach()
        {
            DetachCount++;
            base.Detach();
        }

        public override EntityComponent Clone() => new DetachRecordingComponent();
    }

    private static (World World, DetachRecordingComponent Component) BuildWorldWithOneEntity()
    {
        var world = new World { Name = "TeardownWorld" };
        var entity = new Entity { Name = "Carrier", RootComponent = new LightComponent() };
        var component = new DetachRecordingComponent();
        entity.AddComponent(component);
        // AddEntity only QUEUES (World.AddEntity: _baseObjectsToAdd), and the queue is drained on an
        // update this headless test never runs - the same immediate path GameManagerRestoreWorldTests
        // uses puts the entity straight into World.Entities, which is what ClearEntities walks.
        EditorWorldEditingService.AddEntityReference(world, new EntityReference
        {
            AssetId = Guid.Empty,
            Entity = entity,
        });
        return (world, component);
    }

    [Fact]
    public void ClearEntities_DetachesEveryComponentOfTheDiscardedEntities()
    {
        var (world, component) = BuildWorldWithOneEntity();

        world.ClearEntities();

        Assert.Equal(1, component.DetachCount);
    }

    [Fact]
    public void Clear_DetachesEveryComponentOfTheDiscardedEntities()
    {
        var (world, component) = BuildWorldWithOneEntity();

        world.Clear();

        Assert.Equal(1, component.DetachCount);
    }

    // ---- ADR-0037: the whole tree of a discarded entity is detached ----

    private sealed class DetachRecordingSceneComponent : LightComponent
    {
        public int DetachCount { get; private set; }

        public override void Detach()
        {
            DetachCount++;
            base.Detach();
        }
    }

    private sealed class Tree
    {
        public World World;
        public Entity Entity;
        public DetachRecordingSceneComponent Root;
        public DetachRecordingComponent Listed;
        public DetachRecordingComponent OnChild;
    }

    /// <summary>A root component that records its detaches - a tile map is placed there in the Alundra port -,
    /// a component in the entity's list, and a component on a child entity.</summary>
    private static Tree BuildWorldWithATree()
    {
        var tree = new Tree
        {
            World = new World { Name = "TreeTeardownWorld" },
            Root = new DetachRecordingSceneComponent(),
            Listed = new DetachRecordingComponent(),
            OnChild = new DetachRecordingComponent(),
        };

        tree.Entity = new Entity { Name = "Parent", RootComponent = tree.Root };
        tree.Entity.AddComponent(tree.Listed);

        var child = new Entity { Name = "Child", RootComponent = new LightComponent() };
        child.AddComponent(tree.OnChild);
        tree.Entity.AddChild(child);

        EditorWorldEditingService.AddEntityReference(tree.World, new EntityReference
        {
            AssetId = Guid.Empty,
            Entity = tree.Entity,
        });
        return tree;
    }

    private static void AssertEachDetachedOnce(Tree tree)
    {
        Assert.Equal(1, tree.Root.DetachCount);
        Assert.Equal(1, tree.Listed.DetachCount);
        Assert.Equal(1, tree.OnChild.DetachCount);
    }

    [Fact]
    public void ClearEntities_DetachesTheRootComponent_TheListedComponents_AndTheChildEntities_Once()
    {
        var tree = BuildWorldWithATree();

        tree.World.ClearEntities();

        AssertEachDetachedOnce(tree);
    }

    [Fact]
    public void Clear_DetachesTheRootComponent_TheListedComponents_AndTheChildEntities_Once()
    {
        var tree = BuildWorldWithATree();

        tree.World.Clear();

        AssertEachDetachedOnce(tree);
    }

    [Fact]
    public void AnEntityDestroyedDuringPlay_IsDetachedWhenTheWorldRemovesIt()
    {
        var tree = BuildWorldWithATree();

        tree.Entity.Destroy();
        tree.World.Update(1f / 60f);

        AssertEachDetachedOnce(tree);
        Assert.DoesNotContain(tree.Entity, tree.World.Entities);

        // Clearing the world afterwards does not detach them a second time: the entity is gone.
        tree.World.Clear();
        AssertEachDetachedOnce(tree);
    }
}
