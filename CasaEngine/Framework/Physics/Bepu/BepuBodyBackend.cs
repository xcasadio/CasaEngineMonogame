using System;
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities;
using CasaEngine.Engine.Physics;
using Microsoft.Xna.Framework;
using Matrix = Microsoft.Xna.Framework.Matrix;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace CasaEngine.Framework.Physics.Bepu;

internal enum BepuMobility
{
    Static,
    Kinematic,
    Dynamic
}

/// <summary>
/// Backend of one <see cref="PhysicsBody"/>: a description (shape, pose, profile, mobility, activity)
/// kept alive whether or not the body is currently inserted into the simulation, plus a nullable
/// handle. Mirrors the Bullet backend's "ghost created out of the world, added/removed at will" model,
/// which Bepu has no direct equivalent for.
/// </summary>
internal sealed class BepuBodyBackend : IPhysicsBodyBackend
{
    private readonly BepuPhysicsEngine _engine;
    private BodyHandle? _bodyHandle;
    private StaticHandle? _staticHandle;
    private bool _disposed;

    public BepuMobility Mobility { get; }
    public RigidPose Pose;
    public TypedIndex ShapeIndex { get; }
    public BodyInertia Inertia { get; set; }
    public BodyActivityDescription Activity { get; set; } = new(-1f);
    public ContinuousDetection Continuity { get; set; } = ContinuousDetection.Passive;

    /// <summary>The <see cref="ICollideableComponent"/> or plain object the caller associated with this body.</summary>
    public object UserObject { get; }

    public ResolvedCollisionProfile CollisionProfile { get; }

    /// <summary>Tags per fixture; a single-fixture, non-compound body still has a length-1 array.</summary>
    private readonly string[] _fixtureTags;

    /// <summary>Local pose of each fixture; null for a non-compound (single, identity-pose) body.</summary>
    private readonly Matrix[] _fixtureLocalTransforms;

    private readonly bool _applyGravity;
    private readonly float _linearDamping;
    private readonly float _angularDamping;
    private readonly Vector3 _linearFactor;
    private readonly float _friction;
    private readonly bool _isSensor;

    public bool IsRigidBody { get; }

    public bool IsCompound => _fixtureLocalTransforms != null;

    /// <summary>Child shapes borrowed from the shape cache; null for a non-compound body, whose single
    /// shape is <see cref="ShapeIndex"/> itself.</summary>
    public TypedIndex[] CompoundChildShapeIndices { get; }

    public int FixtureCount => _fixtureTags.Length;

    public bool HasContactResponse => !_isSensor;

    internal BodyHandle? BodyHandleOrNull => _bodyHandle;
    internal StaticHandle? StaticHandleOrNull => _staticHandle;

    public BepuBodyBackend(
        BepuPhysicsEngine engine,
        BepuMobility mobility,
        bool isRigidBody,
        RigidPose pose,
        TypedIndex shapeIndex,
        object userObject,
        ResolvedCollisionProfile collisionProfile,
        string[] fixtureTags,
        Matrix[] fixtureLocalTransforms,
        TypedIndex[] compoundChildShapeIndices,
        bool isSensor,
        bool applyGravity,
        float linearDamping,
        float angularDamping,
        Vector3 linearFactor,
        float friction)
    {
        _engine = engine;
        Mobility = mobility;
        IsRigidBody = isRigidBody;
        Pose = pose;
        ShapeIndex = shapeIndex;
        UserObject = userObject;
        CollisionProfile = collisionProfile;
        _fixtureTags = fixtureTags;
        _fixtureLocalTransforms = fixtureLocalTransforms;
        CompoundChildShapeIndices = compoundChildShapeIndices;
        _isSensor = isSensor;
        _applyGravity = applyGravity;
        _linearDamping = linearDamping;
        _angularDamping = angularDamping;
        _linearFactor = linearFactor;
        _friction = friction;
    }

    public Matrix GetFixtureLocalTransform(int index)
    {
        return _fixtureLocalTransforms != null ? _fixtureLocalTransforms[index] : Matrix.Identity;
    }

    public string GetFixtureTag(int index)
    {
        return index >= 0 && index < _fixtureTags.Length ? _fixtureTags[index] : null;
    }

    /// <summary>
    /// Tag of the fixture a contact or query result touched, from a possibly out-of-range or unknown
    /// (-1) compound child index. A single-fixture body is always attributable, whatever the backend
    /// reported (or failed to report, as sweeps do for compound children).
    /// </summary>
    public string ResolveFixtureTag(int childIndex)
    {
        if (_fixtureTags.Length == 1)
        {
            return _fixtureTags[0];
        }

        return childIndex >= 0 && childIndex < _fixtureTags.Length ? _fixtureTags[childIndex] : null;
    }

    public bool IsInSimulation => _bodyHandle.HasValue || _staticHandle.HasValue;

    public Matrix WorldTransform
    {
        get
        {
            if (_staticHandle.HasValue)
            {
                return _engine.Simulation.Statics[_staticHandle.Value].Pose.ToMatrix();
            }

            if (_bodyHandle.HasValue)
            {
                return _engine.Simulation.Bodies[_bodyHandle.Value].Pose.ToMatrix();
            }

            return Pose.ToMatrix();
        }
        set
        {
            Pose = value.ToRigidPose();

            if (_staticHandle.HasValue)
            {
                // Pose only: never touches the broad phase bounds. Matches the Bullet contract encoded by
                // PhysicsBroadphaseAabbTests: a moved body isn't found by a sweep until RefreshAabb runs.
                _engine.Simulation.Statics[_staticHandle.Value].Pose = Pose;
            }
            else if (_bodyHandle.HasValue)
            {
                var bodyReference = _engine.Simulation.Bodies[_bodyHandle.Value];
                bodyReference.Pose = Pose;
                bodyReference.Awake = true;
            }
        }
    }

