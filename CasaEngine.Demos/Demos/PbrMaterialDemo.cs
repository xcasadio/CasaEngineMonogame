using System;
using System.Collections.Generic;
using System.Globalization;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Materials.Runtime;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Rendering.Shaders;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// The reference scene of the physically based material (plan T10.1, ADR-0108 to ADR-0111): a 5 x 5 grid of spheres that share ONE
/// <see cref="LitPbrMaterial"/> and differ only by per-instance property overrides (roughness from 0.1 to 0.9 along a row, metal from 0
/// to 1 down a column), a coloured smooth metal sphere, an emissive sphere that feeds the bloom, a glass sphere in front of a
/// checkerboard, a skinned character drawn by the PBR skinning techniques, and a directional light that casts shadows on a ground,
/// under a procedural studio environment cube (image-based lighting and sky background). The world runs in the <c>LinearHdr</c>
/// colour pipeline with the <c>AcesApprox</c> tonemapper.
/// <para>
/// Keys: I switches the image-based lighting, B the bloom, T the tonemapping operator, + and - the exposure.
/// </para>
/// <para>
/// Harness variables (plan T10.1; all optional, everything is on by default): <c>CASAENGINE_PBR_DEMO_VALIDATION=1</c> fixes the
/// camera and freezes the skinned animation, so a capture needs no key press and two captures are identical;
/// <c>CASAENGINE_PBR_DEMO_IBL=0</c> removes the specular environment cube from the lighting (the sky stays);
/// <c>CASAENGINE_PBR_DEMO_BLOOM=0</c> switches the bloom off; <c>CASAENGINE_PBR_DEMO_GLASS=0</c> and
/// <c>CASAENGINE_PBR_DEMO_SKINNED=0</c> remove the glass sphere and the skinned character. The measurement pixels and segments are
/// the constants of <see cref="Measurement"/>.
/// </para>
/// </summary>
public sealed class PbrMaterialDemo : Demo
{
    private const string ValidationVariable = "CASAENGINE_PBR_DEMO_VALIDATION";
    private const string IblVariable = "CASAENGINE_PBR_DEMO_IBL";
    private const string BloomVariable = "CASAENGINE_PBR_DEMO_BLOOM";
    private const string GlassVariable = "CASAENGINE_PBR_DEMO_GLASS";
    private const string SkinnedVariable = "CASAENGINE_PBR_DEMO_SKINNED";
    private const string IblAction = "PbrMaterial.Ibl";
    private const float ExposureStep = 0.25f;
    private const int IblSummaryFrame = 600;

    // ---- Scene layout (world units; the camera looks along -Z) ----
    private const int GridColumns = 5;
    private const int GridRows = 5;
    private const float GridSpacing = 2.6f;
    private const float GridRadius = 1.2f;
    private static readonly Vector3 GridFirstSphere = new(-5.2f, 12.0f, 0.0f);
    private static readonly Color GridBaseColor = new(205, 205, 205);
    private static readonly Vector3 ProbeFirstSphere = new(-5.0f, -2.8f, 4.0f);
    private const float ProbeSpacing = 2.5f;

    private const float GroundTopY = -4.0f;
    private static readonly Color GroundColor = new(150, 146, 140);

    private const float GoldRadius = 1.5f;
    private static readonly Vector3 GoldCentre = new(-13.4f, GroundTopY + GoldRadius, 1.5f);
    private static readonly Color GoldBaseColor = new(225, 165, 105);
    private const float GoldRoughness = 0.15f;

    private const float EmissiveRadius = 1.2f;
    private static readonly Vector3 EmissiveCentre = new(-8.9f, GroundTopY + EmissiveRadius, 0.5f);
    private static readonly Vector3 EmissiveColour = new(2.0f, 1.6f, 0.9f);

    private const float GlassRadius = 1.5f;
    private static readonly Vector3 GlassCentre = new(10.4f, -1.8f, 2.5f);
    private static readonly Vector3 CheckerCentre = new(11.6f, -1.5f, -1.0f);
    private const float CheckerWidth = 5.5f;
    private const float CheckerHeight = 5.0f;
    private const int CheckerColumns = 8;
    private const int CheckerRows = 6;
    private const int CheckerCellSize = 8;

