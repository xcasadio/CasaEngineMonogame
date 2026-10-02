using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Physics;

/// <summary>
/// T-ENG-9 (ADR-0047): the life of <see cref="World.MovementObstacleProbe"/>, with the rules of
/// <see cref="World.CollisionField"/>: null by default, kept by <see cref="World.ClearEntities"/>, dropped by
/// <see cref="World.Clear"/>.
/// </summary>
public class MovementObstacleProbeWorldTests
{
    [Fact]
    public void World_HasNoMovementObstacleProbeByDefaultAndAcceptsOne()
    {
        var world = new World();

        Assert.Null(world.MovementObstacleProbe);

        var probe = new NeverBlockingProbe();
        world.MovementObstacleProbe = probe;

        Assert.Same(probe, world.MovementObstacleProbe);
    }

    [Fact]
    public void World_ClearEntities_KeepsTheMovementObstacleProbe()
    {
        var world = new World();
        var probe = new NeverBlockingProbe();
        world.MovementObstacleProbe = probe;

        world.ClearEntities();

        Assert.Same(probe, world.MovementObstacleProbe);
    }

    [Fact]
    public void World_Clear_DropsTheMovementObstacleProbe()
    {
        var world = new World();
        world.MovementObstacleProbe = new NeverBlockingProbe();

        world.Clear();

        Assert.Null(world.MovementObstacleProbe);
    }

    [Fact]
    public void DrawDebug_DefaultImplementation_DoesNothing()
    {
        IMovementObstacleProbe probe = new NeverBlockingProbe();

        probe.DrawDebug(null!);
    }

    private sealed class NeverBlockingProbe : IMovementObstacleProbe
    {
        public bool TryFindObstacle(Entity mover, in Vector3 candidateRootPosition, out Entity obstacle)
        {
            obstacle = null;
            return false;
        }
    }
}
