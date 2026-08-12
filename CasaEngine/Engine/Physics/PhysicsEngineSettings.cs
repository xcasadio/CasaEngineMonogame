using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Physics;

public class PhysicsEngineSettings
{
    public PhysicsEngineFlags Flags = PhysicsEngineFlags.ContinuousCollisionDetection;

    /// The maximum number of simulations the the physics engine can run in a frame to compensate for slowdown
    public int MaxSubSteps = 1;

    /// The length in seconds of a physics simulation frame. The default is 0.016667 (one sixtieth of a second)
    public float FixedTimeStep = 1.0f / 60.0f;

    public Vector3 Gravity { get; set; } = new(0, -9.87f, 0f);
    public bool IsPhysics2dActivated { get; set; } = true;

    /// Channel table and named collision profiles of the project.
    public CollisionProfileTable CollisionProfiles { get; } = CreateDefaultCollisionProfiles();

    private static CollisionProfileTable CreateDefaultCollisionProfiles()
    {
        var table = new CollisionProfileTable();

        table.AddChannel(CollisionProfileNames.WorldStatic);
        table.AddChannel(CollisionProfileNames.WorldDynamic);
        table.AddChannel(CollisionProfileNames.Pawn);
        table.AddChannel(CollisionProfileNames.Trigger);

        //Static bodies block everything but each other, mirroring how Bullet filters static against static.
        table.AddProfile(new CollisionProfile(CollisionProfileNames.WorldStatic, CollisionChannels.WorldStatic)
            .SetResponseToAllChannels(CollisionResponse.Block)
            .SetResponse(CollisionChannels.WorldStatic, CollisionResponse.Ignore));

        table.AddProfile(new CollisionProfile(CollisionProfileNames.WorldDynamic, CollisionChannels.WorldDynamic)
            .SetResponseToAllChannels(CollisionResponse.Block));

        table.AddProfile(new CollisionProfile(CollisionProfileNames.Pawn, CollisionChannels.Pawn)
            .SetResponseToAllChannels(CollisionResponse.Block));

        //A profile that blocks nothing is a sensor: it only raises overlap events.
        table.AddProfile(new CollisionProfile(CollisionProfileNames.Trigger, CollisionChannels.Trigger)
            .SetResponseToAllChannels(CollisionResponse.Overlap));

        return table;
    }
}
