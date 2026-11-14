using System;
using System.Collections.Generic;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering.CellularLayers;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScrollingLayers;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using EngineTexture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// The two kinds of 2D background layers, in one scene:
/// <list type="bullet">
///   <item>scrolling layers (<see cref="ScrollingLayerService"/>): one full picture per layer, repeated to cover the
///   view. A layer follows the camera by a fraction (parallax) and may also scroll by itself. Here: a sky, clouds
///   and two rows of hills;</item>
///   <item>cellular layers (<see cref="CellularLayerService"/>): many small rectangles cut from a sheet, each with its
///   own drift. Here: snow flakes that fall and come back at the top.</item>
/// </list>
/// Both work the same way: the game describes the layers once (<c>SetLayers</c>), then pushes each frame where the
/// camera is and how many logic ticks went by (<c>SetFrame</c>). The engine components advance the layers and draw
/// them; nothing is drawn while the game pushes no frame. Positions and speeds are whole pixels per tick: these
/// layers were made for pixel-exact 2D games.
/// </summary>
public class Layers2dDemo : Demo
{
    // The size of the picture the layers are made for. The camera zooms it to the window.
    private const int ViewWidth = 320;
    private const int ViewHeight = 240;

    private const int SnowLayerId = 1;
    private const int FlakeCount = 40;

    // The demo gives its textures an id: a layer names its pictures by asset id.
    private static readonly Guid SkyId = Guid.Parse("b3000000-0000-0000-0000-000000000001");
    private static readonly Guid CloudsId = Guid.Parse("b3000000-0000-0000-0000-000000000002");
    private static readonly Guid FarHillsId = Guid.Parse("b3000000-0000-0000-0000-000000000003");
    private static readonly Guid NearHillsId = Guid.Parse("b3000000-0000-0000-0000-000000000004");
    private static readonly Guid SnowSheetId = Guid.Parse("b3000000-0000-0000-0000-000000000005");

    private readonly List<IDisposable> _registeredTextures = new();
    private CasaEngineGame? _game;
    private Vector3 _cameraTarget;
    private int _scrollX;
    private bool _travel = true;
    private bool _snow = true;

    // The cellular layers ask the game for random numbers (where a flake comes back). The delegate is created once.
    private readonly Func<uint> _nextRandom;
    private Func<uint>? _previousRandomSource;
    private uint _randomState = 12345u;

    public Layers2dDemo()
    {
        _nextRandom = NextRandom;
    }

    private uint NextRandom()
    {
        // A small fixed-seed generator: the same snow at every run.
        _randomState = _randomState * 1103515245u + 12345u;
        return (_randomState >> 16) & 0x7FFFu;
    }

    public override string Title => "2D layers demo";

    public override string Description =>
        "A 2D landscape made of background layers. The camera travels to the right: the sky does not move, the far " +
        "hills move slowly and the near hills faster, which is parallax. The clouds also drift by themselves, and " +
        "the snow is a second kind of layer made of small independent cells.";

    public override IReadOnlyList<DemoControl> Controls { get; } = new DemoControl[]
    {
        DemoControl.ForActions("move the camera along the landscape", DemoInput.MoveLeft, DemoInput.MoveRight),
        DemoControl.ForActions("start or stop the automatic travel", "Layers2d.Travel"),
        DemoControl.ForActions("show or hide the snow layer", "Layers2d.Snow"),
    };

    public override IReadOnlyList<string> Highlights { get; } = new[]
    {
        "Each scrolling layer is one 320 x 240 picture, repeated: watch a hill leave on one side and come back.",
        "Stop the travel: the clouds still move (auto-scroll), the hills do not (parallax only).",
        "A hidden layer is frozen: the flakes come back where they were.",
    };

    public override string DocumentationPath => "docs/engine/scrolling-layers.md";

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        _cameraTarget = new Vector3(game.Window.ClientBounds.Size.X / 2f, game.Window.ClientBounds.Size.Y / 2f, 0f);

