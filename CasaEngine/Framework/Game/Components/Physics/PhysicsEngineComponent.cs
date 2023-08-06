using System.ComponentModel;
using System.Numerics;
using BulletSharp;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Maths.Curves;
using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using Microsoft.Xna.Framework;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace CasaEngine.Framework.Game.Components.Physics;

public class PhysicsEngineComponent : GameComponent
{
    public PhysicsEngine PhysicsEngine { get; private set; }

    public PhysicsEngineComponent(CasaEngineGame game) : base(game)
    {
        game.Components.Add(this);
        UpdateOrder = (int)ComponentUpdateOrder.Physics;
    }

    public override void Initialize()
    {
        PhysicsEngine = new PhysicsEngine(GameSettings.PhysicsEngineSettings);
        base.Initialize();
    }

    public override void Update(GameTime gameTime)
    {
        PhysicsEngine.Update(GameTimeHelper.GameTimeToMilliseconds(gameTime));
        PhysicsEngine.UpdateContacts();
        PhysicsEngine.SendEvents();
    }

    public CollisionObject CreateGhostObject(Shape2d shape, ref Matrix worldMatrix, ICollideableComponent collideableComponent, Color color)
    {
        var collisionShape = ConvertToCollisionShape(shape);
        return CreateGhostObject(worldMatrix, collideableComponent, collisionShape);
    }

    public CollisionObject AddGhostObject(Shape2d shape, ref Matrix worldMatrix, ICollideableComponent collideableComponent, Color? color = null)
    {
        var collisionShape = ConvertToCollisionShape(shape);
        var collisionObject = AddGhostObject(worldMatrix, collideableComponent, collisionShape, color);
        return collisionObject;
    }

    public CollisionObject AddGhostObject(Shape3d shape, ref Matrix worldMatrix, ICollideableComponent collideableComponent, Color? color = null)
    {
        var collisionShape = ConvertToCollisionShape(shape);
        var collisionObject = AddGhostObject(worldMatrix, collideableComponent, collisionShape, color);
        return collisionObject;
    }

    private PairCachingGhostObject AddGhostObject(Matrix worldMatrix, ICollideableComponent collideableComponent, CollisionShape collisionShape, Color? color = null)
    {
        var collisionObject = CreateGhostObject(worldMatrix, collideableComponent, collisionShape, color);
        PhysicsEngine.World.AddCollisionObject(collisionObject);
        return collisionObject;
    }

    private PairCachingGhostObject CreateGhostObject(Matrix worldMatrix, ICollideableComponent collideableComponent, CollisionShape collisionShape, Color? color = null)
    {
        var ghostObject = new PairCachingGhostObject
        {
            CollisionShape = collisionShape,
            UserObject = collideableComponent,
            WorldTransform = worldMatrix
        };
        ghostObject.CollisionFlags |= CollisionFlags.NoContactResponse;

        if (color.HasValue)
        {
            ghostObject.SetCustomDebugColor(color.Value.ToVector3());
        }

        return ghostObject;
    }

    public RigidBody AddStaticObject(Shape3d shape3d, ref Matrix worldMatrix, object component, PhysicsDefinition physicsDefinition)
    {
        return AddRigidBody(shape3d, ref worldMatrix, component, physicsDefinition);
    }

    public RigidBody AddStaticObject(Shape2d shape2d, ref Matrix worldMatrix, object component, PhysicsDefinition physicsDefinition)
    {
        physicsDefinition.Mass = null;
        return AddRigidBody(shape2d, ref worldMatrix, component, physicsDefinition);
    }

    public RigidBody AddRigidBody(Shape3d shape3d, ref Matrix worldMatrix, object component, PhysicsDefinition physicsDefinition)
    {
        var collisionShape = ConvertToCollisionShape(shape3d);
        return AddRigidBody(collisionShape, ref worldMatrix, component, physicsDefinition);
    }

    public RigidBody AddRigidBody(Shape2d shape2d, ref Matrix worldMatrix, object component, PhysicsDefinition physicsDefinition)
    {
        var collisionShape = ConvertToCollisionShape(shape2d);
        return AddRigidBody(collisionShape, ref worldMatrix, component, physicsDefinition);
    }

