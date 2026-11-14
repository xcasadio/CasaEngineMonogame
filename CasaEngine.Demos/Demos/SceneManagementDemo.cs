using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Stress-tests the world with many entities (two grids of 21 by 21 cubes) that move every frame, and turns on the
/// drawing of the world's space partitioning so you can see how the entities are indexed.
/// <para/>
/// Types to look at: <see cref="Entity"/> and <see cref="StaticModelComponent"/> (one cube is one entity whose root
/// component is the mesh), and <c>World.DisplaySpacePartitioning</c>. Every entity shares one model and one material.
/// </summary>
public class SceneManagementDemo : Demo
{
    private List<Entity> _rotatingEntities = new();
    private readonly List<Entity> _entities = new();
    private const int gridSize = 10;
    private const float transF = 1f;
    public override string Title => "Scene management demo";
    public override string Description =>
        "A world holding several hundred entities: one grid of cubes turns around the vertical axis, another " +
        "moves away, and the world's space partitioning is drawn over them.";

    public override IReadOnlyList<string> Highlights { get; } = new[]
    {
        "The drawn boxes are the cells of the world's space partitioning.",
        "Every cube is an entity whose position the demo writes each frame.",
    };

    public override void Initialize(CasaEngineGame game)
    {
        var world = game.GameManager.CurrentWorld;

        // One material and one mesh for all the cubes. LoadFromFile reads an image that is not in the asset catalogue,
        // relative to the project root; the demo owns the returned texture.
        var checkMat = new LitDiffuseMaterial
        {
            BasColor     = game.AssetContentManager.LoadFromFile<Texture2D>("checkboard.png"),
            DiffuseColor = Color.White,
        };

        var boxModel = StaticModel.CreateFromPrimitive(new BoxPrimitive());
        boxModel.Meshes[0].Initialize(game.GraphicsDevice);
        boxModel.Meshes[0].Material = checkMat;

        // First grid, 21 by 21 cubes in a plane at z = 10, spaced one unit apart. These are the "moving" cubes: Update
        // moves them along Z. Each is its own entity, and all of them share boxModel. They go in _entities so Update can
        // find them again.
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

        // Second grid, built at the same positions as the first, kept in _rotatingEntities in the same i/j order:
        // Update relies on that order to recompute each cube's start position from its index.
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

        // Makes the world draw the debug view of its spatial index after the entities. The same index is what the world
        // queries with the camera frustum to decide which entities to draw.
        world.DisplaySpacePartitioning = true;
    }

    // Writing Position each frame is the point of the demo: the world has to keep its spatial index in step with
    // entities that move. The rotating grid is recomputed from its start position and the total elapsed time, so
    // it turns at one radian per second around the vertical axis through the world origin.
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

        // The other grid moves by integrating: each frame its Z decreases by 5 units per second times the frame time.
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