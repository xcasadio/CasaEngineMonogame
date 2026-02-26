using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Demonstrates 2-view split-screen rendering using RenderPipeline.
/// Left half: camera 1 (front view). Right half: camera 2 (side view).
/// </summary>
public class SplitScreenDemo : Demo
{
    private ArcBallCameraComponent? _camera2;
    private CasaEngineGame? _game;

    public override string Title => "Split-screen demo (2 views)";
    public override string Description => "Demonstrates 2-view split-screen rendering: left half shows a front camera, right half shows a side camera of the same scene.";

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        var world = game.GameManager.CurrentWorld;

        // Ground plane
        var groundEntity = new Entity { Name = "Ground" };
        var groundMesh = new StaticModelComponent();
        groundEntity.RootComponent = groundMesh;
        groundMesh.StaticModel = StaticModel.CreateFromPrimitive(new BoxPrimitive(20, 1, 20));
        groundMesh.StaticModel.Meshes[0].Initialize(game.GraphicsDevice);
        groundMesh.LocalPosition = new Vector3(0, -0.5f, 0);
        world.AddEntity(groundEntity);

        // A column of boxes so that the two cameras see something from different angles
        for (int i = 0; i < 3; i++)
        {
            var boxEntity = new Entity { Name = $"Box {i}" };
            var boxMesh = new StaticModelComponent();
            boxEntity.RootComponent = boxMesh;
            boxMesh.StaticModel = StaticModel.CreateFromPrimitive(new BoxPrimitive(2, 2, 2));
            boxMesh.StaticModel.Meshes[0].Initialize(game.GraphicsDevice);
            boxMesh.LocalPosition = new Vector3((i - 1) * 4, 1, 0);
            world.AddEntity(boxEntity);
        }
    }

    public override CameraComponent CreateCamera(CasaEngineGame game)
    {
        // Camera 1 — inherited from Demo base
        var camera1 = (ArcBallCameraComponent)base.CreateCamera(game);

        // Camera 2 — side view
        var entity2 = new Entity { Name = "Camera 2 (side)" };
        _camera2 = new ArcBallCameraComponent();
        entity2.RootComponent = _camera2;
        entity2.Initialize();
        game.GameManager.CurrentWorld.AddEntity(entity2);

        return camera1;
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        base.InitializeCamera(camera);
        //return;

        var game = _game!;
        var pp = game.GraphicsDevice.PresentationParameters;

        // Compute left/right viewport rectangles
        var rects = SplitScreenLayout.Compute(pp.BackBufferWidth, pp.BackBufferHeight, 2, SplitMode.Vertical);

        // ---- Camera 1: front-left view ----
        var cam1 = (ArcBallCameraComponent)camera;
        cam1.OnScreenResized(rects[0].Width, rects[0].Height);

        // ---- Camera 2: side view ----
        _camera2!.SetCamera(Vector3.Right * 18 + Vector3.Up * 10, Vector3.Zero, Vector3.Up);
        _camera2.OnScreenResized(rects[1].Width, rects[1].Height);

        // ---- Register views ----
        var world = game.GameManager.CurrentWorld;
        var viewManager = game.GameManager.ViewManager;

        viewManager.Clear();
        viewManager.AutoLayoutMode = SplitMode.Vertical;
        viewManager.Add(new RenderView(world, cam1, new BackBufferSurface(rects[0]))
        {
            Name = "View 1 (front)",
            ClearColor = Color.CornflowerBlue,
            ShowDebugOverlay = true,
        });
        viewManager.Add(new RenderView(world, _camera2, new BackBufferSurface(rects[1]))
        {
            Name = "View 2 (side)",
            ClearColor = new Color(0.12f, 0.12f, 0.20f),
            ShowDebugOverlay = true,
        });
    }

    public override void Update(GameTime gameTime)
    {
    }

    public override void Clean()
    {
        if (_game != null)
        {
            _game.GameManager.ViewManager.Clear();
        }

        _camera2 = null;
        _game = null;
    }
}
