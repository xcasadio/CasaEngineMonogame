using System;
using System.IO;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.EffectQuads;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Demos.Demos.PsxSemiTransparency;

/// <summary>
/// E19.g G2e (ADR-0071): the effect quad service and component. A 512 x 1105 synthetic address texture (the size of the real effect
/// sheets, one colour per texel, STP texels and holes) is given to <c>EffectQuadComponent</c> through its <c>ResolveTextures</c> seam
/// and a frame of quads is pushed to <c>EffectQuadService</c> exactly as the game DLL will: a raw window (a mirrored axis one texel
/// back, so an origin of -1), the four world corners in the order of the data, the mode and the sort key. Four scenes
/// (<c>CASAENGINE_EFFECTQUAD_SCENE</c>): <c>a_aura_real</c> (the nine images of the aura of map 476), <c>a_aura_mix</c> (the same
/// with mirrors and the three modes), <c>b_mirror_clamp</c> (windows at the border of the texture) and <c>c_ties_tall</c> (exact
/// coverage ties near the bottom of the texture). The camera maps one PS1 pixel to <c>k</c> screen pixels
/// (<c>CASAENGINE_EFFECTQUAD_ZOOM</c>, 1 or 3) with the PS1 origin on the top-left pixel of the back-buffer. The scene dumps its
/// back-buffer in process (<see cref="GraphicsDevice.GetBackBufferData{T}(T[])"/>, never a capture of the desktop) to the file named
/// by <c>CASAENGINE_EFFECTQUAD_DUMP_PATH</c> (int32 width, height, k, then RGBA bytes), which
/// <c>docs/plan-e19-g3c-annexe/g2e_compare.py</c> of the parent repository compares with its prediction
/// (<c>g2e_expected.json</c>); the scene data below is that file's, unchanged. Run it with
/// <c>CASAENGINE_START_DEMO="Effect quads"</c> from the <c>CasaEngine.Demos</c> folder.
/// </summary>
public class EffectQuadDemo : Demo
{
    private const int TextureWidth = 512;
    private const int TextureHeight = 1105;
    private const int FramesBeforeDump = 20;
    private const string SceneVariable = "CASAENGINE_EFFECTQUAD_SCENE";
    private const string ZoomVariable = "CASAENGINE_EFFECTQUAD_ZOOM";
    private const string DumpPathVariable = "CASAENGINE_EFFECTQUAD_DUMP_PATH";
    private const string DefaultScene = "a_aura_real";

    private static readonly Color Background = new(96, 112, 128, 255);
    private static readonly Guid TextureId = Guid.Parse("a2000000-0000-0000-0000-000000000002");

    private Texture2D? _texture;
    private EffectQuadService? _service;
    private int _zoom = 1;
    private int _frames;
    private bool _dumped;

    public override string Title => "Effect quads";

    public override string Description =>
        "Effect quads of the game pushed to EffectQuadService and drawn by EffectQuadComponent on a synthetic address texture: " +
        "the aura of map 476, mirrored windows at the border of the texture, exact coverage ties. The scene dumps its own " +
        "back-buffer for the comparison with the prediction.";

    public override void Initialize(CasaEngineGame game)
    {
        _zoom = int.TryParse(Environment.GetEnvironmentVariable(ZoomVariable), out var zoom) && zoom >= 1 ? zoom : 1;

        var sceneName = Environment.GetEnvironmentVariable(SceneVariable);
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            sceneName = DefaultScene;
        }

        if (!Scenes.TryGetValue(sceneName, out var quads))
        {
            throw new InvalidOperationException(
                $"{SceneVariable} '{sceneName}' is not a scene of the effect quad demo ({string.Join(", ", Scenes.Keys)}).");
        }

        _texture = new Texture2D(game.GraphicsDevice, TextureWidth, TextureHeight, false, SurfaceFormat.Color);
        var texels = new Color[TextureWidth * TextureHeight];
        for (var j = 0; j < TextureHeight; j++)
        {
            for (var i = 0; i < TextureWidth; i++)
            {
                texels[j * TextureWidth + i] = TexelColor(i, j);
            }
        }

        _texture.SetData(texels);

