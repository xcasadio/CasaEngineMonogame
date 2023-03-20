using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Collections;
using BepuUtilities.Memory;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Numerics;
using Microsoft.Xna.Framework;
using MathHelper = BepuUtilities.MathHelper;
using Vector3 = System.Numerics.Vector3;

namespace CasaEngine.Engine.Physics;

public class PhysicsEngine
{
    private float _timeAccumulator;

    struct ContactResponseParticle
    {
        public Vector3 Position;
        public float Age;
        public Vector3 Normal;
    }

    class EventHandler : IContactEventHandler
    {
        public Simulation Simulation;
        public BufferPool Pool;
        public QuickList<ContactResponseParticle> Particles;

        public EventHandler(Simulation simulation, BufferPool pool)
        {
            Simulation = simulation;
            Particles = new QuickList<ContactResponseParticle>(128, pool);
            Pool = pool;
        }

        public void OnContactAdded<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold,
            Vector3 contactOffset, Vector3 contactNormal, float depth, int featureId, int contactIndex, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold>
        {
            //Simply ignore any particles beyond the allocated space.
            var index = Interlocked.Increment(ref Particles.Count) - 1;
            if (index < Particles.Span.Length)
            {
                ref var particle = ref Particles[index];

                //Contact data is calibrated according to the order of the pair, so using A's position is important.
                particle.Position = contactOffset + (pair.A.Mobility == CollidableMobility.Static ?
                    new StaticReference(pair.A.StaticHandle, Simulation.Statics).Pose.Position :
                    new BodyReference(pair.A.BodyHandle, Simulation.Bodies).Pose.Position);
                particle.Age = 0;
                particle.Normal = contactNormal;
            }
        }

        public void Dispose()
        {
            //In the demo we won't actually call this, since it's going to persist until the demo dies. At that point, the buffer pool will be dropped and all its allocations will be cleaned up anyway.
            Particles.Dispose(Pool);
        }

    }

    /// <summary>
    /// Gets the simulation created by the demo's Initialize call.
    /// </summary>
    public Simulation Simulation { get; protected set; }

    //Note that the buffer pool used by the simulation is not considered to be *owned* by the simulation. The simulation merely uses the pool.
    //Disposing the simulation will not dispose or clear the buffer pool.
    /// <summary>
    /// Gets the buffer pool used by the demo's simulation.
    /// </summary>
    public BufferPool BufferPool { get; } = new();

    /// <summary>
    /// Gets the thread dispatcher available for use by the simulation.
    /// </summary>
    public ThreadDispatcher ThreadDispatcher { get; }

    public PhysicsEngine()
    {
        //Generally, shoving as many threads as possible into the simulation won't produce the best results on systems with multiple logical cores per physical core.
        //Environment.ProcessorCount reports logical core count only, so we'll use a simple heuristic here- it'll leave one or two logical cores idle.
        //For the common Intel quad core with hyperthreading, this'll use six logical cores and leave two logical cores free to be used for other stuff.
        //This is by no means perfect. To maximize performance, you'll need to profile your simulation and target hardware.
        //Note that issues can be magnified on older operating systems like Windows 7 if all logical cores are given work.

        //Generally, the more memory bandwidth you have relative to CPU compute throughput, and the more collision detection heavy the simulation is relative to solving,
        //the more benefit you get out of SMT/hyperthreading. 
        //For example, if you're using the 64 core quad memory channel AMD 3990x on a scene composed of thousands of ragdolls, 
        //there won't be enough memory bandwidth to even feed half the physical cores. Using all 128 logical cores would just add overhead.

        //It may be worth using something like hwloc or CPUID to extract extra information to reason about.
        var targetThreadCount = Math.Max(1, Environment.ProcessorCount > 4 ? Environment.ProcessorCount - 2 : Environment.ProcessorCount - 1);
        ThreadDispatcher = new ThreadDispatcher(targetThreadCount);
    }

    public void Initialize()
    {
        var events = new ContactEvents(ThreadDispatcher, BufferPool);
        Simulation = Simulation.Create(BufferPool, new ContactEventCallbacks(events), new DemoPoseIntegratorCallbacks(new Vector3(0, -10, 0)), new SolveDescription(8, 1));
        var eventHandler = new EventHandler(Simulation, BufferPool);
    }

    public void Update(GameTime gameTime)
    {
        //And here's an example of how to use an accumulator to take a number of timesteps of fixed length in response to variable update dt:
        _timeAccumulator = (float)(gameTime.ElapsedGameTime.TotalMilliseconds / 1000d);
        var targetTimestepDuration = 1 / 120f;
        while (_timeAccumulator >= targetTimestepDuration)
        {
            Simulation.Timestep(targetTimestepDuration);//, ThreadDispatcher);
            _timeAccumulator -= targetTimestepDuration;
        }
        //If you wanted to smooth out the positions of rendered objects to avoid the 'jitter' that an unpredictable number of time steps per update would cause,
        //you can just interpolate the previous and current states using a weight based on the time remaining in the accumulator:
        //var interpolationWeight = _timeAccumulator / targetTimestepDuration;
    }

