using BulletSharp;
using CasaEngine.Framework.SceneManagement.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Physics;

public class PhysicsEngine
{
    const CollisionFilterGroups DefaultGroup = CollisionFilterGroups.DefaultFilter;

    public DiscreteDynamicsWorld? World { get; }

    private readonly CollisionWorld _collisionWorld;

    private readonly CollisionDispatcher _dispatcher;
    private readonly CollisionConfiguration _collisionConfiguration;
    private readonly DbvtBroadphase _broadphase;

    private readonly ContactSolverInfo _solverInfo;

    private readonly DispatcherInfo _dispatchInfo;

    internal readonly bool CanCcd;

    public bool ContinuousCollisionDetection
    {
        get
        {
            if (!CanCcd)
            {
                throw new Exception("ContinuousCollisionDetection must be enabled at physics engine initialization using the proper flag.");
            }

            return _dispatchInfo.UseContinuous;
        }
        set
        {
            if (!CanCcd)
            {
                throw new Exception("ContinuousCollisionDetection must be enabled at physics engine initialization using the proper flag.");
            }

            _dispatchInfo.UseContinuous = value;
        }
    }

    private readonly Dictionary<Collision, (CollisionObject, CollisionObject)> _collisions = new();
    private readonly Dictionary<(CollisionObject, CollisionObject), Collision> _outdatedCollisions = new();

    //private readonly Stack<System.Threading.Channels.Channel<HashSet<ContactPoint>>> _channelsPool = new();
    //private readonly Dictionary<Collision, (System.Threading.Channels.Channel<HashSet<ContactPoint>> Channel, HashSet<ContactPoint> PreviousContacts)> _contactChangedChannels = new();

    private readonly Stack<HashSet<ContactPoint>> _contactsPool = new();
    private readonly Dictionary<Collision, HashSet<ContactPoint>> _contactsUpToDate = new();

    private readonly List<Collision> _markedAsNewColl = new();
    private readonly List<Collision> _markedAsDeprecatedColl = new();
    internal readonly HashSet<Collision> EndedFromComponentRemoval = new();

    /// <summary>
    /// Every pair of components currently colliding with each other
    /// </summary>
    public ICollection<Collision> CurrentCollisions => _collisions.Keys;

    /// <summary>
    /// Should static - static collisions of StaticColliderComponent yield
    /// <see cref="PhysicsComponent"/>.<see cref="PhysicsComponent.NewCollision()"/> and added to
    /// <see cref="PhysicsComponent"/>.<see cref="PhysicsComponent.Collisions"/> ?
    /// </summary>
    /// <remarks>
    /// Regardless of the state of this value you can still retrieve static-static collisions
    /// through <see cref="CurrentCollisions"/>.
    /// </remarks>
    public bool IncludeStaticAgainstStaticCollisions { get; set; } = false;

    /// <summary>
    /// Gets or sets the gravity.
    /// </summary>
    /// <value>
    /// The gravity.
    /// </value>
    /// <exception cref="System.Exception">
    /// Cannot perform this action when the physics engine is set to CollisionsOnly
    /// </exception>
    /*public Vector3 Gravity
    {
        get
        {
            if (World == null)
            {
                throw new Exception("Cannot perform this action when the physics engine is set to CollisionsOnly");
            }

            return World.Gravity;
        }
        set
        {
            if (World == null)
            {
                throw new Exception("Cannot perform this action when the physics engine is set to CollisionsOnly");
            }

            World.Gravity = value;
        }
    }*/

    /// <summary>
    /// The maximum number of steps that the Simulation is allowed to take each tick.
    /// If the engine is running slow (large deltaTime), then you must increase the number of maxSubSteps to compensate for this, otherwise your simulation is “losing” time.
    /// It's important that frame DeltaTime is always less than MaxSubSteps*FixedTimeStep, otherwise you are losing time.
    /// </summary>
    public int MaxSubSteps { get; set; }

    /// <summary>
    /// By decreasing the size of fixedTimeStep, you are increasing the “resolution” of the simulation.
    /// Default is 1.0f / 60.0f or 60fps
    /// </summary>
    public float FixedTimeStep { get; set; }

    public PhysicsEngine(PhysicsEngineSettings configuration)
    {
        MaxSubSteps = configuration.MaxSubSteps;
        FixedTimeStep = configuration.FixedTimeStep;

        _collisionConfiguration = new DefaultCollisionConfiguration();
        _dispatcher = new CollisionDispatcher(_collisionConfiguration);
        _broadphase = new DbvtBroadphase();

        //this allows characters to have proper physics behavior
        _broadphase.OverlappingPairCache.SetInternalGhostPairCallback(new GhostPairCallback());

        //2D pipeline
        if (configuration.IsPhysics2dActivated)
        {
            var simplex = new VoronoiSimplexSolver();
            var pdSolver = new MinkowskiPenetrationDepthSolver();
            var convexAlgo = new Convex2DConvex2DAlgorithm.CreateFunc(simplex, pdSolver);

            _dispatcher.RegisterCollisionCreateFunc(BroadphaseNativeType.Convex2DShape, BroadphaseNativeType.Convex2DShape, convexAlgo);
            _dispatcher.RegisterCollisionCreateFunc(BroadphaseNativeType.Box2DShape, BroadphaseNativeType.Convex2DShape, convexAlgo);
            _dispatcher.RegisterCollisionCreateFunc(BroadphaseNativeType.Convex2DShape, BroadphaseNativeType.Box2DShape, convexAlgo);
            _dispatcher.RegisterCollisionCreateFunc(BroadphaseNativeType.Box2DShape, BroadphaseNativeType.Box2DShape, new Box2DBox2DCollisionAlgorithm.CreateFunc());
        }

        //default solver
        var solver = new SequentialImpulseConstraintSolver();

        if (configuration.Flags.HasFlag(PhysicsEngineFlags.CollisionsOnly))
        {
            _collisionWorld = new CollisionWorld(_dispatcher, _broadphase, _collisionConfiguration);
        }
        else if (configuration.Flags.HasFlag(PhysicsEngineFlags.SoftBodySupport))
        {
            //mSoftRigidDynamicsWorld = new BulletSharp.SoftBody.SoftRigidDynamicsWorld(mDispatcher, mBroadphase, solver, mCollisionConf);
            //mDiscreteDynamicsWorld = mSoftRigidDynamicsWorld;
            //mCollisionWorld = mSoftRigidDynamicsWorld;
            throw new NotImplementedException("SoftBody processing is not yet available");
        }
        else
        {
            World = new DiscreteDynamicsWorld(_dispatcher, _broadphase, solver, _collisionConfiguration);
            _collisionWorld = World;
        }

        if (World != null)
        {
            _solverInfo = World.SolverInfo; //we are required to keep this reference, or the GC will mess up
            _dispatchInfo = World.DispatchInfo;

            _solverInfo.SolverMode |= SolverModes.CacheFriendly; //todo test if helps with performance or not

            if (configuration.Flags.HasFlag(PhysicsEngineFlags.ContinuousCollisionDetection))
            {
                CanCcd = true;
                _solverInfo.SolverMode |= SolverModes.Use2FrictionDirections | SolverModes.RandomizeOrder;
                _dispatchInfo.UseContinuous = true;
            }
            else
            {
                CanCcd = false;
                _dispatchInfo.UseContinuous = false;
            }
        }
    }

