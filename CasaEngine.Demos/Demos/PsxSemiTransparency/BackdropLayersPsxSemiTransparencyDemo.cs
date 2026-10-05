using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Rendering.CellularLayers;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScrollingLayers;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using EngineTexture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Demos.Demos.PsxSemiTransparency;

/// <summary>
/// E19.g G2c (ADR-0051 extended to the background layers): the scrolling layers and the cellular layers draw their STP
/// texels (alpha 128) blended with the mode of the layer and their opaque texels (alpha 255) opaque, per texel. The sheets
/// are PNG files (<c>Content/PsxBackdropLayers</c>) read by the engine's own <see cref="Texture2DLoader"/>, so the demo also
/// proves that a PNG with an alpha of 128 keeps it. A plain background layer is drawn first; the other layers carry one
/// square each, on an otherwise transparent sheet. The scene reads its own back-buffer in process
/// (<see cref="BackBufferProbe"/>) and compares it with the PSX formulas on 8-bit colours, to within one level per channel:
/// run it with <c>CASAENGINE_START_DEMO="Background layers PSX semi-transparency"</c> from the <c>CasaEngine.Demos</c>
/// folder, with <c>CASAENGINE_CAPTURE_SCREENSHOT_PATH</c> to also save the image and <c>CASAENGINE_DEMO_PIXELS_PATH</c> to
/// save the readout.
/// </summary>
public class BackdropLayersPsxSemiTransparencyDemo : Demo
{
    private static readonly Color Background = new(100, 150, 200, 255);

    private const int ViewWidth = 320;
    private const int ViewHeight = 240;

    private static readonly Guid BackgroundSheetId = Guid.Parse("a2000000-0000-0000-0000-000000000001");
    private static readonly Guid Mode0SheetId = Guid.Parse("a2000000-0000-0000-0000-000000000002");
    private static readonly Guid Mode1SheetId = Guid.Parse("a2000000-0000-0000-0000-000000000003");
    private static readonly Guid NoneSheetId = Guid.Parse("a2000000-0000-0000-0000-000000000004");
    private static readonly Guid CellSheetId = Guid.Parse("a2000000-0000-0000-0000-000000000005");

    private readonly BackBufferProbe _probe = new("Background layers PSX semi-transparency");
    private readonly List<IDisposable> _holds = new();
    private CasaEngineGame? _game;
    private Vector3 _cameraTarget;

    public override string Title => "Background layers PSX semi-transparency";

    public override string Description =>
        "Scrolling and cellular background layers whose sheets are PNG files with an alpha of 255 (opaque texel) or 128 (STP texel): " +
        "per texel, the opaque texels stay opaque and the STP texels blend with the mode of the layer (0 average, 1 additive), " +
        "or stay opaque on a layer without a mode. The scene checks its own back-buffer against the PSX formulas.";

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        _cameraTarget = new Vector3(game.Window.ClientBounds.Size.X / 2f, game.Window.ClientBounds.Size.Y / 2f, 0f);

        RegisterSheet(game, BackgroundSheetId, "background.png");
        RegisterSheet(game, Mode0SheetId, "mode0.png");
        RegisterSheet(game, Mode1SheetId, "mode1.png");
        RegisterSheet(game, NoneSheetId, "none.png");
        RegisterSheet(game, CellSheetId, "cellsheet.png");

        game.ScrollingLayerComponent.Service.SetConfiguration(new ScrollingLayerConfiguration(ViewWidth, ViewHeight, ViewWidth, ViewHeight));
        game.ScrollingLayerComponent.Service.SetLayers(new[]
        {
            MakeScrollingLayer(BackgroundSheetId, SpritePsxSemiTransparency.None, orderInLayer: 0),
            MakeScrollingLayer(Mode0SheetId, SpritePsxSemiTransparency.Mode0, orderInLayer: 1),
            MakeScrollingLayer(Mode1SheetId, SpritePsxSemiTransparency.Mode1, orderInLayer: 2),
            MakeScrollingLayer(NoneSheetId, SpritePsxSemiTransparency.None, orderInLayer: 3)
        });

