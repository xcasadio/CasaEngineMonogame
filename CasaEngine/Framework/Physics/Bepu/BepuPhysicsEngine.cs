using System;
using System.Collections.Generic;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities.Memory;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Physics.Bepu;

/// <summary>
/// bepuphysics2 backend behind <see cref="PhysicsWorld"/>. Same public/internal surface as the Bullet
/// backend it replaces for the members <c>PhysicsWorld</c> relays; see the migration analysis for the
/// concept mapping this implements.
/// </summary>
public sealed class BepuPhysicsEngine
{
    private const float ContactTolerance = 0.02f;

    private readonly CollisionProfileTable _collisionProfiles;
    private readonly BufferPool _pool;
    private readonly BepuShapeCache _shapeCache;
    private readonly BepuContactBuffer _contactBuffer = new();

    private readonly List<BepuBodyBackend> _bodyBackends = new();
    private readonly List<BepuBodyBackend> _staticBackends = new();

    /// <summary>Inserted dynamic bodies with a locked linear axis, re-clamped after every step.</summary>
    private readonly List<BepuBodyBackend> _linearLockedBodies = new();

    private int _insertedBodyCount;
    private int _insertedStaticCount;
    private float _accumulator;
    private bool _disposed;

    /// <summary>Continuity assigned to dynamic collidables; statics and kinematic ghosts stay
    /// <see cref="ContinuousDetection.Passive"/> regardless (they are either immobile or teleported,
    /// never swept). Driven by <see cref="PhysicsEngineFlags.ContinuousCollisionDetection"/>.</summary>
    private readonly ContinuousDetection _dynamicContinuity;

    /// <summary>True once the simulation has been torn down: bodies disposed afterwards must not touch it.</summary>
    internal bool IsDisposed => _disposed;

    // Collision bookkeeping: mirrors the Bullet backend's algorithm, simplified because a
    // (component, component) pair is now the collidable pair itself (no native-pointer identity
    // to reconcile: a compound body is a single Bepu handle regardless of its fixture count).
    private readonly HashSet<Collision> _collisions = new();
    private readonly HashSet<Collision> _outdatedCollisions = new();
    private readonly HashSet<Collision> _currentTouching = new();
    private readonly List<Collision> _pendingRemoval = new();
    private readonly Stack<HashSet<ContactPoint>> _contactsPool = new();
    private readonly Dictionary<Collision, HashSet<ContactPoint>> _contactsUpToDate = new();
    private readonly List<Collision> _markedAsNewColl = new();
    private readonly List<Collision> _markedAsDeprecatedColl = new();
    internal readonly HashSet<Collision> EndedFromComponentRemoval = new();

    internal Simulation Simulation { get; }

    internal CollidableProperty<BepuCollidableData> CollidableData { get; }

    /// <summary>Named collision profiles of the project, for the debug renderer's color fallback.</summary>
    internal CollisionProfileTable CollisionProfiles => _collisionProfiles;

    public int MaxSubSteps { get; set; }

    public float FixedTimeStep { get; set; }

    public int CollisionObjectCount => _insertedBodyCount + _insertedStaticCount;

    public BepuPhysicsEngine(PhysicsEngineSettings configuration)
    {
        MaxSubSteps = configuration.MaxSubSteps;
        FixedTimeStep = configuration.FixedTimeStep;
        _collisionProfiles = configuration.CollisionProfiles;
        _dynamicContinuity = configuration.Flags.HasFlag(PhysicsEngineFlags.ContinuousCollisionDetection)
            ? ContinuousDetection.Continuous(1e-3f, 1e-3f)
            : ContinuousDetection.Passive;

        CollidableData = new CollidableProperty<BepuCollidableData>();
        var narrowPhaseCallbacks = new BepuNarrowPhaseCallbacks
        {
            CollidableData = CollidableData,
            ContactBuffer = _contactBuffer
        };
        var poseIntegratorCallbacks = new BepuPoseIntegratorCallbacks
        {
            CollidableData = CollidableData,
            Gravity = configuration.Gravity.ToNumerics()
        };

        _pool = new BufferPool();
        Simulation = Simulation.Create(_pool, narrowPhaseCallbacks, poseIntegratorCallbacks, new SolveDescription(8, 1));
        _shapeCache = new BepuShapeCache(Simulation);
    }

    // ----- Body creation -----------------------------------------------------------------

