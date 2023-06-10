using BulletSharp;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using Microsoft.Xna.Framework;

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
        PhysicsEngine = new PhysicsEngine(GameSettings.Physics3dSettings);
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
        return CreateGhostObject(worldMatrix, collideableComponent, collisionShape, color);
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

    private PairCachingGhostObject CreateGhostObject(Matrix worldMatrix, ICollideableComponent collideableComponent, CollisionShape collisionShape, Color? color)
    {
        var ghostObject = new BulletSharp.PairCachingGhostObject
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

    public RigidBody AddStaticObject(Shape3d shape3d, ref Matrix worldMatrix, PhysicsComponent physicsComponent, Color? color = null)
    {
        return AddRigidBody(shape3d, 0.0f, ref worldMatrix, physicsComponent, color);
    }

    public RigidBody AddRigidBody(Shape3d shape3d, float mass, ref Matrix worldMatrix, PhysicsComponent physicsComponent, Color? color = null)
    {
        var collisionShape = ConvertToCollisionShape(shape3d);
        using var rbInfo = new RigidBodyConstructionInfo(mass, null, collisionShape);
        bool isDynamic = mass != 0.0f;
        if (isDynamic)
        {
            rbInfo.LocalInertia = collisionShape.CalculateLocalInertia(mass);
            rbInfo.MotionState = new DefaultMotionState(worldMatrix);
        }

        var body = new RigidBody(rbInfo)
        {
            Gravity = GameSettings.Physics3dSettings.Gravity,
            UserObject = physicsComponent,
            WorldTransform = worldMatrix
        };

        if (!isDynamic)
        {
            body.CollisionFlags |= CollisionFlags.StaticObject;
        }

        if (color.HasValue)
        {
            body.SetCustomDebugColor(color.Value.ToVector3());
        }

        PhysicsEngine.World.AddRigidBody(body);
        return body;
    }

    private CollisionShape ConvertToCollisionShape(Shape2d shape)
    {
        CollisionShape collisionShape;

        switch (shape.Type)
        {
            case Shape2dType.Compound:
            case Shape2dType.Line:
                throw new ArgumentOutOfRangeException();
            case Shape2dType.Rectangle:
                var rectangle = (shape as ShapeRectangle);
                collisionShape = new BulletSharp.BoxShape(rectangle.Width / 2.0f, rectangle.Height / 2.0f, 0.5f);
                break;
            case Shape2dType.Circle:
                var circle = (shape as ShapeCircle);
                collisionShape = new BulletSharp.CapsuleShape(circle.Radius, 1.0f);
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
                collisionShape = new BulletSharp.BoxShape(box.Size.X / 2.0f, box.Size.Y / 2.0f, box.Size.Z / 2.0f);
                break;
            case Shape3dType.Capsule:
                var capsule = (shape as Capsule);
                collisionShape = new BulletSharp.CapsuleShape(capsule.Radius, capsule.Length);
                break;
            case Shape3dType.Cylinder:
                var cylinder = (shape as Cylinder);
                collisionShape = new BulletSharp.CylinderShape(cylinder.Radius, cylinder.Radius, cylinder.Length);
                break;
            case Shape3dType.Sphere:
                var sphere = (shape as Sphere);
                collisionShape = new BulletSharp.SphereShape(sphere.Radius);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        collisionShape.UserObject = this;
        return collisionShape;
    }

    public void AddCollisionObject(CollisionObject collisionObject)
    {
        //TODO : remove shape
        PhysicsEngine.World.AddCollisionObject(collisionObject);
    }

    public void RemoveCollisionObject(CollisionObject collisionObject)
    {
        //TODO : remove shape
        PhysicsEngine.World.RemoveCollisionObject(collisionObject);
    }

    public void AddBodyObject(RigidBody rigidBody)
    {
        //TODO : remove shape
        PhysicsEngine.World.AddRigidBody(rigidBody);
    }

    public void RemoveBodyObject(RigidBody rigidBody)
    {
        //TODO : remove shape
        PhysicsEngine.World.RemoveRigidBody(rigidBody);
    }
}