        // The way the game DLL feeds the engine: the sheets once, then the frame. The seam stands in for the asset catalog.
        var component = game.EffectQuadComponent;
        _service = component.Service;
        _service.SetSheets(new[] { TextureId });
        component.ResolveTextures(_ => _texture);
        _service.SetFrame(quads);

        var entity = new Entity { Name = "EffectQuadBackground" };
        entity.RootComponent = new BackgroundComponent(_texture);
        game.GameManager.CurrentWorld.AddEntity(entity);
    }

    /// <summary>
    /// The texel (i, j): R = 12 + 3 (i % 64), G = 12 + 3 (j % 64), B = 12 + (i / 64) * 18 + j / 64, so no two texels alike; alpha 0
    /// where (i + 2 j) % 23 == 5, else 128 (STP) where (i + j) % 4 == 1, else 255; the texel (511, 0) is the background.
    /// </summary>
    private static Color TexelColor(int i, int j)
    {
        if (i == 511 && j == 0)
        {
            return Background;
        }

        var alpha = (i + 2 * j) % 23 == 5 ? 0 : (i + j) % 4 == 1 ? 128 : 255;
        return new Color(12 + 3 * (i % 64), 12 + 3 * (j % 64), 12 + (i / 64) * 18 + j / 64, alpha);
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
        _service?.Clear();
        _service = null;
        _texture?.Dispose();
        _texture = null;
    }

    /// <summary>Draws the background, a big sprite of the texel (511, 0) on layer 0, before every quad (which are on layer 300).</summary>
    private sealed class BackgroundComponent : SceneComponent
    {
        private readonly Texture2D _texture;
        private SpriteRendererComponent? _renderer;

        public BackgroundComponent(Texture2D texture)
        {
            _texture = texture;
        }

        private BackgroundComponent(BackgroundComponent other) : base(other)
        {
            _texture = other._texture;
        }

        public override BackgroundComponent Clone() => new(this);

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
            var key = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 0, 0, 0, 0, 0, 0);
            _renderer.DrawSprite(_texture, new Rectangle(511, 0, 1, 1), Point.Zero, new Vector2(-1500f, 2500f), 0f,
                new Vector2(4000f, 4000f), Color.White, 0f, in key, SpriteEffects.None, scissor);
        }
    }

    private static EffectQuad Quad(int sheet, int rawX, int rawY, int rawWidth, int rawHeight, float tlX, float tlY, float trX,
        float trY, float blX, float blY, float brX, float brY, SpritePsxSemiTransparency mode, int pass, int layer, int order,
        int elevation, int coordinate, int localOffset, int stableId)
    {
        return new EffectQuad(sheet, new Rectangle(rawX, rawY, rawWidth, rawHeight), new Vector2(tlX, tlY), new Vector2(trX, trY),
            new Vector2(blX, blY), new Vector2(brX, brY), mode,
            new RenderSortKey2D(pass, layer, order, elevation, coordinate, localOffset, stableId));
    }

    // The four scenes of g2e_expected.json (G2e-0), unchanged: raw window, world corners TL, TR, BL, BR (y up), mode, sort key.
    private static readonly System.Collections.Generic.Dictionary<string, EffectQuad[]> Scenes = new()
    {
        ["a_aura_real"] = new[]
        {
            Quad(0, 245, 97, 24, 72, 95f, -7f, 103f, -4f, 143f, -141f, 151f, -138f, SpritePsxSemiTransparency.Mode1, 300, 300, 0, 214, 11100, 8, 0), // img0
            Quad(0, 245, 97, 24, 72, 170f, -1f, 179f, -2f, 159f, -143f, 168f, -144f, SpritePsxSemiTransparency.Mode1, 300, 300, 0, 214, 11100, 7, 0), // img1
            Quad(0, 0, 410, 64, 24, 128f, -127f, 192f, -127f, 128f, -151f, 192f, -151f, SpritePsxSemiTransparency.None, 300, 300, 0, 214, 11100, 6, 0), // img2
            Quad(0, 405, 0, 64, 79, 128f, -72f, 192f, -72f, 128f, -151f, 192f, -151f, SpritePsxSemiTransparency.Mode1, 300, 300, 0, 214, 11100, 5, 0), // img3
            Quad(0, 0, 362, 47, 47, 137f, -88f, 184f, -88f, 137f, -135f, 184f, -135f, SpritePsxSemiTransparency.Mode1, 300, 300, 0, 214, 11100, 4, 0), // img4
            Quad(0, 270, 97, 24, 72, 121f, -5f, 139f, -2f, 145f, -146f, 163f, -142f, SpritePsxSemiTransparency.Mode1, 300, 300, 0, 214, 11100, 3, 0), // img5
            Quad(0, 360, 242, 64, 55, 128f, -104f, 192f, -104f, 128f, -159f, 192f, -159f, SpritePsxSemiTransparency.Mode1, 300, 300, 0, 214, 11100, 2, 0), // img6
            Quad(0, 144, 242, 71, 63, 108f, -91f, 207f, -91f, 108f, -179f, 207f, -179f, SpritePsxSemiTransparency.Mode1, 300, 300, 0, 214, 11100, 1, 0), // img7
            Quad(0, 0, 306, 64, 55, 103f, -82f, 218f, -82f, 103f, -181f, 218f, -181f, SpritePsxSemiTransparency.Mode1, 300, 300, 0, 214, 11100, 0, 0), // img8
        },
        ["a_aura_mix"] = new[]
        {
            Quad(0, 245, 97, 24, 72, 95f, -7f, 103f, -4f, 143f, -141f, 151f, -138f, SpritePsxSemiTransparency.Mode0, 300, 300, 0, 214, 11100, 8, 0), // img0
            Quad(0, 244, 97, 24, 72, 179f, -2f, 170f, -1f, 168f, -144f, 159f, -143f, SpritePsxSemiTransparency.Mode1, 300, 300, 0, 214, 11100, 7, 0), // img1
            Quad(0, 0, 410, 64, 24, 128f, -127f, 192f, -127f, 128f, -151f, 192f, -151f, SpritePsxSemiTransparency.None, 300, 300, 0, 214, 11100, 6, 0), // img2
            Quad(0, 405, -1, 64, 79, 128f, -151f, 192f, -151f, 128f, -72f, 192f, -72f, SpritePsxSemiTransparency.Mode2, 300, 300, 0, 214, 11100, 5, 0), // img3
            Quad(0, -1, 361, 47, 47, 184f, -135f, 137f, -135f, 184f, -88f, 137f, -88f, SpritePsxSemiTransparency.Mode1, 300, 300, 0, 214, 11100, 4, 0), // img4
            Quad(0, 269, 97, 24, 72, 139f, -2f, 121f, -5f, 163f, -142f, 145f, -146f, SpritePsxSemiTransparency.Mode2, 300, 300, 0, 214, 11100, 3, 0), // img5
            Quad(0, 360, 242, 64, 55, 128f, -104f, 192f, -104f, 128f, -159f, 192f, -159f, SpritePsxSemiTransparency.Mode0, 300, 300, 0, 214, 11100, 2, 0), // img6
            Quad(0, 144, 241, 71, 63, 108f, -179f, 207f, -179f, 108f, -91f, 207f, -91f, SpritePsxSemiTransparency.Mode1, 300, 300, 0, 214, 11100, 1, 0), // img7
            Quad(0, -1, 306, 64, 55, 218f, -82f, 103f, -82f, 218f, -181f, 103f, -181f, SpritePsxSemiTransparency.Mode2, 300, 300, 0, 214, 11100, 0, 0), // img8
        },
        ["b_mirror_clamp"] = new[]
        {
            Quad(0, -1, 40, 8, 8, 40f, -16f, 16f, -16f, 40f, -40f, 16f, -40f, SpritePsxSemiTransparency.None, 300, 300, 0, 214, 0, 0, 1), // b1
            Quad(0, 100, -1, 8, 8, 64f, -48f, 96f, -48f, 64f, -16f, 96f, -16f, SpritePsxSemiTransparency.None, 300, 300, 0, 214, 0, 0, 2), // b2
            Quad(0, -1, -1, 16, 16, 144f, -48f, 112f, -48f, 144f, -16f, 112f, -16f, SpritePsxSemiTransparency.None, 300, 300, 0, 214, 0, 0, 3), // b3
            Quad(0, -1, 200, 8, 8, 188f, -16f, 164f, -16f, 194f, -40f, 170f, -40f, SpritePsxSemiTransparency.Mode1, 300, 300, 0, 214, 0, 0, 4), // b4
            Quad(0, 200, 200, 16, 16, 16f, -72f, 80f, -72f, 16f, -136f, 80f, -136f, SpritePsxSemiTransparency.None, 300, 300, 0, 214, 0, 0, 5), // b5
            Quad(0, -1, 300, 8, 8, 44f, -76f, 20f, -76f, 52f, -100f, 16f, -100f, SpritePsxSemiTransparency.Mode0, 300, 300, 0, 214, 0, 0, 6), // b6
            Quad(0, 50, -1, 8, 8, 50f, -132f, 74f, -134f, 48f, -104f, 76f, -108f, SpritePsxSemiTransparency.Mode2, 300, 300, 0, 214, 0, 0, 7), // b7
            Quad(0, -1, -1, 8, 8, 44f, -128f, 24f, -128f, 44f, -112f, 24f, -112f, SpritePsxSemiTransparency.Mode1, 300, 300, 0, 214, 0, 0, 8), // b8
            Quad(0, -1, 20, 24, 24, 224f, -80f, 200f, -80f, 224f, -104f, 200f, -104f, SpritePsxSemiTransparency.None, 300, 300, 0, 214, 0, 0, 9), // b9
            Quad(0, 300, 0, 16, 8, 232f, -120f, 280f, -120f, 232f, -144f, 280f, -144f, SpritePsxSemiTransparency.None, 300, 300, 0, 214, 0, 0, 10), // b10
            Quad(0, 300, -1, 16, 8, 236f, -140f, 276f, -140f, 236f, -124f, 276f, -124f, SpritePsxSemiTransparency.Mode2, 300, 300, 0, 214, 0, 0, 11), // b11
            Quad(0, -1, 600, 32, 32, 216f, -160f, 200f, -160f, 216f, -176f, 200f, -176f, SpritePsxSemiTransparency.None, 300, 300, 0, 214, 0, 0, 12), // b12
        },
        ["c_ties_tall"] = new[]
        {
            Quad(0, 100, 1040, 64, 64, 16f, -16f, 144f, -16f, 16f, -144f, 144f, -144f, SpritePsxSemiTransparency.None, 300, 300, 0, 214, 0, 0, 1), // c1
            Quad(0, 300, 1080, 24, 24, 160f, -16f, 232f, -16f, 160f, -88f, 232f, -88f, SpritePsxSemiTransparency.None, 300, 300, 0, 214, 0, 0, 2), // c2
            Quad(0, 360, 1040, 64, 64, 16f, -160f, 48f, -160f, 16f, -192f, 48f, -192f, SpritePsxSemiTransparency.None, 300, 300, 0, 214, 0, 0, 3), // c3
            Quad(0, 100, 1056, 48, 48, 100f, -160f, 148f, -160f, 112f, -208f, 160f, -208f, SpritePsxSemiTransparency.Mode0, 300, 300, 0, 214, 0, 0, 4), // c4
            Quad(0, 300, 1056, 52, 48, 224f, -160f, 228f, -160f, 176f, -208f, 280f, -208f, SpritePsxSemiTransparency.Mode1, 300, 300, 0, 214, 0, 0, 5), // c5
            Quad(0, 200, 1088, 40, 16, 100f, -100f, 150f, -103f, 98f, -140f, 152f, -142f, SpritePsxSemiTransparency.None, 300, 300, 0, 214, 0, 0, 6), // c6
            Quad(0, 199, 1090, 12, 14, 84f, -60f, 60f, -60f, 84f, -88f, 60f, -88f, SpritePsxSemiTransparency.Mode2, 300, 300, 0, 214, 0, 0, 7), // c7
            Quad(0, 160, 1060, 32, 40, 104f, -20f, 112f, -21f, 90f, -60f, 130f, -63f, SpritePsxSemiTransparency.Mode1, 300, 300, 0, 214, 0, 0, 8), // c8
        },
    };
}