    public PhysicsBody AddGhostObject(Shape3d shape, Vector3 localScale, ref Matrix worldMatrix, ICollideableComponent collideableComponent, int collisionProfileId, string fixtureTag = null, Color? color = null)
    {
        var physicsBody = CreateGhostObject(worldMatrix, collideableComponent, shape, localScale, collisionProfileId, fixtureTag, color);
        AddCollisionObject(physicsBody);
        return physicsBody;
    }

    public PhysicsBody AddGhostObject(IReadOnlyList<ColliderFixture> fixtures, Vector3 localScale, ref Matrix worldMatrix, ICollideableComponent collideableComponent, int collisionProfileId, Color? color = null)
    {
        var physicsBody = CreateGhostObject(worldMatrix, collideableComponent, fixtures, localScale, collisionProfileId, color);
        AddCollisionObject(physicsBody);
        return physicsBody;
    }

    public PhysicsBody CreateGhostObject(Matrix worldMatrix, ICollideableComponent collideableComponent, Shape3d shape, Vector3 localScale, int collisionProfileId, string fixtureTag = null, Color? color = null)
    {
        var shapeIndex = _shapeCache.GetOrAdd(shape, localScale);
        return CreateGhostObjectInternal(worldMatrix, collideableComponent, shapeIndex, isCompound: false, null, new[] { fixtureTag }, null, collisionProfileId, color);
    }

    public PhysicsBody CreateGhostObject(Matrix worldMatrix, ICollideableComponent collideableComponent, IReadOnlyList<ColliderFixture> fixtures, Vector3 localScale, int collisionProfileId, Color? color = null)
    {
        var built = BuildFixtureShape(fixtures, localScale);
        return CreateGhostObjectInternal(worldMatrix, collideableComponent, built.ShapeIndex, built.IsCompound, built.LocalTransforms, built.Tags, built.CompoundChildren, collisionProfileId, color);
    }

    public PhysicsBody AddRigidBody(Shape3d shape, Vector3 localScale, ref Matrix worldMatrix, object userObject, PhysicsDefinition physicsDefinition, int collisionProfileId, bool useExternalViewManagement, string fixtureTag = null)
    {
        var shapeIndex = _shapeCache.GetOrAdd(shape, localScale);
        var inertiaShape = physicsDefinition.Mass != 0f ? shape : null;
        return CreateRigidBody(shapeIndex, isCompound: false, null, new[] { fixtureTag }, null, inertiaShape, null, localScale, ref worldMatrix, userObject, physicsDefinition, collisionProfileId);
    }

    public PhysicsBody AddRigidBody(IReadOnlyList<ColliderFixture> fixtures, Vector3 localScale, ref Matrix worldMatrix, object userObject, PhysicsDefinition physicsDefinition, int collisionProfileId, bool useExternalViewManagement)
    {
        var built = BuildFixtureShape(fixtures, localScale);
        var inertiaShape = built.IsCompound ? null : fixtures[0].Shape;
        return CreateRigidBody(built.ShapeIndex, built.IsCompound, built.LocalTransforms, built.Tags, built.CompoundChildren, inertiaShape, built.CompoundShape, localScale, ref worldMatrix, userObject, physicsDefinition, collisionProfileId);
    }

    public void AddCollisionObject(PhysicsBody physicsBody)
    {
        GetBackend(physicsBody).InsertIntoSimulation();
    }

    public void RemoveCollisionObject(PhysicsBody physicsBody)
    {
        GetBackend(physicsBody).RemoveFromSimulation();
    }

    public void AddRigidBody(PhysicsBody physicsBody)
    {
        GetBackend(physicsBody).InsertIntoSimulation();
    }

    public void RemoveRigidBody(PhysicsBody physicsBody)
    {
        GetBackend(physicsBody).RemoveFromSimulation();
    }

    public void RefreshBodyAabb(PhysicsBody physicsBody)
    {
        GetBackend(physicsBody).RefreshAabb();
    }

    public void DrawDebugWorld(IPhysicsDebugDrawer debugDrawer)
    {
        BepuPhysicsDebugRenderer.Draw(this, debugDrawer);
    }

    private PhysicsBody CreateGhostObjectInternal(Matrix worldMatrix, ICollideableComponent collideableComponent, TypedIndex shapeIndex, bool isCompound, Matrix[] localTransforms, string[] tags, TypedIndex[] compoundChildren, int collisionProfileId, Color? color = null)
    {
        var collisionProfile = _collisionProfiles.GetResolved(collisionProfileId);
        var backend = new BepuBodyBackend(
            this,
            BepuMobility.Kinematic,
            isRigidBody: false,
            worldMatrix.ToRigidPose(),
            shapeIndex,
            collideableComponent,
            collisionProfile,
            tags,
            localTransforms,
            compoundChildren,
            isSensor: true, // A ghost never pushes anything back, whatever its profile blocks.
            applyGravity: false,
            linearDamping: 0f,
            angularDamping: 0f,
            linearFactor: Vector3.One,
            friction: 0f,
            debugColor: color)
        {
            Activity = new BodyActivityDescription(-1f)
        };

        return new PhysicsBody(backend, collisionProfile);
    }