    public bool NearBodyWorldRayCast(ref Microsoft.Xna.Framework.Vector3 position, ref Microsoft.Xna.Framework.Vector3 feelers,
        out Microsoft.Xna.Framework.Vector3 contactPoint, out Microsoft.Xna.Framework.Vector3 contactNormal)
    {
        throw new NotImplementedException();
    }

    public bool WorldRayCast(ref Microsoft.Xna.Framework.Vector3 start, ref Microsoft.Xna.Framework.Vector3 end, Microsoft.Xna.Framework.Vector3 dir)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Implements handlers for various collision events.
/// </summary>
public interface IContactEventHandler
{
    /// <summary>
    /// Fires when a contact is added.
    /// </summary>
    /// <typeparam name="TManifold">Type of the contact manifold detected.</typeparam>
    /// <param name="eventSource">Collidable that the event was attached to.</param>
    /// <param name="pair">Collidable pair triggering the event.</param>
    /// <param name="contactManifold">Set of remaining contacts in the collision.</param>
    /// <param name="contactOffset">Offset from the pair's local origin to the new contact.</param>
    /// <param name="contactNormal">Normal of the new contact.</param>
    /// <param name="depth">Depth of the new contact.</param>
    /// <param name="featureId">Feature id of the new contact.</param>
    /// <param name="contactIndex">Index of the new contact in the contact manifold.</param>
    /// <param name="workerIndex">Index of the worker thread that fired this event.</param>
    void OnContactAdded<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold,
        Vector3 contactOffset, Vector3 contactNormal, float depth, int featureId, int contactIndex, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold>
    {
    }

    /// <summary>
    /// Fires when a contact is removed.
    /// </summary>
    /// <typeparam name="TManifold">Type of the contact manifold detected.</typeparam>
    /// <param name="eventSource">Collidable that the event was attached to.</param>
    /// <param name="pair">Collidable pair triggering the event.</param>
    /// <param name="contactManifold">Set of remaining contacts in the collision.</param>
    /// <param name="removedFeatureId">Feature id of the contact that was removed and is no longer present in the contact manifold.</param>
    /// <param name="workerIndex">Index of the worker thread that fired this event.</param>
    void OnContactRemoved<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, int removedFeatureId, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold>
    {
    }

    /// <summary>
    /// Fires the first time a pair is observed to be touching. Touching means that there are contacts with nonnegative depths in the manifold.
    /// </summary>
    /// <typeparam name="TManifold">Type of the contact manifold detected.</typeparam>
    /// <param name="eventSource">Collidable that the event was attached to.</param>
    /// <param name="pair">Collidable pair triggering the event.</param>
    /// <param name="contactManifold">Set of remaining contacts in the collision.</param>
    /// <param name="workerIndex">Index of the worker thread that fired this event.</param>
    void OnStartedTouching<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold>
    {
    }

    /// <summary>
    /// Fires whenever a pair is observed to be touching. Touching means that there are contacts with nonnegative depths in the manifold. Will not fire for sleeping pairs.
    /// </summary>
    /// <typeparam name="TManifold">Type of the contact manifold detected.</typeparam>
    /// <param name="eventSource">Collidable that the event was attached to.</param>
    /// <param name="pair">Collidable pair triggering the event.</param>
    /// <param name="contactManifold">Set of remaining contacts in the collision.</param>
    /// <param name="workerIndex">Index of the worker thread that fired this event.</param>
    void OnTouching<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold>
    {
    }


    /// <summary>
    /// Fires when a pair stops touching. Touching means that there are contacts with nonnegative depths in the manifold.
    /// </summary>
    /// <typeparam name="TManifold">Type of the contact manifold detected.</typeparam>
    /// <param name="eventSource">Collidable that the event was attached to.</param>
    /// <param name="pair">Collidable pair triggering the event.</param>
    /// <param name="contactManifold">Set of remaining contacts in the collision.</param>
    /// <param name="workerIndex">Index of the worker thread that fired this event.</param>
    void OnStoppedTouching<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold>
    {
    }


    /// <summary>
    /// Fires when a pair is observed for the first time.
    /// </summary>
    /// <typeparam name="TManifold">Type of the contact manifold detected.</typeparam>
    /// <param name="eventSource">Collidable that the event was attached to.</param>
    /// <param name="pair">Collidable pair triggering the event.</param>
    /// <param name="contactManifold">Set of remaining contacts in the collision.</param>
    /// <param name="workerIndex">Index of the worker thread that fired this event.</param>
    void OnPairCreated<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold>
    {
    }

    /// <summary>
    /// Fires whenever a pair is updated. Will not fire for sleeping pairs.
    /// </summary>
    /// <typeparam name="TManifold">Type of the contact manifold detected.</typeparam>
    /// <param name="eventSource">Collidable that the event was attached to.</param>
    /// <param name="pair">Collidable pair triggering the event.</param>
    /// <param name="contactManifold">Set of remaining contacts in the collision.</param>
    /// <param name="workerIndex">Index of the worker thread that fired this event.</param>
    void OnPairUpdated<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold>
    {
    }