    internal void UpdateContacts()
    {
        EndedFromComponentRemoval.Clear();
        // Mark previous collisions as outdated,
        // we'll iterate through bullet's actives and remove them from here
        // to be left with only the outdated ones.
        foreach (var collision in _collisions)
        {
            _outdatedCollisions.Add(collision.Value, collision.Key);
        }

        // If this needs to be even faster, look into btPersistentManifold.ContactStartedCallback,
        // not yet covered by the wrapper

        int numManifolds = _collisionWorld.Dispatcher.NumManifolds;
        for (int i = 0; i < numManifolds; i++)
        {
            var persistentManifold = _collisionWorld.Dispatcher.GetManifoldByIndexInternal(i);

            int numContacts = persistentManifold.NumContacts;
            if (numContacts == 0)
            {
                continue;
            }

            var ptrA = persistentManifold.Body0;
            var ptrB = persistentManifold.Body1;
            bool aFirst = ptrA.UserObject.GetHashCode() > ptrB.UserObject.GetHashCode();
            (CollisionObject, CollisionObject) collId = aFirst ? (ptrA, ptrB) : (ptrB, ptrA);

            // This collision is up-to-date, remove it from the outdated collisions
            if (_outdatedCollisions.Remove(collId))
            {
                continue;
            }

            // Likely a new collision, or a duplicate

            var a = collId.Item1;
            var b = collId.Item2;

            if (a.UserObject == b.UserObject)
            {
                continue;
            }

            var collision = new Collision(a.UserObject as ICollideableComponent, b.UserObject as ICollideableComponent, persistentManifold.GetContactPoint(0).LocalPointA);
            // PairCachingGhostObject has two identical manifolds when colliding, not 100% sure why that is,
            // CompoundColliderShape shapes all map to the same PhysicsComponent but create unique manifolds.
            if (_collisions.TryAdd(collision, collId))
            {
                _markedAsNewColl.Add(collision);
            }
        }

        // This set only contains outdated collisions by now,
        // mark them as out of date for events and remove them from current collisions
        foreach (var (_, outdatedCollision) in _outdatedCollisions)
        {
            _markedAsDeprecatedColl.Add(outdatedCollision);
            _collisions.Remove(outdatedCollision);
        }

        _outdatedCollisions.Clear();
    }

    internal void ClearCollisionDataOf(ICollideableComponent component)
    {
        foreach (var (collision, key) in _collisions)
        {
            if (ReferenceEquals(collision.ColliderA, component) || ReferenceEquals(collision.ColliderB, component))
            {
                _outdatedCollisions.Add(key, collision);
                EndedFromComponentRemoval.Add(collision);
            }
        }

        // Remove collision and update contact data
        foreach (var (_, collision) in _outdatedCollisions)
        {
            /*if (_contactChangedChannels.TryGetValue(collision, out var tuple))
            {
                _contactChangedChannels[collision] = (tuple.Channel, LatestContactPointsFor(collision));
                _contactsUpToDate[collision] = _contactsPool.Count == 0 ? new HashSet<ContactPoint>() : _contactsPool.Pop();
            }
            else if (_contactsUpToDate.TryGetValue(collision, out var set))
            {
                set.Clear();
            }*/

            _collisions.Remove(collision);
        }

        // Send contacts changed and cleanup channel-related pooled data
        foreach (var (_, collision) in _outdatedCollisions)
        {
            /*if (_contactChangedChannels.TryGetValue(collision, out var tuple) == false)
            {
                continue;
            }

            var channel = tuple.Channel;
            var previousContacts = tuple.PreviousContacts;
            var newContacts = _contactsUpToDate[collision];

            if (previousContacts.SetEquals(newContacts) == false)
            {
                while (channel.Balance < 0)
                {
                    channel.Send(previousContacts);
                }
            }

            previousContacts.Clear();
            _contactsPool.Push(previousContacts);
            _channelsPool.Push(channel);
            _contactChangedChannels.Remove(collision);*/
        }

        // Send collision ended
        foreach (var (_, refCollision) in _outdatedCollisions)
        {
            var collision = new Collision(refCollision.ColliderA, refCollision.ColliderB, refCollision.ContactPoint);
            // See: SendEvents()
            if (IncludeStaticAgainstStaticCollisions == false
                && collision.ColliderA.PhysicsType == PhysicsType.Static
                && collision.ColliderB.PhysicsType == PhysicsType.Static
                /*collision.ColliderA is StaticColliderComponent
                && collision.ColliderB is StaticColliderComponent*/)
            {
                collision.ColliderA.Collisions.Remove(collision);
                collision.ColliderB.Collisions.Remove(collision);
                continue;
            }

            collision.ColliderA.Owner.GameplayProxy?.OnHitEnded(collision);
            collision.ColliderB.Owner.GameplayProxy?.OnHitEnded(collision);

            collision.ColliderA.Collisions.Remove(collision);
            collision.ColliderB.Collisions.Remove(collision);
        }

        _outdatedCollisions.Clear();
    }


