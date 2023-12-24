using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using TomShane.Neoforce.Controls.Graphics;
using Veldrid.SceneGraph;
using Veldrid.SceneGraph.Util;

namespace CasaEngine.Demos.Demos;

public class SceneManagementDemo : Demo
{
    private CasaEngineGame _game;
    private Renderer _renderer;
    private IGroup _root;
    public override string Title => "Scene management demo";

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        var world = game.GameManager.CurrentWorld;

        var pipelineState = new PipelineState();

        _root = Group.Create();
        _root.NameString = "Root";
        _root.PipelineState = pipelineState;

        var scale_xform = MatrixTransform.Create(Matrix.CreateScale(0.05f));
        scale_xform.PipelineState = pipelineState;
        scale_xform.NameString = "Scale XForm";

        var cube = new GeometryNode(); //CreateCube();
        cube.PipelineState = pipelineState;
        scale_xform.AddChild(cube);

        var gridSize = 10;
        var transF = 2.0f / gridSize;
        for (var i = -gridSize; i <= gridSize; ++i)
        {
            for (var j = -gridSize; j <= gridSize; ++j)
            {
                var xform = MatrixTransform.Create(Matrix.CreateTranslation(transF * i, transF * j, 0.0f));
                xform.PipelineState = pipelineState;
                xform.NameString = $"XForm[{i}, {j}]";
                xform.AddChild(scale_xform);
                _root.AddChild(xform);
            }
        }

        _renderer = new Renderer();
        _renderer.Initialize(game.GraphicsDevice);
        /*root.PipelineState = CreateSharedState();

        viewer.SetSceneData(root);
        viewer.ViewAll();
        viewer.Run();*/
    }

    public override CameraComponent CreateCamera(CasaEngineGame game)
    {
        var entity = new Entity();
        var camera = new ArcBallCameraComponent();
        entity.ComponentManager.Components.Add(camera);
        game.GameManager.CurrentWorld.AddEntityImmediately(entity);

        return camera;
    }

    public override void InitializeCamera(CameraComponent camera)
    {

    }

    public override void Update(GameTime gameTime)
    {
        _renderer.HandleOperation(_game.GraphicsDevice, _root,
            _game.GameManager.ActiveCamera.ViewMatrix,
            _game.GameManager.ActiveCamera.ProjectionMatrix);
    }

    protected override void Clean()
    {

    }
}