        RegisterTexture(game, SkyId, CreateSky(game.GraphicsDevice));
        RegisterTexture(game, CloudsId, CreateClouds(game.GraphicsDevice));
        RegisterTexture(game, FarHillsId, CreateHills(game.GraphicsDevice, 110, 26f, 3f, new Color(70, 90, 140)));
        RegisterTexture(game, NearHillsId, CreateHills(game.GraphicsDevice, 170, 34f, 2f, new Color(40, 70, 60)));
        RegisterTexture(game, SnowSheetId, CreateSnowSheet(game.GraphicsDevice));

        // Scrolling layers. The canvas is the size of one layer picture, the view the size of what is shown.
        ScrollingLayerService scrolling = game.ScrollingLayerComponent.Service;
        scrolling.SetConfiguration(new ScrollingLayerConfiguration(ViewWidth, ViewHeight, ViewWidth, ViewHeight));
        scrolling.SetLayers(new[]
        {
            // The parallax factor is a fraction of the camera scroll: 0 = fixed, 1/4 = far, 1/1 = with the scene.
            MakeScrollingLayer(SkyId, order: 0, parallaxNum: 0, parallaxDenom: 1, SpriteBlendMode.Opaque),
            // The clouds: a little parallax, plus one pixel by themselves every 4 ticks.
            MakeScrollingLayer(CloudsId, order: 1, parallaxNum: 1, parallaxDenom: 8, SpriteBlendMode.AlphaBlend, autoScrollPeriod: 4),
            MakeScrollingLayer(FarHillsId, order: 2, parallaxNum: 1, parallaxDenom: 4, SpriteBlendMode.AlphaBlend),
            MakeScrollingLayer(NearHillsId, order: 3, parallaxNum: 1, parallaxDenom: 1, SpriteBlendMode.AlphaBlend),
        });

        // The random source of the cellular layers is the game's to give; the one that was there is put back in Clean.
        _previousRandomSource = game.CellularLayerComponent.RandomSource;
        game.CellularLayerComponent.RandomSource = _nextRandom;