        game.CellularLayerComponent.Service.SetLayers(new[]
        {
            new CellularLayerDefinition
            {
                LayerId = 1,
                AnimTimer = 100,
                AnimNum = 1,
                Ground = true,
                Blend = SpriteBlendMode.Additive, // ignored: the mode decides
                Tint = Color.White,
                PsxSemiTransparency = SpritePsxSemiTransparency.Mode0,
                SheetTextureAssetIds = new[] { CellSheetId },
                Cells = new[]
                {
                    new CellularCellDefinition { Type = CellularCellType.Normal, X0 = 200, Y0 = 40, U0 = 0, U1 = 15, V0 = 0, V1 = 15 }
                }
            }
        });

        // Layer pixel (px, py) is the world position (target.X - 160 + px, target.Y + 120 - py), the world Y axis pointing up.
        AddCheck("mode0 layer, opaque texel (96, 96, 88, a255)", 48, 48, new Color(96, 96, 88));
        AddCheck("mode0 layer, STP texel (24, 32, 24, a128): average with the background", 88, 48, new Color(62, 91, 112));
        AddCheck("mode1 layer, STP texel (96, 96, 96, a128): additive", 48, 108, new Color(196, 246, 255));
        AddCheck("layer without a mode, texel (120, 80, 40, a128): drawn opaque", 48, 168, new Color(120, 80, 40));
        AddCheck("mode0 cellular layer, STP texel (24, 32, 24, a128): average with the background", 208, 48, new Color(62, 91, 112));
        AddCheck("background beside the squares", 150, 150, Background);
    }

    private void AddCheck(string label, float layerX, float layerY, Color expected)
    {
        var world = new Vector3(_cameraTarget.X - ViewWidth / 2f + layerX, _cameraTarget.Y + ViewHeight / 2f - layerY, 0f);
        _probe.Add(label, world, expected);
    }

    private static ScrollingLayerDefinition MakeScrollingLayer(Guid sheetId, SpritePsxSemiTransparency mode, int orderInLayer)
    {
        return new ScrollingLayerDefinition
        {
            FrameTextureAssetIds = new[] { sheetId },
            FactorXNum = 0,
            FactorXDenom = 1,
            FactorYNum = 0,
            FactorYDenom = 1,
            AnimTimer = 100,
            Pass = RenderPass2D.Background,
            OrderInLayer = orderInLayer,
            StableId = orderInLayer,
            Blend = SpriteBlendMode.Opaque,
            Tint = Color.White,
            PsxSemiTransparency = mode
        };
    }

    /// <summary>Loads a PNG of the demo with the engine's texture loader and registers it as a texture asset.</summary>
    private void RegisterSheet(CasaEngineGame game, Guid sheetId, string fileName)
    {
        var assets = game.AssetContentManager;
        var path = Path.Combine(AppContext.BaseDirectory, "Content", "PsxBackdropLayers", fileName);
        var texture2d = (Texture2D)new Texture2DLoader().LoadAsset(path, assets);

        var texture2dId = Guid.NewGuid();
        _holds.Add(assets.Register(texture2dId, texture2d));
        _holds.Add(assets.Register(sheetId, new EngineTexture(texture2dId, game.GraphicsDevice)));
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
        // The layer components submit from their own Update, with the frame the game pushes (here, the demo).
        if (_game != null)
        {
            _game.ScrollingLayerComponent.Service.SetFrame(0, 0, 0, _cameraTarget);
            _game.CellularLayerComponent.Service.SetFrame(0, 0, 1, _cameraTarget);
        }
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
            _game.CellularLayerComponent.Service.Clear();
            _game = null;
        }

        foreach (var hold in _holds)
        {
            hold.Dispose();
        }

        _holds.Clear();
    }
}
