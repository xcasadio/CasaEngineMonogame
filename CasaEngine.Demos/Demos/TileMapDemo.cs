using System;
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScreenEffects;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Renders a 2D tile map. Also hosts the above-UI screen effect smoke (T2.2,
/// ai-agent/tasks/screen-effect-above-ui-tasks.md, ADR-0033): this is the only demo built on a
/// <see cref="Camera2dComponent"/> (<see cref="CreateCamera"/> below), the only camera kind the
/// effect quad's placement formula supports (<see cref="Application.Components.ScreenEffectComponent"/>).
/// </summary>
public class TileMapDemo : Demo
{
    /// <summary>Fade-in/fade-out duration of each half of the smoke's fade, in seconds.</summary>
    private const float FadeSmokeSegmentDuration = 0.6f;

    private enum FadeSmokeState { Idle, FadingOut, FadingIn }

    public override string Title => "Tile map demo";
    public override string Description => "Renders a tile map loaded from sprite and 2D animation assets using the CasaEngine tile map system. Press 1/2 for the above-UI screen effect smoke (T2.2): 1 fades to black BelowUI (default, the HUD below stays lit), 2 fades to black AboveUI (the HUD darkens with the scene).";

    private CasaEngineGame? _game;
    private ScreenEffectSmokeHudScreen? _fadeSmokeScreen;
    private KeyboardState _previousKeyboard;
    private FadeSmokeState _fadeState = FadeSmokeState.Idle;
    private float _fadeStateElapsed;

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        var world = game.GameManager.CurrentWorld;

        //============ tileMap ===============
        var assetInfo = AssetCatalog.GetByFileName(@"Maps\map_1_1.tileMap");
        var tileMapData = game.AssetContentManager.Load<TileMapData>(assetInfo.Id);

        var entity = new Entity { Name = "TileMap" };
        var tileMapComponent = new TileMapComponent();
        entity.RootComponent = tileMapComponent;
        entity.RootComponent.Position = new Vector3(0, 700, 0.0f);
        tileMapComponent.TileMapData = tileMapData;

        world.AddEntity(entity);

        //============ player ===============
        entity = new Entity { Name = "Link" };
        //===
        var animatedSprite = new AnimatedSpriteComponent();
        entity.RootComponent = animatedSprite;
        entity.RootComponent.Position = new Vector3(100, 550, 0.3f);
        //ressources
        LoadSprites(game.AssetContentManager, game.GraphicsDevice);
        var animations = LoadAnimations(game.AssetContentManager, game.GraphicsDevice);
        foreach (var animation in animations)
        {
            animatedSprite.AddAnimation(new Animation2d(animation));
        }
        //===
        var physicsComponent = new CollisionComponent();
        physicsComponent.Fixtures.Add(new ColliderFixture(new Sphere()));
        entity.AddComponent(physicsComponent);
        physicsComponent.PhysicsDefinition.PhysicsType = PhysicsType.Dynamic;
        physicsComponent.PhysicsDefinition.LinearFactor = new Vector3(1, 1, 0);
        physicsComponent.PhysicsDefinition.AngularFactor = new Vector3(0, 0, 1);
        physicsComponent.PhysicsDefinition.Mass = 1.0f;
        physicsComponent.Scale = new Vector3(25);
        physicsComponent.PhysicsDefinition.ApplyGravity = false;
        physicsComponent.PhysicsDefinition.AngularFactor = Vector3.Zero;

        entity.AddComponent(new PlayerComponent());

        world.AddEntity(entity);
    }

    private void LoadSprites(AssetContentManager assetContentManager, GraphicsDevice graphicsDevice)
    {
        var spriteAssetInfos = AssetCatalog.AssetInfos
            .Where(x => x.FileName.EndsWith(Constants.FileNameExtensions.Sprite));

        foreach (var assetInfo in spriteAssetInfos)
        {
            var spriteData = assetContentManager.Load<SpriteData>(assetInfo.Id);
        }
    }

    private List<Animation2dData> LoadAnimations(AssetContentManager assetContentManager, GraphicsDevice graphicsDevice)
    {
        var animationsAssetInfos = AssetCatalog.AssetInfos
            .Where(x => x.FileName.EndsWith(Constants.FileNameExtensions.Animation2d));

        var animations = new List<Animation2dData>();

        foreach (var assetInfo in animationsAssetInfos)
        {
            var animation2dData = assetContentManager.Load<Animation2dData>(assetInfo.Id);
            animations.Add(animation2dData);
        }

        return animations;
    }

    public override CameraComponent CreateCamera(CasaEngineGame game)
    {
        var entity = new Entity();
        var camera = new Camera2dComponent();
        camera.Target = new Vector3(game.Window.ClientBounds.Size.X / 2f, game.Window.ClientBounds.Size.Y / 2f, 0.0f);
        entity.AddComponent(camera);
        entity.Initialize();
        game.GameManager.CurrentWorld.AddEntity(entity);

        return camera;
    }
    public override void InitializeCamera(CameraComponent camera)
    {
        // The camera is already framed on the map centre by CreateCamera.

        // The per-view UI runtime is created after world.LoadContent, so it is guaranteed to be
        // available at this point in the lifecycle (same ordering as UIOverlayDemo).
        _fadeSmokeScreen = new ScreenEffectSmokeHudScreen();
        GetUIView()?.PushScreen(_fadeSmokeScreen);

        StartAutomationFadeSmokeIfRequested();
    }

    public override void Update(GameTime gameTime)
    {
        var keyboard = _game?.IsActive == true ? Keyboard.GetState() : new KeyboardState();

        if (_fadeState == FadeSmokeState.Idle)
        {
            if (keyboard.IsKeyDown(Keys.D1) && !_previousKeyboard.IsKeyDown(Keys.D1))
            {
                StartFadeSmoke(ScreenEffectLayer.BelowUI);
            }
            else if (keyboard.IsKeyDown(Keys.D2) && !_previousKeyboard.IsKeyDown(Keys.D2))
            {
                StartFadeSmoke(ScreenEffectLayer.AboveUI);
            }
        }

        _previousKeyboard = keyboard;

        AdvanceFadeSmoke((float)gameTime.ElapsedGameTime.TotalSeconds);
    }

    public override void Clean()
    {
        if (_fadeSmokeScreen != null)
        {
            GetUIView()?.RemoveScreen(_fadeSmokeScreen);
            _fadeSmokeScreen = null;
        }

        _game = null;
        _fadeState = FadeSmokeState.Idle;
        _fadeStateElapsed = 0f;
    }

    // ---- Above-UI screen effect smoke (T2.2) ----

    /// <summary>
    /// Starts the short fade-to-black-and-back demonstrated by keys 1/2: fade out over
    /// <see cref="FadeSmokeSegmentDuration"/>, hold black an instant, fade back in over the same
    /// duration, then <see cref="ScreenEffectService.Clear"/> - "durée courte, retour automatique"
    /// (plan T2.2). <paramref name="layer"/> selects which side of the UI composition the quad
    /// draws on; <see cref="ScreenEffectLayer.BelowUI"/> is the engine default (nothing changes for
    /// any other consumer), <see cref="ScreenEffectLayer.AboveUI"/> is what this smoke exists to show.
    /// </summary>
    private void StartFadeSmoke(ScreenEffectLayer layer)
    {
        var service = _game?.ScreenEffectComponent?.Service;
        if (service == null)
        {
            return;
        }

        service.Layer = layer;
        service.StartFade(255, 255, 255, 0, 0, 0, FadeSmokeSegmentDuration, SpriteBlendMode.Subtractive);
        _fadeState = FadeSmokeState.FadingOut;
        _fadeStateElapsed = 0f;
    }

    private void AdvanceFadeSmoke(float elapsedSeconds)
    {
        if (_fadeState == FadeSmokeState.Idle)
        {
            return;
        }

        var service = _game?.ScreenEffectComponent?.Service;
        if (service == null)
        {
            _fadeState = FadeSmokeState.Idle;
            return;
        }

        _fadeStateElapsed += elapsedSeconds;
        if (_fadeStateElapsed < FadeSmokeSegmentDuration)
        {
            return;
        }

        if (_fadeState == FadeSmokeState.FadingOut)
        {
            service.StartFade(0, 0, 0, 255, 255, 255, FadeSmokeSegmentDuration, SpriteBlendMode.Subtractive);
            _fadeState = FadeSmokeState.FadingIn;
            _fadeStateElapsed = 0f;
        }
        else
        {
            service.Clear();
            _fadeState = FadeSmokeState.Idle;
            _fadeStateElapsed = 0f;
        }
    }

    /// <summary>
    /// Automation-only entry point, read once at startup: lets a screenshot-driven run of
    /// <c>CasaEngine.Demos</c> (see <c>DemosGame</c>'s own <c>CASAENGINE_CAPTURE_SCREENSHOT_*</c>
    /// variables) exercise a fade without a human pressing 1/2, so the mid-fade numeric pixel check
    /// of plan step T2.2/4 can run unattended. <c>CASAENGINE_TILEMAP_FADE_SMOKE_LAYER</c> set to
    /// "BelowUI" or "AboveUI" starts that fade-out immediately; unset (the normal interactive case),
    /// this is a no-op and only the 1/2 keys drive the smoke.
    /// </summary>
    private void StartAutomationFadeSmokeIfRequested()
    {
        var requestedLayer = Environment.GetEnvironmentVariable("CASAENGINE_TILEMAP_FADE_SMOKE_LAYER");
        if (string.IsNullOrWhiteSpace(requestedLayer))
        {
            return;
        }

        if (Enum.TryParse<ScreenEffectLayer>(requestedLayer, ignoreCase: true, out var layer))
        {
            StartFadeSmoke(layer);
        }
    }

    private IUIViewRuntime? GetUIView()
        => _game?.GameManager.ViewManager.GetActiveUIView();
}