    internal void SendEvents()
    {
        // Move outdated contacts back to the pool, or into contact changed to be compared for changes
        foreach (var (coll, hashset) in _contactsUpToDate)
        {
            //if (_contactChangedChannels.TryGetValue(coll, out var tuple))
            //{
            //    _contactChangedChannels[coll] = (tuple.Channel, hashset);
            //}
            //else
            {
                hashset.Clear();
                _contactsPool.Push(hashset);
            }
        }

        _contactsUpToDate.Clear();

        foreach (var collision in _markedAsNewColl)
        {
            if (IncludeStaticAgainstStaticCollisions == false
                && collision.ColliderA.PhysicsType == PhysicsType.Static
                && collision.ColliderB.PhysicsType == PhysicsType.Static
                /*collision.ColliderA is StaticColliderComponent
                && collision.ColliderB is StaticColliderComponent*/)
            {
                continue;
            }

            collision.ColliderA.Collisions.Add(collision);
            collision.ColliderB.Collisions.Add(collision);

            collision.ColliderA.Owner.GameplayProxy?.OnHit(collision);
            collision.ColliderB.Owner.GameplayProxy?.OnHit(collision);
        }

        // Deprecated collisions don't need to send contact changes, move channels to the pool
        foreach (var collision in _markedAsDeprecatedColl)
        {
            //if (_contactChangedChannels.TryGetValue(collision, out var tuple))
            //{
            //    _channelsPool.Push(tuple.Channel);
            //    _contactChangedChannels.Remove(collision);
            //}
        }

        foreach (var collision in _markedAsDeprecatedColl)
        {
            if (IncludeStaticAgainstStaticCollisions == false
                && collision.ColliderA.PhysicsType == PhysicsType.Static
                && collision.ColliderB.PhysicsType == PhysicsType.Static
                /*collision.ColliderA is StaticColliderComponent
                && collision.ColliderB is StaticColliderComponent*/)
            {
                // Try to remove them still if they were added while
                // 'IncludeStaticAgainstStaticCollisions' was true
                collision.ColliderA.Collisions.Remove(collision);
                collision.ColliderB.Collisions.Remove(collision);
                continue;
            }

            // IncludeStaticAgainstStaticCollisions:
            // Can't do much if something is awaiting the end of a specific
            // static-static collision below though
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

        if (_collisions.ContainsKey(coll) == false)
        {
            return buffer;
        }

        var dispatcher = _collisionWorld.Dispatcher;
        int numManifolds = dispatcher.NumManifolds;
        for (int i = 0; i < numManifolds; i++)
        {
            var persistentManifold = dispatcher.GetManifoldByIndexInternal(i);

            int numContacts = persistentManifold.NumContacts;
            if (numContacts == 0)
            {
                continue;
            }

            var collisionObjectA = persistentManifold.Body0;
            var collisionObjectB = persistentManifold.Body1;

            // Distinct bullet pointer can map to the same PhysicsComponent through CompoundColliderShapes
            // We're retrieving all contacts for a pair of PhysicsComponent here, not for a unique collider
            var collA = collisionObjectB.UserObject as PhysicsComponent;
            var collB = collisionObjectB.UserObject as PhysicsComponent;

            if (false == (coll.ColliderA == collA && coll.ColliderB == collB
                          || coll.ColliderA == collB && coll.ColliderB == collA))
            {
                continue;
            }

            for (int j = 0; j < numContacts; j++)
            {
                var point = persistentManifold.GetContactPoint(j);
                buffer.Add(new ContactPoint
                {
                    ColliderA = collA,
                    ColliderB = collB,
                    Distance = point.Distance1,
                    Normal = point.NormalWorldOnB,
                    PositionOnA = point.PositionWorldOnA,
                    PositionOnB = point.PositionWorldOnB,
                });
            }
        }

        return buffer;
    }

    //internal ChannelMicroThreadAwaiter<HashSet<ContactPoint>> ContactChanged(Collision coll)
    //{
    //    if (_collisions.ContainsKey(coll) == false)
    //        throw new InvalidOperationException("The collision object has been destroyed.");
    //
    //    // Forces this frame's contact to be retrieved and stored so that we can compare it for changes
    //    LatestContactPointsFor(coll);
    //
    //    if (_contactChangedChannels.TryGetValue(coll, out var tuple))
    //        return tuple.Channel.Receive();
    //
    //    var channel = _channelsPool.Count == 0 ? new Channel<HashSet<ContactPoint>> { Preference = ChannelPreference.PreferSender } : channelsPool.Pop();
    //    _contactChangedChannels[coll] = (channel, null);
    //    return channel.Receive();
    //}

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        //if (mSoftRigidDynamicsWorld != null) mSoftRigidDynamicsWorld.Dispose();
        World?.Dispose();
        _collisionWorld?.Dispose();
        _broadphase?.Dispose();
        _dispatcher?.Dispose();
        _collisionConfiguration?.Dispose();
    }