    private static readonly Vector3 SkinnedPosition = new(16.2f, GroundTopY, 2.5f);
    private const float SkinnedScale = 0.065f;
    private const float FrozenAnimationTimeSeconds = 0.4f;

    private static readonly Vector3 CameraPosition = new(0.0f, 8.0f, 26.0f);
    private static readonly Vector3 CameraTarget = new(0.0f, 3.0f, 0.0f);

    // The light travels along this direction (from the upper left, in front, towards the lower right, behind).
    private static readonly Vector3 LightDirection = Vector3.Normalize(new Vector3(0.45f, -0.7f, -0.55f));
    private const float LightIntensity = 1.4f;
    private const float FlatAmbientIntensity = 0.4f;

    /// <summary>
    /// The pixel measurements of plan T10.1 on a 1024 x 768 capture of the demo in validation configuration
    /// (<c>CASAENGINE_PBR_DEMO_VALIDATION=1</c>, captured at least 600 frames after the start). Points are x, y; rectangles are
    /// x, y, width, height; thresholds are grey levels (luminance 0.2126 R + 0.7152 G + 0.0722 B) or ratios.
    /// </summary>
    public static class Measurement
    {
        /// <summary>One horizontal measurement segment: row, first and last pixel (both on the sphere).</summary>
        public readonly record struct Segment(int Y, int XStart, int XEnd);

        // (1) Width of the sun highlight at half height on the five specular probes (black dielectric spheres of roughness 0.1, 0.3,
        // 0.5, 0.7 and 0.9, left to right, set by overrides of the shared material). Each segment is 8 px above the centre of its sphere
        // (where the mirror point of the sun sits, whatever the roughness) and 29 px either side of it, all on the sphere. The width is
        // the number of pixels of the segment whose luminance is above half of the maximum of that segment; it must grow by at least
        // MinimumHighlightWidthStep pixels from one probe to the next. At roughness 0.9 the lobe is flat and the width is bounded by the
        // segment (59 px).
        public static readonly Segment[] ProbeSegments =
        {
            new(557, 342, 400), new(557, 413, 471), new(557, 483, 541), new(557, 553, 611), new(557, 624, 682),
        };

        public const int MinimumHighlightWidthStep = 2;

        // (2) A pixel of the reflection on the coloured metal sphere: the ratios R/G and G/B of its colour must be those of the base
        // colour (GoldBaseColourRatios) within GoldRatioTolerance (relative).
        public static readonly Point GoldReflectionPixel = new(146, 505);
        public static readonly Vector2 GoldBaseColourRatios = new(GoldBaseColor.R / (float)GoldBaseColor.G, GoldBaseColor.G / (float)GoldBaseColor.B);
        public const float GoldRatioTolerance = 0.15f;

        // (3) A pixel 5 px to the right of the emissive sphere (which spans x 262 to 322 on row 526), on the ground: at least
        // MinimumBloomGain levels lighter with the bloom than with CASAENGINE_PBR_DEMO_BLOOM=0.
        public static readonly Point BloomPixel = new(327, 526);
        public const float MinimumBloomGain = 10.0f;

        // (4) A pixel of a dark cell of the checkerboard seen through the glass sphere: at least MinimumGlassDifference levels away
        // from the same pixel with CASAENGINE_PBR_DEMO_GLASS=0.
        public static readonly Point BehindGlassPixel = new(790, 512);
        public const float MinimumGlassDifference = 10.0f;

        // (5) The rectangle of the skinned character: its mean absolute luminance difference with the capture taken with
        // CASAENGINE_PBR_DEMO_SKINNED=0 is above MinimumSkinnedMeanDifference levels.
        public static readonly Rectangle SkinnedRectangle = new(880, 425, 135, 165);
        public const float MinimumSkinnedMeanDifference = 10.0f;

        // (6) A pixel in the shadow of the coloured metal sphere on the ground, and a lit pixel of the same ground at the same
        // column: the first is at least MinimumShadowDarkening levels darker than the second.
        public static readonly Point ShadowPixel = new(230, 550);
        public static readonly Point LitGroundPixel = new(230, 575);
        public const float MinimumShadowDarkening = 20.0f;

