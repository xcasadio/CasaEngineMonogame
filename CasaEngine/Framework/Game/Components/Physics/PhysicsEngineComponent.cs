using BulletSharp;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Physics;
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
    }

    public CollisionObject AddGhostObject(Shape3d shape, ref Matrix worldMatrix)
    {
        var ghostObject = new BulletSharp.PairCachingGhostObject
        {
            CollisionShape = ConvertToCollisionShape(shape),
            UserObject = this,
            WorldTransform = worldMatrix
        };
        ghostObject.CollisionFlags = CollisionFlags.NoContactResponse;

        PhysicsEngine.World.AddCollisionObject(ghostObject);

        return ghostObject;
    }

    public RigidBody AddStaticObject(Shape3d shape3d, ref Matrix worldMatrix)
    {
        return AddRigibBody(shape3d, 0.0f, ref worldMatrix);
    }

    public RigidBody AddRigibBody(Shape3d shape3d, float mass, ref Matrix worldMatrix)
    {
        var collisionShape = ConvertToCollisionShape(shape3d);
        using (var rbInfo = new RigidBodyConstructionInfo(mass, null, collisionShape))
        {
            bool isDynamic = mass != 0.0f;
            if (isDynamic)
            {
                rbInfo.LocalInertia = collisionShape.CalculateLocalInertia(mass);
                rbInfo.MotionState = new DefaultMotionState(worldMatrix);
            }

            var body = new RigidBody(rbInfo);

            body.CollisionFlags = mass != 0.0f ? CollisionFlags.None : CollisionFlags.StaticObject;
            body.Gravity = GameSettings.Physics3dSettings.Gravity;

            body.WorldTransform = worldMatrix;
            PhysicsEngine.World.AddRigidBody(body);
            return body;
        }
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

    public void RemoveStaticObject(CollisionObject collisionObject)
    {
        //TODO : remove shape
        PhysicsEngine.World.RemoveCollisionObject(collisionObject);
    }

    public void RemoveBodyObject(RigidBody rigidBody)
    {
        //TODO : remove shape
        PhysicsEngine.World.RemoveRigidBody(rigidBody);
    }
}