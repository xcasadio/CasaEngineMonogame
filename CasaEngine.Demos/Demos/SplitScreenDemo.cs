using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Materials;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Demonstrates 2-view split-screen rendering using RenderPipeline, with intentionally
/// asymmetric scene content so the per-view debug overlay shows clearly different stats.
/// </summary>
public class SplitScreenDemo : Demo
{
    private ArcBallCameraComponent? _camera2;
    private CasaEngineGame? _game;

    public override string Title => "Split-screen demo (2 views)";
    public override string Description => "Validates per-view render stats in split-screen: the left view sees a heavier textured/transparent cluster, the right view a lighter opaque cluster.";

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        var world = game.GameManager.CurrentWorld;
        var gd = game.GraphicsDevice;

        var heavyChecker = CreateCheckerTexture(gd, 128, new Color(205, 170, 120), new Color(96, 62, 38));
        var steelChecker = CreateCheckerTexture(gd, 128, new Color(188, 196, 214), new Color(84, 96, 128));
        var mossChecker = CreateCheckerTexture(gd, 128, new Color(176, 198, 126), new Color(76, 104, 64));
        var glassTexture = CreateCheckerTexture(gd, 64, new Color(255, 255, 255, 220), new Color(205, 230, 255, 150));

        // Ground plane
        AddPrimitive(world, gd, "Ground", new BoxPrimitive(36, 1, 20), new Vector3(0, -0.5f, 0), Quaternion.Identity,
            new LitDiffuseMaterial { DiffuseColor = new Color(190, 180, 150), SpecularColor = new Vector3(0.25f), SpecularPower = 10f });