    //internal void AddCollider(PhysicsComponent component, CollisionFilterGroups group, CollisionFilterGroups mask)
    //{
    //    collisionWorld.AddCollisionObject(component.NativeCollisionObject, (BulletSharp.CollisionFilterGroups)group, (BulletSharp.CollisionFilterGroups)mask);
    //}
    //
    //internal void RemoveCollider(PhysicsComponent component)
    //{
    //    collisionWorld.RemoveCollisionObject(component.NativeCollisionObject);
    //}
    //
    //internal void AddRigidBody(RigidbodyComponent rigidBody, CollisionFilterGroups group, CollisionFilterGroups mask)
    //{
    //    if (discreteDynamicsWorld == null) throw new Exception("Cannot perform this action when the physics engine is set to CollisionsOnly");
    //
    //    discreteDynamicsWorld.AddRigidBody(rigidBody.InternalRigidBody, (short)group, (short)mask);
    //}
    //
    //internal void RemoveRigidBody(RigidbodyComponent rigidBody)
    //{
    //    if (discreteDynamicsWorld == null) throw new Exception("Cannot perform this action when the physics engine is set to CollisionsOnly");
    //
    //    discreteDynamicsWorld.RemoveRigidBody(rigidBody.InternalRigidBody);
    //}
    //
    //internal void AddCharacter(CharacterComponent character, CollisionFilterGroups group, CollisionFilterGroups mask)
    //{
    //    if (discreteDynamicsWorld == null) throw new Exception("Cannot perform this action when the physics engine is set to CollisionsOnly");
    //
    //    var collider = character.NativeCollisionObject;
    //    var action = character.KinematicCharacter;
    //    discreteDynamicsWorld.AddCollisionObject(collider, (BulletSharp.CollisionFilterGroups)group, (BulletSharp.CollisionFilterGroups)mask);
    //    discreteDynamicsWorld.AddAction(action);
    //
    //    character.Simulation = this;
    //}
    //
    //internal void RemoveCharacter(CharacterComponent character)
    //{
    //    if (discreteDynamicsWorld == null) throw new Exception("Cannot perform this action when the physics engine is set to CollisionsOnly");
    //
    //    var collider = character.NativeCollisionObject;
    //    var action = character.KinematicCharacter;
    //    discreteDynamicsWorld.RemoveCollisionObject(collider);
    //    discreteDynamicsWorld.RemoveAction(action);
    //
    //    character.Simulation = null;
    //}

    /// <summary>
    /// Creates the constraint.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="rigidBodyA">The rigid body a.</param>
    /// <param name="frameA">The frame a.</param>
    /// <param name="useReferenceFrameA">if set to <c>true</c> [use reference frame a].</param>
    /// <returns></returns>
    /// <exception cref="System.Exception">
    /// Cannot perform this action when the physics engine is set to CollisionsOnly
    /// or
    /// Both RigidBodies must be valid
    /// or
    /// A Gear constraint always needs two rigidbodies to be created.
    /// </exception>
    //public static Constraint CreateConstraint(ConstraintTypes type, RigidbodyComponent rigidBodyA, Matrix frameA, bool useReferenceFrameA = false)
    //{
    //    return CreateConstraintInternal(type, rigidBodyA, frameA, useReferenceFrameA: useReferenceFrameA);
    //}

    /// <summary>
    /// Creates the constraint.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="rigidBodyA">The rigid body a.</param>
    /// <param name="rigidBodyB">The rigid body b.</param>
    /// <param name="frameA">The frame a.</param>
    /// <param name="frameB">The frame b.</param>
    /// <param name="useReferenceFrameA">if set to <c>true</c> [use reference frame a].</param>
    /// <returns></returns>
    /// <exception cref="System.Exception">
    /// Cannot perform this action when the physics engine is set to CollisionsOnly
    /// or
    /// Both RigidBodies must be valid
    /// </exception>
    //public static Constraint CreateConstraint(ConstraintTypes type, RigidbodyComponent rigidBodyA, RigidbodyComponent rigidBodyB, Matrix frameA, Matrix frameB, bool useReferenceFrameA = false)
    //{
    //    if (rigidBodyA == null || rigidBodyB == null) throw new Exception("Both RigidBodies must be valid");
    //    return CreateConstraintInternal(type, rigidBodyA, frameA, rigidBodyB, frameB, useReferenceFrameA);
    //}

    /// <summary>
    /// Creates a hinge constraint using a specialized constructor.
    /// </summary>
    /// <param name="rigidBodyA">The rigid body a.</param>
    /// <param name="pivotInA">Pivot point in body a.</param>
    /// <param name="axisInA">Axis in body a.</param>
    /// <param name="useReferenceFrameA">if set to <c>true</c> [use reference frame a].</param>
    /// <exception cref="System.Exception">
    /// Cannot perform this action when the physics engine is set to CollisionsOnly
    /// or
    /// RigidBody must be valid
    /// </exception>
    //public static HingeConstraint CreateHingeConstraint(RigidbodyComponent rigidBodyA, Vector3 pivotInA, Vector3 axisInA, bool useReferenceFrameA = false)
    //{
    //    if (rigidBodyA == null) throw new Exception("RigidBody must be valid");
    //    return CreateHingeConstraintInternal(rigidBodyA, null, pivotInA, default, axisInA, default, useReferenceFrameA);
    //}

    /// <summary>
    /// Creates a hinge constraint using a specialized constructor.
    /// </summary>
    /// <param name="rigidBodyA">The rigid body a.</param>
    /// <param name="pivotInA">Pivot point in body a.</param>
    /// <param name="axisInA">Axis in body a.</param>
    /// <param name="rigidBodyB">The rigid body b.</param>
    /// <param name="pivotInB">Pivot point in body b.</param>
    /// <param name="axisInB">Axis in body b.</param>
    /// <param name="useReferenceFrameA">if set to <c>true</c> [use reference frame a].</param>
    /// <exception cref="System.Exception">
    /// Cannot perform this action when the physics engine is set to CollisionsOnly
    /// or
    /// Both RigidBodies must be valid
    /// </exception>
    //public static HingeConstraint CreateHingeConstraint(RigidbodyComponent rigidBodyA, Vector3 pivotInA, Vector3 axisInA, RigidbodyComponent rigidBodyB, Vector3 pivotInB, Vector3 axisInB, bool useReferenceFrameA = false)
    //{
    //    if (rigidBodyA == null || rigidBodyB == null) throw new Exception("Both RigidBodies must be valid");
    //    return CreateHingeConstraintInternal(rigidBodyA, rigidBodyB, pivotInA, pivotInB, axisInA, axisInB, useReferenceFrameA);
    //}

