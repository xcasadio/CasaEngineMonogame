using System;
using System.Collections.Generic;
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

/// <summary>
/// Plays a cutscene asset that walks an actor to a point. The cutscene is data (a .cutscene file); the demo only builds
/// the scene it acts on and asks the world's director to play it.
/// <para/>
/// Types to look at: <see cref="CutsceneAsset"/> (the data), <see cref="CutsceneDirector"/> (reached through
/// <c>world.RuntimeSystems.CutsceneDirector</c>, it plays and stops), and <see cref="CharacterControllerComponent"/>
/// (the component a MoveTo action drives).
/// </summary>
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

    public override string Description =>
        "A cutscene asset moves an actor: the red cube walks from the blue marker to the yellow one, driven " +
        "by a MoveTo action on its CharacterController. Restart it, stop it halfway, and see the cube come " +
        "back under player control.";

    public override IReadOnlyList<DemoControl> Controls { get; } = new DemoControl[]
    {
        new("Space", "restart the cutscene from the start"),
        new("S", "stop the cutscene where it is"),
        new("R", "put the actor back on the start marker and restart"),
    };

    public override IReadOnlyList<string> Highlights { get; } = new[]
    {
        "The cutscene is data: Content/Cutscenes/move_to_direct.cutscene, played by a CutsceneDirector.",
        "Stopping leaves the actor where it is, with its control mode restored.",
    };

    public override string DocumentationPath => "docs/engine/cutscene_commandes_sequentielles_async_coroutine.md";

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        var world = game.GameManager.CurrentWorld;
        var graphicsDevice = game.GraphicsDevice;

        // The actor the cutscene moves. The cutscene finds it by its entity name ("CutsceneHero", also written in the
        // .cutscene file) and needs a CharacterControllerComponent on it, so keep both in sync with the asset.
        _hero = CreateHero(graphicsDevice);
        _controller = _hero.GetRequiredComponent<CharacterControllerComponent>();
        world.AddEntity(_hero);

        // Scenery only: two flat markers for the start and the destination, and a ground. None of them takes part in
        // the cutscene; they make the walk readable.
        world.AddEntity(CreateMarker(graphicsDevice, "CutsceneStart", StartPosition, new Color(64, 132, 196)));
        world.AddEntity(CreateMarker(graphicsDevice, "CutsceneDestination", Destination, new Color(210, 182, 74)));
        world.AddEntity(CreateGround(graphicsDevice));

        // The cutscene is looked up in the asset catalogue by its file name, then read with LoadCopy, which gives this
        // demo a fresh copy that nobody else holds. Playing is not started here: Update does it on the first frame where
        // the hero belongs to a world (see TryAutoPlay).
        var assetInfo = AssetCatalog.GetByFileName(@"Cutscenes\move_to_direct.cutscene")
            ?? throw new InvalidOperationException("Cutscene demo asset is missing from AssetInfos.json.");
        _cutsceneAsset = game.AssetContentManager.LoadCopy<CutsceneAsset>(assetInfo.Id);
        _autoPlayPending = true;
    }

    // The base Demo creates an arc-ball camera and its InitializeCamera casts to that type, so a demo that wants another
    // camera overrides both methods together.
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
        var keyboard = DemoKeyboard.Read(_game);
        var world = _hero?.World;

        TryAutoPlay();
        TryAutomationRestart(gameTime, world);

        if (world != null && IsPressed(keyboard, Keys.Space))
        {
            PlayFromStart(world);
        }

        if (world != null && IsPressed(keyboard, Keys.S))
        {
            world.RuntimeSystems.CutsceneDirector.Stop();
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
        _hero?.World?.RuntimeSystems.CutsceneDirector.Stop();
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

        // Stop first so a play already running does not keep driving the actor while it is put back on the start marker.
        world.RuntimeSystems.CutsceneDirector.Stop();
        ResetHeroToStart(world);
        world.RuntimeSystems.CutsceneDirector.Play(_cutsceneAsset);
    }

    private void ResetHeroToStart(CasaEngine.Framework.Scene.World.World world)
    {
        world.RuntimeSystems.CutsceneDirector.Stop();

        if (_hero?.RootComponent == null || _controller == null)
        {
            return;
        }

        // Teleport the root component, give control back to the player and clear any leftover motion, so a restart
        // begins from a known state.
        _hero.RootComponent.Position = StartPosition;
        _controller.SetControlMode(CharacterControlMode.Player);
        _controller.Stop();
    }

    // True only on the frame the key goes down, so holding a key restarts the cutscene once.
    private bool IsPressed(KeyboardState keyboard, Keys key)
    {
        return keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    }

    // Starts the first play once the hero has a world; Initialize only raises the flag.
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

    // Lets an external script force one restart at a given game time (see ResolveAutomationRestartTime); it does
    // nothing when the environment variable is not set.
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
        // The root component is what the character controller moves (it reads and writes Owner.RootComponent), so it
        // carries the position. The red box is a child of it and follows.
        var root = new DemoRootComponent();
        var mesh = CreateMeshComponent(graphicsDevice, new BoxPrimitive(0.8f, 1f, 0.8f), new Color(190, 86, 74));
        root.Position = StartPosition;
        root.AddChildComponent(mesh);
        entity.RootComponent = root;
        // The component a MoveTo action drives. Gravity and ground snapping are zero because the ground below is only
        // a mesh with no collision: the controller is meant to slide along the plane at its start height.
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

    // A root with no mesh of its own: a plain SceneComponent whose only job is to hold the transform of its children.
    // Clone is the one member a concrete component must provide.
    private sealed class DemoRootComponent : SceneComponent
    {
        public override EntityComponent Clone()
        {
            return new DemoRootComponent();
        }
    }
}