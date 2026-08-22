using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;

namespace CasaEngine.Framework.Physics.Bepu;

/// <summary>
/// Filtering and material rules for every collidable pair, plus contact recording into a shared
/// <see cref="BepuContactBuffer"/> the engine reads back after each step.
/// </summary>
internal struct BepuNarrowPhaseCallbacks : INarrowPhaseCallbacks
{
    public CollidableProperty<BepuCollidableData> CollidableData;
    public BepuContactBuffer ContactBuffer;

    public void Initialize(Simulation simulation)
    {
        CollidableData.Initialize(simulation);
    }

    /// <summary>
    /// Channel filtering, mirroring Bullet's group/mask semantics. Kinematic-kinematic and
    /// kinematic-static pairs are accepted here (hitbox vs hurtbox, trigger vs trigger): Bepu asks the
    /// question for them, Bullet's broadphase filter would have answered the same way.
    /// </summary>
    public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
    {
        ref var dataA = ref CollidableData[a];
        ref var dataB = ref CollidableData[b];
        return (dataA.BroadphaseMask & dataB.GroupBit) != 0 && (dataB.BroadphaseMask & dataA.GroupBit) != 0;
    }

    public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB)
    {
        return true;
    }

    public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial)
        where TManifold : unmanaged, IContactManifold<TManifold>
    {
        bool createConstraint = ComputeMaterial(pair.A, pair.B, out pairMaterial);

        // A convex manifold here is a plain (non-compound) pair: record it directly. A pair involving a
        // compound reports its contacts through the child overload below with childIndex tags; this
        // generic overload then only receives the aggregated non-convex manifold for material/return.
        if (manifold.Convex)
        {
            for (int i = 0; i < manifold.Count; i++)
            {
                manifold.GetContact(i, out var offset, out var normal, out var depth, out _);
                ContactBuffer.Records.Add(new ContactRecord
                {
                    A = pair.A,
                    B = pair.B,
                    ChildA = -1,
                    ChildB = -1,
                    Offset = offset,
                    Normal = normal,
                    Depth = depth
                });
            }
        }

        return createConstraint;
    }

    public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold)
    {
        for (int i = 0; i < manifold.Count; i++)
        {
            manifold.GetContact(i, out var offset, out var normal, out var depth, out _);
            ContactBuffer.Records.Add(new ContactRecord
            {
                A = pair.A,
                B = pair.B,
                ChildA = childIndexA,
                ChildB = childIndexB,
                Offset = offset,
                Normal = normal,
                Depth = depth
            });
        }

        return ComputeMaterial(pair.A, pair.B, out _);
    }

    private bool ComputeMaterial(CollidableReference a, CollidableReference b, out PairMaterialProperties pairMaterial)
    {
        ref var dataA = ref CollidableData[a];
        ref var dataB = ref CollidableData[b];

        pairMaterial = new PairMaterialProperties
        {
            FrictionCoefficient = dataA.Friction * dataB.Friction,
            MaximumRecoveryVelocity = 2f,
            SpringSettings = new SpringSettings(30, 1)
        };

        if (dataA.IsSensor || dataB.IsSensor)
        {
            return false;
        }

        return a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic;
    }

    public void Dispose()
    {
        CollidableData.Dispose();
    }
}
