using System.Collections.Generic;
using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.SceneManagement.Components;
using Microsoft.Xna.Framework;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Demos.Demos;

public class SceneManagementDemo : Demo
{
    private Entity _entityCube;
    private List<Entity> _entities;
    public override string Title => "Scene management demo";

    public override void Initialize(CasaEngineGame game)
    {
        _entities = new List<Entity>();
        var world = game.GameManager.CurrentWorld;

        var boxPrimitive = new BoxPrimitive(game.GraphicsDevice);
        var staticMesh = boxPrimitive.CreateMesh();
        staticMesh.Initialize(game.GraphicsDevice, null);
        staticMesh.Texture = game.AssetContentManager.GetAsset<Texture>(Texture.DefaultTextureName);

        var gridSize = 10;
        var transF = 1f; //2.0f / gridSize;
        for (var i = -gridSize; i <= gridSize; ++i)
        {
            for (var j = -gridSize; j <= gridSize; ++j)
            {
                var entity = new Entity { Name = $"Cube[{i}, {j}]" };
                var staticMeshComponent = new StaticMeshComponent();
                entity.RootComponent = staticMeshComponent;
                entity.RootComponent.Position = new Vector3(transF * i, transF * j, 10.0f);
                staticMeshComponent.Mesh = staticMesh;

                _entities.Add(entity);
                world.AddEntity(entity);
            }
        }

        _entityCube = new Entity { Name = "Moving cube" };
        var staticMeshComponent2 = new StaticMeshComponent();
        staticMeshComponent2.Mesh = staticMesh;
        _entityCube.RootComponent = staticMeshComponent2;

        world.AddEntity(_entityCube);

        world.DisplaySpacePartitioning = true;
    }

    public override void Update(GameTime gameTime)
    {
        var position = Vector3.Transform(Vector3.UnitX * 20f, Quaternion.CreateFromAxisAngle(Vector3.Up, (float)gameTime.TotalGameTime.TotalSeconds));
        _entityCube.RootComponent.Position = position;

        foreach (var entity in _entities)
        {
            position = entity.RootComponent.Position;
            position.Z -= (float)gameTime.ElapsedGameTime.Milliseconds / 1000f;
            entity.RootComponent.Position = position;
        }
    }

    public override void Clean()
    {

    }
}