    /// <summary>
    /// Fires when a pair ends.
    /// </summary>
    /// <typeparam name="TManifold">Type of the contact manifold detected.</typeparam>
    /// <param name="eventSource">Collidable that the event was attached to.</param>
    /// <param name="pair">Collidable pair triggering the event.</param>
    void OnPairEnded(CollidableReference eventSource, CollidablePair pair)
    {
    }
}

/// <summary>
/// Watches a set of bodies and statics for contact changes and reports events.
/// </summary>
public class ContactEvents : IDisposable
{
    //To know what events to emit, we have to track the previous state of a collision. We don't need to keep around old positions/offets/normals/depths, so it's quite a bit lighter.
    [StructLayout(LayoutKind.Sequential)]
    struct PreviousCollision
    {
        public CollidableReference Collidable;
        public bool Fresh;
        public bool WasTouching;
        public int ContactCount;
        //FeatureIds are identifiers encoding what features on the involved shapes contributed to the contact. We store up to 4 feature ids, one for each potential contact.
        //A "feature" is things like a face, vertex, or edge. There is no single interpretation for what a feature is- the mapping is defined on a per collision pair level.
        //In this demo, we only care to check whether a given contact in the current frame maps onto a contact from a previous frame.
        //We can use this to only emit 'contact added' events when a new contact with an unrecognized id is reported.
        public int FeatureId0;
        public int FeatureId1;
        public int FeatureId2;
        public int FeatureId3;
    }

    Simulation simulation;
    IThreadDispatcher threadDispatcher;
    //BepuUtilities.Memory.WorkerBufferPools threadPools;
    BufferPool pool;

    //We'll use a handle->index mapping in a CollidableProperty to point at our contiguously stored listeners (in the later listeners array).
    //Note that there's also IndexSets for the statics and bodies; those will be checked first before accessing the listenerIndices.
    //The CollidableProperty is quite barebones- it doesn't try to stop all invalid accesses, and the backing memory isn't guaranteed to be zero initialized.
    //IndexSets are tightly bitpacked and are cheap to access, so they're an easy way to check if a collidable can trigger an event before doing any further processing.
    CollidableProperty<int> listenerIndices;
    IndexSet staticListenerFlags;
    IndexSet bodyListenerFlags;
    int listenerCount;

    //For the purpose of this demo, we'll use some regular ol' interfaces rather than using the struct-implementing-interface for specialization.
    //This array will be GC tracked as a result, but that should be mostly fine. If you've got hundreds of thousands of event handlers, you may want to consider alternatives.
    struct Listener
    {
        public CollidableReference Source;
        public IContactEventHandler Handler;
        public QuickList<PreviousCollision> PreviousCollisions;
    }
    Listener[] listeners;

    //The callbacks are invoked from a multithreaded context, and we don't know how many pairs will exist. 
    //Rather than attempting to synchronize all accesses, every worker thread spits out the results into a worker-local list to be processed later by the main thread flush.
    struct PendingWorkerAdd
    {
        public int ListenerIndex;
        public PreviousCollision Collision;
    }
    QuickList<PendingWorkerAdd>[] pendingWorkerAdds;

    /// <summary>
    /// Creates a new contact events stream.
    /// </summary>
    /// <param name="threadDispatcher">Thread dispatcher to pull per-thread buffer pools from, if any.</param>
    /// <param name="pool">Buffer pool used to manage resources internally. If null, the simulation's pool will be used.</param>
    /// <param name="initialListenerCapacity">Number of listeners to allocate space for initially.</param>
    public ContactEvents(IThreadDispatcher threadDispatcher = null, BufferPool pool = null, int initialListenerCapacity = 64)
    {
        this.threadDispatcher = threadDispatcher;
        this.pool = pool;
        listeners = new Listener[initialListenerCapacity];
    }

    IUnmanagedMemoryPool GetPoolForWorker(int workerIndex)
    {
        return pool;
        //return threadDispatcher == null ? pool : threadPools[workerIndex];
    }

    /// <summary>
    /// Initializes the contact events system with a simulation.
    /// </summary>
    /// <param name="simulation">Simulation to use with the contact events demo.</param>
    /// <remarks>The constructor and initialization are split because of how this class is expected to be used. 
    /// It will be passed into a simulation's constructor as a part of its contact callbacks, so there is no simulation available at the time of construction.</remarks>
    public void Initialize(Simulation simulation)
    {
        this.simulation = simulation;
        if (pool == null)
            pool = simulation.BufferPool;
        //threadPools = threadDispatcher != null ? new WorkerBufferPools(pool, threadDispatcher.ThreadCount) : null;
        simulation.Timestepper.BeforeCollisionDetection += SetFreshnessForCurrentActivityStatus;
        listenerIndices = new CollidableProperty<int>(simulation, pool);
        pendingWorkerAdds = new QuickList<PendingWorkerAdd>[threadDispatcher == null ? 1 : threadDispatcher.ThreadCount];
    }