    private PhysicsBody CreateRigidBody(TypedIndex shapeIndex, bool isCompound, Matrix[] localTransforms, string[] tags, TypedIndex[] compoundChildren, Shape3d inertiaShape, Compound? compoundForInertia, Vector3 localScale, ref Matrix worldMatrix, object userObject, PhysicsDefinition physicsDefinition, int collisionProfileId)
    {
        var collisionProfile = _collisionProfiles.GetResolved(collisionProfileId);
        bool isDynamic = physicsDefinition.Mass != 0f;

        var backend = new BepuBodyBackend(
            this,
            isDynamic ? BepuMobility.Dynamic : BepuMobility.Static,
            isRigidBody: true,
            worldMatrix.ToRigidPose(),
            shapeIndex,
            userObject,
            collisionProfile,
            tags,
            localTransforms,
            compoundChildren,
            isSensor: collisionProfile.IsSensor,
            applyGravity: isDynamic && physicsDefinition.ApplyGravity,
            linearDamping: physicsDefinition.LinearDamping,
            angularDamping: physicsDefinition.AngularDamping,
            linearFactor: physicsDefinition.LinearFactor,
            friction: physicsDefinition.Friction,
            debugColor: physicsDefinition.DebugColor)
        {
            Activity = isDynamic ? CreateDynamicActivity(physicsDefinition.SleepThreshold) : new BodyActivityDescription(-1f),
            Continuity = isDynamic ? _dynamicContinuity : ContinuousDetection.Passive
        };

        if (isDynamic)
        {
            var inertia = compoundForInertia.HasValue
                ? ComputeCompoundInertia(compoundForInertia.Value, compoundChildren.Length, physicsDefinition.Mass)
                : BepuShapeCache.ComputeInertia(inertiaShape, localScale, physicsDefinition.Mass);

            LockInverseInertiaAxes(ref inertia, physicsDefinition.AngularFactor);

            backend.Inertia = inertia;
        }

        var physicsBody = new PhysicsBody(backend, collisionProfile);
        backend.InsertIntoSimulation();
        return physicsBody;
    }

    /// <summary>
    /// Composite inertia of a dynamic compound: each child contributes its own shape's inertia at an
    /// equal share of the total mass (fixtures carry no per-child density), rotated and offset by its
    /// local pose via <see cref="Compound.ComputeInertia(Span{float}, Shapes)"/>. That overload computes
    /// the tensor about the compound's own origin — i.e. the body's pose — without recentering to the
    /// combined center of mass, which is required here: the body pose must stay the entity transform.
    /// </summary>
    private BodyInertia ComputeCompoundInertia(Compound compound, int childCount, float totalMass)
    {
        var masses = new float[childCount];
        float perChildMass = totalMass / childCount;
        for (int i = 0; i < childCount; i++)
        {
            masses[i] = perChildMass;
        }

        return compound.ComputeInertia(masses, Simulation.Shapes);
    }

    /// <summary>
    /// AngularFactor: zeroes the row/column of <see cref="BodyInertia.InverseInertiaTensor"/> for every
    /// locked axis, not only its diagonal term. A single axis-aligned shape at the origin has no
    /// off-diagonal terms to begin with, but a compound's composite tensor does (parallel-axis cross
    /// terms from offset children), so leaving them non-zero would let the solver apply torque around a
    /// "locked" axis through those cross terms.
    /// </summary>
    private static void LockInverseInertiaAxes(ref BodyInertia inertia, Vector3 angularFactor)
    {
        if (angularFactor.X == 0f)
        {
            inertia.InverseInertiaTensor.XX = 0f;
            inertia.InverseInertiaTensor.YX = 0f;
            inertia.InverseInertiaTensor.ZX = 0f;
        }

        if (angularFactor.Y == 0f)
        {
            inertia.InverseInertiaTensor.YY = 0f;
            inertia.InverseInertiaTensor.YX = 0f;
            inertia.InverseInertiaTensor.ZY = 0f;
        }

        if (angularFactor.Z == 0f)
        {
            inertia.InverseInertiaTensor.ZZ = 0f;
            inertia.InverseInertiaTensor.ZX = 0f;
            inertia.InverseInertiaTensor.ZY = 0f;
        }
    }