        // Left cluster: intentionally heavier view with more draw buckets and texture variation.
        AddPrimitive(world, gd, "HeavyBoxA", new BoxPrimitive(2.3f, 2.3f, 2.3f), new Vector3(-13f, 1.15f, -1.8f),
            Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(12f)),
            CreateTexturedLitMaterial(heavyChecker, Color.White));
        AddPrimitive(world, gd, "HeavyBoxB", new BoxPrimitive(2.3f, 2.3f, 2.3f), new Vector3(-10f, 1.15f, 0.4f),
            Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(-18f)),
            CreateTexturedLitMaterial(steelChecker, Color.White));
        AddPrimitive(world, gd, "HeavyBoxC", new BoxPrimitive(2.3f, 2.3f, 2.3f), new Vector3(-7f, 1.15f, 2.0f),
            Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(24f)),
            CreateTexturedLitMaterial(mossChecker, Color.White));
        AddPrimitive(world, gd, "HeavyColumn", new BoxPrimitive(1.2f, 4.0f, 1.2f), new Vector3(-10f, 2.0f, -3.5f),
            Quaternion.Identity,
            new LitDiffuseMaterial { DiffuseColor = new Color(196, 116, 84), SpecularColor = new Vector3(0.35f), SpecularPower = 18f });
        AddPrimitive(world, gd, "HeavyCubeRear", new BoxPrimitive(1.6f, 1.6f, 1.6f), new Vector3(-7.5f, 0.8f, -4.5f),
            Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(-30f)),
            new LitDiffuseMaterial { DiffuseColor = new Color(110, 130, 196), SpecularColor = new Vector3(0.45f), SpecularPower = 22f });

        AddPrimitive(world, gd, "GlassPaneA", new BoxPrimitive(1.6f, 3.2f, 0.12f), new Vector3(-12.0f, 1.6f, 1.6f),
            Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(18f)),
            CreateTransparentUnlitMaterial(glassTexture, new Color(200, 228, 255, 170), 0.68f));
        AddPrimitive(world, gd, "GlassPaneB", new BoxPrimitive(1.6f, 3.2f, 0.12f), new Vector3(-9.0f, 1.6f, 2.8f),
            Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(-10f)),
            CreateTransparentUnlitMaterial(glassTexture, new Color(255, 214, 184, 170), 0.62f));
        AddPrimitive(world, gd, "GlassPaneC", new BoxPrimitive(1.6f, 3.2f, 0.12f), new Vector3(-6.0f, 1.6f, 0.6f),
            Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(-28f)),
            CreateTransparentUnlitMaterial(glassTexture, new Color(196, 255, 214, 170), 0.58f));

        // Right cluster: intentionally lighter view with only a couple of opaque items.
        AddPrimitive(world, gd, "LightBoxA", new BoxPrimitive(2.4f, 2.4f, 2.4f), new Vector3(10f, 1.2f, 0f),
            Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(20f)),
            new LitDiffuseMaterial { DiffuseColor = new Color(110, 130, 200), SpecularColor = new Vector3(0.35f), SpecularPower = 20f });
        AddPrimitive(world, gd, "LightBoxB", new BoxPrimitive(1.4f, 3.8f, 1.4f), new Vector3(13.4f, 1.9f, -1.2f),
            Quaternion.Identity,
            new LitDiffuseMaterial { DiffuseColor = new Color(180, 160, 110), SpecularColor = new Vector3(0.25f), SpecularPower = 12f });
    }

    public override CameraComponent CreateCamera(CasaEngineGame game)
    {
        // Camera 1 — inherited from Demo base
        var camera1 = (ArcBallCameraComponent)base.CreateCamera(game);

        // Camera 2 — side view
        var entity2 = new Entity { Name = "Camera 2 (side)" };
        _camera2 = new ArcBallCameraComponent();
        entity2.RootComponent = _camera2;
        entity2.Initialize();
        game.GameManager.CurrentWorld.AddEntity(entity2);

        return camera1;
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        base.InitializeCamera(camera);
        //return;

        var game = _game!;
        var pp = game.GraphicsDevice.PresentationParameters;

        // Compute left/right viewport rectangles
        var rects = SplitScreenLayout.Compute(pp.BackBufferWidth, pp.BackBufferHeight, 2, SplitMode.Vertical);

        // ---- Camera 1: heavy left cluster ----
        var cam1 = (ArcBallCameraComponent)camera;
        cam1.SetCamera(new Vector3(-10f, 8f, 18f), new Vector3(-10f, 1.4f, 0f), Vector3.Up);
        cam1.OnScreenResized(rects[0].Width, rects[0].Height);

        // ---- Camera 2: lighter right cluster ----
        _camera2!.SetCamera(new Vector3(24f, 7f, 6f), new Vector3(10f, 1.4f, 0f), Vector3.Up);
        _camera2.OnScreenResized(rects[1].Width, rects[1].Height);

        // ---- Register views ----
        var world = game.GameManager.CurrentWorld;
        var viewManager = game.GameManager.ViewManager;

        viewManager.Clear();
        viewManager.AutoLayoutMode = SplitMode.Vertical;
        viewManager.Add(new RenderView(world, cam1, new BackBufferSurface(rects[0]))
        {
            Name = "View 1 (stats heavy)",
            ClearColor = Color.CornflowerBlue,
            ShowDebugOverlay = true,
        });
        viewManager.Add(new RenderView(world, _camera2, new BackBufferSurface(rects[1]))
        {
            Name = "View 2 (stats light)",
            ClearColor = new Color(0.12f, 0.12f, 0.20f),
            ShowDebugOverlay = true,
        });
    }

    public override void Update(GameTime gameTime)
    {
    }

    public override void Clean()
    {
        if (_game != null)
        {
            _game.GameManager.ViewManager.Clear();
        }

        _camera2 = null;
        _game = null;
    }

    private static void AddPrimitive(
        Framework.World.World world,
        GraphicsDevice graphicsDevice,
        string name,
        BoxPrimitive primitive,
        Vector3 position,
        Quaternion rotation,
        MaterialBase material)
    {
        var entity = new Entity { Name = name };
        var component = new StaticModelComponent();
        entity.RootComponent = component;
        component.StaticModel = StaticModel.CreateFromPrimitive(primitive);
        component.StaticModel.Meshes[0].Initialize(graphicsDevice);
        component.StaticModel.Meshes[0].Material = material;
        component.LocalPosition = position;
        component.LocalOrientation = rotation;
        world.AddEntity(entity);
    }

    private static LitDiffuseMaterial CreateTexturedLitMaterial(Texture2D texture, Color tint)
        => new()
        {
            DiffuseColor = tint,
            BasColor = texture,
            SpecularColor = new Vector3(0.45f),
            SpecularPower = 28f,
        };

    private static UnlitTextureMaterial CreateTransparentUnlitMaterial(Texture2D texture, Color tint, float alpha)
        => new()
        {
            BasColor = texture,
            Tint = tint,
            Alpha = alpha,
            IsTransparent = true,
            Queue = RenderQueue.Transparent,
            BlendState = BlendState.AlphaBlend,
        };

    private static Texture2D CreateCheckerTexture(GraphicsDevice gd, int size, Color a, Color b)
    {
        int cellSize = size / 8;
        var tex = new Texture2D(gd, size, size);
        var data = new Color[size * size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int cx = x / cellSize;
            int cy = y / cellSize;
            data[y * size + x] = (cx + cy) % 2 == 0 ? a : b;
        }

        tex.SetData(data);
        return tex;
    }
}
