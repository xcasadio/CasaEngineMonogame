using System.Collections.Generic;
using System.IO;
using CasaEngine.Engine;
using CasaEngine.Engine.Physics;
using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.SceneManagement.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Demos.Demos;

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
        var entity = new Entity { Name = "ground" };
        var size = new Vector3(150, 1, 1f);
        //====
        var meshComponent = new StaticMeshComponent();
        entity.RootComponent = meshComponent;
        meshComponent.Mesh = new BoxPrimitive(game.GraphicsDevice, size.X, size.Y, size.Z).CreateMesh();
        meshComponent.Mesh.Initialize(game.GraphicsDevice, game.AssetContentManager);
        var fileName = Path.Combine(EngineEnvironment.ProjectPath, "checkboard.png");
        meshComponent.Mesh.Texture = new CasaEngine.Framework.Assets.Textures.Texture(Texture2D.FromFile(game.GraphicsDevice, fileName));
        entity.RootComponent.Position = new Vector3(0, 0, 0);
        //====
        var box2dCollisionComponent = new Box2dCollisionComponent();
        entity.AddComponent(box2dCollisionComponent);
        box2dCollisionComponent.PhysicsDefinition.PhysicsType = PhysicsType.Static;
        box2dCollisionComponent.Scale = new Vector3((int)size.X, (int)size.Y, 1f);
        box2dCollisionComponent.PhysicsDefinition.Mass = 0.0f;

        world.AddEntity(entity);

        //============ Create bodies ===============
        float u = 1 * SCALING - 0.04f;
        IList<Vector3> points = new List<Vector3>();
        points.Add(new Vector3(0, u, 0));
        points.Add(new Vector3(-u, -u, 0));
        points.Add(new Vector3(u, -u, 0));

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
                const int boxSize = 2;

                //TODO
                entity = new Entity { Name = $"shape 2d {j}-{i}" };
                //====
                meshComponent = new StaticMeshComponent();
                entity.RootComponent = meshComponent;
                //====
                Physics2dComponent physics2dComponent = null;
                switch (j % 2)
                {
                    case 0:
                        physics2dComponent = new Box2dCollisionComponent();
                        physics2dComponent.Scale = new Vector3(boxSize, boxSize, 1f);
                        meshComponent.Mesh = new BoxPrimitive(game.GraphicsDevice, boxSize, boxSize, 1.0f).CreateMesh();
                        break;

                    case 1:
                        physics2dComponent = new CircleCollisionComponent();
                        physics2dComponent.Scale = new Vector3(boxSize, boxSize, boxSize);
                        meshComponent.Mesh = new SpherePrimitive(game.GraphicsDevice, boxSize).CreateMesh();
                        break;
                }

                physics2dComponent.Position = new Vector3(i + boxSize + 1, 8 + j * boxSize, 0);
                entity.AddComponent(physics2dComponent);
                physics2dComponent.PhysicsDefinition.PhysicsType = PhysicsType.Dynamic;
                physics2dComponent.PhysicsDefinition.Mass = mass;


                meshComponent.Mesh.Initialize(game.GraphicsDevice, game.AssetContentManager);
                fileName = Path.Combine(EngineEnvironment.ProjectPath, "paper_box_texture.jpg");
                meshComponent.Mesh.Texture = new CasaEngine.Framework.Assets.Textures.Texture(Texture2D.FromFile(game.GraphicsDevice, fileName));
                world.AddEntity(entity);

                y += deltaY;
            }

            x += deltaX;
        }
    }

    public override void Update(GameTime gameTime)
    {

    }

    public override void Clean()
    {

    }
}