    private readonly struct FixtureShapeResult
    {
        public FixtureShapeResult(TypedIndex shapeIndex, bool isCompound, Matrix[] localTransforms, string[] tags, TypedIndex[] compoundChildren, Compound? compoundShape = null)
        {
            ShapeIndex = shapeIndex;
            IsCompound = isCompound;
            LocalTransforms = localTransforms;
            Tags = tags;
            CompoundChildren = compoundChildren;
            CompoundShape = compoundShape;
        }

        public TypedIndex ShapeIndex { get; }
        public bool IsCompound { get; }
        public Matrix[] LocalTransforms { get; }
        public string[] Tags { get; }
        public TypedIndex[] CompoundChildren { get; }

        /// <summary>The compound value itself (for a compound result), needed to compute a composite
        /// inertia tensor without going back through the shape registry.</summary>
        public Compound? CompoundShape { get; }
    }

    private FixtureShapeResult BuildFixtureShape(IReadOnlyList<ColliderFixture> fixtures, Vector3 localScale)
    {
        ArgumentNullException.ThrowIfNull(fixtures);

        if (fixtures.Count == 0)
        {
            throw new ArgumentException("A body requires at least one collider fixture.", nameof(fixtures));
        }

        if (fixtures.Count == 1 && fixtures[0].HasIdentityPose)
        {
            var single = fixtures[0];
            var shapeIndex = _shapeCache.GetOrAdd(single.Shape, localScale);
            return new FixtureShapeResult(shapeIndex, isCompound: false, null, new[] { single.Tag }, null);
        }

        var childShapeIndices = new TypedIndex[fixtures.Count];
        var tags = new string[fixtures.Count];
        var localTransforms = new Matrix[fixtures.Count];

        _pool.Take<CompoundChild>(fixtures.Count, out var children);
        for (int i = 0; i < fixtures.Count; i++)
        {
            var fixture = fixtures[i];
            var childShapeIndex = _shapeCache.GetOrAdd(fixture.Shape, localScale);
            childShapeIndices[i] = childShapeIndex;
            tags[i] = fixture.Tag;
            localTransforms[i] = fixture.GetLocalMatrix();

            var localPose = new RigidPose((fixture.LocalPosition * localScale).ToNumerics(), fixture.LocalRotation.ToNumerics());
            children[i] = new CompoundChild(localPose, childShapeIndex);
        }

        var compound = new Compound(children);
        var compoundIndex = Simulation.Shapes.Add(compound);
        return new FixtureShapeResult(compoundIndex, isCompound: true, localTransforms, tags, childShapeIndices, compound);
    }

    // ----- Queries -------------------------------------------------------------------------

    public PhysicsQueryShape CreateQueryShape(Shape3d shape, Vector3 localScaling)
    {
        return new PhysicsQueryShape(BepuPhysicsQueryShapeBackend.Create(shape, localScaling));
    }

    public HitResult ShapeSweep(PhysicsQueryShape shape, Matrix from, Matrix to, uint channelMask = ChannelMask.All, bool hitTriggers = false, ICollideableComponent ignoredComponent = null)
    {
        ArgumentNullException.ThrowIfNull(shape);

        var backend = GetQueryShapeBackend(shape);
        var pose = from.ToRigidPose();
        var velocity = new BodyVelocity((to.Translation - from.Translation).ToNumerics());

        _closestSweepHandlerStatic.Reset(this, channelMask, hitTriggers, ignoredComponent, from.Translation);
        RunSweep(backend, pose, velocity, ref _closestSweepHandlerStatic);
        return _closestSweepHandlerStatic.GetResult();
    }

    public bool ShapeSweep(PhysicsQueryShape shape, Matrix from, Matrix to, out HitResult result, uint channelMask = ChannelMask.All, bool hitTriggers = false, ICollideableComponent ignoredComponent = null)
    {
        result = ShapeSweep(shape, from, to, channelMask, hitTriggers, ignoredComponent);
        return result.Succeeded;
    }

    public HitResult ShapeSweep(Shape3d shape, Vector3 localScale, Matrix from, Matrix to, uint channelMask = ChannelMask.All, bool hitTriggers = false, ICollideableComponent ignoredComponent = null)
    {
        using var queryShape = CreateQueryShape(shape, localScale);
        return ShapeSweep(queryShape, from, to, channelMask, hitTriggers, ignoredComponent);
    }

