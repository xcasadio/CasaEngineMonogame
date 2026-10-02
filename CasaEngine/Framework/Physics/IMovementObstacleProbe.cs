using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Scene.Entities;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Physics;

/// <summary>
/// Optional extension point of the field stage of <see cref="Scene.Entities.Components.CharacterControllerComponent"/>
/// (ADR-0047): lets a game declare dynamic obstacles that a moving character respects, with the exact contact of
/// ADR-0045. Installed on <see cref="Scene.World.World.MovementObstacleProbe"/>; the rule of what is an obstacle belongs
/// to the game, the engine ships no registry.
/// </summary>
/// <remarks>
/// The probe filters the field stage only: the rigid sweep and the step-up that follow do not consult it. Only the
/// arrival point of each trial is tested, so an obstacle thinner than the step can be crossed.
/// </remarks>
public interface IMovementObstacleProbe
{
    /// <summary>
    /// Whether an obstacle blocks the root of <paramref name="mover"/> at <paramref name="candidateRootPosition"/>.
    /// Consulted only in the field stage, for a non-zero horizontal axis, when the controller resolves its collision
    /// dependencies; never returns the mover itself. Contract: O(number of obstacles), no allocation, callable several
    /// times per entity and per frame, changes nothing, raises no exception on each frame.
    /// </summary>
    /// <param name="mover">The entity whose controller is moving.</param>
    /// <param name="candidateRootPosition">The candidate position of the root of <paramref name="mover"/>, in world
    /// (simulation) space.</param>
    /// <param name="obstacle">The blocking obstacle: non-null exactly when this method returns true.</param>
    bool TryFindObstacle(Entity mover, in Vector3 candidateRootPosition, out Entity obstacle);

    /// <summary>
    /// Draws the obstacles for the physics debug view (<c>DisplayPhysics</c>). Coordinates are in the simulation
    /// (logical) space of the world, the one of the physics debug view, not the projected render space. No allocation:
    /// this runs on the draw path. The default draws nothing.
    /// </summary>
    void DrawDebug(IPhysicsDebugDrawer drawer)
    {
    }
}