    //static TypedConstraint CreateConstraintInternal(ConstraintTypes type, RigidbodyComponent rigidBodyA, Matrix frameA, RigidbodyComponent rigidBodyB = null, Matrix frameB = default, bool useReferenceFrameA = false)
    //{
    //    if (rigidBodyA == null) throw new Exception($"{nameof(rigidBodyA)} must be valid");
    //    if (rigidBodyB != null && rigidBodyB.Simulation != rigidBodyA.Simulation) throw new Exception("Both RigidBodies must be on the same simulation");
    //
    //    TypedConstraint constraintBase = null;
    //    var rbA = rigidBodyA.InternalRigidBody;
    //    var rbB = rigidBodyB?.InternalRigidBody;
    //    switch (type)
    //    {
    //        case ConstraintTypes.Point2Point:
    //        {
    //            var constraint = rigidBodyB == null
    //                ? new BulletSharp.Point2PointConstraint(rbA, frameA.Translation)
    //                : new BulletSharp.Point2PointConstraint(rbA, rbB, frameA.Translation, frameB.Translation);
    //                constraintBase = constraint;
    //                break;
    //        }
    //        case ConstraintTypes.Hinge:
    //        {
    //            var constraint = rigidBodyB == null
    //                    ? new BulletSharp.HingeConstraint(rbA, frameA)
    //                    : new BulletSharp.HingeConstraint(rbA, rbB, frameA, frameB, useReferenceFrameA);
    //                constraintBase = constraint;
    //                break;
    //            }
    //        case ConstraintTypes.Slider:
    //        {
    //            var constraint = rigidBodyB == null
    //                ? new BulletSharp.SliderConstraint(rbA, frameA, useReferenceFrameA)
    //                : new BulletSharp.SliderConstraint(rbA, rbB, frameA, frameB, useReferenceFrameA);
    //                break;
    //            }
    //        case ConstraintTypes.ConeTwist:
    //            {
    //                var constraint = rigidBodyB == null ?
    //                            new BulletSharp.ConeTwistConstraint(rbA, frameA) :
    //                            new BulletSharp.ConeTwistConstraint(rbA, rbB, frameA, frameB);
    //                break;
    //            }
    //        case ConstraintTypes.Generic6DoF:
    //            {
    //                var constraint = rigidBodyB == null ?
    //                            new BulletSharp.Generic6DofConstraint(rbA, frameA, useReferenceFrameA) :
    //                            new BulletSharp.Generic6DofConstraint(rbA, rbB, frameA, frameB, useReferenceFrameA);
    //                break;
    //            }
    //        case ConstraintTypes.Generic6DoFSpring:
    //        {
    //            var constraint = rigidBodyB == null
    //                ? new BulletSharp.Generic6DofSpringConstraint(rbA, frameA, useReferenceFrameA)
    //                : new BulletSharp.Generic6DofSpringConstraint(rbA, rbB, frameA, frameB, useReferenceFrameA);
    //                break;
    //            }
    //        case ConstraintTypes.Gear:
    //            {
    //                var constraint = rigidBodyB == null ?
    //                            throw new Exception("A Gear constraint always needs two rigidbodies to be created.") :
    //                            new BulletSharp.GearConstraint(rbA, rbB, frameA.Translation, frameB.Translation);
    //                constraintBase = constraint;
    //                break;
    //            }
    //        default:
    //            throw new ArgumentException(type.ToString());
    //    }
    //
    //    //if (rigidBodyB != null)
    //    //{
    //    //    constraintBase.RigidBodyB = rigidBodyB;
    //    //    rigidBodyB.LinkedConstraints.Add(constraintBase);
    //    //}
    //    //constraintBase.RigidBodyA = rigidBodyA;
    //    //rigidBodyA.LinkedConstraints.Add(constraintBase);
    //
    //    return constraintBase;
    //}
    //
    //static HingeConstraint CreateHingeConstraintInternal(RigidbodyComponent rigidBodyA, RigidbodyComponent rigidBodyB, Vector3 pivotInA, Vector3 pivotInB, Vector3 axisInA, Vector3 axisInB, bool useReferenceFrameA = false)
    //{
    //    if (rigidBodyB != null && rigidBodyB.Simulation != rigidBodyA.Simulation)
    //        throw new Exception("Both RigidBodies must be on the same simulation");
    //
    //    var rbA = rigidBodyA.InternalRigidBody;
    //    var rbB = rigidBodyB?.InternalRigidBody;
    //
    //    var constraint = rigidBodyB == null
    //        ? new BulletSharp.HingeConstraint(rbA, pivotInA, axisInA, useReferenceFrameA)
    //        : new BulletSharp.HingeConstraint(rbA, rbB, pivotInA, pivotInB, axisInA, axisInB, useReferenceFrameA);
    //
    //    if (rigidBodyB != null)
    //    {
    //        constraint.RigidBodyB = rigidBodyB;
    //        rigidBodyB.LinkedConstraints.Add(constraint);
    //    }
    //    constraint.RigidBodyA = rigidBodyA;
    //    rigidBodyA.LinkedConstraints.Add(constraint);
    //
    //    return constraint;
    //}

    /// <summary>
    /// Adds the constraint to the engine processing pipeline.
    /// </summary>
    /// <param name="constraint">The constraint.</param>
    /// <exception cref="System.Exception">Cannot perform this action when the physics engine is set to CollisionsOnly</exception>
    public void AddConstraint(TypedConstraint constraint)
    {
        if (World == null)
        {
            throw new Exception("Cannot perform this action when the physics engine is set to CollisionsOnly");
        }

        World.AddConstraint(constraint);
        //constraint.Simulation = this;
    }

    /// <summary>
    /// Adds the constraint to the engine processing pipeline.
    /// </summary>
    /// <param name="constraint">The constraint.</param>
    /// <param name="disableCollisionsBetweenLinkedBodies">if set to <c>true</c> [disable collisions between linked bodies].</param>
    /// <exception cref="System.Exception">Cannot perform this action when the physics engine is set to CollisionsOnly</exception>
    public void AddConstraint(TypedConstraint constraint, bool disableCollisionsBetweenLinkedBodies)
    {
        if (World == null)
        {
            throw new Exception("Cannot perform this action when the physics engine is set to CollisionsOnly");
        }

        World.AddConstraint(constraint, disableCollisionsBetweenLinkedBodies);
        //constraint.Simulation = this;
    }

