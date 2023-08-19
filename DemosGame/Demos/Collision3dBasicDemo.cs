using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Physics;
using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DemosGame.Demos;

public class Collision3dBasicDemo : Demo
{
    public override string Title => "Collision 3d basic demo";

    public override void Initialize(CasaEngineGame game)
    {
        var world = game.GameManager.CurrentWorld;

        //============ Create ground ===============
        var entity = new Entity();
        var physicsComponent = new PhysicsComponent(entity);
        entity.ComponentManager.Components.Add(physicsComponent);
        physicsComponent.PhysicsDefinition.PhysicsType = PhysicsType.Static;
        physicsComponent.Shape = new Box { Size = new Vector3(50, 1, 50) };
        physicsComponent.PhysicsDefinition.Mass = 0.0f;
        var meshComponent = new StaticMeshComponent(entity);
        entity.ComponentManager.Components.Add(meshComponent);
        meshComponent.Mesh = new BoxPrimitive(game.GraphicsDevice, 50, 1, 50).CreateMesh();
        meshComponent.Mesh.Initialize(game.GraphicsDevice);
        meshComponent.Mesh.Texture = new CasaEngine.Framework.Assets.Textures.Texture(game.GraphicsDevice, @"checkboard.png", game.GameManager.AssetContentManager);
        world.AddEntityImmediately(entity);

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
                    entity = new Entity();
                    entity.Coordinates.LocalPosition = start + new Vector3(i, k, j);
                    physicsComponent = new PhysicsComponent(entity);
                    entity.ComponentManager.Components.Add(physicsComponent);
                    physicsComponent.PhysicsDefinition.PhysicsType = PhysicsType.Dynamic;
                    physicsComponent.Shape = new Box { Size = Vector3.One };
                    physicsComponent.PhysicsDefinition.Mass = mass;
                    meshComponent = new StaticMeshComponent(entity);
                    entity.ComponentManager.Components.Add(meshComponent);
                    meshComponent.Mesh = boxPrimitive;
                    meshComponent.Mesh.Initialize(game.GraphicsDevice);
                    meshComponent.Mesh.Texture = new CasaEngine.Framework.Assets.Textures.Texture(game.GraphicsDevice, @"paper_box_texture.jpg", game.GameManager.AssetContentManager);
                    world.AddEntityImmediately(entity);
                }
            }
        }
    }

    public override void Update(GameTime gameTime)
    {

    }

    protected override void Clean()
    {

    }
}