using System;
using System.Numerics;
using BepuPhysics;
using BepuUtilities;

namespace CasaEngine.Framework.Physics.Bepu;

/// <summary>
/// Per-body gravity, damping and <c>LinearFactor</c> (plane lock) applied every integration pass.
/// <c>AngularFactor</c> is baked once into the dynamic body's inverse inertia tensor at creation
/// instead (see <see cref="BepuBodyBackend"/>), since it never needs to change at runtime.
/// </summary>
internal struct BepuPoseIntegratorCallbacks : IPoseIntegratorCallbacks
{
    public CollidableProperty<BepuCollidableData> CollidableData;
    public Vector3 Gravity;

    private Bodies _bodies;

    public readonly AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;

    public readonly bool AllowSubstepsForUnconstrainedBodies => false;

    public readonly bool IntegrateVelocityForKinematics => false;

    public void Initialize(Simulation simulation)
    {
        _bodies = simulation.Bodies;
    }

    public void PrepareForIntegration(float dt)
    {
    }

    public void IntegrateVelocity(
        System.Numerics.Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation, BodyInertiaWide localInertia,
        System.Numerics.Vector<int> integrationMask, int workerIndex, System.Numerics.Vector<float> dt, ref BodyVelocityWide velocity)
    {
        int laneCount = System.Numerics.Vector<int>.Count;
        var indexToHandle = _bodies.ActiveSet.IndexToHandle;

        for (int lane = 0; lane < laneCount; lane++)
        {
            if (integrationMask[lane] == 0)
            {
                continue;
            }

            var handle = indexToHandle[bodyIndices[lane]];
            ref var data = ref CollidableData[handle];
            float laneDt = dt[lane];

            Vector3Wide.ReadSlot(ref velocity.Linear, lane, out var linear);

            if (data.ApplyGravity)
            {
                linear += Gravity * laneDt;
            }

            if (data.LinearDamping != 0f)
            {
                linear *= MathF.Pow(Math.Clamp(1f - data.LinearDamping, 0f, 1f), laneDt);
            }

            if (data.LinearFactor.X == 0f)
            {
                linear.X = 0f;
            }

            if (data.LinearFactor.Y == 0f)
            {
                linear.Y = 0f;
            }

            if (data.LinearFactor.Z == 0f)
            {
                linear.Z = 0f;
            }

            Vector3Wide.WriteSlot(linear, lane, ref velocity.Linear);

            if (data.AngularDamping != 0f)
            {
                Vector3Wide.ReadSlot(ref velocity.Angular, lane, out var angular);
                angular *= MathF.Pow(Math.Clamp(1f - data.AngularDamping, 0f, 1f), laneDt);
                Vector3Wide.WriteSlot(angular, lane, ref velocity.Angular);
            }
        }
    }
}
