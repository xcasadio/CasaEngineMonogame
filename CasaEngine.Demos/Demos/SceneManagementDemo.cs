using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Demos.Demos;

public class SceneManagementDemo : Demo
{
    private Entity _entityCube;
    public override string Title => "Scene management demo";

    public override void Initialize(CasaEngineGame game)
    {
        var boxPrimitive = new BoxPrimitive(game.GraphicsDevice);
        var staticMesh = boxPrimitive.CreateMesh();
        staticMesh.Initialize(game.GraphicsDevice, null);

        staticMesh.Texture = game.GameManager.AssetContentManager.GetAsset<Texture>(Texture.DefaultTextureName);

        var gridSize = 10;
        var transF = 1f; //2.0f / gridSize;

        for (var i = -gridSize; i <= gridSize; ++i)
        {
            for (var j = -gridSize; j <= gridSize; ++j)
            {
                var entity = new Entity();
                entity.Name = $"Cube[{i}, {j}]";
                entity.Coordinates.LocalPosition = new Vector3(transF * i, transF * j, 0.0f);
                var staticMeshComponent = new StaticMeshComponent();
                staticMeshComponent.Mesh = staticMesh;
                entity.ComponentManager.Add(staticMeshComponent);
                entity.Initialize(game);

                //rootEntity.Children.Add(entity);
                game.GameManager.CurrentWorld.AddEntity(entity);
            }
        }

        _entityCube = new Entity();
        _entityCube.Name = "Moving cube";

        var staticMeshComponent2 = new StaticMeshComponent();
        staticMeshComponent2.Mesh = staticMesh;
        _entityCube.ComponentManager.Add(staticMeshComponent2);
        _entityCube.Initialize(game);
        game.GameManager.CurrentWorld.AddEntity(_entityCube);

        //var sceneGraphComponent = new SceneGraphComponent2(game);
        //sceneGraphComponent.Initialize();

        game.GameManager.CurrentWorld.Initialize();
    }

    public override void Update(GameTime gameTime)
    {
        var position = Vector3.Transform(Vector3.UnitX * 20f, Quaternion.CreateFromAxisAngle(Vector3.Up, (float)gameTime.TotalGameTime.TotalSeconds));
        _entityCube.Coordinates.LocalPosition = position;
        _entityCube.IsPositionUpdated = true;
    }

    protected override void Clean()
    {

    }
}