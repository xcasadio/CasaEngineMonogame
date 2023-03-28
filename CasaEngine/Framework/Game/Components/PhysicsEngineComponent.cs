using BepuPhysics;
using BepuPhysics.Collidables;
using CasaEngine.Engine.Physics;
using CasaEngine.Engine.Physics.Shapes;
using Microsoft.Xna.Framework;
using Box = CasaEngine.Engine.Physics.Shapes.Box;
using Capsule = CasaEngine.Engine.Physics.Shapes.Capsule;
using Cylinder = CasaEngine.Engine.Physics.Shapes.Cylinder;
using Quaternion = System.Numerics.Quaternion;
using Sphere = CasaEngine.Engine.Physics.Shapes.Sphere;
using Vector3 = System.Numerics.Vector3;

namespace CasaEngine.Framework.Game.Components
{
    public class PhysicsEngineComponent : GameComponent
    {
        public PhysicsEngine PhysicsEngine { get; }

        public PhysicsEngineComponent(CasaEngineGame game) : base(game)
        {
            game.Components.Add(this);
            UpdateOrder = (int)ComponentUpdateOrder.Physics;

            PhysicsEngine = new PhysicsEngine();
        }

        public override void Initialize()
        {
            PhysicsEngine.Initialize();
            base.Initialize();
        }

        public override void Update(GameTime gameTime)
        {
            PhysicsEngine.Update(gameTime);
        }

        public StaticHandle AddStaticObject(Shape shape)
        {
            /*
             var listenedBody1 = Simulation.Bodies.Add(BodyDescription.CreateConvexDynamic(new Vector3(0, 5, 0), 1, Simulation.Shapes, new Box(1, 2, 3)));
            events.Register(Simulation.Bodies[listenedBody1].CollidableReference, eventHandler);
             */

            Vector3 position = ConvertToVector3(shape.Location);
            Quaternion orientation = CreateQuaternion(shape.Orientation);
            var physicsInfo = ConvertToShape(shape);
            return PhysicsEngine.Simulation.Statics.Add(new StaticDescription(position, orientation, physicsInfo.Item1));
        }

        public BodyHandle AddBodyObject(Shape shape, float mass)
        {
            /*
             var listenedBody1 = Simulation.Bodies.Add(BodyDescription.CreateConvexDynamic(new Vector3(0, 5, 0), 1, Simulation.Shapes, new Box(1, 2, 3)));
            events.Register(Simulation.Bodies[listenedBody1].CollidableReference, eventHandler);
             */

            Vector3 position = ConvertToVector3(shape.Location);
            Quaternion orientation = CreateQuaternion(shape.Orientation);
            var physicsInfo = ConvertToShape(shape);

            //return typeIndex
            var rigidPose = new RigidPose(position, orientation);
            var bodyVelocity = new BodyVelocity(Vector3.Zero, Vector3.Zero);
            var bodyInertia = physicsInfo.Item2.ComputeInertia(mass);
            var collidableDescription = new CollidableDescription(physicsInfo.Item1, ContinuousDetection.Continuous());
            var bodyActivityDescription = new BodyActivityDescription(0.01f);

            return PhysicsEngine.Simulation.Bodies.Add(BodyDescription.CreateDynamic(rigidPose, bodyVelocity, bodyInertia, collidableDescription, bodyActivityDescription));
        }

        private (TypedIndex, IConvexShape) ConvertToShape(Shape shape)
        {
            TypedIndex typeIndex;
            IConvexShape convexShape;

            switch (shape.Type)
            {
                case ShapeType.Compound:
                    throw new ArgumentOutOfRangeException();
                case ShapeType.Box:
                    var box = (shape as Box);
                    var physicsBox = new BepuPhysics.Collidables.Box(box.Width, box.Height, box.Length);
                    typeIndex = PhysicsEngine.Simulation.Shapes.Add(physicsBox);
                    convexShape = physicsBox;
                    break;
                case ShapeType.Capsule:
                    var capsule = (shape as Capsule);
                    var physicsCapsule = new BepuPhysics.Collidables.Capsule(capsule.Radius, capsule.Length);
                    typeIndex = PhysicsEngine.Simulation.Shapes.Add(physicsCapsule);
                    convexShape = physicsCapsule;
                    break;
                case ShapeType.Cylinder:
                    var cylinder = (shape as Cylinder);
                    var physicsCylinder = new BepuPhysics.Collidables.Cylinder(cylinder.Radius, cylinder.Length);
                    typeIndex = PhysicsEngine.Simulation.Shapes.Add(physicsCylinder);
                    convexShape = physicsCylinder;
                    break;
                case ShapeType.Sphere:
                    var sphere = (shape as Sphere);
                    var physicsSphere = new BepuPhysics.Collidables.Sphere(sphere.Radius);
                    typeIndex = PhysicsEngine.Simulation.Shapes.Add(physicsSphere);
                    convexShape = physicsSphere;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return (typeIndex, convexShape);
        }

        private static Quaternion CreateQuaternion(Microsoft.Xna.Framework.Quaternion quaternion)
        {
            return new(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W);
        }

        private static Vector3 ConvertToVector3(Microsoft.Xna.Framework.Vector3 location)
        {
            return new(location.X, location.Y, location.Z);
        }

        public void RemoveStaticObject(StaticHandle staticHandle)
        {
            //TODO : remove shape
            PhysicsEngine.Simulation.Statics.Remove(staticHandle);
        }

        public (Microsoft.Xna.Framework.Vector3, Microsoft.Xna.Framework.Quaternion) GetTransformations(StaticHandle staticHandle)
        {
            var staticReference = PhysicsEngine.Simulation.Statics.GetStaticReference(staticHandle);
            return (staticReference.Pose.Position, staticReference.Pose.Orientation);
        }

        public (Microsoft.Xna.Framework.Vector3, Microsoft.Xna.Framework.Quaternion) GetTransformations(BodyHandle bodyHandle)
        {
            var bodyReference = PhysicsEngine.Simulation.Bodies.GetBodyReference(bodyHandle);
            return (bodyReference.Pose.Position, bodyReference.Pose.Orientation);
        }

        public void RemoveBodyObject(BodyHandle bodyHandle)
        {
            //TODO : remove shape
            PhysicsEngine.Simulation.Bodies.Remove(bodyHandle);
        }

        public void UpdatePositionAndOrientation(Microsoft.Xna.Framework.Vector3 position, Microsoft.Xna.Framework.Quaternion orientation, StaticHandle staticHandle)
        {
            Vector3 pos = ConvertToVector3(position);
            Quaternion rotation = CreateQuaternion(orientation);
            PhysicsEngine.Simulation.Statics.GetDescription(staticHandle, out StaticDescription staticDescription);
            staticDescription.Pose.Position = pos;
            staticDescription.Pose.Orientation = rotation;
            PhysicsEngine.Simulation.Statics.ApplyDescription(staticHandle, staticDescription);
        }

        public void UpdatePositionAndOrientation(Microsoft.Xna.Framework.Vector3 position, Microsoft.Xna.Framework.Quaternion orientation, BodyHandle bodyHandle)
        {
            Vector3 pos = ConvertToVector3(position);
            Quaternion rotation = CreateQuaternion(orientation);
            var bodyDescription = PhysicsEngine.Simulation.Bodies.GetDescription(bodyHandle);
            bodyDescription.Pose.Position = pos;
            bodyDescription.Pose.Orientation = rotation;
            PhysicsEngine.Simulation.Bodies.ApplyDescription(bodyHandle, bodyDescription);
        }
    }
}