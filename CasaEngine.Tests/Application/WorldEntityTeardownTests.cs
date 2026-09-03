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
}