        // (7) A pixel of the reflection on the smooth metal sphere of the grid (metal 1, roughness 0.1, the bottom row, left column):
        // at least MinimumIblDrop levels darker with CASAENGINE_PBR_DEMO_IBL=0 than with the lighting from the environment. The
        // demo logs the generation counter of the IBL cache at frame IblSummaryFrame (it must be 1).
        public static readonly Point MirrorPixel = new(385, 395);
        public const float MinimumIblDrop = 30.0f;
    }

    private CasaEngineGame? _game;
    private bool _validation;
    private bool _iblOn = true;
    private bool _bloomOn = true;
    private bool _glassOn = true;
    private bool _skinnedOn = true;
    private int _frame;
    private int _lastLoggedGenerationCount;
    private bool _overridesApplied;
    private TextureCube? _skyCube;
    private Texture2D? _checkerTexture;
    private readonly List<StaticModelComponent> _gridComponents = new();
    private readonly List<MaterialPropertyBlock> _gridBlocks = new();

    public override string Title => "PBR material demo";

    public override string Description =>
        "The physically based material: a grid of spheres sharing one material (roughness along a row, metal down a column, set by per-instance overrides), a coloured metal sphere, an emissive sphere with bloom, a glass sphere, a skinned character, a shadow-casting sun and a studio environment, in the LinearHdr pipeline with the ACES approximation.";

    public override IReadOnlyList<string> Highlights { get; } = new[]
    {
        "Along a row the highlight of the sun widens with the roughness; down a column the spheres turn from plastic to metal.",
        "The coloured metal sphere reflects the studio in its own colour; the emissive sphere glows (bloom).",
        "The glass sphere keeps the checkerboard readable and its specular highlight bright.",
        "The skinned character is lit by the PBR skinning technique (direct light and shadows, no image-based lighting).",
    };

    public override IReadOnlyList<DemoControl> Controls { get; } = new DemoControl[]
    {
        DemoControl.ForActions("turn the image-based lighting on or off", IblAction),
        DemoControl.ForActions("turn the bloom on or off", "ColorPipeline.Bloom"),
        DemoControl.ForActions("switch the tonemapping operator (AcesApprox, Reinhard)", "ColorPipeline.Tonemapper"),
        DemoControl.ForActions("raise the exposure by 0.25", "ColorPipeline.ExposureUp"),
        DemoControl.ForActions("lower the exposure by 0.25", "ColorPipeline.ExposureDown"),
    };

    public override string DocumentationPath => "docs/engine/pbr-rendering.md";

    public override CameraComponent CreateCamera(CasaEngineGame game)
    {
        if (!_validation)
        {
            return base.CreateCamera(game);
        }

        var entity = new Entity { Name = "PbrMaterialCamera" };
        var camera = new CameraLookAtComponent();
        entity.RootComponent = camera;
        entity.Initialize();
        game.GameManager.CurrentWorld.AddEntity(entity);
        return camera;
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        if (camera is CameraLookAtComponent lookAt)
        {
            lookAt.SetPositionAndTarget(CameraPosition, CameraTarget);
        }
        else
        {
            ((ArcBallCameraComponent)camera).SetCamera(CameraPosition, CameraTarget, Vector3.Up);
        }
    }

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        _validation = Environment.GetEnvironmentVariable(ValidationVariable) == "1";
        _iblOn = Environment.GetEnvironmentVariable(IblVariable) != "0";
        _bloomOn = Environment.GetEnvironmentVariable(BloomVariable) != "0";
        _glassOn = Environment.GetEnvironmentVariable(GlassVariable) != "0";
        _skinnedOn = Environment.GetEnvironmentVariable(SkinnedVariable) != "0";
        _frame = 0;
        _lastLoggedGenerationCount = 0;
        _overridesApplied = false;
        _gridComponents.Clear();
        _gridBlocks.Clear();

        var world = game.GameManager.CurrentWorld;
        GraphicsDevice graphicsDevice = game.GraphicsDevice;
        _skyCube = CreateStudioCube(graphicsDevice);

        AddGround(world, graphicsDevice);
        AddGrid(world, graphicsDevice);
        AddGoldSphere(world, graphicsDevice);
        AddEmissiveSphere(world, graphicsDevice);
        AddChecker(world, graphicsDevice);
        if (_glassOn)
        {
            AddGlassSphere(world, graphicsDevice);
        }

