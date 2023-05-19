using BulletSharp;
using CasaEngine.Engine.Physics2D;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Physics;

public class PhysicsEngine
{
    const CollisionFilterGroups DefaultGroup = (CollisionFilterGroups)BulletSharp.CollisionFilterGroups.DefaultFilter;

    public DiscreteDynamicsWorld World { get; }
    private readonly BulletSharp.CollisionWorld collisionWorld;

    private readonly BulletSharp.CollisionDispatcher dispatcher;
    private readonly BulletSharp.CollisionConfiguration collisionConfiguration;
    private readonly BulletSharp.DbvtBroadphase broadphase;

    // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
    private readonly BulletSharp.ContactSolverInfo solverInfo;

    private readonly BulletSharp.DispatcherInfo dispatchInfo;

    internal readonly bool CanCcd;

    public bool ContinuousCollisionDetection
    {
        get
        {
            if (!CanCcd)
            {
                throw new Exception("ContinuousCollisionDetection must be enabled at physics engine initialization using the proper flag.");
            }

            return dispatchInfo.UseContinuous;
        }
        set
        {
            if (!CanCcd)
            {
                throw new Exception("ContinuousCollisionDetection must be enabled at physics engine initialization using the proper flag.");
            }

            dispatchInfo.UseContinuous = value;
        }
    }

    /// <summary>
    /// Totally disable the simulation if set to true
    /// </summary>
    public static bool DisableSimulation = false;

    private readonly Dictionary<Collision, (CollisionObject, CollisionObject)> collisions = new();
    private readonly Dictionary<(CollisionObject, CollisionObject), Collision> outdatedCollisions = new();

    private readonly Stack<System.Threading.Channels.Channel<HashSet<ContactPoint>>> channelsPool = new();
    private readonly Dictionary<Collision, (System.Threading.Channels.Channel<HashSet<ContactPoint>> Channel, HashSet<ContactPoint> PreviousContacts)> contactChangedChannels = new();

    private readonly Stack<HashSet<ContactPoint>> contactsPool = new();
    private readonly Dictionary<Collision, HashSet<ContactPoint>> contactsUpToDate = new();

    private readonly List<Collision> markedAsNewColl = new();
    private readonly List<Collision> markedAsDeprecatedColl = new();
    internal readonly HashSet<Collision> EndedFromComponentRemoval = new();