    /// <summary>
    /// Removes the constraint from the engine processing pipeline.
    /// </summary>
    /// <param name="constraint">The constraint.</param>
    /// <exception cref="System.Exception">Cannot perform this action when the physics engine is set to CollisionsOnly</exception>
    public void RemoveConstraint(TypedConstraint constraint)
    {
        if (World == null)
        {
            throw new Exception("Cannot perform this action when the physics engine is set to CollisionsOnly");
        }

        World.RemoveConstraint(constraint);
        //constraint.Simulation = null;
    }

    /// <summary>
    /// Raycasts and returns the closest hit
    /// </summary>
    /// <param name="from">The starting point of this raycast</param>
    /// <param name="to">The end point of this raycast</param>
    /// <param name="filterGroup">The collision group of this raycast</param>
    /// <param name="filterFlags">The collision group that this raycast can collide with</param>
    /// <param name="hitTriggers">Whether this test should collide with <see cref="PhysicsTriggerComponentBase"/></param>
    /// <returns>The result of this test</returns>
    public HitResult Raycast(Vector3 from, Vector3 to, CollisionFilterGroups filterGroup = DefaultGroup, CollisionFilterGroups filterFlags = DefaultGroup, bool hitTriggers = false)
    {
        var callback = StrideClosestRayResultCallback.Shared(ref from, ref to, hitTriggers, filterGroup, filterFlags);
        _collisionWorld.RayTest(from, to, callback);
        return callback.Result;
    }

    /// <summary>
    /// Raycasts, returns true when it hit something
    /// </summary>
    /// <param name="from">The starting point of this raycast</param>
    /// <param name="to">The end point of this raycast</param>
    /// <param name="result">Information about this test</param>
    /// <param name="filterGroup">The collision group of this raycast</param>
    /// <param name="filterFlags">The collision group that this raycast can collide with</param>
    /// <param name="hitTriggers">Whether this test should collide with <see cref="PhysicsTriggerComponentBase"/></param>
    /// <returns>True if the test collided with an object in the simulation</returns>
    public bool Raycast(Vector3 from, Vector3 to, out HitResult result, CollisionFilterGroups filterGroup = DefaultGroup, CollisionFilterGroups filterFlags = DefaultGroup, bool hitTriggers = false)
    {
        var callback = StrideClosestRayResultCallback.Shared(ref from, ref to, hitTriggers, filterGroup, filterFlags);
        _collisionWorld.RayTest(from, to, callback);
        result = callback.Result;
        return result.Succeeded;
    }

    /// <summary>
    /// Raycasts penetrating any shape the ray encounters.
    /// Filtering by CollisionGroup
    /// </summary>
    /// <param name="from">The starting point of this raycast</param>
    /// <param name="to">The end point of this raycast</param>
    /// <param name="resultsOutput">The collection to add intersections to</param>
    /// <param name="filterGroup">The collision group of this raycast</param>
    /// <param name="filterFlags">The collision group that this raycast can collide with</param>
    /// <param name="hitTriggers">Whether this test should collide with <see cref="PhysicsTriggerComponentBase"/></param>
    public void RaycastPenetrating(Vector3 from, Vector3 to, ICollection<HitResult> resultsOutput, CollisionFilterGroups filterGroup = DefaultGroup, CollisionFilterGroups filterFlags = DefaultGroup, bool hitTriggers = false)
    {
        var callback = StrideAllHitsRayResultCallback.Shared(ref from, ref to, hitTriggers, resultsOutput, filterGroup, filterFlags);
        _collisionWorld.RayTest(from, to, callback);
    }

    /// <summary>
    /// Performs a sweep test using a collider shape and returns the closest hit
    /// </summary>
    /// <param name="shape">The shape used when testing collisions with colliders in the simulation</param>
    /// <param name="from">The starting point of this sweep</param>
    /// <param name="to">The end point of this sweep</param>
    /// <param name="filterGroup">The collision group of this shape sweep</param>
    /// <param name="filterFlags">The collision group that this shape sweep can collide with</param>
    /// <param name="hitTriggers">Whether this test should collide with <see cref="PhysicsTriggerComponentBase"/></param>
    /// <exception cref="System.ArgumentException">This kind of shape cannot be used for a ShapeSweep.</exception>
    /// <returns>The result of this test</returns>
    //public HitResult ShapeSweep(ColliderShape shape, Matrix from, Matrix to, CollisionFilterGroups filterGroup = DefaultGroup, CollisionFilterGroups filterFlags = DefaultGroup, bool hitTriggers = false)
    //{
    //    var sh = shape.InternalShape as BulletSharp.ConvexShape;
    //    if (sh == null)
    //    {
    //        throw new ArgumentException("This kind of shape cannot be used for a ShapeSweep.");
    //    }
    //
    //    var callback = StrideClosestConvexResultCallback.Shared(hitTriggers, filterGroup, filterFlags);
    //    collisionWorld.ConvexSweepTest(sh, from, to, callback);
    //    return callback.Result;
    //}

    /// <summary>
    /// Performs a sweep test using a collider shape and never stops until "to"
    /// </summary>
    /// <param name="shape">The shape against which colliders in the simulation will be tested</param>
    /// <param name="from">The starting point of this sweep</param>
    /// <param name="to">The end point of this sweep</param>
    /// <param name="resultsOutput">The collection to add hit results to</param>
    /// <param name="filterGroup">The collision group of this shape sweep</param>
    /// <param name="filterFlags">The collision group that this shape sweep can collide with</param>
    /// <param name="hitTriggers">Whether this test should collide with <see cref="PhysicsTriggerComponentBase"/></param>
    /// <exception cref="System.ArgumentException">This kind of shape cannot be used for a ShapeSweep.</exception>
    //public void ShapeSweepPenetrating(ColliderShape shape, Matrix from, Matrix to, ICollection<HitResult> resultsOutput, CollisionFilterGroups filterGroup = DefaultGroup, CollisionFilterGroups filterFlags = DefaultGroup, bool hitTriggers = false)
    //{
    //    var sh = shape.InternalShape as BulletSharp.ConvexShape;
    //    if (sh == null)
    //    {
    //        throw new ArgumentException("This kind of shape cannot be used for a ShapeSweep.");
    //    }
    //
    //    var rcb = StrideAllHitsConvexResultCallback.Shared(resultsOutput, hitTriggers, filterGroup, filterFlags);
    //    collisionWorld.ConvexSweepTest(sh, from, to, rcb);
    //}