    public Vector3 LinearVelocity
    {
        get
        {
            if (!_bodyHandle.HasValue || Mobility != BepuMobility.Dynamic)
            {
                return Vector3.Zero;
            }

            return _engine.Simulation.Bodies[_bodyHandle.Value].Velocity.Linear;
        }
        set
        {
            if (!_bodyHandle.HasValue || Mobility != BepuMobility.Dynamic)
            {
                return;
            }

            _engine.Simulation.Bodies[_bodyHandle.Value].Velocity.Linear = value.ToNumerics();
        }
    }

    public void ApplyImpulse(Vector3 impulse, Vector3 relativePosition)
    {
        if (!_bodyHandle.HasValue || Mobility != BepuMobility.Dynamic)
        {
            return;
        }

        var bodyReference = _engine.Simulation.Bodies[_bodyHandle.Value];
        var impulseN = impulse.ToNumerics();
        var relativeN = relativePosition.ToNumerics();

        ref var velocity = ref bodyReference.Velocity;
        velocity.Linear += impulseN * bodyReference.LocalInertia.InverseMass;

        if (relativeN != System.Numerics.Vector3.Zero)
        {
            var torque = System.Numerics.Vector3.Cross(relativeN, impulseN);
            bodyReference.ComputeInverseInertia(out var worldInverseInertia);
            velocity.Angular += Symmetric3x3.Transform(torque, worldInverseInertia);
        }

        ref var data = ref _engine.CollidableData[_bodyHandle.Value];
        if (data.LinearFactor.X == 0f)
        {
            velocity.Linear.X = 0f;
        }

        if (data.LinearFactor.Y == 0f)
        {
            velocity.Linear.Y = 0f;
        }

        if (data.LinearFactor.Z == 0f)
        {
            velocity.Linear.Z = 0f;
        }

        bodyReference.Awake = true;
    }

    /// <summary>
    /// The unique refresh of the broad phase bounds. A static's refresh also wakes any dynamic that was
    /// resting on the old or the new position (Bepu's default <c>StaticsShouldntAwakenKinematics</c> filter).
    /// </summary>
    public void RefreshAabb()
    {
        if (_staticHandle.HasValue)
        {
            var statics = _engine.Simulation.Statics;
            statics.ApplyDescription(_staticHandle.Value, statics.GetDescription(_staticHandle.Value));
        }
        else if (_bodyHandle.HasValue)
        {
            _engine.Simulation.Bodies.UpdateBounds(_bodyHandle.Value);
        }
    }

    public void InsertIntoSimulation()
    {
        if (IsInSimulation)
        {
            return;
        }

        if (Mobility == BepuMobility.Static)
        {
            var description = new StaticDescription(Pose, ShapeIndex, Continuity);
            _staticHandle = _engine.Simulation.Statics.Add(description);
            _engine.RegisterStatic(_staticHandle.Value, this);
            WriteCollidableData(ref _engine.CollidableData.Allocate(_staticHandle.Value));
        }
        else
        {
            var collidable = new CollidableDescription(ShapeIndex, Continuity);
            var description = Mobility == BepuMobility.Dynamic
                ? BodyDescription.CreateDynamic(Pose, Inertia, collidable, Activity)
                : BodyDescription.CreateKinematic(Pose, collidable, Activity);
            _bodyHandle = _engine.Simulation.Bodies.Add(description);
            _engine.RegisterBody(_bodyHandle.Value, this);
            WriteCollidableData(ref _engine.CollidableData.Allocate(_bodyHandle.Value));
        }
    }

    public void RemoveFromSimulation()
    {
        //A disposed engine has already torn its simulation down: the handles are dead, there is
        //nothing left to remove (a component may dispose its bodies after its world went away).
        if (_engine.IsDisposed)
        {
            _staticHandle = null;
            _bodyHandle = null;
            return;
        }

        if (_staticHandle.HasValue)
        {
            Pose = _engine.Simulation.Statics[_staticHandle.Value].Pose;
            _engine.Simulation.Statics.Remove(_staticHandle.Value);
            _engine.UnregisterStatic(_staticHandle.Value);
            _staticHandle = null;
        }
        else if (_bodyHandle.HasValue)
        {
            Pose = _engine.Simulation.Bodies[_bodyHandle.Value].Pose;
            _engine.Simulation.Bodies.Remove(_bodyHandle.Value);
            _engine.UnregisterBody(_bodyHandle.Value);
            _bodyHandle = null;
        }
    }

    private void WriteCollidableData(ref BepuCollidableData data)
    {
        data.GroupBit = CollisionProfile.GroupBit;
        data.BroadphaseMask = CollisionProfile.BroadphaseMask;
        data.IsSensor = _isSensor;
        data.ApplyGravity = _applyGravity;
        data.LinearDamping = _linearDamping;
        data.AngularDamping = _angularDamping;
        data.LinearFactor = _linearFactor.ToNumerics();
        data.Friction = _friction;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        RemoveFromSimulation();

        if (!_engine.IsDisposed)
        {
            _engine.ReleaseBodyShape(this);
        }

        _disposed = true;
    }
}