    /// <summary>
    /// Every pair of components currently colliding with each other
    /// </summary>
    public ICollection<Collision> CurrentCollisions => collisions.Keys;

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
    public Vector3 Gravity
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
    }

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


    public PhysicsEngine(Physics3dSettings configuration)
    {
        MaxSubSteps = configuration.MaxSubSteps;
        FixedTimeStep = configuration.FixedTimeStep;

        collisionConfiguration = new BulletSharp.DefaultCollisionConfiguration();
        dispatcher = new BulletSharp.CollisionDispatcher(collisionConfiguration);
        broadphase = new BulletSharp.DbvtBroadphase();

        //this allows characters to have proper physics behavior
        broadphase.OverlappingPairCache.SetInternalGhostPairCallback(new BulletSharp.GhostPairCallback());

        //2D pipeline
        var simplex = new BulletSharp.VoronoiSimplexSolver();
        var pdSolver = new BulletSharp.MinkowskiPenetrationDepthSolver();
        var convexAlgo = new BulletSharp.Convex2DConvex2DAlgorithm.CreateFunc(simplex, pdSolver);

        dispatcher.RegisterCollisionCreateFunc(BulletSharp.BroadphaseNativeType.Convex2DShape, BulletSharp.BroadphaseNativeType.Convex2DShape, convexAlgo);
        //dispatcher.RegisterCollisionCreateFunc(BulletSharp.BroadphaseNativeType.Box2DShape, BulletSharp.BroadphaseNativeType.Convex2DShape, convexAlgo);
        //dispatcher.RegisterCollisionCreateFunc(BulletSharp.BroadphaseNativeType.Convex2DShape, BulletSharp.BroadphaseNativeType.Box2DShape, convexAlgo);
        //dispatcher.RegisterCollisionCreateFunc(BulletSharp.BroadphaseNativeType.Box2DShape, BulletSharp.BroadphaseNativeType.Box2DShape, new BulletSharp.Box2DBox2DCollisionAlgorithm.CreateFunc());
        //~2D pipeline

        //default solver
        var solver = new BulletSharp.SequentialImpulseConstraintSolver();

        if (configuration.Flags.HasFlag(PhysicsEngineFlags.CollisionsOnly))
        {
            collisionWorld = new BulletSharp.CollisionWorld(dispatcher, broadphase, collisionConfiguration);
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
            World = new BulletSharp.DiscreteDynamicsWorld(dispatcher, broadphase, solver, collisionConfiguration);
            collisionWorld = World;
        }

        if (World != null)
        {
            solverInfo = World.SolverInfo; //we are required to keep this reference, or the GC will mess up
            dispatchInfo = World.DispatchInfo;

            solverInfo.SolverMode |= BulletSharp.SolverModes.CacheFriendly; //todo test if helps with performance or not

            if (configuration.Flags.HasFlag(PhysicsEngineFlags.ContinuousCollisionDetection))
            {
                CanCcd = true;
                solverInfo.SolverMode |= BulletSharp.SolverModes.Use2FrictionDirections | BulletSharp.SolverModes.RandomizeOrder;
                dispatchInfo.UseContinuous = true;
            }
            else
            {
                CanCcd = false;
                dispatchInfo.UseContinuous = false;
            }
        }
    }

    internal void UpdateContacts()
    {
        EndedFromComponentRemoval.Clear();
        // Mark previous collisions as outdated,
        // we'll iterate through bullet's actives and remove them from here
        // to be left with only the outdated ones.
        foreach (var collision in collisions)
        {
            outdatedCollisions.Add(collision.Value, collision.Key);
        }

        // If this needs to be even faster, look into btPersistentManifold.ContactStartedCallback,
        // not yet covered by the wrapper

        int numManifolds = collisionWorld.Dispatcher.NumManifolds;
        for (int i = 0; i < numManifolds; i++)
        {
            var persistentManifold = collisionWorld.Dispatcher.GetManifoldByIndexInternal(i);

            int numContacts = persistentManifold.NumContacts;
            if (numContacts == 0)
            {
                continue;
            }

            var ptrA = persistentManifold.Body0;
            var ptrB = persistentManifold.Body1;
            bool aFirst = ptrA.GetHashCode() > ptrB.GetHashCode();
            (CollisionObject, CollisionObject) collId = aFirst ? (ptrA, ptrB) : (ptrB, ptrA);

            // This collision is up-to-date, remove it from the outdated collisions
            if (outdatedCollisions.Remove(collId))
            {
                continue;
            }

            // Likely a new collision, or a duplicate

            var a = collId.Item1;
            var b = collId.Item2;
            var collision = new Collision(a.UserObject as PhysicsComponent, b.UserObject as PhysicsComponent);
            // PairCachingGhostObject has two identical manifolds when colliding, not 100% sure why that is,
            // CompoundColliderShape shapes all map to the same PhysicsComponent but create unique manifolds.
            if (collisions.TryAdd(collision, collId))
            {
                markedAsNewColl.Add(collision);
            }
        }

        // This set only contains outdated collisions by now,
        // mark them as out of date for events and remove them from current collisions
        foreach (var (_, outdatedCollision) in outdatedCollisions)
        {
            markedAsDeprecatedColl.Add(outdatedCollision);
            collisions.Remove(outdatedCollision);
        }

        outdatedCollisions.Clear();
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        //if (mSoftRigidDynamicsWorld != null) mSoftRigidDynamicsWorld.Dispose();
        if (World != null)
        {
            World.Dispose();
        }
        else
        {
            collisionWorld?.Dispose();
        }

        broadphase?.Dispose();
        dispatcher?.Dispose();
        collisionConfiguration?.Dispose();
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
        collisionWorld.RayTest(from, to, callback);
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
        collisionWorld.RayTest(from, to, callback);
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
        collisionWorld.RayTest(from, to, callback);
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

    private readonly SimulationArgs simulationArgs = new SimulationArgs();

    internal void Update(float deltaTime)
    {
        if (collisionWorld == null)
        {
            return;
        }

        simulationArgs.DeltaTime = deltaTime;

        //UpdatedRigidbodies = 0;

        OnSimulationBegin(simulationArgs);

        if (World != null)
        {
            World.StepSimulation(deltaTime, MaxSubSteps, FixedTimeStep);
        }
        else
        {
            collisionWorld.PerformDiscreteCollisionDetection();
        }

        OnSimulationEnd(simulationArgs);
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
        private static StrideAllHitsConvexResultCallback shared;

        private ICollection<HitResult> resultsList;

        public override float AddSingleResult(BulletSharp.LocalConvexResult convexResult, bool normalInWorldSpace)
        {
            resultsList.Add(ComputeHitResult(ref convexResult, normalInWorldSpace));
            return convexResult.HitFraction;
        }

        public static StrideAllHitsConvexResultCallback Shared(ICollection<HitResult> buffer, bool hitTriggers, CollisionFilterGroups filterGroup = DefaultGroup, CollisionFilterGroups filterMask = DefaultGroup)
        {
            shared ??= new StrideAllHitsConvexResultCallback();
            shared.resultsList = buffer;
            shared.Recycle(hitTriggers, filterGroup, filterMask);
            return shared;
        }
    }

    private class StrideClosestConvexResultCallback : StrideReusableConvexResultCallback
    {
        [ThreadStatic]
        private static StrideClosestConvexResultCallback shared;

        private BulletSharp.LocalConvexResult closestHit;
        private bool normalInWorldSpace;

        public HitResult Result => ComputeHitResult(ref closestHit, normalInWorldSpace);

        public override float AddSingleResult(BulletSharp.LocalConvexResult convexResult, bool normalInWorldSpaceParam)
        {
            float fraction = convexResult.HitFraction;

            // This is 'm_closestHitFraction', Bullet will look at this value to ignore hits further away,
            // this method will only be called for hits closer than the last.
            // See btCollisionWorld::rayTestSingleInternal
            System.Diagnostics.Debug.Assert(convexResult.HitFraction <= ClosestHitFraction);
            ClosestHitFraction = fraction;

            closestHit = convexResult;
            normalInWorldSpace = normalInWorldSpaceParam;
            return fraction;
        }

        protected override void Recycle(bool hitNoContResp, CollisionFilterGroups filterGroup = CollisionFilterGroups.DefaultFilter, CollisionFilterGroups filterMask = (CollisionFilterGroups)(-1))
        {
            base.Recycle(hitNoContResp, filterGroup, filterMask);
            closestHit = default;
        }

        public static StrideClosestConvexResultCallback Shared(bool hitTriggers, CollisionFilterGroups filterGroup = DefaultGroup, CollisionFilterGroups filterMask = DefaultGroup)
        {
            shared ??= new StrideClosestConvexResultCallback();
            shared.Recycle(hitTriggers, filterGroup, filterMask);
            return shared;
        }
    }

    private class StrideAllHitsRayResultCallback : StrideReusableRayResultCallback
    {
        [ThreadStatic]
        private static StrideAllHitsRayResultCallback shared;

        private ICollection<HitResult> resultsList;

        public override float AddSingleResult(BulletSharp.LocalRayResult rayResult, bool normalInWorldSpace)
        {
            resultsList.Add(ComputeHitResult(ref rayResult, normalInWorldSpace));
            return rayResult.HitFraction;
        }

        public static StrideAllHitsRayResultCallback Shared(ref Vector3 from, ref Vector3 to, bool hitTriggers, ICollection<HitResult> buffer, CollisionFilterGroups filterGroup = DefaultGroup, CollisionFilterGroups filterMask = DefaultGroup)
        {
            shared ??= new StrideAllHitsRayResultCallback();
            shared.resultsList = buffer;
            shared.Recycle(ref from, ref to, hitTriggers, filterGroup, filterMask);
            return shared;
        }
    }

    private class StrideClosestRayResultCallback : StrideReusableRayResultCallback
    {
        [ThreadStatic]
        private static StrideClosestRayResultCallback shared;

        private BulletSharp.LocalRayResult closestHit;
        private bool normalInWorldSpace;

        public HitResult Result => ComputeHitResult(ref closestHit, normalInWorldSpace);

        public override float AddSingleResult(BulletSharp.LocalRayResult rayResult, bool normalInWorldSpaceParam)
        {
            float fraction = rayResult.HitFraction;

            // This is 'm_closestHitFraction', Bullet will look at this value to ignore hits further away,
            // this method will only be called for hits closer than the last.
            // See btCollisionWorld::rayTestSingleInternal
            System.Diagnostics.Debug.Assert(rayResult.HitFraction <= ClosestHitFraction);
            ClosestHitFraction = fraction;

            closestHit = rayResult;
            normalInWorldSpace = normalInWorldSpaceParam;
            return fraction;
        }

        protected override void Recycle(ref Vector3 from, ref Vector3 to, bool hitNoContResp, CollisionFilterGroups filterGroup = DefaultGroup, CollisionFilterGroups filterMask = DefaultGroup)
        {
            base.Recycle(ref from, ref to, hitNoContResp, filterGroup, filterMask);
            closestHit = default;
        }

        public static StrideClosestRayResultCallback Shared(ref Vector3 from, ref Vector3 to, bool hitTriggers, CollisionFilterGroups filterGroup = DefaultGroup, CollisionFilterGroups filterMask = DefaultGroup)
        {
            shared ??= new StrideClosestRayResultCallback();
            shared.Recycle(ref from, ref to, hitTriggers, filterGroup, filterMask);
            return shared;
        }
    }

    private abstract class StrideReusableRayResultCallback : BulletSharp.RayResultCallback
    {
        /// <summary>
        /// Our <see cref="PhysicsTriggerComponentBase"/> have <see cref="BulletSharp.CollisionFlags.NoContactResponse"/>
        /// set to let objects pass through them.
        /// By default we want intersection test to reflect that behavior to avoid throwing off our users.
        /// This boolean controls whether the test ignores(when false) or includes(when true) <see cref="PhysicsTriggerComponentBase"/>.
        /// </summary>
        private bool hitNoContactResponseObjects;
        private Vector3 rayFromWorld;
        private Vector3 rayToWorld;

        protected HitResult ComputeHitResult(ref BulletSharp.LocalRayResult rayResult, bool normalInWorldSpace)
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
                //Collider = obj.UserObject as PhysicsComponent,
                Point = Vector3.Lerp(rayFromWorld, rayToWorld, rayResult.HitFraction),
                Normal = normal,
                HitFraction = rayResult.HitFraction,
            };
        }

        protected virtual void Recycle(ref Vector3 from, ref Vector3 to, bool hitNoContResp, CollisionFilterGroups filterGroup = DefaultGroup, CollisionFilterGroups filterMask = DefaultGroup)
        {
            rayFromWorld = from;
            rayToWorld = to;
            ClosestHitFraction = float.PositiveInfinity;
            Flags = 0;
            CollisionFilterGroup = filterGroup;
            CollisionFilterMask = filterMask;
            hitNoContactResponseObjects = hitNoContResp;
        }

        public override bool NeedsCollision(BulletSharp.BroadphaseProxy proxy0)
        {
            if (hitNoContactResponseObjects == false
                && proxy0.ClientObject is BulletSharp.CollisionObject co
                && (co.CollisionFlags & BulletSharp.CollisionFlags.NoContactResponse) != 0)
            {
                return false;
            }

            return base.NeedsCollision(proxy0);
        }
    }

    private abstract class StrideReusableConvexResultCallback : BulletSharp.ConvexResultCallback
    {
        /// <summary>
        /// Our <see cref="PhysicsTriggerComponentBase"/> have <see cref="BulletSharp.CollisionFlags.NoContactResponse"/>
        /// set to let objects pass through them.
        /// By default we want intersection test to reflect that behavior to avoid throwing off our users.
        /// This boolean controls whether the test ignores(when false) or includes(when true) <see cref="PhysicsTriggerComponentBase"/>.
        /// </summary>
        private bool hitNoContactResponseObjects;

        protected static HitResult ComputeHitResult(ref BulletSharp.LocalConvexResult convexResult, bool normalInWorldSpace)
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
                //Collider = obj.UserObject as PhysicsComponent,
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
            hitNoContactResponseObjects = hitNoContResp;
        }

        public override bool NeedsCollision(BulletSharp.BroadphaseProxy proxy0)
        {
            if (hitNoContactResponseObjects == false
                && proxy0.ClientObject is BulletSharp.CollisionObject co
                && (co.CollisionFlags & BulletSharp.CollisionFlags.NoContactResponse) != 0)
            {
                return false;
            }

            return base.NeedsCollision(proxy0);
        }
    }

    public bool NearBodyWorldRayCast(ref Vector3 position, ref Vector3 feelers,
        out Vector3 contactPoint, out Vector3 contactNormal)
    {
        throw new NotImplementedException();
    }

    public bool WorldRayCast(ref Vector3 start, ref Vector3 end, Vector3 dir)
    {
        throw new NotImplementedException();
    }
}