    public void ClearForces()
    {
        if (World == null)
        {
            throw new Exception("Cannot perform this action when the physics engine is set to CollisionsOnly");
        }

        World.ClearForces();
    }

    public bool SpeculativeContactRestitution
    {
        get
        {
            if (World == null)
            {
                throw new Exception("Cannot perform this action when the physics engine is set to CollisionsOnly");
            }

            return World.ApplySpeculativeContactRestitution;
        }
        set
        {
            if (World == null)
            {
                throw new Exception("Cannot perform this action when the physics engine is set to CollisionsOnly");
            }

            World.ApplySpeculativeContactRestitution = value;
        }
    }

    public class SimulationArgs : EventArgs
    {
        public float DeltaTime;
    }

    /// <summary>
    /// Called right before the physics simulation.
    /// This event might not be fired by the main thread.
    /// </summary>
    public event EventHandler<SimulationArgs> SimulationBegin;

    protected virtual void OnSimulationBegin(SimulationArgs e)
    {
        var handler = SimulationBegin;
        handler?.Invoke(this, e);
    }

    //internal int UpdatedRigidbodies;

    private readonly SimulationArgs _simulationArgs = new();

    internal void Update(float deltaTime)
    {
        if (_collisionWorld == null)
        {
            return;
        }

        _simulationArgs.DeltaTime = deltaTime;

        //UpdatedRigidbodies = 0;

        OnSimulationBegin(_simulationArgs);

        if (World != null)
        {
            World.StepSimulation(deltaTime, MaxSubSteps, FixedTimeStep);
        }
        else
        {
            _collisionWorld.PerformDiscreteCollisionDetection();
        }

        OnSimulationEnd(_simulationArgs);
    }

    /// <summary>
    /// Called right after the physics simulation.
    /// This event might not be fired by the main thread.
    /// </summary>
    public event EventHandler<SimulationArgs> SimulationEnd;

    protected virtual void OnSimulationEnd(SimulationArgs e)
    {
        SimulationEnd?.Invoke(this, e);
    }

    private class StrideAllHitsConvexResultCallback : StrideReusableConvexResultCallback
    {
        [ThreadStatic]
        private static StrideAllHitsConvexResultCallback _shared;

        private ICollection<HitResult> _resultsList;

        public override float AddSingleResult(LocalConvexResult convexResult, bool normalInWorldSpace)
        {
            _resultsList.Add(ComputeHitResult(ref convexResult, normalInWorldSpace));
            return convexResult.HitFraction;
        }

        public static StrideAllHitsConvexResultCallback Shared(ICollection<HitResult> buffer, bool hitTriggers, CollisionFilterGroups filterGroup = DefaultGroup, CollisionFilterGroups filterMask = DefaultGroup)
        {
            _shared ??= new StrideAllHitsConvexResultCallback();
            _shared._resultsList = buffer;
            _shared.Recycle(hitTriggers, filterGroup, filterMask);
            return _shared;
        }
    }

    private class StrideClosestConvexResultCallback : StrideReusableConvexResultCallback
    {
        [ThreadStatic]
        private static StrideClosestConvexResultCallback _shared;

        private LocalConvexResult _closestHit;
        private bool _normalInWorldSpace;

        public HitResult Result => ComputeHitResult(ref _closestHit, _normalInWorldSpace);

        public override float AddSingleResult(LocalConvexResult convexResult, bool normalInWorldSpaceParam)
        {
            float fraction = convexResult.HitFraction;

            // This is 'm_closestHitFraction', Bullet will look at this value to ignore hits further away,
            // this method will only be called for hits closer than the last.
            // See btCollisionWorld::rayTestSingleInternal
            System.Diagnostics.Debug.Assert(convexResult.HitFraction <= ClosestHitFraction);
            ClosestHitFraction = fraction;

            _closestHit = convexResult;
            _normalInWorldSpace = normalInWorldSpaceParam;
            return fraction;
        }

        protected override void Recycle(bool hitNoContResp, CollisionFilterGroups filterGroup = CollisionFilterGroups.DefaultFilter, CollisionFilterGroups filterMask = (CollisionFilterGroups)(-1))
        {
            base.Recycle(hitNoContResp, filterGroup, filterMask);
            _closestHit = default;
        }

        public static StrideClosestConvexResultCallback Shared(bool hitTriggers, CollisionFilterGroups filterGroup = DefaultGroup, CollisionFilterGroups filterMask = DefaultGroup)
        {
            _shared ??= new StrideClosestConvexResultCallback();
            _shared.Recycle(hitTriggers, filterGroup, filterMask);
            return _shared;
        }
    }

    private class StrideAllHitsRayResultCallback : StrideReusableRayResultCallback
    {
        [ThreadStatic]
        private static StrideAllHitsRayResultCallback _shared;

        private ICollection<HitResult> _resultsList;

        public override float AddSingleResult(LocalRayResult rayResult, bool normalInWorldSpace)
        {
            _resultsList.Add(ComputeHitResult(ref rayResult, normalInWorldSpace));
            return rayResult.HitFraction;
        }

        public static StrideAllHitsRayResultCallback Shared(ref Vector3 from, ref Vector3 to, bool hitTriggers, ICollection<HitResult> buffer, CollisionFilterGroups filterGroup = DefaultGroup, CollisionFilterGroups filterMask = DefaultGroup)
        {
            _shared ??= new StrideAllHitsRayResultCallback();
            _shared._resultsList = buffer;
            _shared.Recycle(ref from, ref to, hitTriggers, filterGroup, filterMask);
            return _shared;
        }
    }

    private class StrideClosestRayResultCallback : StrideReusableRayResultCallback
    {
        [ThreadStatic]
        private static StrideClosestRayResultCallback _shared;

        private LocalRayResult _closestHit;
        private bool _normalInWorldSpace;

