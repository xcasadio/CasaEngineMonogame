using System;
using System.Collections.Generic;
using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.AI.Navigation;
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
/// Plays a cutscene whose NavigateTo action sends an actor along a path found on a navigation grid, instead of in a
/// straight line (compare <see cref="CutsceneMoveToDemo"/>).
/// <para/>
/// Types to look at: <see cref="NavigationGrid2D"/> (the walkable map), <see cref="NavigationAgentComponent"/> (finds the
/// path), <see cref="CharacterControllerNavigationDriverComponent"/> (walks it) and <see cref="CharacterControllerComponent"/>
/// (the body that moves). The cutscene asset only names the actor and the destination.
/// </summary>
public sealed class CutsceneNavigateToDemo : Demo
{
    // The grid covers x from -3 to 4 and z from -1 to 2 (see CreateNavigationGrid): the start and destination markers
    // are inside it.
    private static readonly Vector3 GridOrigin = new(-3f, 0f, -1f);
    private static readonly Vector3 StartPosition = new(-2.5f, 0.5f, 0.5f);
    private static readonly Vector3 Destination = new(3.5f, 0.5f, 0.5f);
    private CasaEngineGame _game;
    private Entity _hero;
    private CharacterControllerComponent _controller;
    private NavigationAgentComponent _navigationAgent;
    private CharacterControllerNavigationDriverComponent _navigationDriver;
    private CutsceneAsset _cutsceneAsset;
    private bool _autoPlayPending;

    public override string Title => "Cutscene NavigateTo demo";

    public override string Description =>
        "A cutscene asset sends an actor through a navigation grid: the NavigateTo action asks the cube's " +
        "NavigationAgentComponent for a path, and a CharacterControllerNavigationDriverComponent walks it.";

    public override IReadOnlyList<DemoControl> Controls { get; } = new DemoControl[]
    {
        DemoControl.ForActions("restart the cutscene from the start", "Cutscene.Restart"),
        DemoControl.ForActions("stop the cutscene where it is", "Cutscene.Stop"),
        DemoControl.ForActions("put the actor back on the start marker and restart", "Cutscene.Reset"),
    };

    public override IReadOnlyList<string> Highlights { get; } = new[]
    {
        "The cube follows the path of a NavigationGrid2D, not a straight line.",
        "The cutscene is data: Content/Cutscenes/navigate_to_grid.cutscene.",
    };

    public override string DocumentationPath => "docs/engine/navigation-engine-features.md";

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        var world = game.GameManager.CurrentWorld;
        var graphicsDevice = game.GraphicsDevice;
        NavigationGrid2D navigationGrid = CreateNavigationGrid();

        // The actor: the cutscene finds it by its entity name ("CutsceneNavHero", also written in the .cutscene file) and
        // requires on it a NavigationAgentComponent with a map, a CharacterControllerNavigationDriverComponent and a
        // CharacterControllerComponent. The three are fetched back here to reset them on restart.
        _hero = CreateHero(graphicsDevice, navigationGrid);
        _controller = _hero.GetRequiredComponent<CharacterControllerComponent>();
        _navigationAgent = _hero.GetRequiredComponent<NavigationAgentComponent>();
        _navigationDriver = _hero.GetRequiredComponent<CharacterControllerNavigationDriverComponent>();
        world.AddEntity(_hero);

        // Scenery only: markers for the start and destination, and a ground under them. Navigation reads the grid, not
        // these meshes.
        world.AddEntity(CreateMarker(graphicsDevice, "CutsceneNavStart", StartPosition, new Color(64, 132, 196)));
        world.AddEntity(CreateMarker(graphicsDevice, "CutsceneNavDestination", Destination, new Color(210, 182, 74)));
        world.AddEntity(CreateGround(graphicsDevice));

