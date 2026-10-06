using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Demos.Demos.PsxSemiTransparency;

/// <summary>
/// E19.g G2b-1 (ADR-0068): free PS1 quads, drawn by <c>SpriteRendererComponent.DrawPsxQuad</c> on a 16 x 16 address texture (one
/// colour per texel): a texture scaled four times, a mirrored quad scaled 1.5 times, a parallelogram, a trapezoid (its two
/// diagonals differ), one quad per PSX mode (1 and 0), two mirrored 1:1 quads, and three 1:1 rows (a) by the quad and (b) by the
/// rectangle path of the sprite renderer, which must read the same pixels. The camera maps one PS1 pixel to <c>k</c> screen
/// pixels (<c>CASAENGINE_PSXQUAD_ZOOM</c>, 1 or 3, the integer factor of ADR-0048) with the PS1 origin on the top-left pixel of
/// the back-buffer. The scene dumps its back-buffer in process (<see cref="GraphicsDevice.GetBackBufferData{T}(T[])"/>, never a
/// capture of the desktop) to the file named by <c>CASAENGINE_PSXQUAD_DUMP_PATH</c> (int32 width, height, k, then RGBA bytes),
/// which <c>docs/plan-e19-g2b-annexe/g2b1_compare.py</c> of the parent repository compares with the prediction
/// <c>g2b1_predictions.json</c>. Run it with <c>CASAENGINE_START_DEMO="PSX free quads"</c> from the <c>CasaEngine.Demos</c> folder.
/// </summary>
public class PsxFreeQuadDemo : Demo
{
    private const int TextureSize = 16;
    private const int FramesBeforeDump = 20;
    private const string ZoomVariable = "CASAENGINE_PSXQUAD_ZOOM";
    private const string DumpPathVariable = "CASAENGINE_PSXQUAD_DUMP_PATH";

    private static readonly Color Background = new(100, 150, 200, 255);
    private static readonly Guid TextureId = Guid.Parse("a2000000-0000-0000-0000-000000000001");

    private Texture2D? _texture;
    private int _zoom = 1;
    private int _frames;
    private bool _dumped;
    private readonly BackBufferProbe _probe = new("PSX free quads");

    public override string Title => "PSX free quads";

    public override string Description =>
        "Free PS1 quads on an address texture: scaled, mirrored, sheared and trapezoid quads, one per PSX mode, and 1:1 quads that " +
        "must equal the rectangle path. The scene dumps its own back-buffer for the comparison with the prediction.";

    public override void Initialize(CasaEngineGame game)
    {
        _zoom = int.TryParse(Environment.GetEnvironmentVariable(ZoomVariable), out var zoom) && zoom >= 1 ? zoom : 1;

        _texture = new Texture2D(game.GraphicsDevice, TextureSize, TextureSize, false, SurfaceFormat.Color);
        var texels = new Color[TextureSize * TextureSize];
        for (var j = 0; j < TextureSize; j++)
        {
            for (var i = 0; i < TextureSize; i++)
            {
                texels[j * TextureSize + i] = TexelColor(i, j);
            }
        }

        _texture.SetData(texels);

        var entity = new Entity { Name = "PsxFreeQuads" };
        entity.RootComponent = new PsxFreeQuadComponent(_texture);
        game.GameManager.CurrentWorld.AddEntity(entity);

        _probe.Add("background", PixelCentre(2, 2), Background);
        _probe.Add("magnify4 texel (0, 0)", PixelCentre(9, 9), TexelColor(0, 0));
        _probe.Add("row plain (a) texel (2, 3)", PixelCentre(8, 88), TexelColor(2, 3));
    }

    /// <summary>The world position of the centre of the PS1 pixel (x, y), whatever the factor (PS1 y is down, world y is up).</summary>
    private Vector3 PixelCentre(int x, int y)
    {
        return new Vector3(x + 0.5f / _zoom, -(y + 0.5f / _zoom), 0f);
    }

    /// <summary>The texel (i, j): a unique opaque colour (R and G carry the coordinates), STP (alpha 128) where (i + j) % 4 == 1.</summary>
    private static Color TexelColor(int i, int j)
    {
        if (i == 15 && j == 15)
        {
            return Background;
        }

        var alpha = (i + j) % 4 == 1 ? 128 : 255;
        return new Color(20 + 14 * i, 20 + 14 * j, 60 + (i * 7 + j * 3) % 11 * 10, alpha);
    }

    public override CameraComponent CreateCamera(CasaEngineGame game)
    {
        var entity = new Entity();
        var camera = new Camera2dComponent();
        var width = game.GraphicsDevice.PresentationParameters.BackBufferWidth;
        var height = game.GraphicsDevice.PresentationParameters.BackBufferHeight;
        camera.Zoom = _zoom;
        // The PS1 origin on the top-left pixel of the back-buffer: a PS1 point (x, y) is the screen point (k x, k y).
        camera.Target = new Vector3(width / (2f * _zoom), -height / (2f * _zoom), 0f);
        entity.AddComponent(camera);
        entity.Initialize();
        game.GameManager.CurrentWorld.AddEntity(entity);

        return camera;
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        // The camera is framed by CreateCamera.
    }

    public override void Update(GameTime gameTime)
    {
    }

