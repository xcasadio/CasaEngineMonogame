using CasaEngine.Engine;
using CasaEngine.Engine.Physics;
using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Materials;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.IO;

namespace CasaEngine.Demos.Demos;

public class Collision3dBasicDemo : Demo
{
    public override string Title => "Collision 3d basic demo";
    public override string Description => "Demonstrates basic 3D collision detection between dynamic rigid bodies (spheres, boxes) with gravity and restitution.";

    public override void Initialize(CasaEngineGame game)
    {
        var world = game.GameManager.CurrentWorld;

        //============ Create ground ===============
        var entity = new Entity { Name = "ground" };
        //===
        var physicsComponent = new BoxCollisionComponent();
        entity.AddComponent(physicsComponent);
        //===
        var meshComponent = new StaticModelComponent();
        entity.RootComponent = meshComponent;
        meshComponent.StaticModel = StaticModel.CreateFromPrimitive(new BoxPrimitive(50, 1, 50));
        meshComponent.StaticModel.Meshes[0].Initialize(game.GraphicsDevice);
        //===
        physicsComponent.PhysicsDefinition.PhysicsType = PhysicsType.Static;
        physicsComponent.LocalScale = new Vector3(50, 1, 50);
        physicsComponent.PhysicsDefinition.Mass = 0.0f;

        var fileName = Path.Combine(EngineEnvironment.ProjectPath, "checkboard.png");
        var groundMat = new LitDiffuseMaterial
        {
            Albedo       = Texture2D.FromFile(game.GraphicsDevice, fileName),
            DiffuseColor = Color.White,
        };
        meshComponent.StaticModel.Meshes[0].Material = groundMat;

        world.AddEntity(entity);

        //============ Create box ===============
        const float mass = 1.0f;
        const int ArraySizeX = 5, ArraySizeY = 5, ArraySizeZ = 5;
        Vector3 start = new Vector3(
            -(float)ArraySizeX + (float)ArraySizeX / 2f,
            ArraySizeY + 10,
            -(float)ArraySizeZ + (float)ArraySizeZ / 2f);

        var boxModel = StaticModel.CreateFromPrimitive(new BoxPrimitive());
        fileName = Path.Combine(EngineEnvironment.ProjectPath, "paper_box_texture.jpg");
        boxModel.Meshes[0].Initialize(game.GraphicsDevice);
        var boxMat = new LitDiffuseMaterial
        {
            Albedo       = Texture2D.FromFile(game.GraphicsDevice, fileName),
            DiffuseColor = Color.White,
        };
        boxModel.Meshes[0].Material = boxMat;

        for (int k = 0; k < ArraySizeY; k++)
        {
            for (int i = 0; i < ArraySizeX; i++)
            {
                for (int j = 0; j < ArraySizeZ; j++)
                {
                    entity = new Entity { Name = $"box {i}-{k}" };
                    //===
                    meshComponent = new StaticModelComponent();
                    entity.RootComponent = meshComponent;
                    meshComponent.Position = start + new Vector3(i, k, j);
                    meshComponent.StaticModel = boxModel;
                    //===
                    physicsComponent = new BoxCollisionComponent();
                    meshComponent.AddChildComponent(physicsComponent);
                    physicsComponent.PhysicsDefinition.PhysicsType = PhysicsType.Dynamic;
                    physicsComponent.PhysicsDefinition.Mass = mass;

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