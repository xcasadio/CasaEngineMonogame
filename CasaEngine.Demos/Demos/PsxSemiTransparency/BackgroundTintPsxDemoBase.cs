using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScrollingLayers;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using EngineTexture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Demos.Demos.PsxSemiTransparency;

/// <summary>
/// E19.g G2d (ADR-0066): the full-view tint overlay of the scrolling layers draws with the PSX mode of the map (the
/// <c>BGColorA</c> byte of the original) instead of a fixed average. One scene per mode: the plain background layer of the
/// G2c demo (<c>Content/PsxBackdropLayers/background.png</c>, (100, 150, 200), pass <c>Background</c>) and one tint over the
/// whole view. The scene reads its own back-buffer in process (<see cref="BackBufferProbe"/>) at three layer points and
/// compares it with the PSX formula on 8-bit colours, to within one level per channel. Run a scene from the
/// <c>CasaEngine.Demos</c> folder with <c>CASAENGINE_START_DEMO="&lt;Title&gt;"</c>, with
/// <c>CASAENGINE_CAPTURE_SCREENSHOT_PATH</c> to also save the image and <c>CASAENGINE_DEMO_PIXELS_PATH</c> to save the readout.
/// </summary>
public abstract class BackgroundTintPsxDemoBase : Demo
{
    protected static readonly Color Background = new(100, 150, 200, 255);

    private const int ViewWidth = 320;
    private const int ViewHeight = 240;

    private static readonly Guid BackgroundSheetId = Guid.Parse("a3000000-0000-0000-0000-000000000001");

    private static readonly RenderSortKey2D TintSortKey = new((int)RenderPass2D.Effects, -1, 0, 0, 0, 0, 0);

    private readonly BackBufferProbe _probe;
    private readonly List<IDisposable> _holds = new();
    private CasaEngineGame? _game;
    private Vector3 _cameraTarget;

    protected BackgroundTintPsxDemoBase(string title)
    {
        Title = title;
        _probe = new BackBufferProbe(title);
    }

    public override string Title { get; }

    /// <summary>The tint colour, as the DLL hands it to the engine (alpha 255).</summary>
    protected abstract Color TintColor { get; }

    protected abstract SpritePsxSemiTransparency TintMode { get; }

    /// <summary>The back-buffer colour expected at every probe point.</summary>
    protected abstract Color ExpectedColor { get; }

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        _cameraTarget = new Vector3(game.Window.ClientBounds.Size.X / 2f, game.Window.ClientBounds.Size.Y / 2f, 0f);

        var assets = game.AssetContentManager;
        var path = Path.Combine(AppContext.BaseDirectory, "Content", "PsxBackdropLayers", "background.png");
        var texture2d = (Texture2D)new Texture2DLoader().LoadAsset(path, assets);
        var texture2dId = Guid.NewGuid();
        _holds.Add(assets.Register(texture2dId, texture2d));
        _holds.Add(assets.Register(BackgroundSheetId, new EngineTexture(texture2dId, game.GraphicsDevice)));

        var service = game.ScrollingLayerComponent.Service;
        service.SetConfiguration(new ScrollingLayerConfiguration(ViewWidth, ViewHeight, ViewWidth, ViewHeight));
        service.SetLayers(new[]
        {
            new ScrollingLayerDefinition
            {
                FrameTextureAssetIds = new[] { BackgroundSheetId },
                FactorXNum = 0,
                FactorXDenom = 1,
                FactorYNum = 0,
                FactorYDenom = 1,
                AnimTimer = 100,
                Pass = RenderPass2D.Background,
                OrderInLayer = 0,
                StableId = 0,
                Blend = SpriteBlendMode.Opaque,
                Tint = Color.White,
                PsxSemiTransparency = SpritePsxSemiTransparency.None
            }
        });
        service.SetTint(new ScrollingTintDefinition(TintColor, TintSortKey, TintMode));

        // Layer pixel (px, py) is the world position (target.X - 160 + px, target.Y + 120 - py), the world Y axis pointing up.
        AddCheck($"tint {TintMode} over the background, near the top left", 48, 48);
        AddCheck($"tint {TintMode} over the background, centre", 160, 120);
        AddCheck($"tint {TintMode} over the background, near the bottom right", 300, 220);
    }

    private void AddCheck(string label, float layerX, float layerY)
    {
        var world = new Vector3(_cameraTarget.X - ViewWidth / 2f + layerX, _cameraTarget.Y + ViewHeight / 2f - layerY, 0f);
        _probe.Add(label, world, ExpectedColor);
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
        // The camera is already framed on the window centre by CreateCamera.
    }

    public override void Update(GameTime gameTime)
    {
        // The layer component submits from its own Update, with the frame the game pushes (here, the demo).
        _game?.ScrollingLayerComponent.Service.SetFrame(0, 0, 0, _cameraTarget);
    }

    public override void PostDraw(CasaEngineGame game, GameTime gameTime)
    {
        _probe.OnPostDraw(game);
    }

    public override void Clean()
    {
        if (_game != null)
        {
            _game.ScrollingLayerComponent.Service.Clear();
            _game = null;
        }

        foreach (var hold in _holds)
        {
            hold.Dispose();
        }

        _holds.Clear();
    }
}
