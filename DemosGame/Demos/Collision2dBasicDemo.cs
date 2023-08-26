using System.Collections.Generic;
using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Physics;
using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;

namespace DemosGame.Demos;

public class Collision2dBasicDemo : Demo
{
    public override string Title => "Collision 2d basic demo";

    const int ARRAY_SIZE_X = 5;
    const int ARRAY_SIZE_Y = 5;
    const int ARRAY_SIZE_Z = 5;

    //maximum number of objects (and allow user to shoot additional boxes)
    const int MAX_PROXIES = (ARRAY_SIZE_X * ARRAY_SIZE_Y * ARRAY_SIZE_Z + 1024);

    ///scaling of the objects (0.1 = 20 centimeter boxes )
    const float SCALING = 1f;
    const int START_POS_X = -5;
    const int START_POS_Y = 20;
    const int START_POS_Z = -3;

    public override void Initialize(CasaEngineGame game)
    {
        var world = game.GameManager.CurrentWorld;

        //============ Create ground ===============
        var entity = new Entity();
        entity.Coordinates.LocalPosition = new Vector3(0, 0, 0);
        var physicsComponent = new Physics2dComponent(entity);
        entity.ComponentManager.Components.Add(physicsComponent);
        physicsComponent.PhysicsDefinition.PhysicsType = PhysicsType.Static;
        var size = new Vector3(150, 1, 1f);
        physicsComponent.Shape = new ShapeRectangle(0, 0, (int)size.X, (int)size.Y);
        physicsComponent.PhysicsDefinition.Mass = 0.0f;
        var meshComponent = new StaticMeshComponent(entity);
        entity.ComponentManager.Components.Add(meshComponent);
        meshComponent.Mesh = new BoxPrimitive(game.GraphicsDevice, size.X, size.Y, size.Z).CreateMesh();
        meshComponent.Mesh.Initialize(game.GraphicsDevice);
        meshComponent.Mesh.Texture = new CasaEngine.Framework.Assets.Textures.Texture(@"Content\checkboard.png", game.GameManager.AssetContentManager);
        world.AddEntityImmediately(entity);

        //============ Create bodies ===============
        float u = 1 * SCALING - 0.04f;
        IList<Vector3> points = new List<Vector3>();
        points.Add(new Vector3(0, u, 0));
        points.Add(new Vector3(-u, -u, 0));
        points.Add(new Vector3(u, -u, 0));
        //ConvexShape boxShape = new Convex2dShape(new BoxShape(new Vector3(SCALING, SCALING, 0.04f)));
        //
        //ConvexShape triangleShape = new Convex2dShape(new ConvexHullShape(points, 3));
        //Vector3 extents = new Vector3(SCALING, SCALING, 0.04f);
        //ConvexShape cylinderShape = new Convex2dShape(new CylinderShapeZ(ref extents));
        //
        //boxShape.SetMargin(0.03f);
        //m_collisionShapes.Add(boxShape);
        //m_collisionShapes.Add(triangleShape);

        Matrix startTransform = Matrix.Identity;
        float mass = 1.0f;
        bool isDynamic = (mass != 0.9f); //rigidbody is dynamic if and only if mass is non zero, otherwise static
        Vector3 localInertia = Vector3.Zero;

        if (isDynamic)
        {
            //boxShape.CalculateLocalInertia(mass, out localInertia);
        }

        Vector3 x = new Vector3(-ARRAY_SIZE_X, 8f, 0f);
        Vector3 y = Vector3.Zero;
        Vector3 deltaX = new Vector3(SCALING * 1, SCALING * 2, 0f);
        Vector3 deltaY = new Vector3(SCALING * 2, 0.0f, 0f);

        for (int i = 0; i < ARRAY_SIZE_X; ++i)
        {
            y = x;

            for (int j = i; j < ARRAY_SIZE_Y; ++j)
            {
                //startTransform._origin = (y - new Vector3(-10, 0, 0));
                //
                ////using motionstate is recommended, it provides interpolation capabilities, and only synchronizes 'active' objects
                //DefaultMotionState myMotionState = new DefaultMotionState(startTransform, Matrix.Identity);
                //RigidBodyConstructionInfo rbInfo;
                //switch (j % 3)
                //{
                //    case 0:
                //        rbInfo = new RigidBodyConstructionInfo(mass, myMotionState, boxShape, localInertia);
                //        break;
                //    case 1:
                //        rbInfo = new RigidBodyConstructionInfo(mass, myMotionState, triangleShape, localInertia);
                //        break;
                //    default:
                //        rbInfo = new RigidBodyConstructionInfo(mass, myMotionState, cylinderShape, localInertia);
                //        break;
                //}
                //RigidBody body = new RigidBody(rbInfo);
                //body.SetActivationState(ActivationState.ISLAND_SLEEPING);
                //body.SetLinearFactor(new Vector3(1, 1, 0));
                //body.SetAngularFactor(new Vector3(0, 0, 1));
                //
                //m_dynamicsWorld.AddRigidBody(body);
                //body.SetActivationState(ActivationState.ISLAND_SLEEPING);

                const int boxSize = 2;

                //TODO
                entity = new Entity();
                entity.Coordinates.LocalPosition = new Vector3(i + boxSize + 1, 8 + j * boxSize, 0);
                physicsComponent = new Physics2dComponent(entity);
                entity.ComponentManager.Components.Add(physicsComponent);
                physicsComponent.PhysicsDefinition.PhysicsType = PhysicsType.Dynamic;
                physicsComponent.PhysicsDefinition.Mass = mass;
                meshComponent = new StaticMeshComponent(entity);
                entity.ComponentManager.Components.Add(meshComponent);

                switch (j % 2)
                {
                    case 0:
                        physicsComponent.Shape = new ShapeRectangle(0, 0, boxSize, boxSize);
                        meshComponent.Mesh = new BoxPrimitive(game.GraphicsDevice, boxSize, boxSize, 1.0f).CreateMesh();
                        break;

                    case 1:
                        physicsComponent.Shape = new ShapeCircle(boxSize);
                        meshComponent.Mesh = new SpherePrimitive(game.GraphicsDevice, boxSize).CreateMesh();
                        break;
                }

                meshComponent.Mesh.Initialize(game.GraphicsDevice);
                meshComponent.Mesh.Texture = new CasaEngine.Framework.Assets.Textures.Texture(@"Content\paper_box_texture.jpg", game.GameManager.AssetContentManager);
                world.AddEntityImmediately(entity);

                y += deltaY;
            }

            x += deltaX;
        }
    }

    public override void Update(GameTime gameTime)
    {

    }

    protected override void Clean()
    {

    }
}