    public bool ShapeSweep(Shape3d shape, Vector3 localScale, Matrix from, Matrix to, out HitResult result, uint channelMask = ChannelMask.All, bool hitTriggers = false, ICollideableComponent ignoredComponent = null)
    {
        using var queryShape = CreateQueryShape(shape, localScale);
        return ShapeSweep(queryShape, from, to, out result, channelMask, hitTriggers, ignoredComponent);
    }

    public void ShapeSweepPenetrating(PhysicsQueryShape shape, Matrix from, Matrix to, ICollection<HitResult> resultsOutput, uint channelMask = ChannelMask.All, bool hitTriggers = false, ICollideableComponent ignoredComponent = null)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(resultsOutput);

        var backend = GetQueryShapeBackend(shape);
        var pose = from.ToRigidPose();
        var velocity = new BodyVelocity((to.Translation - from.Translation).ToNumerics());

        _allHitsSweepHandlerStatic.Reset(this, channelMask, hitTriggers, ignoredComponent, resultsOutput);
        RunSweep(backend, pose, velocity, ref _allHitsSweepHandlerStatic);
    }

    public void ShapeSweepPenetrating(Shape3d shape, Vector3 localScale, Matrix from, Matrix to, ICollection<HitResult> resultsOutput, uint channelMask = ChannelMask.All, bool hitTriggers = false, ICollideableComponent ignoredComponent = null)
    {
        using var queryShape = CreateQueryShape(shape, localScale);
        ShapeSweepPenetrating(queryShape, from, to, resultsOutput, channelMask, hitTriggers, ignoredComponent);
    }

    public bool NearBodyWorldRayCast(ref Vector3 position, ref Vector3 feelers, out Vector3 contactPoint, out Vector3 contactNormal)
    {
        throw new NotImplementedException();
    }

    public bool WorldRayCast(ref Vector3 start, ref Vector3 end, Vector3 dir)
    {
        throw new NotImplementedException();
    }

    [ThreadStatic]
    private static ClosestSweepHitHandler _closestSweepHandlerStatic;

    [ThreadStatic]
    private static AllHitsSweepHitHandler _allHitsSweepHandlerStatic;

    private void RunSweep<THandler>(BepuPhysicsQueryShapeBackend backend, RigidPose pose, BodyVelocity velocity, ref THandler handler)
        where THandler : struct, ISweepHitHandler
    {
        const float maximumT = 1f;

        switch (backend.ShapeType)
        {
            case BepuQueryShapeType.Box:
                Simulation.Sweep(backend.Box, pose, velocity, maximumT, _pool, ref handler);
                break;
            case BepuQueryShapeType.Sphere:
                Simulation.Sweep(backend.Sphere, pose, velocity, maximumT, _pool, ref handler);
                break;
            case BepuQueryShapeType.Capsule:
                Simulation.Sweep(backend.Capsule, pose, velocity, maximumT, _pool, ref handler);
                break;
            case BepuQueryShapeType.Cylinder:
                Simulation.Sweep(backend.Cylinder, pose, velocity, maximumT, _pool, ref handler);
                break;
        }
    }

    internal bool AllowSweepTest(CollidableReference collidable, uint channelMask, bool hitTriggers, ICollideableComponent ignoredComponent)
    {
        var backend = ResolveBackend(collidable);
        if (backend == null)
        {
            return false;
        }

        if (!hitTriggers && !backend.HasContactResponse)
        {
            return false;
        }

        if (ignoredComponent != null && ReferenceEquals(backend.UserObject, ignoredComponent))
        {
            return false;
        }

        return (backend.CollisionProfile.GroupBit & channelMask) != 0;
    }

    private struct ClosestSweepHitHandler : ISweepHitHandler
    {
        private BepuPhysicsEngine _engine;
        private uint _channelMask;
        private bool _hitTriggers;
        private ICollideableComponent _ignoredComponent;
        private Vector3 _from;
        private HitResult _result;
        private bool _hasHit;

        public void Reset(BepuPhysicsEngine engine, uint channelMask, bool hitTriggers, ICollideableComponent ignoredComponent, Vector3 from)
        {
            _engine = engine;
            _channelMask = channelMask;
            _hitTriggers = hitTriggers;
            _ignoredComponent = ignoredComponent;
            _from = from;
            _result = default;
            _hasHit = false;
        }

        public bool AllowTest(CollidableReference collidable) => _engine.AllowSweepTest(collidable, _channelMask, _hitTriggers, _ignoredComponent);

        public bool AllowTest(CollidableReference collidable, int childIndex) => AllowTest(collidable);

        public void OnHit(ref float maximumT, float t, System.Numerics.Vector3 hitLocation, System.Numerics.Vector3 hitNormal, CollidableReference collidable)
        {
            _hasHit = true;
            maximumT = t;
            var backend = _engine.ResolveBackend(collidable);
            _result = new HitResult
            {
                Succeeded = true,
                Point = hitLocation,
                Normal = hitNormal,
                HitFraction = t,
                Collider = backend?.UserObject as PhysicsBaseComponent,
                Tag = backend?.ResolveFixtureTag(-1)
            };
        }

        public void OnHitAtZeroT(ref float maximumT, CollidableReference collidable)
        {
            _hasHit = true;
            maximumT = 0f;
            var backend = _engine.ResolveBackend(collidable);
            _result = new HitResult
            {
                Succeeded = true,
                Point = _from,
                Normal = Vector3.Zero,
                HitFraction = 0f,
                Collider = backend?.UserObject as PhysicsBaseComponent,
                Tag = backend?.ResolveFixtureTag(-1)
            };
        }

        public HitResult GetResult() => _hasHit ? _result : new HitResult { Succeeded = false };
    }

    private struct AllHitsSweepHitHandler : ISweepHitHandler
    {
        private BepuPhysicsEngine _engine;
        private uint _channelMask;
        private bool _hitTriggers;
        private ICollideableComponent _ignoredComponent;
        private ICollection<HitResult> _output;

        public void Reset(BepuPhysicsEngine engine, uint channelMask, bool hitTriggers, ICollideableComponent ignoredComponent, ICollection<HitResult> output)
        {
            _engine = engine;
            _channelMask = channelMask;
            _hitTriggers = hitTriggers;
            _ignoredComponent = ignoredComponent;
            _output = output;
        }

        public bool AllowTest(CollidableReference collidable) => _engine.AllowSweepTest(collidable, _channelMask, _hitTriggers, _ignoredComponent);

        public bool AllowTest(CollidableReference collidable, int childIndex) => AllowTest(collidable);

        public void OnHit(ref float maximumT, float t, System.Numerics.Vector3 hitLocation, System.Numerics.Vector3 hitNormal, CollidableReference collidable)
        {
            var backend = _engine.ResolveBackend(collidable);
            _output.Add(new HitResult
            {
                Succeeded = true,
                Point = hitLocation,
                Normal = hitNormal,
                HitFraction = t,
                Collider = backend?.UserObject as PhysicsBaseComponent,
                Tag = backend?.ResolveFixtureTag(-1)
            });
        }

        public void OnHitAtZeroT(ref float maximumT, CollidableReference collidable)
        {
            var backend = _engine.ResolveBackend(collidable);
            _output.Add(new HitResult
            {
                Succeeded = true,
                Point = Vector3.Zero,
                Normal = Vector3.Zero,
                HitFraction = 0f,
                Collider = backend?.UserObject as PhysicsBaseComponent,
                Tag = backend?.ResolveFixtureTag(-1)
            });
        }
    }

    /// <summary>
    /// Sleep settings of a dynamic body: the threshold of its definition, held for
    /// <see cref="PhysicsDefinition.SleepDelaySeconds"/> worth of fixed steps (capped by Bepu's byte counter).
    /// </summary>
    private BodyActivityDescription CreateDynamicActivity(float sleepThreshold)
    {
        float stepsUnderThreshold = FixedTimeStep > 0f ? PhysicsDefinition.SleepDelaySeconds / FixedTimeStep : 32f;
        byte minimumStepCount = (byte)Math.Clamp((int)MathF.Round(stepsUnderThreshold), 1, byte.MaxValue);
        return new BodyActivityDescription(sleepThreshold, minimumStepCount);
    }

    // ----- Registration / resolution ---------------------------------------------------------

    internal void RegisterBody(BodyHandle handle, BepuBodyBackend backend)
    {
        EnsureCapacity(_bodyBackends, handle.Value);
        _bodyBackends[handle.Value] = backend;
        _insertedBodyCount++;

        if (backend.HasLinearLock)
        {
            _linearLockedBodies.Add(backend);
        }
    }

    internal void UnregisterBody(BodyHandle handle)
    {
        if (handle.Value < _bodyBackends.Count)
        {
            var backend = _bodyBackends[handle.Value];
            if (backend != null && backend.HasLinearLock)
            {
                _linearLockedBodies.Remove(backend);
            }

            _bodyBackends[handle.Value] = null;
        }

        _insertedBodyCount--;
    }

    /// <summary>
    /// Post-step half of the linear factor: the pose integrator masks the velocity it integrates, but the
    /// solver can still push a body along a locked axis inside the step (contact normals are free to
    /// point along it). Clamp the locked axes back so a planar world never drifts, as Bullet guaranteed.
    /// </summary>
    private void EnforceLinearLocks()
    {
        for (int i = 0; i < _linearLockedBodies.Count; i++)
        {
            _linearLockedBodies[i].EnforceLinearLock();
        }
    }

    internal void RegisterStatic(StaticHandle handle, BepuBodyBackend backend)
    {
        EnsureCapacity(_staticBackends, handle.Value);
        _staticBackends[handle.Value] = backend;
        _insertedStaticCount++;
    }

    internal void UnregisterStatic(StaticHandle handle)
    {
        if (handle.Value < _staticBackends.Count)
        {
            _staticBackends[handle.Value] = null;
        }

        _insertedStaticCount--;
    }

    private static void EnsureCapacity(List<BepuBodyBackend> list, int index)
    {
        while (list.Count <= index)
        {
            list.Add(null);
        }
    }

    /// <summary>Backend registered for a body handle, or null if the handle is stale/unregistered.
    /// Used by the debug renderer, which walks raw body sets (handles), not collidable references.</summary>
    internal BepuBodyBackend GetBodyBackend(BodyHandle handle)
    {
        return handle.Value >= 0 && handle.Value < _bodyBackends.Count ? _bodyBackends[handle.Value] : null;
    }

    /// <summary>Backend registered for a static handle, or null if the handle is stale/unregistered.</summary>
    internal BepuBodyBackend GetStaticBackend(StaticHandle handle)
    {
        return handle.Value >= 0 && handle.Value < _staticBackends.Count ? _staticBackends[handle.Value] : null;
    }

    internal BepuBodyBackend ResolveBackend(CollidableReference reference)
    {
        if (reference.Mobility == CollidableMobility.Static)
        {
            int index = reference.StaticHandle.Value;
            return index >= 0 && index < _staticBackends.Count ? _staticBackends[index] : null;
        }

        int bodyIndex = reference.BodyHandle.Value;
        return bodyIndex >= 0 && bodyIndex < _bodyBackends.Count ? _bodyBackends[bodyIndex] : null;
    }

    /// <summary>Contacts recorded during the last <see cref="Update"/> step, for the debug renderer.</summary>
    internal BepuContactBuffer ContactBuffer => _contactBuffer;

    internal Vector3 GetPosition(CollidableReference reference)
    {
        return reference.Mobility == CollidableMobility.Static
            ? Simulation.Statics[reference.StaticHandle].Pose.Position
            : Simulation.Bodies[reference.BodyHandle].Pose.Position;
    }

    internal void ReleaseBodyShape(BepuBodyBackend backend)
    {
        if (backend.IsCompound)
        {
            Simulation.Shapes.RemoveAndDispose(backend.ShapeIndex, _pool);
            foreach (var child in backend.CompoundChildShapeIndices)
            {
                _shapeCache.Release(child);
            }
        }
        else
        {
            _shapeCache.Release(backend.ShapeIndex);
        }
    }

    private static BepuBodyBackend GetBackend(PhysicsBody physicsBody)
    {
        ArgumentNullException.ThrowIfNull(physicsBody);

        if (physicsBody.Backend is not BepuBodyBackend backend)
        {
            throw new InvalidOperationException("Physics body belongs to another physics backend.");
        }

        return backend;
    }

    private static BepuPhysicsQueryShapeBackend GetQueryShapeBackend(PhysicsQueryShape shape)
    {
        if (shape.Backend is not BepuPhysicsQueryShapeBackend backend)
        {
            throw new InvalidOperationException("Physics query shape belongs to another physics backend.");
        }

        return backend;
    }

    // ----- Stepping and events ---------------------------------------------------------------

    internal void Update(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        if (MaxSubSteps == 0)
        {
            _contactBuffer.Clear();
            Simulation.Timestep(deltaTime);
            EnforceLinearLocks();
            return;
        }

        _accumulator += deltaTime;
        int numSubSteps = (int)(_accumulator / FixedTimeStep);

        if (numSubSteps <= 0)
        {
            return;
        }

        _accumulator -= numSubSteps * FixedTimeStep;
        int clampedSteps = Math.Min(numSubSteps, MaxSubSteps);

        for (int i = 0; i < clampedSteps; i++)
        {
            _contactBuffer.Clear();
            Simulation.Timestep(FixedTimeStep);
            EnforceLinearLocks();
        }
    }

    internal void UpdateContacts()
    {
        EndedFromComponentRemoval.Clear();
        _outdatedCollisions.Clear();

        foreach (var collision in _collisions)
        {
            _outdatedCollisions.Add(collision);
        }

        _currentTouching.Clear();

        foreach (var record in _contactBuffer.Records)
        {
            if (record.Depth < -ContactTolerance)
            {
                continue;
            }

            var backendA = ResolveBackend(record.A);
            var backendB = ResolveBackend(record.B);

            if (backendA?.UserObject is not ICollideableComponent componentA || backendB?.UserObject is not ICollideableComponent componentB)
            {
                continue;
            }

            if (ReferenceEquals(componentA, componentB))
            {
                continue;
            }

            var contactWorldPoint = GetPosition(record.A) + record.Offset;
            _currentTouching.Add(new Collision(componentA, componentB, contactWorldPoint));
        }

        foreach (var collision in _currentTouching)
        {
            if (_outdatedCollisions.Remove(collision))
            {
                continue;
            }

            if (_collisions.Add(collision))
            {
                _markedAsNewColl.Add(collision);
            }
        }

        foreach (var stale in _outdatedCollisions)
        {
            _markedAsDeprecatedColl.Add(stale);
            _collisions.Remove(stale);
        }

        _outdatedCollisions.Clear();
    }

    internal void ClearCollisionDataOf(ICollideableComponent component)
    {
        _pendingRemoval.Clear();

        foreach (var collision in _collisions)
        {
            if (ReferenceEquals(collision.ColliderA, component) || ReferenceEquals(collision.ColliderB, component))
            {
                _pendingRemoval.Add(collision);
                EndedFromComponentRemoval.Add(collision);
            }
        }

        foreach (var collision in _pendingRemoval)
        {
            _collisions.Remove(collision);
            collision.ColliderA.Owner.GameplayProxy?.OnHitEnded(collision);
            collision.ColliderB.Owner.GameplayProxy?.OnHitEnded(collision);
            collision.ColliderA.Collisions.Remove(collision);
            collision.ColliderB.Collisions.Remove(collision);
        }
    }

    internal void SendEvents()
    {
        foreach (var (_, hashset) in _contactsUpToDate)
        {
            hashset.Clear();
            _contactsPool.Push(hashset);
        }

        _contactsUpToDate.Clear();

        foreach (var collision in _markedAsNewColl)
        {
            collision.ColliderA.Collisions.Add(collision);
            collision.ColliderB.Collisions.Add(collision);
            collision.ColliderA.Owner.GameplayProxy?.OnHit(collision);
            collision.ColliderB.Owner.GameplayProxy?.OnHit(collision);
        }

        foreach (var collision in _markedAsDeprecatedColl)
        {
            collision.ColliderA.Owner.GameplayProxy?.OnHitEnded(collision);
            collision.ColliderB.Owner.GameplayProxy?.OnHitEnded(collision);
            collision.ColliderA.Collisions.Remove(collision);
            collision.ColliderB.Collisions.Remove(collision);
        }

        _markedAsNewColl.Clear();
        _markedAsDeprecatedColl.Clear();
    }

    internal HashSet<ContactPoint> LatestContactPointsFor(Collision coll)
    {
        if (_contactsUpToDate.TryGetValue(coll, out var buffer))
        {
            return buffer;
        }

        buffer = _contactsPool.Count == 0 ? new HashSet<ContactPoint>() : _contactsPool.Pop();
        _contactsUpToDate[coll] = buffer;

        if (!_collisions.Contains(coll))
        {
            return buffer;
        }

        foreach (var record in _contactBuffer.Records)
        {
            var backendA = ResolveBackend(record.A);
            var backendB = ResolveBackend(record.B);

            if (backendA?.UserObject is not ICollideableComponent componentA || backendB?.UserObject is not ICollideableComponent componentB)
            {
                continue;
            }

            bool matches = (coll.ColliderA == componentA && coll.ColliderB == componentB)
                           || (coll.ColliderA == componentB && coll.ColliderB == componentA);

            if (!matches)
            {
                continue;
            }

            var positionOnA = GetPosition(record.A) + record.Offset;
            var positionOnB = positionOnA - record.Normal * record.Depth;

            buffer.Add(new ContactPoint
            {
                ColliderA = componentA,
                ColliderB = componentB,
                Distance = -record.Depth,
                Normal = record.Normal,
                PositionOnA = positionOnA,
                PositionOnB = positionOnB,
                FixtureTagA = backendA.ResolveFixtureTag(record.ChildA),
                FixtureTagB = backendB.ResolveFixtureTag(record.ChildB)
            });
        }

        return buffer;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Simulation.Dispose();
        _pool.Clear();
    }
}
