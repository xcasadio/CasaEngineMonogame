using CasaEngine.Engine;
using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Materials;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.IO;

namespace CasaEngine.Demos.Demos;

public class SceneManagementDemo : Demo
{
    private List<Entity> _rotatingEntities = new();
    private readonly List<Entity> _entities = new();
    private const int gridSize = 10;
    private const float transF = 1f;
    public override string Title => "Scene management demo";
    public override string Description => "Shows scene management with a grid of rotating entities added and removed at runtime.";

    public override void Initialize(CasaEngineGame game)
    {
        var world = game.GameManager.CurrentWorld;

        var fileName = Path.Combine(EngineEnvironment.ProjectPath, "checkboard.png");
        var checkMat = new LitDiffuseMaterial
        {
            BasColor     = Texture2D.FromFile(game.GraphicsDevice, fileName),
            DiffuseColor = Color.White,
        };

        var boxModel = StaticModel.CreateFromPrimitive(new BoxPrimitive());
        boxModel.Meshes[0].Initialize(game.GraphicsDevice);
        boxModel.Meshes[0].Material = checkMat;

        for (var i = -gridSize; i <= gridSize; ++i)
        {
            for (var j = -gridSize; j <= gridSize; ++j)
            {
                var entity = new Entity { Name = $"moving cube[{i}, {j}]" };
                var staticMeshComponent = new StaticModelComponent();
                entity.RootComponent = staticMeshComponent;
                entity.RootComponent.Position = new Vector3(transF * i, transF * j, 10.0f);
                staticMeshComponent.StaticModel = boxModel;

                _entities.Add(entity);
                world.AddEntity(entity);
            }
        }

        for (var i = -gridSize; i <= gridSize; ++i)
        {
            for (var j = -gridSize; j <= gridSize; ++j)
            {
                var entity = new Entity { Name = $"Rotating cube[{i}, {j}]" };
                var staticMeshComponent = new StaticModelComponent();
                entity.RootComponent = staticMeshComponent;
                entity.RootComponent.Position = new Vector3(transF * i, transF * j, 10.0f);
                staticMeshComponent.StaticModel = boxModel;

                _rotatingEntities.Add(entity);
                world.AddEntity(entity);
            }
        }

        world.DisplaySpacePartitioning = true;
    }

    public override void Update(GameTime gameTime)
    {
        var x = 0;
        for (var i = -gridSize; i <= gridSize; ++i)
        {
            for (var j = -gridSize; j <= gridSize; ++j)
            {
                var entity = _rotatingEntities[x];
                var startPosition = new Vector3(transF * i, transF * j, 10.0f);
                entity.RootComponent.Position = Vector3.Transform(startPosition, Quaternion.CreateFromAxisAngle(Vector3.Up, (float)gameTime.TotalGameTime.TotalSeconds)); ;
                x++;
            }
        }

        foreach (var entity in _entities)
        {
            var position = entity.RootComponent.Position;
            position.Z -= (float)gameTime.ElapsedGameTime.Milliseconds / 1000f * 5f;
            entity.RootComponent.Position = position;
        }
    }

    public override void Clean()
    {
        _rotatingEntities.Clear();
        _entities.Clear();
    }
}