using BulletSharp;

using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Physics;
using Microsoft.Xna.Framework;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace CasaEngine.Framework.Application.Components.Physics;

public class PhysicsEngineComponent : GameComponent, IPhysicsWorldContext
{
    private readonly CasaEngineGame? _casaEngineGame;
    private readonly Dictionary<CasaEngine.Framework.Scene.World.World, PhysicsWorldContext> _physicsWorldContexts = [];
    private readonly List<CasaEngine.Framework.Scene.World.World> _worldsToUpdate = [];
    private PhysicsWorldContext? _bootstrapContext;

    public PhysicsEngine PhysicsEngine => ResolveCurrentContext().PhysicsEngine;

    public PhysicsEngineComponent(CasaEngineGame game) : base(game)
    {
        _casaEngineGame = Game as CasaEngineGame;
        game.Components.Add(this);
        UpdateOrder = (int)ComponentUpdateOrder.Physics;
    }

    public override void Initialize()
    {
        base.Initialize();
    }

    public PhysicsWorldContext GetOrCreateContext(CasaEngine.Framework.Scene.World.World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (_physicsWorldContexts.TryGetValue(world, out var context))
        {
            return context;
        }

        context = new PhysicsWorldContext(_casaEngineGame?.ExecutionPolicy.UseExternalViewManagement == true);
        if (_bootstrapContext?.PhysicsEngine.World?.DebugDrawer != null)
        {
            context.PhysicsEngine.World.DebugDrawer = _bootstrapContext.PhysicsEngine.World.DebugDrawer;
        }

        _physicsWorldContexts.Add(world, context);
        return context;
    }

    public void ReleaseContext(CasaEngine.Framework.Scene.World.World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (_physicsWorldContexts.Remove(world, out var context))
        {
            context.Dispose();
        }
    }

    public override void Update(GameTime gameTime)
    {
        if (_casaEngineGame?.ExecutionPolicy.UpdatePhysicsEngine == false)
        {
            return;
        }

        float elapsedTime = GameTimeHelper.ConvertElapsedTimeToSeconds(gameTime);
        _worldsToUpdate.Clear();

        if (_casaEngineGame != null)
        {
            var views = _casaEngineGame.GameManager.ViewManager.Views;
            for (int i = 0; i < views.Count; i++)
            {
                AddWorldToUpdate(views[i].World);
            }

            var currentWorld = _casaEngineGame.GameManager.CurrentWorld;
            if (currentWorld != null)
            {
                AddWorldToUpdate(currentWorld);
            }
        }

        for (int i = 0; i < _worldsToUpdate.Count; i++)
        {
            GetOrCreateContext(_worldsToUpdate[i]).Update(elapsedTime);
        }
    }

    public void Update(float elapsedTime)
    {
        ResolveCurrentContext().Update(elapsedTime);
    }

    private void AddWorldToUpdate(CasaEngine.Framework.Scene.World.World world)
    {
        if (!_worldsToUpdate.Contains(world))
        {
            _worldsToUpdate.Add(world);
        }
    }

    private PhysicsWorldContext ResolveCurrentContext()
    {
        var currentWorld = _casaEngineGame?.GameManager.CurrentWorld;
        if (currentWorld != null)
        {
            return GetOrCreateContext(currentWorld);
        }

        _bootstrapContext ??= new PhysicsWorldContext(_casaEngineGame?.ExecutionPolicy.UseExternalViewManagement == true);
        return _bootstrapContext;
    }

    public CollisionObject AddGhostObject(CollisionShape collisionShape, ref Matrix worldMatrix, ICollideableComponent collideableComponent, Color? color = null)
    {
        return ResolveCurrentContext().AddGhostObject(collisionShape, ref worldMatrix, collideableComponent, color);
    }

    public PairCachingGhostObject CreateGhostObject(Matrix worldMatrix, ICollideableComponent collideableComponent, CollisionShape collisionShape, Color? color = null)
    {
        return ResolveCurrentContext().CreateGhostObject(worldMatrix, collideableComponent, collisionShape, color);
    }

    public RigidBody AddStaticObject(CollisionShape collisionShape, Vector3 localScale, ref Matrix worldMatrix, object component, PhysicsDefinition physicsDefinition)
    {
        return ResolveCurrentContext().AddStaticObject(collisionShape, localScale, ref worldMatrix, component, physicsDefinition);
    }

    public RigidBody AddRigidBody(CollisionShape collisionShape, Vector3 localScale, ref Matrix worldMatrix, object component, PhysicsDefinition physicsDefinition)
    {
        return ResolveCurrentContext().AddRigidBody(collisionShape, localScale, ref worldMatrix, component, physicsDefinition);
    }

    public RigidBody AddRigidBody(CollisionShape collisionShape, ref Matrix worldMatrix, object userObject, PhysicsDefinition physicsDefinition)
    {
        return ResolveCurrentContext().AddRigidBody(collisionShape, ref worldMatrix, userObject, physicsDefinition);
    }

    public void AddCollisionObject(CollisionObject collisionObject)
    {
        ResolveCurrentContext().AddCollisionObject(collisionObject);
    }

    public void RemoveCollisionObject(CollisionObject collisionObject)
    {
        ResolveCurrentContext().RemoveCollisionObject(collisionObject);
    }

    public void AddRigidBody(RigidBody rigidBody)
    {
        ResolveCurrentContext().AddRigidBody(rigidBody);
    }

    public void RemoveRigidBody(RigidBody rigidBody)
    {
        ResolveCurrentContext().RemoveRigidBody(rigidBody);
    }

    public void ClearCollisionDataFrom(ICollideableComponent component)
    {
        ResolveCurrentContext().ClearCollisionDataFrom(component);
    }

    public bool WorldRayCast(ref Vector3 start, ref Vector3 end, Vector3 dir)
    {
        return ResolveCurrentContext().WorldRayCast(ref start, ref end, dir);
    }

    public bool NearBodyWorldRayCast(ref Vector3 position, ref Vector3 feelers, out Vector3 contactPoint, out Vector3 contactNormal)
    {
        return ResolveCurrentContext().NearBodyWorldRayCast(ref position, ref feelers, out contactPoint, out contactNormal);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _bootstrapContext?.Dispose();
            _bootstrapContext = null;

            foreach (var context in _physicsWorldContexts.Values)
            {
                context.Dispose();
            }

            _physicsWorldContexts.Clear();
            _worldsToUpdate.Clear();
        }

        base.Dispose(disposing);
    }
}