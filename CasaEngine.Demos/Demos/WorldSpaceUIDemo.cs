using System;
using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.GUI;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Materials;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Demonstrates rendering a hosted UI runtime into a texture and displaying it on a world-space quad.
/// </summary>
public sealed class WorldSpaceUIDemo : Demo
{
    private CasaEngineGame? _game;
    private WorldUIComponent? _worldUi;
    private HudScreen? _hudScreen;

    public override string Title => "World-space UI demo";
    public override string Description => "Renders a UI runtime offscreen and displays the resulting texture on a quad inside the 3D world.";

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        var world = game.GameManager.CurrentWorld;

        var groundEntity = new Entity { Name = "Ground" };
        var groundMesh = new StaticModelComponent();
        groundEntity.RootComponent = groundMesh;
        groundMesh.StaticModel = StaticModel.CreateFromPrimitive(new BoxPrimitive(24, 1, 24));
        groundMesh.StaticModel.Meshes[0].Initialize(game.GraphicsDevice);
        groundMesh.StaticModel.Meshes[0].Material = new LitDiffuseMaterial { DiffuseColor = new Color(160, 150, 135) };
        groundMesh.LocalPosition = new Vector3(0, -0.5f, 0);
        world.AddEntity(groundEntity);

        var screenEntity = new Entity { Name = "World UI Screen" };
        var screenMesh = new StaticModelComponent();
        screenEntity.RootComponent = screenMesh;
        screenMesh.StaticModel = StaticModel.CreateFromPrimitive(new PlanePrimitive(8f, 4.5f));
        screenMesh.StaticModel.Meshes[0].Initialize(game.GraphicsDevice);

        var screenMaterial = new UnlitTextureMaterial
        {
            Tint = Color.White,
            Alpha = 1.0f,
        };

        screenMesh.StaticModel.Meshes[0].Material = screenMaterial;
        screenMesh.LocalPosition = new Vector3(0, 4.0f, 0);
        screenMesh.LocalOrientation = Quaternion.CreateFromAxisAngle(Vector3.Right, MathHelper.PiOver2);
        world.AddEntity(screenEntity);

        _worldUi = new WorldUIComponent(game.GraphicsDevice, 1024, 576)
        {
            UIView = null,
        };
        _worldUi.UIView = game.UIViewRuntimeFactory.Create(game, _worldUi.Surface, game.RuntimeContext);
        _worldUi.BindToMaterial(screenMaterial);
        world.RegisterWorldUI(_worldUi);

        _hudScreen = new HudScreen(static () => { });
        _worldUi.UIView?.PushScreen(_hudScreen);
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        ((ArcBallCameraComponent)camera).SetCamera(new Vector3(0, 7, 18), new Vector3(0, 4, 0), Vector3.Up);
    }

    public override void Update(GameTime gameTime)
    {
    }

    public override void Clean()
    {
        if (_game?.GameManager.CurrentWorld != null && _worldUi != null)
        {
            _game.GameManager.CurrentWorld.UnregisterWorldUI(_worldUi);
            if (_hudScreen != null)
            {
                _worldUi.UIView?.RemoveScreen(_hudScreen);
            }
            _worldUi.Dispose();
        }

        _hudScreen = null;
        _worldUi = null;
        _game = null;
    }
}