        if (_skinnedOn)
        {
            AddSkinnedCharacter(game, world);
        }

        Logs.WriteInfo($"[PbrMaterialDemo] validation {_validation}, IBL {_iblOn}, bloom {_bloomOn}, glass {_glassOn}, skinned {_skinnedOn}.");
    }

    public override void ConfigureSceneLighting(World world)
    {
        var settings = world.EnvironmentSettings;
        settings.Type = EnvironmentType.Cubemap;
        settings.BackgroundMode = EnvironmentBackgroundMode.Environment;
        settings.BackgroundColor = new Color(20, 20, 20);
        settings.EnvironmentAssetId = Guid.Empty;
        settings.BackgroundCubemapAssetId = Guid.Empty;
        settings.SpecularEnvironmentCubemapAssetId = Guid.Empty;
        settings.BackgroundCubemap = _skyCube;
        settings.SpecularEnvironmentCubemap = _skyCube;
        settings.AmbientColor = new Vector3(0.6f);
        ApplyIbl(settings);

        settings.Shadows.Enabled = true;
        settings.Shadows.Resolution = 2048;
        settings.Shadows.DepthBias = 0.001f;
        settings.Shadows.NormalBias = 0.002f;
        settings.Shadows.MaxDistance = 64.0f;

        settings.ColorPipeline.Mode = ColorPipelineMode.LinearHdr;
        settings.ColorPipeline.Tonemapper = TonemapOperator.AcesApprox;
        settings.ColorPipeline.Exposure = 1.0f;
        settings.ColorPipeline.BloomEnabled = _bloomOn;
        settings.MarkDirty();

        DemoSceneLightRig.AddDirectionalLight(world, "PbrSun", LightDirection, Color.White, Color.White, LightIntensity, castShadows: true);
    }

    public override void Update(GameTime gameTime)
    {
        if (_game == null)
        {
            return;
        }

        _frame++;
        if (!_overridesApplied)
        {
            ApplyInstanceOverrides();
        }

        var settings = _game.GameManager.CurrentWorld.EnvironmentSettings;
        var colorPipeline = settings.ColorPipeline;
        bool changed = false;

        if (!_validation)
        {
            if (DemoInput.WasPressed(_game, IblAction))
            {
                _iblOn = !_iblOn;
                ApplyIbl(settings);
                changed = true;
            }

            if (DemoInput.WasPressed(_game, "ColorPipeline.Bloom"))
            {
                colorPipeline.BloomEnabled = !colorPipeline.BloomEnabled;
                changed = true;
            }

            if (DemoInput.WasPressed(_game, "ColorPipeline.Tonemapper"))
            {
                colorPipeline.Tonemapper = colorPipeline.Tonemapper == TonemapOperator.AcesApprox ? TonemapOperator.Reinhard : TonemapOperator.AcesApprox;
                changed = true;
            }

            if (DemoInput.WasPressed(_game, "ColorPipeline.ExposureUp"))
            {
                colorPipeline.Exposure += ExposureStep;
                changed = true;
            }

            if (DemoInput.WasPressed(_game, "ColorPipeline.ExposureDown"))
            {
                colorPipeline.Exposure -= ExposureStep;
                changed = true;
            }
        }

        if (changed)
        {
            settings.MarkDirty();
            Logs.WriteInfo($"[PbrMaterialDemo] IBL {_iblOn}, {colorPipeline.Tonemapper}, exposure {colorPipeline.Exposure}, bloom {colorPipeline.BloomEnabled}.");
        }

        var cache = _game.IblCache;
        if (cache != null && cache.GenerationCount != _lastLoggedGenerationCount)
        {
            _lastLoggedGenerationCount = cache.GenerationCount;
            Logs.WriteInfo($"[PbrMaterialDemo] IBL generation count {cache.GenerationCount} at frame {_frame}, outside the view loop: {cache.GeneratedOutsideViewLoop}.");
        }

        if (_frame == IblSummaryFrame && cache != null)
        {
            Logs.WriteInfo($"[PbrMaterialDemo] IBL after {_frame} frames: {cache.GenerationCount} generation(s), outside the view loop: {cache.GeneratedOutsideViewLoop}.");
        }
    }

    public override void OnScreenResized(CasaEngineGame game, int width, int height)
    {
        game.GameManager.ViewManager.ApplyBackBufferLayout(width, height);
    }

    public override void Clean()
    {
        if (_game?.GameManager.CurrentWorld is { } world)
        {
            world.EnvironmentSettings.ResetToDefaults();
            world.EnvironmentSettings.MarkDirty();
        }

        _gridComponents.Clear();
        _gridBlocks.Clear();
        _checkerTexture?.Dispose();
        _checkerTexture = null;
        _skyCube?.Dispose();
        _skyCube = null;
        _overridesApplied = false;
        _frame = 0;
        _lastLoggedGenerationCount = 0;
        _validation = false;
        _iblOn = true;
        _bloomOn = true;
        _glassOn = true;
        _skinnedOn = true;
        _game = null;
    }

    /// <summary>
    /// The image-based lighting is the specular cube (reflections) and the irradiance taken from it (ambient diffuse). Off, the cube still
    /// draws the sky behind the scene but lights nothing: no reflection (specular intensity 0) and a flat ambient colour.
    /// </summary>
    private void ApplyIbl(WorldEnvironmentSettings settings)
    {
        settings.SpecularIntensity = _iblOn ? 1.0f : 0.0f;
        settings.AmbientSource = _iblOn ? AmbientSource.Auto : AmbientSource.Flat;
        settings.AmbientIntensity = _iblOn ? 1.0f : FlatAmbientIntensity;
    }

    // ---- Scene construction ----

    private void AddGround(World world, GraphicsDevice graphicsDevice)
    {
        AddPrimitive(
            world,
            graphicsDevice,
            "PBR ground",
            new BoxPrimitive(120.0f, 0.4f, 60.0f),
            new Vector3(0.0f, GroundTopY - 0.2f, 0.0f),
            new LitPbrMaterial { Name = "PbrGround", BaseColor = GroundColor, Metallic = 0.0f, Roughness = 0.8f },
            castShadows: false);
    }

    /// <summary>One material, one model: the 25 spheres differ only by the property block of their instance.</summary>
    private void AddGrid(World world, GraphicsDevice graphicsDevice)
    {
        var sharedMaterial = new LitPbrMaterial { Name = "PbrGrid", BaseColor = GridBaseColor, Metallic = 0.0f, Roughness = 0.5f };
        StaticModel sharedModel = StaticModel.CreateFromPrimitive(new SpherePrimitive(GridRadius * 2.0f, 32));
        sharedModel.Meshes[0].Initialize(graphicsDevice);
        sharedModel.Meshes[0].Material = sharedMaterial;

        for (int row = 0; row < GridRows; row++)
        {
            for (int column = 0; column < GridColumns; column++)
            {
                var block = new MaterialPropertyBlock();
                block.SetFloat(ShaderParameterNames.Metallic, GridMetallic(row));
                block.SetFloat(ShaderParameterNames.Roughness, GridRoughness(column));

                var entity = new Entity { Name = $"PBR grid {row} {column}" };
                var component = new StaticModelComponent { StaticModel = sharedModel, CastShadows = false };
                entity.RootComponent = component;
                component.LocalPosition = GridFirstSphere + new Vector3(GridSpacing * column, -GridSpacing * row, 0.0f);
                world.AddEntity(entity);

                _gridComponents.Add(component);
                _gridBlocks.Add(block);
            }
        }

        // The specular probe row: the same material and model, a black base colour and no metal, so the only light on the sphere
        // is the specular lobe of the sun (the technique of plan T4.3 to measure the width of a highlight), in front of the grid.
        for (int column = 0; column < GridColumns; column++)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor(ShaderParameterNames.BaseColor, Color.Black);
            block.SetFloat(ShaderParameterNames.Metallic, 0.0f);
            block.SetFloat(ShaderParameterNames.Roughness, GridRoughness(column));

            var entity = new Entity { Name = $"PBR probe {column}" };
            var component = new StaticModelComponent { StaticModel = sharedModel, CastShadows = false };
            entity.RootComponent = component;
            component.LocalPosition = ProbeFirstSphere + new Vector3(ProbeSpacing * column, 0.0f, 0.0f);
            world.AddEntity(entity);

            _gridComponents.Add(component);
            _gridBlocks.Add(block);
        }
    }

    /// <summary>Metal of grid row <paramref name="row"/> (0 at the top): 0, 0.25, 0.5, 0.75, 1.</summary>
    public static float GridMetallic(int row) => row / (float)(GridRows - 1);

    /// <summary>Perceptual roughness of grid column <paramref name="column"/> (0 on the left): 0.1, 0.3, 0.5, 0.7, 0.9.</summary>
    public static float GridRoughness(int column) => 0.1f + (0.2f * column);

    /// <summary>
    /// The generated sub-mesh components exist once the world is loaded, so the property blocks are attached on the first updates
    /// that find them (the flag is raised when all 25 have one).
    /// </summary>
    private void ApplyInstanceOverrides()
    {
        int attached = 0;
        for (int i = 0; i < _gridComponents.Count; i++)
        {
            StaticModelComponent component = _gridComponents[i];
            for (int child = 0; child < component.Children.Count; child++)
            {
                if (component.Children[child] is StaticModelSubMeshComponent subMesh)
                {
                    subMesh.PropertyOverrides = _gridBlocks[i];
                    attached++;
                    break;
                }
            }
        }

        _overridesApplied = attached == _gridComponents.Count;
    }

    private void AddGoldSphere(World world, GraphicsDevice graphicsDevice)
    {
        AddPrimitive(
            world,
            graphicsDevice,
            "PBR gold sphere",
            new SpherePrimitive(GoldRadius * 2.0f, 48),
            GoldCentre,
            new LitPbrMaterial { Name = "PbrGold", BaseColor = GoldBaseColor, Metallic = 1.0f, Roughness = GoldRoughness });
    }

    private void AddEmissiveSphere(World world, GraphicsDevice graphicsDevice)
    {
        AddPrimitive(
            world,
            graphicsDevice,
            "PBR emissive sphere",
            new SpherePrimitive(EmissiveRadius * 2.0f, 48),
            EmissiveCentre,
            new LitPbrMaterial
            {
                Name = "PbrEmissive",
                BaseColor = Color.Black,
                Metallic = 0.0f,
                Roughness = 0.6f,
                EmissiveColor = EmissiveColour,
            });
    }

    /// <summary>The checkerboard quad the glass sphere stands in front of (always present: it is the control of the glass).</summary>
    private void AddChecker(World world, GraphicsDevice graphicsDevice)
    {
        int width = CheckerColumns * CheckerCellSize;
        int height = CheckerRows * CheckerCellSize;
        var light = new Color(230, 230, 230);
        var dark = new Color(30, 30, 30);
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                pixels[(y * width) + x] = (((x / CheckerCellSize) + (y / CheckerCellSize)) % 2 == 0) ? light : dark;
            }
        }

        _checkerTexture = new Texture2D(graphicsDevice, width, height);
        _checkerTexture.SetData(pixels);
        _checkerTexture.Name = "PbrDemoChecker";

        AddPrimitive(
            world,
            graphicsDevice,
            "PBR checkerboard",
            new BoxPrimitive(CheckerWidth, CheckerHeight, 0.1f),
            CheckerCentre,
            new UnlitTextureMaterial { Name = "PbrDemoChecker", BasColor = _checkerTexture },
            castShadows: false);
    }

    /// <summary>A glass sphere (lit-pbr in the transparent queue) in front of the checkerboard.</summary>
    private void AddGlassSphere(World world, GraphicsDevice graphicsDevice)
    {
        AddPrimitive(
            world,
            graphicsDevice,
            "PBR glass sphere",
            new SpherePrimitive(GlassRadius * 2.0f, 48),
            GlassCentre,
            new LitPbrMaterial
            {
                Name = "PbrGlass",
                BaseColor = new Color(255, 255, 255, 64),
                Metallic = 0.0f,
                Roughness = 0.05f,
                Queue = RenderQueue.Transparent,
                BlendState = BlendState.AlphaBlend,
                DepthStencilState = DepthStencilState.DepthRead,
            },
            castShadows: false);
    }

    private static void AddSkinnedCharacter(CasaEngineGame game, World world)
    {
        var skinnedMesh = game.AssetContentManager.LoadFromFile<SkinnedMesh>("SkinnedMesh\\kid_idle.model");
        skinnedMesh.Initialize(game.AssetContentManager);

        var entity = new Entity { Name = "PBR skinned character" };
        var component = new SkinnedMeshComponent
        {
            SkinnedMesh = skinnedMesh,
            SkinningModeSelection = SkinningModeSelection.LinearBlend,
            CastShadows = true,
            ReceiveShadows = true,
            PbrMaterial = new LitPbrMaterial
            {
                Name = "PbrSkinned",
                BaseColor = Color.White,
                Metallic = 0.0f,
                Roughness = 0.5f,
                BlendState = BlendState.NonPremultiplied,
                DepthStencilState = DepthStencilState.Default,
                RasterizerState = RasterizerState.CullCounterClockwise,
                SamplerState = SamplerState.AnisotropicClamp,
            },
        };

        entity.RootComponent = component;
        component.LocalPosition = SkinnedPosition;
        component.LocalOrientation = Quaternion.Identity;
        component.LocalScale = new Vector3(SkinnedScale);
        component.PlayAnimation(0);
        if (Environment.GetEnvironmentVariable(ValidationVariable) == "1")
        {
            component.SeekAnimation(FrozenAnimationTimeSeconds);
            component.PauseAnimation();
        }

        world.AddEntity(entity);
    }

    private static void AddPrimitive(
        World world,
        GraphicsDevice graphicsDevice,
        string name,
        GeometricPrimitive primitive,
        Vector3 position,
        MaterialBase material,
        bool castShadows = true)
    {
        var entity = new Entity { Name = name };
        var mesh = new StaticModelComponent { CastShadows = castShadows };
        entity.RootComponent = mesh;
        mesh.StaticModel = StaticModel.CreateFromPrimitive(primitive);
        mesh.StaticModel.Meshes[0].Initialize(graphicsDevice);
        mesh.StaticModel.Meshes[0].Material = material;
        mesh.LocalPosition = position;
        world.AddEntity(entity);
    }

    // ---- The studio environment ----

    /// <summary>
    /// A neutral grey studio: a light sky above, a darker floor below and a bright disc where the sun is. Neutral on purpose, so a
    /// coloured metal reflects it in its own colour and the measurement of the colour of the reflection is not tinted by the cube.
    /// </summary>
    private static TextureCube CreateStudioCube(GraphicsDevice graphicsDevice)
    {
        var faces = new Color[6][];
        CubeMapFace[] order =
        {
            CubeMapFace.PositiveX, CubeMapFace.NegativeX, CubeMapFace.PositiveY,
            CubeMapFace.NegativeY, CubeMapFace.PositiveZ, CubeMapFace.NegativeZ,
        };

        int size = ColorPipelineIblScene.FaceSize;
        Vector3 towardsSun = -LightDirection;
        for (int face = 0; face < faces.Length; face++)
        {
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector3 direction = IblCubeGenerator.GetTexelDirection(order[face], x, y, size);
                    pixels[(y * size) + x] = StudioColor(direction, towardsSun);
                }
            }

            faces[face] = pixels;
        }

        return ColorPipelineIblScene.CreateCube(graphicsDevice, faces, "PbrDemoStudioCube");
    }

    private const float SunDiscCosine = 0.995f;
    private const byte HorizonGrey = 150;
    private const byte ZenithGrey = 215;
    private const byte FloorGrey = 55;
    private const byte NadirGrey = 25;
    private const byte SunGrey = 245;

    private static Color StudioColor(Vector3 direction, Vector3 towardsSun)
    {
        if (Vector3.Dot(direction, towardsSun) >= SunDiscCosine)
        {
            return new Color(SunGrey, SunGrey, SunGrey);
        }

        float height = direction.Y;
        float grey = height >= 0.0f
            ? MathHelper.Lerp(HorizonGrey, ZenithGrey, MathF.Pow(height, 0.6f))
            : MathHelper.Lerp(
                MathHelper.Lerp(HorizonGrey, FloorGrey, MathHelper.Clamp(-height / 0.25f, 0.0f, 1.0f)),
                NadirGrey,
                -height);
        var level = (byte)MathF.Round(grey);
        return new Color(level, level, level);
    }
}