        // One cellular layer: the snow. Each cell is a rectangle of the sheet with its own start and drift.
        game.CellularLayerComponent.Service.SetLayers(new[]
        {
            new CellularLayerDefinition
            {
                LayerId = SnowLayerId,
                AnimTimer = 100,
                AnimNum = 1,
                // Ground = true puts the layer in front of the scene (effects pass), false behind it.
                Ground = true,
                Blend = SpriteBlendMode.AlphaBlend,
                Tint = Color.White,
                SheetTextureAssetIds = new[] { SnowSheetId },
                Cells = CreateFlakes(),
            },
        });
    }

    private static ScrollingLayerDefinition MakeScrollingLayer(
        Guid textureId, int order, int parallaxNum, int parallaxDenom, SpriteBlendMode blend, int autoScrollPeriod = 0)
    {
        return new ScrollingLayerDefinition
        {
            // One id per animation frame of the layer; these layers have one frame.
            FrameTextureAssetIds = new[] { textureId },
            FactorXNum = parallaxNum,
            FactorXDenom = parallaxDenom,
            FactorYNum = 0,
            FactorYDenom = 1,
            ScrollXSpeed = 0,
            ScrollXPeriod = autoScrollPeriod,
            AnimTimer = 100,
            Pass = RenderPass2D.Background,
            // Layers of the same pass are drawn in this order: the sky first, the near hills last.
            OrderInLayer = order,
            StableId = order,
            Blend = blend,
            Tint = Color.White,
        };
    }

    /// <summary>
    /// Flakes spread over the 320 x 240 screen of the cellular layers. FallRespawn: the cell drifts, and when it leaves
    /// by the bottom it comes back at the top at a random X.
    /// </summary>
    private static CellularCellDefinition[] CreateFlakes()
    {
        var cells = new CellularCellDefinition[FlakeCount];
        for (int i = 0; i < FlakeCount; i++)
        {
            bool big = i % 3 == 0;
            cells[i] = new CellularCellDefinition
            {
                Type = CellularCellType.FallRespawn,
                // The rectangle of the sheet, both ends included: a 3 x 3 flake or a 2 x 2 one.
                U0 = big ? 0 : 8,
                V0 = 0,
                U1 = big ? 2 : 9,
                V1 = big ? 2 : 1,
                // A fixed spread: multiples of two primes give positions that look random and never change.
                X0 = i * 53 % ViewWidth,
                Y0 = i * 37 % ViewHeight,
                // Pixels per tick. The big flakes fall faster, so they look closer.
                DY = big ? 2 : 1,
            };
        }

        return cells;
    }

    /// <summary>
    /// A layer names its pictures by asset id, so a texture made in code is registered in the asset manager under an id.
    /// A game would name textures of its content instead.
    /// </summary>
    private void RegisterTexture(CasaEngineGame game, Guid id, Texture2D texture2d)
    {
        var assets = game.AssetContentManager;
        var texture2dId = Guid.NewGuid();
        _registeredTextures.Add(assets.Register(texture2dId, texture2d));
        _registeredTextures.Add(assets.Register(id, new EngineTexture(texture2dId, game.GraphicsDevice)));
    }

    public override CameraComponent CreateCamera(CasaEngineGame game)
    {
        // A 2D camera on the centre of the window, zoomed so that the 320 x 240 picture fills the height.
        var entity = new Entity { Name = "2D camera" };
        var camera = new Camera2dComponent();
        camera.Target = new Vector3(game.Window.ClientBounds.Size.X / 2f, game.Window.ClientBounds.Size.Y / 2f, 0f);
        camera.Zoom = game.Window.ClientBounds.Size.Y / (float)ViewHeight;
        entity.AddComponent(camera);
        entity.Initialize();
        game.GameManager.CurrentWorld.AddEntity(entity);
        return camera;
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        // Framed by CreateCamera.
    }

    public override void Update(GameTime gameTime)
    {
        if (_game == null)
        {
            return;
        }

        if (DemoInput.WasPressed(_game, "Layers2d.Travel"))
        {
            _travel = !_travel;
        }

        if (DemoInput.WasPressed(_game, "Layers2d.Snow"))
        {
            _snow = !_snow;
            // Layers are switched by the id the game gave them.
            _game.CellularLayerComponent.Service.SetLayerActive(SnowLayerId, _snow);
        }

        // The camera scroll, in pixels of the layer picture. In a game it comes from the camera that follows the player.
        if (DemoInput.IsDown(_game, DemoInput.MoveRight))
        {
            _scrollX += 3;
        }
        else if (DemoInput.IsDown(_game, DemoInput.MoveLeft))
        {
            _scrollX -= 3;
        }
        else if (_travel)
        {
            _scrollX += 1;
        }

        // The push of the frame: the scroll, the number of logic ticks since the last push (one per update here), and
        // where the centre of the view is in the world. The layer components do the rest in their own update.
        _game.ScrollingLayerComponent.Service.SetFrame(_scrollX, 0, 1, _cameraTarget);
        _game.CellularLayerComponent.Service.SetFrame(0, 0, 1, _cameraTarget);
    }

    public override void Clean()
    {
        if (_game != null)
        {
            // The services belong to the game, not to the world: a demo that sets layers removes them.
            _game.ScrollingLayerComponent.Service.Clear();
            _game.CellularLayerComponent.Service.Clear();
            if (_previousRandomSource != null)
            {
                _game.CellularLayerComponent.RandomSource = _previousRandomSource;
                _previousRandomSource = null;
            }

            _game = null;
        }

        for (int i = 0; i < _registeredTextures.Count; i++)
        {
            _registeredTextures[i].Dispose();
        }

        _registeredTextures.Clear();
        _scrollX = 0;
        _travel = true;
        _snow = true;
        _randomState = 12345u;
    }

    // ---- The pictures, drawn in code so that the demo needs no file. ----

    private static Texture2D CreateSky(GraphicsDevice graphicsDevice)
    {
        var pixels = new Color[ViewWidth * ViewHeight];
        var top = new Color(20, 24, 70);
        var bottom = new Color(150, 170, 220);
        for (int y = 0; y < ViewHeight; y++)
        {
            Color row = Color.Lerp(top, bottom, y / (float)(ViewHeight - 1));
            for (int x = 0; x < ViewWidth; x++)
            {
                pixels[y * ViewWidth + x] = row;
            }
        }

        // A few stars in the upper part.
        for (int i = 0; i < 40; i++)
        {
            int x = i * 97 % ViewWidth;
            int y = i * 41 % 100;
            pixels[y * ViewWidth + x] = Color.White;
        }

        return CreateTexture(graphicsDevice, ViewWidth, ViewHeight, pixels);
    }

    private static Texture2D CreateClouds(GraphicsDevice graphicsDevice)
    {
        var pixels = new Color[ViewWidth * ViewHeight];
        var cloud = new Color(235, 240, 250);
        AddEllipse(pixels, 60, 50, 34, 9, cloud);
        AddEllipse(pixels, 85, 44, 22, 8, cloud);
        AddEllipse(pixels, 210, 75, 40, 10, cloud);
        AddEllipse(pixels, 240, 68, 20, 8, cloud);
        return CreateTexture(graphicsDevice, ViewWidth, ViewHeight, pixels);
    }

    private static void AddEllipse(Color[] pixels, int centreX, int centreY, int radiusX, int radiusY, Color color)
    {
        for (int y = centreY - radiusY; y <= centreY + radiusY; y++)
        {
            for (int x = centreX - radiusX; x <= centreX + radiusX; x++)
            {
                float dx = (x - centreX) / (float)radiusX;
                float dy = (y - centreY) / (float)radiusY;
                if (dx * dx + dy * dy <= 1f)
                {
                    pixels[y * ViewWidth + x] = color;
                }
            }
        }
    }

    /// <summary>A row of hills: transparent above the ridge, one colour below. The ridge closes on itself over the width.</summary>
    private static Texture2D CreateHills(GraphicsDevice graphicsDevice, int ridgeY, float amplitude, float waves, Color color)
    {
        var pixels = new Color[ViewWidth * ViewHeight];
        for (int x = 0; x < ViewWidth; x++)
        {
            float angle = x / (float)ViewWidth * MathHelper.TwoPi;
            float height = MathF.Sin(angle * waves) * amplitude + MathF.Sin(angle * (waves + 2f)) * amplitude * 0.35f;
            int top = ridgeY - (int)height;
            for (int y = Math.Max(top, 0); y < ViewHeight; y++)
            {
                pixels[y * ViewWidth + x] = color;
            }
        }

        return CreateTexture(graphicsDevice, ViewWidth, ViewHeight, pixels);
    }

    /// <summary>The sheet of the snow layer: 256 x 256, a 3 x 3 flake at (0, 0) and a 2 x 2 one at (8, 0).</summary>
    private static Texture2D CreateSnowSheet(GraphicsDevice graphicsDevice)
    {
        const int size = 256;
        var pixels = new Color[size * size];
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                pixels[y * size + x] = Color.White;
            }
        }

        for (int y = 0; y < 2; y++)
        {
            for (int x = 8; x < 10; x++)
            {
                pixels[y * size + x] = new Color(210, 220, 240);
            }
        }

        return CreateTexture(graphicsDevice, size, size, pixels);
    }

    private static Texture2D CreateTexture(GraphicsDevice graphicsDevice, int width, int height, Color[] pixels)
    {
        var texture = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
        texture.SetData(pixels);
        return texture;
    }
}
