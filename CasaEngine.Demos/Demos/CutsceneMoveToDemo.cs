using System;
using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos.Demos;

public sealed class CutsceneMoveToDemo : Demo
{
    private static readonly Vector3 StartPosition = new(-5f, 0.5f, 0f);
    private static readonly Vector3 Destination = new(5f, 0.5f, 0f);
    private static readonly TimeSpan? AutomationRestartTime = ResolveAutomationRestartTime();
    private CasaEngineGame? _game;
    private Entity? _hero;
    private CharacterControllerComponent? _controller;
    private CutsceneAsset? _cutsceneAsset;
    private KeyboardState _previousKeyboard;
    private bool _autoPlayPending;
    private bool _automationRestartTriggered;

    public override string Title => "Cutscene MoveTo demo";

    public override string Description => "Validates the cutscene asset pipeline with a direct MoveTo action driving a CharacterController. [Space]/[R] restart from start, [S] stops the current playback.";

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        var world = game.GameManager.CurrentWorld;
        var graphicsDevice = game.GraphicsDevice;

        _hero = CreateHero(graphicsDevice);
        _controller = _hero.GetRequiredComponent<CharacterControllerComponent>();
        world.AddEntity(_hero);

        world.AddEntity(CreateMarker(graphicsDevice, "CutsceneStart", StartPosition, new Color(64, 132, 196)));
        world.AddEntity(CreateMarker(graphicsDevice, "CutsceneDestination", Destination, new Color(210, 182, 74)));
        world.AddEntity(CreateGround(graphicsDevice));

        var assetInfo = AssetCatalog.GetByFileName(@"Content\Cutscenes\move_to_direct.cutscene")
            ?? throw new InvalidOperationException("Cutscene demo asset is missing from AssetInfos.json.");
        _cutsceneAsset = game.AssetContentManager.Load<CutsceneAsset>(assetInfo.Id, cache: false);
        _autoPlayPending = true;
    }

    public override CameraComponent CreateCamera(CasaEngineGame game)
    {
        var entity = new Entity { Name = "CutsceneDemoCamera" };
        var camera = new CameraLookAtComponent();
        entity.RootComponent = camera;
        entity.Initialize();
        game.GameManager.CurrentWorld.AddEntity(entity);
        return camera;
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        camera.SetPositionAndTarget(new Vector3(0f, 7f, 12f), new Vector3(0f, 0.5f, 0f));
    }

    public override void Update(GameTime gameTime)
    {
        var keyboard = _game?.IsActive == true ? Keyboard.GetState() : new KeyboardState();
        var world = _hero?.World;

        TryAutoPlay();
        TryAutomationRestart(gameTime, world);

        if (world != null && IsPressed(keyboard, Keys.Space))
        {
            PlayFromStart(world);
        }

        if (world != null && IsPressed(keyboard, Keys.S))
        {
            world.CutsceneDirector.Stop();
        }

        if (world != null && IsPressed(keyboard, Keys.R))
        {
            PlayFromStart(world);
        }

        _previousKeyboard = keyboard;
    }

    public override void Clean()
    {
        _game = null;
        _hero?.World?.CutsceneDirector.Stop();
        _hero = null;
        _controller = null;
        _cutsceneAsset = null;
        _autoPlayPending = false;
        _automationRestartTriggered = false;
    }

    private void PlayFromStart(CasaEngine.Framework.Scene.World.World world)
    {
        if (_cutsceneAsset == null)
        {
            return;
        }

        world.CutsceneDirector.Stop();
        ResetHeroToStart(world);
        world.CutsceneDirector.Play(_cutsceneAsset);
    }

    private void ResetHeroToStart(CasaEngine.Framework.Scene.World.World world)
    {
        world.CutsceneDirector.Stop();

        if (_hero?.RootComponent == null || _controller == null)
        {
            return;
        }

        _hero.RootComponent.Position = StartPosition;
        _controller.SetControlMode(CharacterControlMode.Player);
        _controller.Stop();
    }

    private bool IsPressed(KeyboardState keyboard, Keys key)
    {
        return keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    }

    private void TryAutoPlay()
    {
        if (!_autoPlayPending)
        {
            return;
        }

        var world = _hero?.World;
        if (world == null)
        {
            return;
        }

        _autoPlayPending = false;
        PlayFromStart(world);
    }

    private void TryAutomationRestart(GameTime gameTime, CasaEngine.Framework.Scene.World.World? world)
    {
        if (_automationRestartTriggered
            || AutomationRestartTime is null
            || world == null
            || gameTime.TotalGameTime < AutomationRestartTime.Value)
        {
            return;
        }

        _automationRestartTriggered = true;
        PlayFromStart(world);
    }

    private static TimeSpan? ResolveAutomationRestartTime()
    {
        var rawValue = Environment.GetEnvironmentVariable("CASAENGINE_CUTSCENE_RESTART_AT_MS");
        return int.TryParse(rawValue, out int restartDelayMs) && restartDelayMs >= 0
            ? TimeSpan.FromMilliseconds(restartDelayMs)
            : null;
    }

    private static Entity CreateHero(GraphicsDevice graphicsDevice)
    {
        var entity = new Entity { Name = "CutsceneHero" };
        var root = new DemoRootComponent();
        var mesh = CreateMeshComponent(graphicsDevice, new BoxPrimitive(0.8f, 1f, 0.8f), new Color(190, 86, 74));
        root.Position = StartPosition;
        root.AddChildComponent(mesh);
        entity.RootComponent = root;
        entity.AddComponent(new CharacterControllerComponent
        {
            Settings = new CharacterControllerSettings
            {
                MaxHorizontalSpeed = 3.5f,
                Acceleration = 18f,
                Deceleration = 24f,
                Gravity = 0f,
                GroundSnapDistance = 0f,
            }
        });
        return entity;
    }

    private static Entity CreateMarker(GraphicsDevice graphicsDevice, string name, Vector3 position, Color color)
    {
        var entity = new Entity { Name = name };
        var marker = new StaticModelComponent();
        marker.StaticModel = StaticModel.CreateFromPrimitive(new BoxPrimitive(0.35f, 0.08f, 0.35f));
        marker.StaticModel.Meshes[0].Initialize(graphicsDevice);
        marker.StaticModel.Meshes[0].Material = new LitDiffuseMaterial { DiffuseColor = color };
        marker.Position = new Vector3(position.X, 0.04f, position.Z);
        entity.RootComponent = marker;
        return entity;
    }

    private static Entity CreateGround(GraphicsDevice graphicsDevice)
    {
        var entity = new Entity { Name = "CutsceneGround" };
        var ground = CreateMeshComponent(graphicsDevice, new BoxPrimitive(12f, 0.08f, 3f), new Color(86, 114, 96));
        ground.Position = new Vector3(0f, -0.08f, 0f);
        entity.RootComponent = ground;
        return entity;
    }

    private static StaticModelComponent CreateMeshComponent(GraphicsDevice graphicsDevice, GeometricPrimitive primitive, Color color)
    {
        var component = new StaticModelComponent();
        component.StaticModel = StaticModel.CreateFromPrimitive(primitive);
        component.StaticModel.Meshes[0].Initialize(graphicsDevice);
        component.StaticModel.Meshes[0].Material = new LitDiffuseMaterial { DiffuseColor = color };
        return component;
    }

    private sealed class DemoRootComponent : SceneComponent
    {
        public override EntityComponent Clone()
        {
            return new DemoRootComponent();
        }
    }
}