    public override void PostDraw(CasaEngineGame game, GameTime gameTime)
    {
        _probe.OnPostDraw(game);

        if (_dumped || ++_frames < FramesBeforeDump)
        {
            return;
        }

        _dumped = true;
        var path = Environment.GetEnvironmentVariable(DumpPathVariable);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var device = game.GraphicsDevice;
        var width = device.PresentationParameters.BackBufferWidth;
        var height = device.PresentationParameters.BackBufferHeight;
        var data = new byte[width * height * 4];
        device.GetBackBufferData(data);

        using var stream = File.Create(Path.GetFullPath(path));
        using var writer = new BinaryWriter(stream);
        writer.Write(width);
        writer.Write(height);
        writer.Write(_zoom);
        writer.Write(data);
    }

    public override void Clean()
    {
        _texture?.Dispose();
        _texture = null;
    }

    /// <summary>Queues the quads each frame, straight on the sprite renderer.</summary>
    private sealed class PsxFreeQuadComponent : SceneComponent
    {
        // PS1 quads: the four vertices in data order (top-left, top-right, bottom-left, bottom-right), PS1 y down, and the raw
        // window (Sx, Sy, w, h) of the data. The rectangle path rows (b) take the window of the extractor (SourceX/SourceY).
        private readonly record struct Quad(
            (int X, int Y) Tl, (int X, int Y) Tr, (int X, int Y) Bl, (int X, int Y) Br, Rectangle Raw, SpritePsxSemiTransparency Mode);

        private readonly record struct RectRow(int Left, int Top, Rectangle Source, SpriteEffects Effects);

        private static readonly Quad[] Quads =
        {
            new((8, 8), (40, 8), (8, 40), (40, 40), new Rectangle(0, 0, 8, 8), SpritePsxSemiTransparency.None),
            new((62, 8), (50, 8), (62, 20), (50, 20), new Rectangle(0, 0, 8, 8), SpritePsxSemiTransparency.None),
            new((70, 8), (94, 8), (76, 32), (100, 32), new Rectangle(0, 0, 8, 8), SpritePsxSemiTransparency.None),
            new((112, 8), (132, 8), (108, 32), (140, 32), new Rectangle(0, 0, 8, 8), SpritePsxSemiTransparency.None),
            new((8, 56), (20, 56), (10, 68), (22, 68), new Rectangle(0, 0, 8, 8), SpritePsxSemiTransparency.Mode1),
            new((40, 56), (52, 56), (42, 68), (54, 68), new Rectangle(0, 0, 8, 8), SpritePsxSemiTransparency.Mode0),
            new((88, 56), (80, 56), (88, 64), (80, 64), new Rectangle(0, 0, 8, 8), SpritePsxSemiTransparency.None),
            new((104, 64), (112, 64), (104, 56), (112, 56), new Rectangle(0, 0, 8, 8), SpritePsxSemiTransparency.None),
            // 1:1 rows, (a): by the quad.
            new((8, 88), (16, 88), (8, 96), (16, 96), new Rectangle(2, 3, 8, 8), SpritePsxSemiTransparency.None),
            new((48, 88), (40, 88), (48, 96), (40, 96), new Rectangle(2, 3, 8, 8), SpritePsxSemiTransparency.None),
            new((72, 96), (80, 96), (72, 88), (80, 88), new Rectangle(2, 3, 8, 8), SpritePsxSemiTransparency.None),
        };

        // 1:1 rows, (b): by the rectangle path, the window of the extractor (a mirrored axis names its window one texel on).
        private static readonly RectRow[] RectRows =
        {
            new(24, 88, new Rectangle(2, 3, 8, 8), SpriteEffects.None),
            new(56, 88, new Rectangle(3, 3, 8, 8), SpriteEffects.FlipHorizontally),
            new(88, 88, new Rectangle(2, 4, 8, 8), SpriteEffects.FlipVertically),
        };

        private readonly Texture2D _texture;
        private SpriteRendererComponent? _renderer;

        public PsxFreeQuadComponent(Texture2D texture)
        {
            _texture = texture;
        }

        private PsxFreeQuadComponent(PsxFreeQuadComponent other) : base(other)
        {
            _texture = other._texture;
        }

        public override PsxFreeQuadComponent Clone() => new(this);

        public override void InitializeWithWorld(World world)
        {
            base.InitializeWithWorld(world);
            _renderer = world.Game.GetGameComponent<SpriteRendererComponent>();
        }

        public override void Draw(float elapsedTime)
        {
            if (_renderer == null)
            {
                return;
            }

            var scissor = _renderer.GraphicsDevice.ScissorRectangle;

            // Sort key order 0: the background (texel (15, 15)), behind every quad.
            var key = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 0, 0, 0, 0, 0, 0);
            _renderer.DrawSprite(_texture, new Rectangle(15, 15, 1, 1), Point.Zero, new Vector2(-1500f, 2500f), 0f,
                new Vector2(4000f, 4000f), Color.White, 0f, in key, SpriteEffects.None, scissor);

            key = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 0, 1, 0, 0, 0, 0);
            foreach (var quad in Quads)
            {
                _renderer.DrawPsxQuad(_texture, quad.Raw, World(quad.Tl), World(quad.Tr), World(quad.Bl), World(quad.Br),
                    Color.White, 0f, in key, quad.Mode, scissor);
            }

            foreach (var row in RectRows)
            {
                _renderer.DrawSprite(_texture, row.Source, Point.Zero, new Vector2(row.Left, -row.Top), 0f, Vector2.One,
                    Color.White, 0f, in key, row.Effects, scissor);
            }
        }

        private static Vector2 World((int X, int Y) ps1)
        {
            return new Vector2(ps1.X, -ps1.Y);
        }
    }
}