    /// <summary>
    /// Begins listening for events related to the given collidable.
    /// </summary>
    /// <param name="collidable">Collidable to monitor for events.</param>
    /// <param name="handler">Handlers to use for the collidable.</param>
    public void Register(CollidableReference collidable, IContactEventHandler handler)
    {
        Debug.Assert(!IsListener(collidable), "Should only try to register listeners that weren't previously registered");
        if (collidable.Mobility == CollidableMobility.Static)
            staticListenerFlags.Add(collidable.RawHandleValue, pool);
        else
            bodyListenerFlags.Add(collidable.RawHandleValue, pool);
        if (listenerCount > listeners.Length)
        {
            Array.Resize(ref listeners, listeners.Length * 2);
        }
        //Note that allocations for the previous collision list are deferred until they actually exist.
        listeners[listenerCount] = new Listener { Handler = handler, Source = collidable };
        listenerIndices[collidable] = listenerCount;
        ++listenerCount;
    }

    /// <summary>
    /// Begins listening for events related to the given body.
    /// </summary>
    /// <param name="body">Body to monitor for events.</param>
    /// <param name="handler">Handlers to use for the body.</param>
    public void Register(BodyHandle body, IContactEventHandler handler)
    {
        Register(simulation.Bodies[body].CollidableReference, handler);
    }

    /// <summary>
    /// Begins listening for events related to the given static.
    /// </summary>
    /// <param name="staticHandle">Static to monitor for events.</param>
    /// <param name="handler">Handlers to use for the static.</param>
    public void Register(StaticHandle staticHandle, IContactEventHandler handler)
    {
        Register(new CollidableReference(staticHandle), handler);
    }

    /// <summary>
    /// Stops listening for events related to the given collidable.
    /// </summary>
    /// <param name="collidable">Collidable to stop listening for.</param>
    public void Unregister(CollidableReference collidable)
    {
        Debug.Assert(IsListener(collidable), "Should only try to unregister listeners that actually exist.");
        if (collidable.Mobility == CollidableMobility.Static)
        {
            staticListenerFlags.Remove(collidable.RawHandleValue);
        }
        else
        {
            bodyListenerFlags.Remove(collidable.RawHandleValue);
        }
        var index = listenerIndices[collidable];
        --listenerCount;
        ref var removedSlot = ref listeners[index];
        if (removedSlot.PreviousCollisions.Span.Allocated)
            removedSlot.PreviousCollisions.Dispose(pool);
        ref var lastSlot = ref listeners[listenerCount];
        if (index < listenerCount)
        {
            listenerIndices[lastSlot.Source] = index;
            removedSlot = lastSlot;
        }
        lastSlot = default;
    }

    /// <summary>
    /// Stops listening for events related to the given body.
    /// </summary>
    /// <param name="body">Body to stop listening for.</param>
    public void Unregister(BodyHandle body)
    {
        Unregister(simulation.Bodies[body].CollidableReference);
    }

    /// <summary>
    /// Stops listening for events related to the given static.
    /// </summary>
    /// <param name="staticHandle">Static to stop listening for.</param>
    public void Unregister(StaticHandle staticHandle)
    {
        Unregister(new CollidableReference(staticHandle));
    }

    /// <summary>
    /// Checks if a collidable is registered as a listener.
    /// </summary>
    /// <param name="collidable">Collidable to check.</param>
    /// <returns>True if the collidable has been registered as a listener, false otherwise.</returns>
    public bool IsListener(CollidableReference collidable)
    {
        if (collidable.Mobility == CollidableMobility.Static)
        {
            return staticListenerFlags.Contains(collidable.RawHandleValue);
        }
        else
        {
            return bodyListenerFlags.Contains(collidable.RawHandleValue);
        }
    }