    public RigidBody AddRigidBody(CollisionShape collisionShape, ref Matrix worldMatrix, object userObject, PhysicsDefinition physicsDefinition)
    {
        using var rbInfo = new RigidBodyConstructionInfo(physicsDefinition.Mass ?? 0f, physicsDefinition.MotionState, collisionShape);
        rbInfo.AdditionalAngularDampingFactor = physicsDefinition.AdditionalAngularDampingFactor;
        rbInfo.AdditionalAngularDampingThresholdSqr = physicsDefinition.AdditionalAngularDampingThresholdSqr;
        rbInfo.AdditionalDamping = physicsDefinition.AdditionalDamping;
        rbInfo.AdditionalDampingFactor = physicsDefinition.AdditionalDampingFactor;
        rbInfo.AdditionalLinearDampingThresholdSqr = physicsDefinition.AdditionalLinearDampingThresholdSqr;
        rbInfo.AngularDamping = physicsDefinition.AngularDamping;
        rbInfo.AngularSleepingThreshold = physicsDefinition.AngularSleepingThreshold;
        rbInfo.Friction = physicsDefinition.Friction;
        rbInfo.LinearDamping = physicsDefinition.LinearDamping;
        rbInfo.LinearSleepingThreshold = physicsDefinition.LinearSleepingThreshold;
        //rbInfo.LocalInertia = physicsDefinition.LocalInertia;
        rbInfo.Restitution = physicsDefinition.Restitution;
        rbInfo.RollingFriction = physicsDefinition.RollingFriction;

        bool isDynamic = physicsDefinition.Mass.HasValue && physicsDefinition.Mass != 0.0f;
        if (isDynamic)
        {
            rbInfo.LocalInertia = collisionShape.CalculateLocalInertia(physicsDefinition.Mass.Value);
            rbInfo.MotionState = new DefaultMotionState(worldMatrix);
        }

        var body = new RigidBody(rbInfo)
        {
            Gravity = physicsDefinition.ApplyGravity is true ? GameSettings.PhysicsEngineSettings.Gravity : Vector3.Zero,
            UserObject = userObject,
            WorldTransform = worldMatrix,
            LinearFactor = physicsDefinition.LinearFactor,
            AngularFactor = physicsDefinition.AngularFactor
        };

        if (!isDynamic)
        {
            body.CollisionFlags |= CollisionFlags.StaticObject;
        }

        if (physicsDefinition.DebugColor.HasValue)
        {
            body.SetCustomDebugColor(physicsDefinition.DebugColor.Value.ToVector3());
        }

        PhysicsEngine.World.AddRigidBody(body);

        if (physicsDefinition.ApplyGravity is false)
        {
            body.Gravity = Vector3.Zero;
        }

        return body;
    }

    private CollisionShape ConvertToCollisionShape(Shape2d shape)
    {
        CollisionShape collisionShape;

        switch (shape.Type)
        {
            case Shape2dType.Rectangle:
                var rectangle = (shape as ShapeRectangle);
                collisionShape = new BoxShape(rectangle.Width / 2f, rectangle.Height / 2f, 0.5f);
                break;
            case Shape2dType.Circle:
                var circle = (shape as ShapeCircle);
                collisionShape = new SphereShape(circle.Radius / 2f);
                break;
            //case Shape2dType.Polygone:
            //    var polygone = (shape as ShapePolygone);
            //    collisionShape = new BulletSharp.CylinderShape(cylinder.Radius, cylinder.Radius, cylinder.Length);
            //    break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        collisionShape.UserObject = this;
        return collisionShape;
    }

    private CollisionShape ConvertToCollisionShape(Shape3d shape)
    {
        CollisionShape collisionShape;

        switch (shape.Type)
        {
            case Shape3dType.Compound:
                throw new ArgumentOutOfRangeException();
            case Shape3dType.Box:
                var box = (shape as Box);
                collisionShape = new BoxShape(box.Size.X / 2.0f, box.Size.Y / 2.0f, box.Size.Z / 2.0f);
                break;
            case Shape3dType.Capsule:
                var capsule = (shape as Capsule);
                collisionShape = new CapsuleShape(capsule.Radius, capsule.Length);
                break;
            case Shape3dType.Cylinder:
                var cylinder = (shape as Cylinder);
                collisionShape = new CylinderShape(cylinder.Radius, cylinder.Radius, cylinder.Length);
                break;
            case Shape3dType.Sphere:
                var sphere = (shape as Sphere);
                collisionShape = new SphereShape(sphere.Radius);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        collisionShape.UserObject = this;
        return collisionShape;
    }

    public void AddCollisionObject(CollisionObject collisionObject)
    {
        if (!PhysicsEngine.World.CollisionObjectArray.Contains(collisionObject))
        {
            PhysicsEngine.World.AddCollisionObject(collisionObject);
        }
    }

    public void RemoveCollisionObject(CollisionObject collisionObject)
    {
        if (PhysicsEngine.World.CollisionObjectArray.Contains(collisionObject))
        {
            PhysicsEngine.World.RemoveCollisionObject(collisionObject);
        }
    }

    public void AddRigidBody(RigidBody rigidBody)
    {
        PhysicsEngine.World.AddRigidBody(rigidBody);
    }

    public void RemoveRigidBody(RigidBody rigidBody)
    {
        PhysicsEngine.World.RemoveRigidBody(rigidBody);
    }
}