public struct HitResult
{
    public Vector3 Normal;

    public Vector3 Point;

    public float HitFraction;

    public bool Succeeded;

    /// <summary>
    /// The Collider hit if Succeeded
    /// </summary>
    //public PhysicsComponent Collider;
}

[Flags]
public enum PhysicsEngineFlags
{
    None = 0x0,

    CollisionsOnly = 0x1,

    SoftBodySupport = 0x2,

    MultiThreaded = 0x4,

    UseHardwareWhenPossible = 0x8,

    ContinuousCollisionDetection = 0x10,
}

/// <summary>
/// A pair of component colliding with each other.
/// Pair of [b,a] is considered equal to [a,b].
/// </summary>
public readonly struct Collision : IEquatable<Collision>
{
    public readonly PhysicsComponent ColliderA;

    public readonly PhysicsComponent ColliderB;

    public Collision(PhysicsComponent a, PhysicsComponent b)
    {
        ColliderA = a;
        ColliderB = b;
    }

    public static bool operator ==(in Collision a, in Collision b)
    {
        return (Equals(a.ColliderA, b.ColliderA) && Equals(a.ColliderB, b.ColliderB))
               || (Equals(a.ColliderB, b.ColliderA) && Equals(a.ColliderA, b.ColliderB));
    }

    public static bool operator !=(in Collision a, in Collision b) => (a == b) == false;

    public override bool Equals(object obj)
    {
        return obj is Collision other && Equals(other);
    }

    public bool Equals(Collision other) => this == other;

    public override int GetHashCode()
    {
        int aH = ColliderA.GetHashCode();
        int bH = ColliderB.GetHashCode();
        // This ensures that a pair of components will return the same hash regardless
        // of if they are setup as [b,a] or [a,b]
        return aH > bH ? HashCode.Combine(aH, bH) : HashCode.Combine(bH, aH);
    }
}