    /// <summary>
    /// Callback attached to the simulation's ITimestepper which executes just prior to collision detection to take a snapshot of activity states to determine which pairs we should expect updates in.
    /// </summary>
    void SetFreshnessForCurrentActivityStatus(float dt, IThreadDispatcher threadDispatcher)
    {
        //Every single pair tracked by the contact events has a 'freshness' flag. If the final flush sees a pair that is stale, it'll remove it
        //and any necessary events to represent the end of that pair are reported.
        //HandleManifoldForCollidable sets 'Fresh' to true for any processed pair, but pairs between sleeping or static bodies will not show up in HandleManifoldForCollidable since they're not active.
        //We don't want Flush to report that sleeping pairs have stopped colliding, so we pre-initialize any such sleeping/static pair as 'fresh'.

        //This could be multithreaded reasonably easily if there are a ton of listeners or collisions, but that would be a pretty high bar.
        //For simplicity, the demo will keep it single threaded.
        var bodyHandleToLocation = simulation.Bodies.HandleToLocation;
        for (int listenerIndex = 0; listenerIndex < listenerCount; ++listenerIndex)
        {
            ref var listener = ref listeners[listenerIndex];
            var source = listener.Source;
            //If it's a body, and it's in the active set (index 0), then every pair associated with the listener should expect updates.
            var sourceExpectsUpdates = source.Mobility != CollidableMobility.Static && bodyHandleToLocation[source.BodyHandle.Value].SetIndex == 0;
            if (sourceExpectsUpdates)
            {
                var previousCollisions = listeners[listenerIndex].PreviousCollisions;
                for (int j = 0; j < previousCollisions.Count; ++j)
                {
                    //Pair updates will set the 'freshness' to true when they happen, so that they won't be considered 'stale' in the flush and removed.
                    previousCollisions[j].Fresh = false;
                }
            }
            else
            {
                //The listener is either static or sleeping. We should only expect updates if the other collidable is awake.
                var previousCollisions = listeners[listenerIndex].PreviousCollisions;
                for (int j = 0; j < previousCollisions.Count; ++j)
                {
                    ref var previousCollision = ref previousCollisions[j];
                    previousCollision.Fresh = previousCollision.Collidable.Mobility == CollidableMobility.Static || bodyHandleToLocation[previousCollision.Collidable.BodyHandle.Value].SetIndex > 0;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void UpdatePreviousCollision<TManifold>(ref PreviousCollision collision, ref TManifold manifold, bool isTouching) where TManifold : unmanaged, IContactManifold<TManifold>
    {
        Debug.Assert(manifold.Count <= 4, "This demo was built on the assumption that nonconvex manifolds will have a maximum of four contacts, but that might have changed.");
        //If the above assert gets hit because of a change to nonconvex manifold capacities, the packed feature id representation this uses will need to be updated.
        //I very much doubt the nonconvex manifold will ever use more than 8 contacts, so addressing this wouldn't require much of a change.
        for (int j = 0; j < manifold.Count; ++j)
        {
            Unsafe.Add(ref collision.FeatureId0, j) = manifold.GetFeatureId(j);
        }
        collision.ContactCount = manifold.Count;
        collision.Fresh = true;
        collision.WasTouching = isTouching;
    }

    void HandleManifoldForCollidable<TManifold>(int workerIndex, CollidableReference source, CollidableReference other, CollidablePair pair, ref TManifold manifold) where TManifold : unmanaged, IContactManifold<TManifold>
    {
        //The "source" refers to the object that an event handler was (potentially) attached to, so we look for listeners registered for it.
        //(This function is called for both orders of the pair, so we'll catch listeners for either.)
        if (IsListener(source))
        {
            var listenerIndex = listenerIndices[source];
            //This collidable is registered. Is the opposing collidable present?
            ref var listener = ref listeners[listenerIndex];

            int previousCollisionIndex = -1;
            bool isTouching = false;
            for (int i = 0; i < listener.PreviousCollisions.Count; ++i)
            {
                ref var collision = ref listener.PreviousCollisions[i];
                //Since the 'Packed' field contains both the handle type (dynamic, kinematic, or static) and the handle index packed into a single bitfield, an equal value guarantees we are dealing with the same collidable.
                if (collision.Collidable.Packed == other.Packed)
                {
                    previousCollisionIndex = i;
                    //This manifold is associated with an existing collision.
                    //For every contact in the old collsion still present (by feature id), set a flag in this bitmask so we can know when a contact is removed.
                    int previousContactsStillExist = 0;
                    for (int contactIndex = 0; contactIndex < manifold.Count; ++contactIndex)
                    {
                        //We can check if each contact was already present in the previous frame by looking at contact feature ids. See the 'PreviousCollision' type for a little more info on FeatureIds.
                        var featureId = manifold.GetFeatureId(contactIndex);
                        var featureIdWasInPreviousCollision = false;
                        for (int previousContactIndex = 0; previousContactIndex < collision.ContactCount; ++previousContactIndex)
                        {
                            if (featureId == Unsafe.Add(ref collision.FeatureId0, previousContactIndex))
                            {
                                featureIdWasInPreviousCollision = true;
                                previousContactsStillExist |= 1 << previousContactIndex;
                                break;
                            }
                        }
                        if (!featureIdWasInPreviousCollision)
                        {
                            manifold.GetContact(contactIndex, out var offset, out var normal, out var depth, out _);
                            listener.Handler.OnContactAdded(source, pair, ref manifold, offset, normal, depth, featureId, contactIndex, workerIndex);
                        }
                        if (manifold.GetDepth(ref manifold, contactIndex) >= 0)
                            isTouching = true;
                    }
                    if (previousContactsStillExist != (1 << collision.ContactCount) - 1)
                    {
                        //At least one contact that used to exist no longer does.
                        for (int previousContactIndex = 0; previousContactIndex < collision.ContactCount; ++previousContactIndex)
                        {
                            if ((previousContactsStillExist & (1 << previousContactIndex)) == 0)
                            {
                                listener.Handler.OnContactRemoved(source, pair, ref manifold, Unsafe.Add(ref collision.FeatureId0, previousContactIndex), workerIndex);
                            }
                        }
                    }
                    if (!collision.WasTouching && isTouching)
                    {
                        listener.Handler.OnStartedTouching(source, pair, ref manifold, workerIndex);
                    }
                    else if (collision.WasTouching && !isTouching)
                    {
                        listener.Handler.OnStoppedTouching(source, pair, ref manifold, workerIndex);
                    }
                    if (isTouching)
                    {
                        listener.Handler.OnTouching(source, pair, ref manifold, workerIndex);
                    }
                    UpdatePreviousCollision(ref collision, ref manifold, isTouching);
                    break;
                }
            }
            if (previousCollisionIndex < 0)
            {
                //There was no collision previously.
                ref var addsforWorker = ref pendingWorkerAdds[workerIndex];
                //EnsureCapacity will create the list if it doesn't already exist.
                addsforWorker.EnsureCapacity(Math.Max(addsforWorker.Count + 1, 64), GetPoolForWorker(workerIndex));
                ref var pendingAdd = ref addsforWorker.AllocateUnsafely();
                pendingAdd.ListenerIndex = listenerIndex;
                pendingAdd.Collision.Collidable = other;
                listener.Handler.OnPairCreated(source, pair, ref manifold, workerIndex);
                //Dispatch events for all contacts in this new manifold.
                for (int i = 0; i < manifold.Count; ++i)
                {
                    manifold.GetContact(i, out var offset, out var normal, out var depth, out var featureId);
                    listener.Handler.OnContactAdded(source, pair, ref manifold, offset, normal, depth, featureId, i, workerIndex);
                    if (depth >= 0)
                        isTouching = true;
                }
                if (isTouching)
                {
                    listener.Handler.OnStartedTouching(source, pair, ref manifold, workerIndex);
                    listener.Handler.OnTouching(source, pair, ref manifold, workerIndex);
                }
                UpdatePreviousCollision(ref pendingAdd.Collision, ref manifold, isTouching);
            }
            listener.Handler.OnPairUpdated(source, pair, ref manifold, workerIndex);

        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void HandleManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold) where TManifold : unmanaged, IContactManifold<TManifold>
    {
        HandleManifoldForCollidable(workerIndex, pair.A, pair.B, pair, ref manifold);
        HandleManifoldForCollidable(workerIndex, pair.B, pair.A, pair, ref manifold);
    }

    //For final events fired by the flush that still expect a manifold, we'll provide a special empty type.
    struct EmptyManifold : IContactManifold<EmptyManifold>
    {
        public int Count => 0;
        public bool Convex => true;
        //This type never has any contacts, so there's no need for any property grabbers.
        public void GetContact(int contactIndex, out Vector3 offset, out Vector3 normal, out float depth, out int featureId) { throw new NotImplementedException(); }
        public ref float GetDepth(ref EmptyManifold manifold, int contactIndex) { throw new NotImplementedException(); }
        public int GetFeatureId(int contactIndex) { throw new NotImplementedException(); }
        public ref int GetFeatureId(ref EmptyManifold manifold, int contactIndex) { throw new NotImplementedException(); }
        public ref Vector3 GetNormal(ref EmptyManifold manifold, int contactIndex) { throw new NotImplementedException(); }
        public ref Vector3 GetOffset(ref EmptyManifold manifold, int contactIndex) { throw new NotImplementedException(); }
    }

    public void Flush()
    {
        //For simplicity, this is completely sequential. Note that it's technically possible to extract more parallelism, but the complexity cost is high and you would need
        //very large numbers of events being processed to make it worth it.

        //Remove any stale collisions. Stale collisions are those which should have received a new manifold update but did not because the manifold is no longer active.
        for (int i = 0; i < listenerCount; ++i)
        {
            ref var listener = ref listeners[i];
            //Note reverse order. We remove during iteration.
            for (int j = listener.PreviousCollisions.Count - 1; j >= 0; --j)
            {
                ref var collision = ref listener.PreviousCollisions[j];
                if (!collision.Fresh)
                {
                    //Sort the references to be consistent with the direct narrow phase results.
                    CollidablePair pair;
                    NarrowPhase.SortCollidableReferencesForPair(listener.Source, collision.Collidable, out _, out _, out pair.A, out pair.B);
                    if (collision.ContactCount > 0)
                    {
                        var emptyManifold = new EmptyManifold();
                        for (int previousContactCount = 0; previousContactCount < collision.ContactCount; ++previousContactCount)
                        {
                            listener.Handler.OnContactRemoved(listener.Source, pair, ref emptyManifold, Unsafe.Add(ref collision.FeatureId0, previousContactCount), 0);
                        }
                        if (collision.WasTouching)
                            listener.Handler.OnStoppedTouching(listener.Source, pair, ref emptyManifold, 0);
                    }
                    listener.Handler.OnPairEnded(collision.Collidable, pair);
                    //This collision was not updated since the last flush despite being active. It should be removed.
                    listener.PreviousCollisions.FastRemoveAt(j);
                    if (listener.PreviousCollisions.Count == 0)
                    {
                        listener.PreviousCollisions.Dispose(pool);
                        listener.PreviousCollisions = default;
                    }
                }
                else
                {
                    collision.Fresh = false;
                }
            }
        }

        for (int i = 0; i < pendingWorkerAdds.Length; ++i)
        {
            ref var pendingAdds = ref pendingWorkerAdds[i];
            for (int j = 0; j < pendingAdds.Count; ++j)
            {
                ref var add = ref pendingAdds[j];
                ref var collisions = ref listeners[add.ListenerIndex].PreviousCollisions;
                //Ensure capacity will initialize the slot if necessary.
                collisions.EnsureCapacity(Math.Max(8, collisions.Count + 1), pool);
                collisions.AllocateUnsafely() = pendingAdds[j].Collision;
            }
            if (pendingAdds.Span.Allocated)
                pendingAdds.Dispose(GetPoolForWorker(i));
            //We rely on zeroing out the count for lazy initialization.
            pendingAdds = default;
        }
        //threadPools?.Clear();
    }

    public void Dispose()
    {
        if (bodyListenerFlags.Flags.Allocated)
            bodyListenerFlags.Dispose(pool);
        if (staticListenerFlags.Flags.Allocated)
            staticListenerFlags.Dispose(pool);
        listenerIndices.Dispose();
        simulation.Timestepper.BeforeCollisionDetection -= SetFreshnessForCurrentActivityStatus;
        //threadPools?.Dispose();
        for (int i = 0; i < pendingWorkerAdds.Length; ++i)
        {
            Debug.Assert(!pendingWorkerAdds[i].Span.Allocated, "The pending worker adds should have been disposed by the previous flush.");
        }
    }
}

//The narrow phase needs a way to tell our contact events system about changes to contacts, so they'll need to be a part of the INarrowPhaseCallbacks.
public struct ContactEventCallbacks : INarrowPhaseCallbacks
{
    ContactEvents events;

    public ContactEventCallbacks(ContactEvents events)
    {
        this.events = events;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
    {
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB)
    {
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : unmanaged, IContactManifold<TManifold>
    {
        pairMaterial.FrictionCoefficient = 1f;
        pairMaterial.MaximumRecoveryVelocity = 2f;
        pairMaterial.SpringSettings = new SpringSettings(30, 1);
        events.HandleManifold(workerIndex, pair, ref manifold);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold)
    {
        return true;
    }

    public void Initialize(Simulation simulation)
    {
        events.Initialize(simulation);
    }

    public void Dispose()
    {
    }

}
public struct DemoPoseIntegratorCallbacks : IPoseIntegratorCallbacks
{
    /// <summary>
    /// Gravity to apply to dynamic bodies in the simulation.
    /// </summary>
    public Vector3 Gravity;
    /// <summary>
    /// Fraction of dynamic body linear velocity to remove per unit of time. Values range from 0 to 1. 0 is fully undamped, while values very close to 1 will remove most velocity.
    /// </summary>
    public float LinearDamping;
    /// <summary>
    /// Fraction of dynamic body angular velocity to remove per unit of time. Values range from 0 to 1. 0 is fully undamped, while values very close to 1 will remove most velocity.
    /// </summary>
    public float AngularDamping;


    /// <summary>
    /// Gets how the pose integrator should handle angular velocity integration.
    /// </summary>
    public readonly AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;

    /// <summary>
    /// Gets whether the integrator should use substepping for unconstrained bodies when using a substepping solver.
    /// If true, unconstrained bodies will be integrated with the same number of substeps as the constrained bodies in the solver.
    /// If false, unconstrained bodies use a single step of length equal to the dt provided to Simulation.Timestep. 
    /// </summary>
    public readonly bool AllowSubstepsForUnconstrainedBodies => false;

    /// <summary>
    /// Gets whether the velocity integration callback should be called for kinematic bodies.
    /// If true, IntegrateVelocity will be called for bundles including kinematic bodies.
    /// If false, kinematic bodies will just continue using whatever velocity they have set.
    /// Most use cases should set this to false.
    /// </summary>
    public readonly bool IntegrateVelocityForKinematics => false;

    public void Initialize(Simulation simulation)
    {
        //In this demo, we don't need to initialize anything.
        //If you had a simulation with per body gravity stored in a CollidableProperty<T> or something similar, having the simulation provided in a callback can be helpful.
    }

    /// <summary>
    /// Creates a new set of simple callbacks for the demos.
    /// </summary>
    /// <param name="gravity">Gravity to apply to dynamic bodies in the simulation.</param>
    /// <param name="linearDamping">Fraction of dynamic body linear velocity to remove per unit of time. Values range from 0 to 1. 0 is fully undamped, while values very close to 1 will remove most velocity.</param>
    /// <param name="angularDamping">Fraction of dynamic body angular velocity to remove per unit of time. Values range from 0 to 1. 0 is fully undamped, while values very close to 1 will remove most velocity.</param>
    public DemoPoseIntegratorCallbacks(Vector3 gravity, float linearDamping = .03f, float angularDamping = .03f) : this()
    {
        Gravity = gravity;
        LinearDamping = linearDamping;
        AngularDamping = angularDamping;
    }

    Vector3Wide gravityWideDt;
    Vector<float> linearDampingDt;
    Vector<float> angularDampingDt;

    /// <summary>
    /// Callback invoked ahead of dispatches that may call into <see cref="IntegrateVelocity"/>.
    /// It may be called more than once with different values over a frame. For example, when performing bounding box prediction, velocity is integrated with a full frame time step duration.
    /// During substepped solves, integration is split into substepCount steps, each with fullFrameDuration / substepCount duration.
    /// The final integration pass for unconstrained bodies may be either fullFrameDuration or fullFrameDuration / substepCount, depending on the value of AllowSubstepsForUnconstrainedBodies. 
    /// </summary>
    /// <param name="dt">Current integration time step duration.</param>
    /// <remarks>This is typically used for precomputing anything expensive that will be used across velocity integration.</remarks>
    public void PrepareForIntegration(float dt)
    {
        //No reason to recalculate gravity * dt for every body; just cache it ahead of time.
        //Since these callbacks don't use per-body damping values, we can precalculate everything.
        linearDampingDt = new Vector<float>(MathF.Pow(MathHelper.Clamp(1 - LinearDamping, 0, 1), dt));
        angularDampingDt = new Vector<float>(MathF.Pow(MathHelper.Clamp(1 - AngularDamping, 0, 1), dt));
        gravityWideDt = Vector3Wide.Broadcast(Gravity * dt);
    }

    /// <summary>
    /// Callback for a bundle of bodies being integrated.
    /// </summary>
    /// <param name="bodyIndices">Indices of the bodies being integrated in this bundle.</param>
    /// <param name="position">Current body positions.</param>
    /// <param name="orientation">Current body orientations.</param>
    /// <param name="localInertia">Body's current local inertia.</param>
    /// <param name="integrationMask">Mask indicating which lanes are active in the bundle. Active lanes will contain 0xFFFFFFFF, inactive lanes will contain 0.</param>
    /// <param name="workerIndex">Index of the worker thread processing this bundle.</param>
    /// <param name="dt">Durations to integrate the velocity over. Can vary over lanes.</param>
    /// <param name="velocity">Velocity of bodies in the bundle. Any changes to lanes which are not active by the integrationMask will be discarded.</param>
    public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation, BodyInertiaWide localInertia, Vector<int> integrationMask, int workerIndex, Vector<float> dt, ref BodyVelocityWide velocity)
    {
        //This is a handy spot to implement things like position dependent gravity or per-body damping.
        //This implementation uses a single damping value for all bodies that allows it to be precomputed.
        //We don't have to check for kinematics; IntegrateVelocityForKinematics returns false, so we'll never see them in this callback.
        //Note that these are SIMD operations and "Wide" types. There are Vector<float>.Count lanes of execution being evaluated simultaneously.
        //The types are laid out in array-of-structures-of-arrays (AOSOA) format. That's because this function is frequently called from vectorized contexts within the solver.
        //Transforming to "array of structures" (AOS) format for the callback and then back to AOSOA would involve a lot of overhead, so instead the callback works on the AOSOA representation directly.
        velocity.Linear = (velocity.Linear + gravityWideDt) * linearDampingDt;
        velocity.Angular = velocity.Angular * angularDampingDt;
    }
}
public struct DemoNarrowPhaseCallbacks : INarrowPhaseCallbacks
{
    public SpringSettings ContactSpringiness;
    public float MaximumRecoveryVelocity;
    public float FrictionCoefficient;

    public DemoNarrowPhaseCallbacks(SpringSettings contactSpringiness, float maximumRecoveryVelocity = 2f, float frictionCoefficient = 1f)
    {
        ContactSpringiness = contactSpringiness;
        MaximumRecoveryVelocity = maximumRecoveryVelocity;
        FrictionCoefficient = frictionCoefficient;
    }

    public void Initialize(Simulation simulation)
    {
        //Use a default if the springiness value wasn't initialized... at least until struct field initializers are supported outside of previews.
        if (ContactSpringiness.AngularFrequency == 0 && ContactSpringiness.TwiceDampingRatio == 0)
        {
            ContactSpringiness = new(30, 1);
            MaximumRecoveryVelocity = 2f;
            FrictionCoefficient = 1f;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
    {
        //While the engine won't even try creating pairs between statics at all, it will ask about kinematic-kinematic pairs.
        //Those pairs cannot emit constraints since both involved bodies have infinite inertia. Since most of the demos don't need
        //to collect information about kinematic-kinematic pairs, we'll require that at least one of the bodies needs to be dynamic.
        return a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB)
    {
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : unmanaged, IContactManifold<TManifold>
    {
        pairMaterial.FrictionCoefficient = FrictionCoefficient;
        pairMaterial.MaximumRecoveryVelocity = MaximumRecoveryVelocity;
        pairMaterial.SpringSettings = ContactSpringiness;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold)
    {
        return true;
    }

    public void Dispose()
    {
    }
}