        public HitResult Result => ComputeHitResult(ref _closestHit, _normalInWorldSpace);

        public override float AddSingleResult(LocalRayResult rayResult, bool normalInWorldSpaceParam)
        {
            float fraction = rayResult.HitFraction;

            // This is 'm_closestHitFraction', Bullet will look at this value to ignore hits further away,
            // this method will only be called for hits closer than the last.
            // See btCollisionWorld::rayTestSingleInternal
            System.Diagnostics.Debug.Assert(rayResult.HitFraction <= ClosestHitFraction);
            ClosestHitFraction = fraction;

            _closestHit = rayResult;
            _normalInWorldSpace = normalInWorldSpaceParam;
            return fraction;
        }

        protected override void Recycle(ref Vector3 from, ref Vector3 to, bool hitNoContResp, CollisionFilterGroups filterGroup = DefaultGroup, CollisionFilterGroups filterMask = DefaultGroup)
        {
            base.Recycle(ref from, ref to, hitNoContResp, filterGroup, filterMask);
            _closestHit = default;
        }

        public static StrideClosestRayResultCallback Shared(ref Vector3 from, ref Vector3 to, bool hitTriggers, CollisionFilterGroups filterGroup = DefaultGroup, CollisionFilterGroups filterMask = DefaultGroup)
        {
            _shared ??= new StrideClosestRayResultCallback();
            _shared.Recycle(ref from, ref to, hitTriggers, filterGroup, filterMask);
            return _shared;
        }
    }

    private abstract class StrideReusableRayResultCallback : RayResultCallback
    {
        /// <summary>
        /// Our <see cref="PhysicsTriggerComponentBase"/> have <see cref="BulletSharp.CollisionFlags.NoContactResponse"/>
        /// set to let objects pass through them.
        /// By default we want intersection test to reflect that behavior to avoid throwing off our users.
        /// This boolean controls whether the test ignores(when false) or includes(when true) <see cref="PhysicsTriggerComponentBase"/>.
        /// </summary>
        private bool _hitNoContactResponseObjects;
        private Vector3 _rayFromWorld;
        private Vector3 _rayToWorld;

        protected HitResult ComputeHitResult(ref LocalRayResult rayResult, bool normalInWorldSpace)
        {
            var obj = rayResult.CollisionObject;
            if (obj == null)
            {
                return new HitResult { Succeeded = false };
            }

            Vector3 normal = rayResult.HitNormalLocal;
            if (!normalInWorldSpace)
            {
                normal = Vector3.TransformNormal(normal, obj.WorldTransform);
            }

            return new HitResult
            {
                Succeeded = true,
                Collider = obj.UserObject as PhysicsComponent,
                Point = Vector3.Lerp(_rayFromWorld, _rayToWorld, rayResult.HitFraction),
                Normal = normal,
                HitFraction = rayResult.HitFraction,
            };
        }

        protected virtual void Recycle(ref Vector3 from, ref Vector3 to, bool hitNoContResp, CollisionFilterGroups filterGroup = DefaultGroup, CollisionFilterGroups filterMask = DefaultGroup)
        {
            _rayFromWorld = from;
            _rayToWorld = to;
            ClosestHitFraction = float.PositiveInfinity;
            Flags = 0;
            CollisionFilterGroup = filterGroup;
            CollisionFilterMask = filterMask;
            _hitNoContactResponseObjects = hitNoContResp;
        }

        public override bool NeedsCollision(BroadphaseProxy proxy0)
        {
            if (_hitNoContactResponseObjects == false
                && proxy0.ClientObject is CollisionObject co
                && (co.CollisionFlags & CollisionFlags.NoContactResponse) != 0)
            {
                return false;
            }

            return base.NeedsCollision(proxy0);
        }
    }

    private abstract class StrideReusableConvexResultCallback : ConvexResultCallback
    {
        /// <summary>
        /// Our <see cref="PhysicsTriggerComponentBase"/> have <see cref="BulletSharp.CollisionFlags.NoContactResponse"/>
        /// set to let objects pass through them.
        /// By default we want intersection test to reflect that behavior to avoid throwing off our users.
        /// This boolean controls whether the test ignores(when false) or includes(when true) <see cref="PhysicsTriggerComponentBase"/>.
        /// </summary>
        private bool _hitNoContactResponseObjects;

        protected static HitResult ComputeHitResult(ref LocalConvexResult convexResult, bool normalInWorldSpace)
        {
            var obj = convexResult.HitCollisionObject;
            if (obj == null)
            {
                return new HitResult { Succeeded = false };
            }

            Vector3 normal = convexResult.HitNormalLocal;
            if (!normalInWorldSpace)
            {
                normal = Vector3.TransformNormal(normal, obj.WorldTransform);
            }

            return new HitResult
            {
                Succeeded = true,
                Collider = obj.UserObject as PhysicsComponent,
                Point = convexResult.HitPointLocal,
                Normal = normal,
                HitFraction = convexResult.HitFraction,
            };
        }

        protected virtual void Recycle(bool hitNoContResp, CollisionFilterGroups filterGroup = DefaultGroup, CollisionFilterGroups filterMask = DefaultGroup)
        {
            ClosestHitFraction = float.PositiveInfinity;
            CollisionFilterGroup = filterGroup;
            CollisionFilterMask = filterMask;
            _hitNoContactResponseObjects = hitNoContResp;
        }

        public override bool NeedsCollision(BroadphaseProxy proxy0)
        {
            if (_hitNoContactResponseObjects == false
                && proxy0.ClientObject is CollisionObject co
                && (co.CollisionFlags & CollisionFlags.NoContactResponse) != 0)
            {
                return false;
            }

            return base.NeedsCollision(proxy0);
        }
    }

    public bool NearBodyWorldRayCast(ref Vector3 position, ref Vector3 feelers, out Vector3 contactPoint, out Vector3 contactNormal)
    {
        throw new NotImplementedException();
    }

    public bool WorldRayCast(ref Vector3 start, ref Vector3 end, Vector3 dir)
    {
        throw new NotImplementedException();
    }
}