public struct ContactPoint : IEquatable<ContactPoint>
{
    public PhysicsComponent ColliderA;
    public PhysicsComponent ColliderB;
    public float Distance;
    public Vector3 Normal;
    public Vector3 PositionOnA;
    public Vector3 PositionOnB;


    public bool Equals(ContactPoint other)
    {
        return ((ColliderA == other.ColliderA && ColliderB == other.ColliderB)
                || (ColliderA == other.ColliderB && ColliderB == other.ColliderA))
               && Distance == other.Distance
               && Normal == other.Normal
               && PositionOnA == other.PositionOnA
               && PositionOnB == other.PositionOnB;
    }


    public override bool Equals(object obj) => obj is ContactPoint other && Equals(other);


    public override int GetHashCode()
    {
        return HashCode.Combine(ColliderA, ColliderB, Distance, Normal, PositionOnA, PositionOnB);
    }
}

public enum ConstraintTypes
{
    /// <summary>
    ///     The translation vector of the matrix to create this will represent the pivot, the rest is ignored
    /// </summary>
    Point2Point,

    Hinge,

    Slider,

    ConeTwist,

    Generic6DoF,

    Generic6DoFSpring,

    /// <summary>
    ///     The translation vector of the matrix to create this will represent the axis, the rest is ignored
    /// </summary>
    Gear,
}