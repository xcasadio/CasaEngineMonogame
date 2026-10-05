using System;
using System.Collections.Generic;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using EngineTexture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Demos.Demos.PsxSemiTransparency;

/// <summary>
/// E19.g G2a (ADR-0051): one sprite of each PSX semi-transparency mode on a plain background, drawn by the sorted sprite
/// path of <see cref="AnimatedSpriteComponent"/> (entities with a <see cref="DepthSortable2DComponent"/>). Each sprite is
/// three texels wide: an opaque texel, a semi-transparent (STP) texel and a transparent one. The first sprite carries no
/// mode and shows what the sorted path drew before the mode existed. The scene reads its own back-buffer in process
/// (<see cref="BackBufferProbe"/>) and compares it with the PSX formulas on 8-bit colours, to within one level per channel:
/// run it with <c>CASAENGINE_START_DEMO="PSX sprite semi-transparency"</c> from the <c>CasaEngine.Demos</c> folder, with
/// <c>CASAENGINE_CAPTURE_SCREENSHOT_PATH</c> to also save the image and <c>CASAENGINE_DEMO_PIXELS_PATH</c> to save the readout.
/// </summary>
public class PsxSemiTransparencyDemo : Demo
{
    private static readonly Color Background = new(100, 150, 200, 255);
    private static readonly Color OpaqueTexel = new(60, 40, 20, 255);
    private static readonly Color StpTexel = new(120, 80, 40, 128);

    private const int TexelScale = 16;
    private const float SpriteTop = 400f;

    private static readonly Guid Texture2dId = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid SheetId = Guid.Parse("a1000000-0000-0000-0000-000000000002");

    private readonly BackBufferProbe _probe = new("PSX sprite semi-transparency");
    private readonly List<IDisposable> _holds = new();

    public override string Title => "PSX sprite semi-transparency";

    public override string Description =>
        "One sprite of each PSX semi-transparency mode (none, 0 average, 1 additive, 2 subtractive, 3 quarter) on a plain " +
        "background, drawn as two disjoint passes by the sorted sprite path. Each sprite has an opaque texel, an STP texel and a " +
        "transparent texel. The scene checks its own back-buffer against the PSX formulas.";

    public override void Initialize(CasaEngineGame game)
    {
        var world = game.GameManager.CurrentWorld;
        var assets = game.AssetContentManager;

        // Sheet of four texels: opaque, STP (alpha 128), transparent, background.
        var texture = new Texture2D(game.GraphicsDevice, 4, 1, false, SurfaceFormat.Color);
        texture.SetData(new[] { OpaqueTexel, StpTexel, new Color(0, 0, 0, 0), Background });
        _holds.Add(assets.Register(Texture2dId, texture));
        _holds.Add(assets.Register(SheetId, new EngineTexture(Texture2dId, game.GraphicsDevice)));

        AddSprite(world, assets, "background", new Rectangle(3, 0, 1, 1), SpritePsxSemiTransparency.None,
            new Vector3(-1500f, 2500f, 0f), new Vector3(4000f, 4000f, 1f), orderInLayer: -1, seed: 0);

        var modes = new[]
        {
            (SpritePsxSemiTransparency.None, "none", StpTexel),
            (SpritePsxSemiTransparency.Mode0, "mode0", new Color(110, 115, 120)),
            (SpritePsxSemiTransparency.Mode1, "mode1", new Color(220, 230, 240)),
            (SpritePsxSemiTransparency.Mode2, "mode2", new Color(0, 70, 160)),
            (SpritePsxSemiTransparency.Mode3, "mode3", new Color(130, 170, 210))
        };

        for (var index = 0; index < modes.Length; index++)
        {
            var (mode, name, expectedStp) = modes[index];
            var left = 100f + index * 140f;
            AddSprite(world, assets, name, new Rectangle(0, 0, 3, 1), mode,
                new Vector3(left, SpriteTop, 0f), new Vector3(TexelScale, TexelScale, 1f), orderInLayer: 0, seed: index + 1);

            var centerY = SpriteTop - TexelScale / 2f;
            _probe.Add($"{name} opaque texel", new Vector3(left + TexelScale * 0.5f, centerY, 0f), OpaqueTexel);
            _probe.Add($"{name} STP texel", new Vector3(left + TexelScale * 1.5f, centerY, 0f), expectedStp);
            _probe.Add($"{name} transparent texel", new Vector3(left + TexelScale * 2.5f, centerY, 0f), Background);
        }
    }

    private void AddSprite(
        CasaEngine.Framework.Scene.World.World world, AssetContentManager assets, string name, Rectangle region,
        SpritePsxSemiTransparency mode, Vector3 position, Vector3 scale, int orderInLayer, int seed)
    {
        var spriteId = Guid.Parse($"a1000000-0000-0000-0001-{seed:D12}");
        var spriteData = new SpriteData(spriteId)
        {
            Name = $"psx_demo_{name}",
            SpriteSheetAssetId = SheetId,
            PositionInTexture = region,
            Origin = Point.Zero,
            PsxSemiTransparency = mode
        };
        _holds.Add(assets.Register(spriteId, spriteData));

        var animationData = new Animation2dData { Name = $"psx_demo_{name}" };
        animationData.Parts.Add(new Animation2dPartData { Id = "body", DefaultSpriteId = spriteId });

        var animatedSprite = new AnimatedSpriteComponent();
        var entity = new Entity { Name = $"PsxDemo_{name}", RootComponent = animatedSprite };
        animatedSprite.Position = position;
        animatedSprite.Scale = scale;
        animatedSprite.AddAnimation(new Animation2d(animationData));
        entity.AddComponent(new DepthSortable2DComponent { OrderInLayer = orderInLayer });
        world.AddEntity(entity);
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
    }

    public override void PostDraw(CasaEngineGame game, GameTime gameTime)
    {
        _probe.OnPostDraw(game);
    }

    public override void Clean()
    {
        foreach (var hold in _holds)
        {
            hold.Dispose();
        }

        _holds.Clear();
    }
}
