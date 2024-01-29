using System.IO;
using CasaEngine.Core.Shapes;
using CasaEngine.Engine;
using CasaEngine.Engine.Physics;
using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.SceneManagement;
using CasaEngine.Framework.SceneManagement.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Demos.Demos;

public class Collision3dBasicDemo : Demo
{
    public override string Title => "Collision 3d basic demo";

    public override void Initialize(CasaEngineGame game)
    {
        var world = game.GameManager.CurrentWorld;

        //============ Create ground ===============
        var entity = new AActor { Name = "ground" };
        //===
        var physicsComponent = new BoxCollisionComponent();
        entity.AddComponent(physicsComponent);
        physicsComponent.PhysicsDefinition.PhysicsType = PhysicsType.Static;
        physicsComponent.LocalScale = new Vector3(50, 1, 50);
        physicsComponent.PhysicsDefinition.Mass = 0.0f;
        //===
        var meshComponent = new StaticMeshComponent();
        entity.RootComponent = meshComponent;
        meshComponent.Mesh = new BoxPrimitive(game.GraphicsDevice, 50, 1, 50).CreateMesh();
        meshComponent.Mesh.Initialize(game.GraphicsDevice, game.AssetContentManager);
        var fileName = Path.Combine(EngineEnvironment.ProjectPath, "checkboard.png");
        meshComponent.Mesh.Texture = new CasaEngine.Framework.Assets.Textures.Texture(Texture2D.FromFile(game.GraphicsDevice, fileName));

        world.AddEntity(entity);

        //============ Create box ===============
        const float mass = 1.0f;
        const int ArraySizeX = 5, ArraySizeY = 5, ArraySizeZ = 5;
        Vector3 start = new Vector3(
            -(float)ArraySizeX + (float)ArraySizeX / 2f,
            ArraySizeY + 10,
            -(float)ArraySizeZ + (float)ArraySizeZ / 2f);

        var boxPrimitive = new BoxPrimitive(game.GraphicsDevice, 1, 1, 1).CreateMesh();

        for (int k = 0; k < ArraySizeY; k++)
        {
            for (int i = 0; i < ArraySizeX; i++)
            {
                for (int j = 0; j < ArraySizeZ; j++)
                {
                    entity = new AActor { Name = $"box {i}-{k}" };
                    //===
                    physicsComponent = new BoxCollisionComponent();
                    physicsComponent.Position = start + new Vector3(i, k, j);
                    entity.AddComponent(physicsComponent);
                    physicsComponent.PhysicsDefinition.PhysicsType = PhysicsType.Dynamic;
                    physicsComponent.LocalScale = Vector3.One;
                    physicsComponent.PhysicsDefinition.Mass = mass;
                    //===
                    meshComponent = new StaticMeshComponent();
                    entity.RootComponent = meshComponent;
                    meshComponent.Mesh = boxPrimitive;
                    meshComponent.Mesh.Initialize(game.GraphicsDevice, game.AssetContentManager);
                    fileName = Path.Combine(EngineEnvironment.ProjectPath, "paper_box_texture.jpg");
                    meshComponent.Mesh.Texture = new Framework.Assets.Textures.Texture(Texture2D.FromFile(game.GraphicsDevice, fileName));

                    world.AddEntity(entity);
                }
            }
        }
    }

    public override void Update(GameTime gameTime)
    {

    }

    public override void Clean()
    {

    }
}