        // The cutscene is found in the asset catalogue by file name, then read with LoadCopy (a fresh copy owned by this
        // demo). Playing waits for the first Update where the hero has a world (see TryAutoPlay).
        var assetInfo = AssetCatalog.GetByFileName(@"Cutscenes\navigate_to_grid.cutscene")
            ?? throw new InvalidOperationException("Cutscene navigation demo asset is missing from AssetInfos.json.");
        _cutsceneAsset = game.AssetContentManager.LoadCopy<CutsceneAsset>(assetInfo.Id);
        _autoPlayPending = true;
    }

    // The base Demo creates an arc-ball camera and its InitializeCamera casts to that type, so a demo that wants another
    // camera overrides both methods together.
    public override CameraComponent CreateCamera(CasaEngineGame game)
    {
        var entity = new Entity { Name = "CutsceneNavigateDemoCamera" };
        var camera = new CameraLookAtComponent();
        entity.RootComponent = camera;
        entity.Initialize();
        game.GameManager.CurrentWorld.AddEntity(entity);
        return camera;
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        camera.SetPositionAndTarget(new Vector3(0f, 8f, 13f), new Vector3(0f, 0.5f, 0f));
    }

    public override void Update(GameTime gameTime)
    {
        var world = _hero?.World;

        TryAutoPlay();

        if (world != null && DemoInput.WasPressed(_game, "Cutscene.Restart"))
        {
            PlayFromStart(world);
        }

        if (world != null && DemoInput.WasPressed(_game, "Cutscene.Stop"))
        {
            world.RuntimeSystems.CutsceneDirector.Stop();
        }

        if (world != null && DemoInput.WasPressed(_game, "Cutscene.Reset"))
        {
            PlayFromStart(world);
        }
    }

    public override void Clean()
    {
        _game = null;
        _hero?.World?.RuntimeSystems.CutsceneDirector.Stop();
        _hero = null;
        _controller = null;
        _navigationAgent = null;
        _navigationDriver = null;
        _cutsceneAsset = null;
        _autoPlayPending = false;
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

        // Drop the path search and the walk in progress, put the root component back on the start marker, then hand
        // control back to the player so the restart begins from a known state.
        _navigationAgent?.Cancel();
        _navigationDriver?.Cancel();
        _hero.RootComponent.Position = StartPosition;
        _controller.SetControlMode(CharacterControlMode.Player);
        _controller.Stop();
    }

    // True only on the frame the key goes down, so holding a key restarts the cutscene once.

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

    private static NavigationGrid2D CreateNavigationGrid()
    {
        // 7 by 3 cells of size 1. The grid lies in the XZ plane: a cell's x index runs along world X and its y index along
        // world Z, starting at GridOrigin. Every cell is walkable on the Ground layer, so the path is a straight run here;
        // mark a cell with NavigationGridCell.Blocked to force a detour.
        var grid = new NavigationGrid2D(7, 3, 1f, GridOrigin);
        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                grid.SetCell(x, y, new NavigationGridCell(true, 1f, NavigationLayerMask.Ground));
            }
        }

        return grid;
    }

    private static Entity CreateHero(GraphicsDevice graphicsDevice, NavigationGrid2D navigationGrid)
    {
        var entity = new Entity { Name = "CutsceneNavHero" };
        // The root component is what the character controller moves (it reads and writes Owner.RootComponent), so it
        // carries the position. The red box is a child of it and follows.
        var root = new DemoRootComponent();
        var mesh = CreateMeshComponent(graphicsDevice, new BoxPrimitive(0.8f, 1f, 0.8f), new Color(190, 86, 74));
        root.Position = StartPosition;
        root.AddChildComponent(mesh);
        entity.RootComponent = root;
        // The agent computes the path on the grid; the query restricts it to cells on the Ground layer. The driver then
        // turns that path into moves on the character controller below.
        entity.AddComponent(new NavigationAgentComponent
        {
            NavigationMap = navigationGrid,
            Query = new NavigationQuery { LayerMask = NavigationLayerMask.Ground },
            StoppingDistance = 0.12f,
        });
        entity.AddComponent(new CharacterControllerNavigationDriverComponent());
        // Gravity and ground snapping are zero because the ground is only a mesh with no collision: the controller
        // is meant to slide along the plane at its start height.
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
        var entity = new Entity { Name = "CutsceneNavigationGround" };
        var ground = CreateMeshComponent(graphicsDevice, new BoxPrimitive(8f, 0.08f, 4f), new Color(86, 114, 96));
        ground.Position = new Vector3(0f, -0.08f, 0.5f);
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