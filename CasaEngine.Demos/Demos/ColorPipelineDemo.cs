using System;
using System.Collections.Generic;
using System.Globalization;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.Particles;
using CasaEngine.Framework.Particles.Rendering;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// The measurement scene of the colour pipeline (ADR-0104, <c>ai-agent/tasks/pbr-rendering-tasks.md</c>, phase 1). A fixed
/// camera looks at a ramp of five grey planes (<c>unlit-texture</c>, 0, 64, 128, 191 and 255), three <c>lit-diffuse</c>
/// spheres under one directional light, a strong emissive plane on a dark background, a solid MGUI panel and a block of
/// screen text drawn by the 2D renderer. Every measurement pixel is a constant below; the colour pipeline is chosen by the
/// harness variables of <see cref="DemosGame"/> (<c>CASAENGINE_DEMO_COLOR_PIPELINE</c>, <c>CASAENGINE_DEMO_TONEMAPPER</c>,
/// <c>CASAENGINE_DEMO_EXPOSURE</c>) or by the keys of this demo.
/// <para>
/// <c>CASAENGINE_COLOR_DEMO_LIGHT_INTENSITY</c> (a number, invariant culture, default 1) sets the intensity of the
/// directional light. <c>CASAENGINE_COLOR_DEMO_BUFFERED=1</c> runs the demo in a buffered virtual resolution
/// (<see cref="VirtualResolutionMode.BufferedFit"/>, 1024 x 768, so the pixels stay where they are without it): the view
/// then draws into a <c>RenderTargetSurface</c> presented by <c>VirtualResolutionPresenter</c>, user interface and text
/// included. Keys: M changes the mode, T the operator, = / + raises the exposure by 0.25 and - lowers it, B toggles the
/// bloom (<c>CASAENGINE_DEMO_BLOOM=1</c> starts with it on).
/// </para>
/// <para>
/// <c>CASAENGINE_COLOR_DEMO_PBR=1</c> adds a row of five black dielectric <c>lit-pbr</c> spheres (roughness 0.1 to 0.9) below the
/// others, to measure the width of the specular highlight; without it the scene is the one above, unchanged. Their centres and
/// measurement lines are the constants <see cref="PbrSphereCentres"/> and <see cref="PbrMeasurementLines"/>.
/// </para>
/// <para>
/// <c>CASAENGINE_COLOR_DEMO_GLASS=1</c> adds, above the other objects, a checkerboard quad (an <c>unlit-texture</c> material) and in
/// front of its left half a glass sphere: a <c>lit-pbr</c> material in the transparent queue (base colour white, alpha 64,
/// roughness 0.05). The right half of the checkerboard has no sphere and is the control: the checkerboard must stay readable behind
/// the glass and the specular highlight must stay bright (transparent PBR keeps the specular, plan T5.1). Independent of
/// <c>CASAENGINE_COLOR_DEMO_PBR</c>; without it the scene is unchanged. Measurement constants: <see cref="GlassBehindSegment"/>, <see cref="GlassControlSegment"/> and <see cref="GlassHighlightPixel"/>.
/// </para>
/// <para>
/// <c>CASAENGINE_COLOR_DEMO_IBL=1</c> measures the image-based lighting of <c>lit-pbr</c> (plan T6.3, ADR-0110). The world gets a
/// procedural specular environment cube (64 texels per face, an 8 x 8 checkerboard whose light cells carry a tint of their own per
/// face and whose dark cells are 10 % of it) and two metal spheres with a white base colour are added on the left of the scene: one
/// of roughness 0 (a mirror, <see cref="IblSmoothSphereCentre"/>), one of roughness 0.6 (<see cref="IblRoughSphereCentre"/>). The
/// demo logs the reference colour of five pixels of the mirror (<see cref="IblSmoothSpherePixels"/>), computed on the CPU from the
/// source cube. <c>CASAENGINE_COLOR_DEMO_IBL_FORCE_BASE_LEVEL=1</c> forces the prefiltered band 0 (debug constant of the shader) to
/// show the effect of the prefiltering on the rough sphere (<see cref="IblRoughMeasurementLine"/>).
/// <c>CASAENGINE_COLOR_DEMO_IBL_CHANGE_ENVIRONMENT=1</c> replaces the specular cube by another one at frames 200 and 400; the demo
/// logs every change of the generation counter of the IBL cache and a summary at frame 600. Everything is off by default: without
/// the variable the scene is unchanged.
/// </para>
/// <para>
/// <c>CASAENGINE_COLOR_DEMO_PROBES=1</c> measures the prefiltered local reflection probes of <c>lit-pbr</c> (plan T7.1, ADR-0110).
/// The world gets a green checkerboard environment cube, two reflection probes (a red cube and a blue cube, <see cref="ProbeRadius"/>,
/// <see cref="ProbeBlendDistance"/>) and one metal sphere of roughness 0.6 (<see cref="IblRoughSphereCentre"/>, measured on
/// <see cref="IblRoughMeasurementLine"/>). <c>CASAENGINE_COLOR_DEMO_PROBE_CASE=A|blend|B</c> places the two probe volumes so that the fixed
/// camera is in probe A alone (influence 1), in the blend zone of both, or in probe B alone.
/// <c>CASAENGINE_COLOR_DEMO_IBL_FORCE_BASE_LEVEL=1</c> forces the prefiltered band 0 for the probes too. The demo logs the generation
/// counter of the IBL cache and a summary at frame 600. Off by default: without the variable the scene is unchanged.
/// </para>
/// <para>
/// <c>CASAENGINE_DEMO_AA=Fxaa</c> (a harness variable of <see cref="DemosGame"/>) turns the FXAA pass on (plan T1.2, ADR-0114). The
/// measurement constants are <see cref="FxaaTextBorderRect"/>, <see cref="FxaaPanelBorderRect"/> and <see cref="FxaaEdgeSegment"/>; the
/// demo writes the number of FXAA passes of each view at the moment of the capture.
/// </para>
/// <para>
/// <c>CASAENGINE_COLOR_DEMO_SSAO=1</c> adds the measurement scene of the screen-space ambient occlusion (plan T3.3, ADR-0116): a wide
/// wall seen face on along the bottom of the image (the floor), a cube standing against it and a glass sphere in front of the foot of
/// the cube. It is independent of the other scene variables; <c>CASAENGINE_DEMO_SSAO=1</c> turns the SSAO on. Measurement constants:
/// <see cref="SsaoFootPixel"/>, <see cref="SsaoTopPixel"/>, <see cref="SsaoFarFloorPixel"/>, <see cref="SsaoSkyPixel"/> and
/// <see cref="SsaoGlassHollowPixel"/>. The demo writes the number of SSAO compositions of each view at the moment of the capture.
/// </para>
/// </summary>
public sealed class ColorPipelineDemo : Demo
{
    private const string LightIntensityVariable = "CASAENGINE_COLOR_DEMO_LIGHT_INTENSITY";
    private const string BufferedVariable = "CASAENGINE_COLOR_DEMO_BUFFERED";
    private const string PbrVariable = "CASAENGINE_COLOR_DEMO_PBR";
    private const string GlassVariable = "CASAENGINE_COLOR_DEMO_GLASS";
    private const string SsaoVariable = "CASAENGINE_COLOR_DEMO_SSAO";
    private const string IblVariable = "CASAENGINE_COLOR_DEMO_IBL";
    private const string IblForceBaseLevelVariable = "CASAENGINE_COLOR_DEMO_IBL_FORCE_BASE_LEVEL";
    private const string IblChangeEnvironmentVariable = "CASAENGINE_COLOR_DEMO_IBL_CHANGE_ENVIRONMENT";
    private const string ProbesVariable = "CASAENGINE_COLOR_DEMO_PROBES";
    private const string ProbeCaseVariable = "CASAENGINE_COLOR_DEMO_PROBE_CASE";
    private const int VirtualWidth = 1024;
    private const int VirtualHeight = 768;
    private const float ExposureStep = 0.25f;

    // ---- Measurement pixels (window coordinates, 1024 x 768, the same with and without CASAENGINE_COLOR_DEMO_BUFFERED) ----
    // Found by capturing the demo in Legacy (light intensity 1) and reading the image. Legacy values in the comments.

    /// <summary>Centre of the grey 128 plane (the plane spans x 462-561, y 201-300). Legacy value 128, 128, 128.</summary>
    public static readonly Point Grey128Pixel = new(512, 251);

    /// <summary>Centres of the five ramp planes, in the order 0, 64, 128, 191, 255. Legacy values 0, 64, 128, 191, 255.</summary>
    public static readonly Point[] RampPixels =
    {
        new(279, 251), new(395, 251), new(512, 251), new(628, 251), new(744, 251),
    };

    /// <summary>
    /// Three points on the lit (upper right) half of the grey 128 sphere, which is centred on (278, 467) with a radius of
    /// about 45 px. Legacy values 120, 128 and 111 (light intensity 1).
    /// </summary>
    public static readonly Point[] LitSpherePixels =
    {
        new(298, 440), new(292, 452), new(310, 460),
    };

    /// <summary>Centre of the emissive plane (x 645-778, y 425-508). Legacy value 255, 255, 255.</summary>
    public static readonly Point EmissivePixel = new(712, 467);

    /// <summary>Background pixel 8 px right of the last pixel of the emissive plane (x 778). Legacy value 20, 20, 20.</summary>
    public static readonly Point EmissiveEdgePixel = new(786, 467);

    /// <summary>Inside the solid interface panel (x 16-175, y 16-79, black border). Legacy value 200, 60, 60.</summary>
    public static readonly Point UiPixel = new(96, 48);

    /// <summary>Inside the screen text block (x 24-279, y 112-135). Legacy value 60, 200, 240.</summary>
    public static readonly Point TextPixel = new(150, 124);

    /// <summary>
    /// Centre of the 3D sprite (a 3 x 3 world-unit quad at world (10, 4), drawn by the sprite renderer in the scene, grey 128).
    /// Legacy value 128, 128, 128. In LinearHdr it reads like the grey 128 plane: it proves the sprite renderer binds the constant.
    /// </summary>
    public static readonly Point Sprite3dGrey128Pixel = new(842, 251);

    /// <summary>
    /// A pixel of the fixed particle (a 2 x 2 world-unit billboard at world (10, -2.5), grey 128, flipbook frame on the white
    /// texel of the fallback texture): 10 px right of and 10 px above its centre (842, 467), where the texture is plain white.
    /// Legacy value 128, 128, 128. In LinearHdr it reads like the grey 128 plane: it proves the particle renderer binds the constant.
    /// </summary>
    public static readonly Point ParticleGrey128Pixel = new(852, 457);

    // ---- FXAA measurements (CASAENGINE_DEMO_AA=Fxaa, plan T1.2, ADR-0114; window coordinates, 1024 x 768) ----
    // Found by capturing the demo in LinearHdr (AcesApprox, exposure 1, light intensity 1, scripts lin-v2.sh) with and without FXAA. In
    // that capture the ramp planes start at y 128 and the lit white-ish sphere is centred on (351, 498), radius about 60.

    /// <summary>
    /// A rectangle over the top edge and the top corners of the screen text block (x 24-279, y 112-135) on the plain background of the
    /// scene. The text is flushed after the FXAA pass, so every pixel must be the same with and without FXAA. It stops above the
    /// scene objects the block hides (the ramp planes start at y 128 below it, and one of them reaches x 280 just right of the block).
    /// </summary>
    public static readonly Rectangle FxaaTextBorderRect = new(18, 106, 266, 20);

    /// <summary>
    /// A rectangle over the whole edge of the solid interface panel (x 19-173, y 19-77), outset by 9 px. The interface is composed
    /// after the FXAA pass, so every pixel must be the same with and without FXAA.
    /// </summary>
    public static readonly Rectangle FxaaPanelBorderRect = new(10, 10, 172, 76);

    /// <summary>A vertical segment of 10 pixels through the top edge of the lit sphere centred on (351, 498): the first pixel is the background.</summary>
    public readonly record struct FxaaSegment(int X0, int Y0, int X1, int Y1);

    /// <summary>
    /// The segment crossing the top edge of that sphere (background, then the lit surface). The number of its pixels whose luminance
    /// is strictly between the luminances of its two ends must rise by at least 1 with FXAA: 3 without, 4 with (measured).
    /// </summary>
    public static readonly FxaaSegment FxaaEdgeSegment = new(360, 434, 360, 443);

    /// <summary>Perceptual roughness of the five <c>lit-pbr</c> spheres of <c>CASAENGINE_COLOR_DEMO_PBR=1</c>, left to right.</summary>
    public static readonly float[] PbrRoughness = { 0.1f, 0.3f, 0.5f, 0.7f, 0.9f };

    /// <summary>
    /// World position of the first PBR sphere and the step to the next one: below the other spheres (world y -7), on the same
    /// columns, radius 1.3 like them.
    /// </summary>
    private static readonly Vector3 PbrFirstSpherePosition = new(-7.0f, -7.0f, 0.0f);
    private const float PbrSphereSpacing = 3.5f;

    /// <summary>Centre pixel of each PBR sphere (window coordinates, 1024 x 768).</summary>
    public static readonly Point[] PbrSphereCentres =
    {
        new(277, 617), new(394, 617), new(511, 617), new(628, 617), new(745, 617),
    };

    /// <summary>One horizontal measurement line of a PBR sphere: row, first and last pixel (both on the sphere).</summary>
    public readonly record struct MeasurementLine(int Y, int XStart, int XEnd);

    /// <summary>
    /// Measurement line of each PBR sphere. The width of the highlight of a sphere is the number of pixels of its line whose
    /// luminance is above half of the maximum of that line. Each line
    /// runs 30 px either side of the sphere centre (all of it on the sphere) on the row of the brightest pixel inside the disk of
    /// radius 30 around the centre, found by capturing with light intensity 1, Reinhard, exposure 1.
    /// </summary>
    public static readonly MeasurementLine[] PbrMeasurementLines =
    {
        new(612, 247, 307), new(612, 364, 424), new(611, 481, 541), new(607, 598, 658), new(604, 715, 775),
    };

    // ---- Image-based lighting scene (CASAENGINE_COLOR_DEMO_IBL=1) ----

    /// <summary>World position of the mirror sphere (metal 1, white base colour, roughness 0), left of the other objects.</summary>
    public static readonly Vector3 IblSmoothSphereCentre = new(-11.5f, -1.0f, 0.0f);

    /// <summary>World position of the rough sphere (metal 1, white base colour, roughness <see cref="IblRoughSphereRoughness"/>).</summary>
    public static readonly Vector3 IblRoughSphereCentre = new(-11.5f, -6.0f, 0.0f);

    /// <summary>Radius of the two spheres, in world units (the primitive takes the diameter).</summary>
    public const float IblSphereRadius = 2.0f;

    /// <summary>Roughness of the second sphere.</summary>
    public const float IblRoughSphereRoughness = 0.6f;

    /// <summary>
    /// Five pixels of the mirror sphere (window coordinates, 1024 x 768), each inside one cell of the reflected checkerboard, not on
    /// an edge. The demo logs the reference colour of each one when <c>CASAENGINE_COLOR_DEMO_IBL=1</c>.
    /// </summary>
    public static readonly Point[] IblSmoothSpherePixels =
    {
        new(104, 414), new(114, 414), new(132, 462), new(114, 378), new(172, 422),
    };

    /// <summary>A line of 20 pixels through the centre of the rough sphere, on which the contrast of the reflection is measured.</summary>
    public static readonly MeasurementLine IblRoughMeasurementLine = new(585, 116, 135);

    private const int IblEnvironmentChangeFirstFrame = 200;
    private const int IblEnvironmentChangeSecondFrame = 400;
    private const int IblSummaryFrame = 600;

    // ---- Reflection probe scene (CASAENGINE_COLOR_DEMO_PROBES=1) ----

    /// <summary>Influence radius of both probes, in world units.</summary>
    public const float ProbeRadius = 10.0f;

    /// <summary>Blend distance of both probes: the weight is 1 up to <c>ProbeRadius - ProbeBlendDistance</c> from the probe, then fades to 0.</summary>
    public const float ProbeBlendDistance = 4.0f;

    // The camera of the demo is at (0, 0, 20). A probe at the camera is in its full-weight zone; a probe 40 units away is out of range.
    private static readonly Vector3 ProbeAtCamera = new(0.0f, 0.0f, 20.0f);
    private static readonly Vector3 ProbeOutOfRange = new(40.0f, 0.0f, 20.0f);

    // Blend case: the camera is 8 units from each probe, in the fade zone (6 to 10 units) where each raw weight is 0.5.
    private static readonly Vector3 ProbeBlendA = new(-8.0f, 0.0f, 20.0f);
    private static readonly Vector3 ProbeBlendB = new(8.0f, 0.0f, 20.0f);

    // Light-cell tints of the three cubes (display colours): the dark cells are 10 % of them.
    private static readonly Color ProbeGlobalTint = new(40, 230, 40);
    private static readonly Color ProbeATint = new(230, 40, 40);
    private static readonly Color ProbeBTint = new(40, 40, 230);

    // ---- Glass scene (CASAENGINE_COLOR_DEMO_GLASS=1) ----

    /// <summary>A horizontal measurement segment of the glass scene: row, first and last pixel (window coordinates, 1024 x 768).</summary>
    public readonly record struct GlassSegment(int Y, int XStart, int XEnd);

    /// <summary>
    /// The checkerboard of the glass scene starts at x 545 and its cells are 25 px wide; on the measured row the cells of odd column
    /// number (counted from x 545) are the light ones. The two segments below are 3 cells wide and 8 cells (200 px) apart, so they
    /// cross cells of the same parity: the first one is behind the glass sphere (centre about (658, 88), radius about 62 px), the
    /// second one is the control, on the half of the board without a sphere. Measured with Reinhard, exposure 1, light intensity 1:
    /// contrast behind 101, control 136.5 (ratio 0.74).
    /// </summary>
    public static readonly GlassSegment GlassBehindSegment = new(96, 620, 694);

    /// <summary>Control segment of the glass scene, see <see cref="GlassBehindSegment"/>.</summary>
    public static readonly GlassSegment GlassControlSegment = new(96, 820, 894);

    /// <summary>Left edge of the first checkerboard column of the glass scene and the width of a cell, in pixels.</summary>
    public const int GlassCheckerOriginX = 545;
    public const int GlassCheckerCellWidth = 25;

    /// <summary>The specular highlight of the glass sphere (upper right of the sphere). Reads 253, 253, 253 with the settings above.</summary>
    public static readonly Point GlassHighlightPixel = new(678, 62);

    private static readonly Vector3 GlassCheckerPosition = new(7.0f, 8.3f, 0.0f);
    private static readonly Vector3 GlassSpherePosition = new(4.0f, 8.3f, 1.0f);
    private const float GlassCheckerWidth = 12.0f;
    private const float GlassCheckerHeight = 4.2f;
    private const float GlassSphereDiameter = 3.6f;
    private const int GlassCheckerColumns = 16;
    private const int GlassCheckerRows = 6;
    private const int GlassCheckerCellSize = 8;

    // ---- SSAO scene (CASAENGINE_COLOR_DEMO_SSAO=1, plan T3.3, ADR-0116; window coordinates, 1024 x 768) ----

    // The floor is a wide wall seen face on, the cube stands against it, the glass sphere is in front of the foot of the cube.
    private static readonly Vector3 SsaoFloorPosition = new(0.0f, -6.5f, -2.05f);
    private const float SsaoFloorWidth = 40.0f;
    private const float SsaoFloorHeight = 4.0f;
    private static readonly Vector3 SsaoCubePosition = new(-5.0f, -6.4f, -1.0f);
    private const float SsaoCubeSize = 2.0f;
    private static readonly Vector3 SsaoGlassPosition = new(-2.8f, -6.9f, 1.2f);
    private const float SsaoGlassDiameter = 2.0f;

    // Measurement pixels of the SSAO scene. Measured in LinearHdr, AcesApprox, exposure 1, light intensity 1, with and without
    // CASAENGINE_DEMO_SSAO=1 (radius 0.5, intensity 1, bias 0.02): the levels are the grey level of the pixel, off then on.

    /// <summary>Floor just right of the cube's right face, at mid height, at the foot of the cube: 176 without SSAO, 153 with (23 levels darker).</summary>
    public static readonly Point SsaoFootPixel = new(345, 650);

    /// <summary>Floor just above the top edge of the cube: 176 without SSAO, 151 with (25 levels darker).</summary>
    public static readonly Point SsaoTopPixel = new(285, 610);

    /// <summary>Floor far from every object: 176 with and without SSAO.</summary>
    public static readonly Point SsaoFarFloorPixel = new(800, 650);

    /// <summary>Background (sky): 11 with and without SSAO.</summary>
    public static readonly Point SsaoSkyPixel = new(600, 350);

    /// <summary>
    /// A pixel seen through the glass sphere, over the floor in the hollow beside the cube: 164 without SSAO, 143 with (21 levels
    /// darker, as the floor it covers, 23 at <see cref="SsaoFootPixel"/>). The occlusion is multiplied into the whole scene target, so a
    /// transparent surface in front of a hollow is darkened with what is behind it (a written limit, ADR-0116).
    /// </summary>
    public static readonly Point SsaoGlassHollowPixel = new(345, 690);

    private static readonly Vector2 TextBlockPosition = new(24.0f, 112.0f);
    private static readonly Vector2 SpriteWorldPosition = new(8.5f, 5.5f);
    private static readonly Color Grey128 = new(128, 128, 128);

    private static readonly Color BackgroundColor = new(20, 20, 20);
    private static readonly Color PanelColor = new(200, 60, 60);
    private static readonly Color TextColor = new(60, 200, 240);
    private static readonly byte[] RampLevels = { 0, 64, 128, 191, 255 };

    private CasaEngineGame? _game;
    private VirtualResolutionSettings? _previousVirtualResolution;
    private bool _virtualResolutionChanged;
    private float _lightIntensity = 1.0f;
    private ColorPipelinePanelScreen? _panel;
    private Texture2D? _fontTexture;
    private Texture2D? _whiteTexture;
    private Texture2D? _checkerTexture;
    private SpriteFont? _font;
    private bool _iblEnabled;
    private bool _probesEnabled;
    private TextureCube?[]? _probeCubes;
    private TextureCube?[]? _iblCubes;
    private Color[][][]? _iblFacePixels;
    private int _iblCubeIndex;
    private CameraComponent? _camera;
    private int _frame;
    private int _lastLoggedIblGenerationCount;
    private int _iblEnvironmentChanges;

    public override string Title => "Colour pipeline demo";

    public override string Description =>
        "Measurement scene of the colour pipeline: a grey ramp, three lit spheres, an emissive plane, an interface panel and a screen text. Choose the pipeline with CASAENGINE_DEMO_COLOR_PIPELINE, CASAENGINE_DEMO_TONEMAPPER and CASAENGINE_DEMO_EXPOSURE, or with M, T and +/- in the demo.";

    public override IReadOnlyList<DemoControl> Controls { get; } = new DemoControl[]
    {
        DemoControl.ForActions("switch between the Legacy and the LinearHdr pipeline", "ColorPipeline.Mode"),
        DemoControl.ForActions("switch the tonemapping operator (AcesApprox, Reinhard)", "ColorPipeline.Tonemapper"),
        DemoControl.ForActions("raise the exposure by 0.25", "ColorPipeline.ExposureUp"),
        DemoControl.ForActions("lower the exposure by 0.25", "ColorPipeline.ExposureDown"),
        DemoControl.ForActions("turn the bloom on or off", "ColorPipeline.Bloom"),
    };

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        var world = game.GameManager.CurrentWorld;
        _lightIntensity = ReadLightIntensity();

        if (Environment.GetEnvironmentVariable(BufferedVariable) == "1")
        {
            // The demos have no virtual resolution; in this mode the demo plays in a buffered one. Put back in Clean.
            _previousVirtualResolution = game.RuntimeContext.ProjectSettings.VirtualResolution;
            game.RuntimeContext.ProjectSettings.VirtualResolution = new VirtualResolutionSettings
            {
                Width = VirtualWidth,
                Height = VirtualHeight,
                Mode = VirtualResolutionMode.BufferedFit,
            };
            _virtualResolutionChanged = true;
        }

        for (int i = 0; i < RampLevels.Length; i++)
        {
            byte level = RampLevels[i];
            AddPrimitive(
                world,
                game.GraphicsDevice,
                $"Ramp {level}",
                new BoxPrimitive(3.0f, 3.0f, 0.1f),
                new Vector3(-7.0f + (3.5f * i), 4.0f, 0.0f),
                new UnlitTextureMaterial { Name = $"Ramp{level}", Tint = new Color(level, level, level) });
        }

        var sphereColors = new[] { new Color(128, 128, 128), new Color(255, 255, 255), new Color(200, 100, 50) };
        for (int i = 0; i < sphereColors.Length; i++)
        {
            AddPrimitive(
                world,
                game.GraphicsDevice,
                $"Sphere {i}",
                new SpherePrimitive(2.6f, 32),
                new Vector3(-7.0f + (3.5f * i), -2.5f, 0.0f),
                new LitDiffuseMaterial
                {
                    Name = $"Sphere{i}",
                    DiffuseColor = sphereColors[i],
                    SpecularColor = Vector3.Zero,
                });
        }

        AddPrimitive(
            world,
            game.GraphicsDevice,
            "Emissive plane",
            new BoxPrimitive(4.0f, 2.5f, 0.1f),
            new Vector3(6.0f, -2.5f, 0.0f),
            new LitDiffuseMaterial
            {
                Name = "Emissive",
                DiffuseColor = Color.Black,
                SpecularColor = Vector3.Zero,
                EmissiveColor = new Vector3(4.0f),
            });


        if (Environment.GetEnvironmentVariable(PbrVariable) == "1")
        {
            for (int i = 0; i < PbrRoughness.Length; i++)
            {
                AddPrimitive(
                    world,
                    game.GraphicsDevice,
                    $"PBR sphere {i}",
                    new SpherePrimitive(2.6f, 32),
                    PbrFirstSpherePosition + new Vector3(PbrSphereSpacing * i, 0.0f, 0.0f),
                    new LitPbrMaterial
                    {
                        Name = $"PbrSphere{i}",
                        BaseColor = Color.Black,
                        Metallic = 0.0f,
                        Roughness = PbrRoughness[i],
                    });
            }
        }

        _iblEnabled = Environment.GetEnvironmentVariable(IblVariable) == "1";
        if (_iblEnabled)
        {
            AddIblScene(world, game.GraphicsDevice);
        }

        _probesEnabled = Environment.GetEnvironmentVariable(ProbesVariable) == "1";
        if (_probesEnabled)
        {
            AddProbeScene(world, game.GraphicsDevice);
        }

        if (Environment.GetEnvironmentVariable(GlassVariable) == "1")
        {
            AddGlassScene(world, game.GraphicsDevice);
        }

        if (Environment.GetEnvironmentVariable(SsaoVariable) == "1")
        {
            AddSsaoScene(world, game.GraphicsDevice);
        }

        CreateBlockFont(game.GraphicsDevice);

        _whiteTexture = new Texture2D(game.GraphicsDevice, 1, 1);
        _whiteTexture.SetData(new[] { Color.White });
    }

    public override void ConfigureSceneLighting(World world)
    {
        var environmentSettings = world.EnvironmentSettings;
        environmentSettings.Type = EnvironmentType.None;
        environmentSettings.BackgroundMode = EnvironmentBackgroundMode.SolidColor;
        environmentSettings.BackgroundColor = BackgroundColor;
        environmentSettings.BackgroundCubemap = null;
        environmentSettings.SpecularEnvironmentCubemap = null;
        environmentSettings.AmbientColor = new Vector3(0.1f);
        environmentSettings.AmbientIntensity = 1.0f;
        environmentSettings.SpecularIntensity = 0.0f;

        if (_iblEnabled && _iblCubes is not null)
        {
            // The specular cube of the world is the procedural checkerboard cube; its intensity is 1 so the reference is the texel itself.
            environmentSettings.SpecularEnvironmentCubemap = _iblCubes[0];
            environmentSettings.SpecularIntensity = 1.0f;
        }

        if (_probesEnabled && _probeCubes is not null)
        {
            // The global cube of the probe scene is the green one; the probes override it where they have influence.
            environmentSettings.SpecularEnvironmentCubemap = _probeCubes[0];
            environmentSettings.SpecularIntensity = 1.0f;
        }

        environmentSettings.MarkDirty();

        // Light travelling to the left, down and away from the camera: the right half of each sphere is lit.
        DemoSceneLightRig.AddDirectionalLight(
            world,
            "ColorPipelineLight",
            new Vector3(-0.5f, -0.45f, -0.74f),
            Color.White,
            Color.Black,
            _lightIntensity);
    }

    public override CameraComponent CreateCamera(CasaEngineGame game)
    {
        var entity = new Entity { Name = "ColorPipelineCamera" };
        var camera = new PerspectiveCameraComponent();
        entity.RootComponent = camera;
        entity.Initialize();
        game.GameManager.CurrentWorld.AddEntity(entity);
        return camera;
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        _camera = camera;
        camera.SetPositionAndTarget(new Vector3(0.0f, 0.0f, 20.0f), Vector3.Zero);
        Logs.WriteInfo($"[ColorPipelineDemo] Virtual resolution in force: {_game?.ActiveVirtualResolution?.Mode.ToString() ?? "none"}.");

        // The per-view UI runtime exists once the world is loaded, so the panel is pushed here (same ordering as UIOverlayDemo).
        var uiView = _game?.GameManager.ViewManager.GetActiveUIView();
        if (uiView == null)
        {
            return;
        }

        _panel = new ColorPipelinePanelScreen(PanelColor);
        uiView.PushScreen(_panel);
    }

    public override void Update(GameTime gameTime)
    {
        if (_game == null)
        {
            return;
        }

        // Screen text: a block of the solid glyph, queued for the 2D renderer every frame.
        if (_font != null)
        {
            _game.Renderer2DComponent.DrawText(_font, "AAAAAAAA", TextBlockPosition, 0.0f, new Vector2(4.0f, 2.0f), TextColor, 0.0f);
        }

        SubmitMeasurementSpriteAndParticle();

        if (_iblEnabled)
        {
            UpdateIblScene(_game);
        }
        else if (_probesEnabled)
        {
            UpdateProbeScene(_game);
        }

        var settings = _game.GameManager.CurrentWorld.EnvironmentSettings;
        var colorPipeline = settings.ColorPipeline;
        bool changed = false;

        if (DemoInput.WasPressed(_game, "ColorPipeline.Mode"))
        {
            colorPipeline.Mode = colorPipeline.Mode == ColorPipelineMode.Legacy ? ColorPipelineMode.LinearHdr : ColorPipelineMode.Legacy;
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

        if (DemoInput.WasPressed(_game, "ColorPipeline.Bloom"))
        {
            colorPipeline.BloomEnabled = !colorPipeline.BloomEnabled;
            changed = true;
        }

        if (changed)
        {
            settings.MarkDirty();
            Logs.WriteInfo($"[ColorPipelineDemo] {colorPipeline.Mode}, {colorPipeline.Tonemapper}, exposure {colorPipeline.Exposure}, bloom {colorPipeline.BloomEnabled}.");
        }
    }

    /// <summary>
    /// The 3D sprite and the fixed particle are queued every frame (the renderers clear their queues at each flush). The sprite is
    /// a 3 x 3 quad centred on world (10, 4): position is its top-left corner, the origin is (0, 0).
    /// </summary>
    private void SubmitMeasurementSpriteAndParticle()
    {
        if (_game == null)
        {
            return;
        }

        if (_whiteTexture != null)
        {
            _game.SpriteRendererComponent.DrawSprite(
                _whiteTexture,
                new Rectangle(0, 0, 1, 1),
                Point.Zero,
                SpriteWorldPosition,
                0.0f,
                new Vector2(3.0f, 3.0f),
                Grey128,
                0.0f,
                SpriteEffects.None);
        }

        // The fallback texture of the particle renderer is 2 x 2 (magenta, white / white, magenta): frame 1 of a 2 x 2 flipbook
        // is the top-right quarter, centred on its white texel.
        _game.ParticleRendererComponent.Submit(new ParticleRenderPacket
        {
            Position = new Vector3(10.0f, -2.5f, 0.0f),
            Size = new Vector2(2.0f, 2.0f),
            Color = Grey128,
            Alpha = 1.0f,
            TextureAssetId = Guid.Empty,
            FlipbookColumns = 2,
            FlipbookRows = 2,
            FlipbookFrameIndex = 1,
            BlendMode = ParticleBlendMode.Alpha,
            DepthTest = false,
            DepthWrite = false,
        });
    }

    public override void OnScreenResized(CasaEngineGame game, int width, int height)
    {
        game.GameManager.ViewManager.ApplyBackBufferLayout(width, height);
    }

    public override void Clean()
    {
        var game = _game;
        if (game != null)
        {
            if (_panel != null)
            {
                game.GameManager.ViewManager.GetActiveUIView()?.RemoveScreen(_panel);
            }

            if (game.GameManager.CurrentWorld is { } world)
            {
                world.EnvironmentSettings.ResetToDefaults();
                world.EnvironmentSettings.MarkDirty();
            }

            if (_virtualResolutionChanged)
            {
                game.RuntimeContext.ProjectSettings.VirtualResolution = _previousVirtualResolution;
            }
        }

        _panel = null;
        _font = null;
        _fontTexture?.Dispose();
        _fontTexture = null;
        _whiteTexture?.Dispose();
        _whiteTexture = null;
        _checkerTexture?.Dispose();
        _checkerTexture = null;
        if (_iblCubes != null)
        {
            foreach (TextureCube? cube in _iblCubes)
            {
                cube?.Dispose();
            }
        }

        if (_probeCubes != null)
        {
            _game?.GameManager.CurrentWorld.ReflectionProbes.Clear();
            foreach (TextureCube? cube in _probeCubes)
            {
                cube?.Dispose();
            }
        }

        _probeCubes = null;
        _probesEnabled = false;
        _iblCubes = null;
        _iblFacePixels = null;
        _iblEnabled = false;
        _iblCubeIndex = 0;
        _iblEnvironmentChanges = 0;
        _lastLoggedIblGenerationCount = 0;
        _frame = 0;
        _camera = null;
        LitPbrMaterial.DebugForceIblBaseLevel = false;
        _previousVirtualResolution = null;
        _virtualResolutionChanged = false;
        _game = null;
    }

    /// <summary>
    /// A checkerboard quad (light and dark cells) and, in front of its left half, a glass sphere: <see cref="LitPbrMaterial"/> in
    /// the transparent queue, alpha-blended (premultiplied output), reading the depth buffer without writing it.
    /// </summary>
    private void AddGlassScene(World world, GraphicsDevice graphicsDevice)
    {
        int width = GlassCheckerColumns * GlassCheckerCellSize;
        int height = GlassCheckerRows * GlassCheckerCellSize;
        var light = new Color(230, 230, 230);
        var dark = new Color(30, 30, 30);
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int column = x / GlassCheckerCellSize;
                int row = y / GlassCheckerCellSize;
                pixels[(y * width) + x] = ((column + row) % 2 == 0) ? light : dark;
            }
        }

        _checkerTexture = new Texture2D(graphicsDevice, width, height);
        _checkerTexture.SetData(pixels);
        _checkerTexture.Name = "GlassChecker";

        AddPrimitive(
            world,
            graphicsDevice,
            "Glass checkerboard",
            new BoxPrimitive(GlassCheckerWidth, GlassCheckerHeight, 0.1f),
            GlassCheckerPosition,
            new UnlitTextureMaterial { Name = "GlassChecker", BasColor = _checkerTexture });

        AddPrimitive(
            world,
            graphicsDevice,
            "Glass sphere",
            new SpherePrimitive(GlassSphereDiameter, 32),
            GlassSpherePosition,
            new LitPbrMaterial
            {
                Name = "GlassSphere",
                BaseColor = new Color(255, 255, 255, 64),
                Metallic = 0.0f,
                Roughness = 0.05f,
                Queue = RenderQueue.Transparent,
                BlendState = BlendState.AlphaBlend,
                DepthStencilState = DepthStencilState.DepthRead,
            });
    }


    /// <summary>
    /// The SSAO scene (plan T3.3, ADR-0116): a wide wall facing the camera along the bottom of the image (the "floor" of the
    /// measurement: a surface seen face on), a cube standing on it and a glass sphere (<see cref="LitPbrMaterial"/> in the transparent
    /// queue) in front of the foot of the cube. The camera is the fixed one of the demo, so every pixel below is the same each run.
    /// </summary>
    private static void AddSsaoScene(World world, GraphicsDevice graphicsDevice)
    {
        AddPrimitive(
            world,
            graphicsDevice,
            "SSAO floor",
            new BoxPrimitive(SsaoFloorWidth, SsaoFloorHeight, 0.1f),
            SsaoFloorPosition,
            new LitDiffuseMaterial { Name = "SsaoFloor", DiffuseColor = new Color(170, 170, 170), SpecularColor = Vector3.Zero });

        AddPrimitive(
            world,
            graphicsDevice,
            "SSAO cube",
            new BoxPrimitive(SsaoCubeSize, SsaoCubeSize, SsaoCubeSize),
            SsaoCubePosition,
            new LitDiffuseMaterial { Name = "SsaoCube", DiffuseColor = new Color(200, 110, 60), SpecularColor = Vector3.Zero });

        AddPrimitive(
            world,
            graphicsDevice,
            "SSAO glass sphere",
            new SpherePrimitive(SsaoGlassDiameter, 32),
            SsaoGlassPosition,
            new LitPbrMaterial
            {
                Name = "SsaoGlass",
                BaseColor = new Color(255, 255, 255, 64),
                Metallic = 0.0f,
                Roughness = 0.05f,
                Queue = RenderQueue.Transparent,
                BlendState = BlendState.AlphaBlend,
                DepthStencilState = DepthStencilState.DepthRead,
            });
    }
    /// <summary>
    /// The procedural environment cubes (three, with the tints rotated, for the environment changes), the two metal spheres and the
    /// debug switch of the prefiltering.
    /// </summary>
    private void AddIblScene(World world, GraphicsDevice graphicsDevice)
    {
        _iblFacePixels = new[]
        {
            ColorPipelineIblScene.BuildFacePixels(0),
            ColorPipelineIblScene.BuildFacePixels(1),
            ColorPipelineIblScene.BuildFacePixels(2),
        };
        _iblCubes = new TextureCube?[_iblFacePixels.Length];
        for (int i = 0; i < _iblFacePixels.Length; i++)
        {
            _iblCubes[i] = ColorPipelineIblScene.CreateCube(graphicsDevice, _iblFacePixels[i], $"ColorPipelineIblCube{i}");
        }

        LitPbrMaterial.DebugForceIblBaseLevel = Environment.GetEnvironmentVariable(IblForceBaseLevelVariable) == "1";

        AddPrimitive(
            world,
            graphicsDevice,
            "IBL mirror sphere",
            new SpherePrimitive(IblSphereRadius * 2.0f, 32),
            IblSmoothSphereCentre,
            new LitPbrMaterial { Name = "IblMirror", BaseColor = Color.White, Metallic = 1.0f, Roughness = 0.0f });

        AddPrimitive(
            world,
            graphicsDevice,
            "IBL rough sphere",
            new SpherePrimitive(IblSphereRadius * 2.0f, 32),
            IblRoughSphereCentre,
            new LitPbrMaterial { Name = "IblRough", BaseColor = Color.White, Metallic = 1.0f, Roughness = IblRoughSphereRoughness });
    }

    /// <summary>
    /// The probe scene (plan T7.1): the three cubes (green global, red probe A, blue probe B), the two probes placed according to
    /// <c>CASAENGINE_COLOR_DEMO_PROBE_CASE</c> and one metal sphere of roughness 0.6.
    /// </summary>
    private void AddProbeScene(World world, GraphicsDevice graphicsDevice)
    {
        _probeCubes = new TextureCube?[]
        {
            ColorPipelineIblScene.CreateCube(graphicsDevice, ColorPipelineIblScene.BuildUniformTintFacePixels(ProbeGlobalTint), "ColorPipelineProbeGlobalCube"),
            ColorPipelineIblScene.CreateCube(graphicsDevice, ColorPipelineIblScene.BuildUniformTintFacePixels(ProbeATint), "ColorPipelineProbeACube"),
            ColorPipelineIblScene.CreateCube(graphicsDevice, ColorPipelineIblScene.BuildUniformTintFacePixels(ProbeBTint), "ColorPipelineProbeBCube"),
        };

        LitPbrMaterial.DebugForceIblBaseLevel = Environment.GetEnvironmentVariable(IblForceBaseLevelVariable) == "1";

        string probeCase = Environment.GetEnvironmentVariable(ProbeCaseVariable) ?? "A";
        Vector3 positionA;
        Vector3 positionB;
        switch (probeCase.ToLowerInvariant())
        {
            case "blend":
                positionA = ProbeBlendA;
                positionB = ProbeBlendB;
                break;
            case "b":
                positionA = ProbeOutOfRange;
                positionB = ProbeAtCamera;
                break;
            default:
                if (!string.Equals(probeCase, "A", StringComparison.OrdinalIgnoreCase))
                {
                    Logs.WriteWarning($"[ColorPipelineDemo] {ProbeCaseVariable} '{probeCase}' is not A, blend or B; A is used.");
                }

                positionA = ProbeAtCamera;
                positionB = ProbeOutOfRange;
                break;
        }

        world.ReflectionProbes.Add(new ReflectionProbe
        {
            Name = "Probe A (red)",
            Position = positionA,
            InfluenceRadius = ProbeRadius,
            BlendDistance = ProbeBlendDistance,
            SpecularCubemap = _probeCubes[1],
        });
        world.ReflectionProbes.Add(new ReflectionProbe
        {
            Name = "Probe B (blue)",
            Position = positionB,
            InfluenceRadius = ProbeRadius,
            BlendDistance = ProbeBlendDistance,
            SpecularCubemap = _probeCubes[2],
        });

        AddPrimitive(
            world,
            graphicsDevice,
            "Probe rough sphere",
            new SpherePrimitive(IblSphereRadius * 2.0f, 32),
            IblRoughSphereCentre,
            new LitPbrMaterial { Name = "ProbeRough", BaseColor = Color.White, Metallic = 1.0f, Roughness = IblRoughSphereRoughness });

        Logs.WriteInfo($"[ColorPipelineDemo] Probe scene, case {probeCase}: probe A at {positionA}, probe B at {positionB}, radius {ProbeRadius}, blend distance {ProbeBlendDistance}.");
    }

    /// <summary>
    /// Per frame work of the probe scene: the log of every change of the generation counter of the IBL cache (once per change,
    /// never per frame) and a summary at frame 600.
    /// </summary>
    private void UpdateProbeScene(CasaEngineGame game)
    {
        _frame++;
        var cache = game.IblCache;
        if (cache == null)
        {
            return;
        }

        if (cache.GenerationCount != _lastLoggedIblGenerationCount)
        {
            _lastLoggedIblGenerationCount = cache.GenerationCount;
            Logs.WriteInfo($"[ColorPipelineDemo] Probe scene generation count {cache.GenerationCount} at frame {_frame}, outside the view loop: {cache.GeneratedOutsideViewLoop}.");
        }

        if (_frame == IblSummaryFrame)
        {
            Logs.WriteInfo($"[ColorPipelineDemo] Probe scene after {_frame} frames: {cache.GenerationCount} generation(s) for {cache.EntryCount} source cube(s), outside the view loop: {cache.GeneratedOutsideViewLoop}.");
        }
    }

    /// <summary>
    /// Per frame work of the IBL scene: the environment changes asked for, the log of every change of the generation counter of the
    /// IBL cache (once per change, never per frame) and, at frame 3, the reference colour of the pixels of the mirror sphere.
    /// </summary>
    private void UpdateIblScene(CasaEngineGame game)
    {
        _frame++;
        var settings = game.GameManager.CurrentWorld.EnvironmentSettings;

        if (Environment.GetEnvironmentVariable(IblChangeEnvironmentVariable) == "1" && _iblCubes is not null
            && (_frame == IblEnvironmentChangeFirstFrame || _frame == IblEnvironmentChangeSecondFrame))
        {
            _iblCubeIndex = _frame == IblEnvironmentChangeFirstFrame ? 1 : 2;
            settings.SpecularEnvironmentCubemap = _iblCubes[_iblCubeIndex];
            settings.MarkDirty();
            _iblEnvironmentChanges++;
            Logs.WriteInfo($"[ColorPipelineDemo] IBL environment change {_iblEnvironmentChanges} at frame {_frame}: cube {_iblCubeIndex}.");
        }

        var cache = game.IblCache;
        if (cache != null && cache.GenerationCount != _lastLoggedIblGenerationCount)
        {
            _lastLoggedIblGenerationCount = cache.GenerationCount;
            Logs.WriteInfo($"[ColorPipelineDemo] IBL generation count {cache.GenerationCount} at frame {_frame}, outside the view loop: {cache.GeneratedOutsideViewLoop}.");
        }

        if (_frame == IblSummaryFrame && cache != null)
        {
            Logs.WriteInfo($"[ColorPipelineDemo] IBL after {_frame} frames: {cache.GenerationCount} generation(s) for {_iblEnvironmentChanges} environment change(s) (plus the first environment), outside the view loop: {cache.GeneratedOutsideViewLoop}.");
        }

        if (_frame == 3)
        {
            LogIblReferences(game, settings);
        }
    }

    /// <summary>
    /// The reference colour of each pixel of <see cref="IblSmoothSpherePixels"/>: the texel of the source cube read on the CPU along
    /// the reflection of the camera ray, decoded, multiplied by the specular intensity of the environment, tonemapped and encoded
    /// like the view does (plan T6.3 (a)).
    /// </summary>
    private void LogIblReferences(CasaEngineGame game, WorldEnvironmentSettings settings)
    {
        if (_camera == null || _iblFacePixels == null)
        {
            return;
        }

        var pipeline = settings.ColorPipeline;
        var presentation = game.GraphicsDevice.PresentationParameters;
        foreach (Point pixel in IblSmoothSpherePixels)
        {
            if (!ColorPipelineIblScene.TryComputeReflection(
                    _camera.ViewMatrix, _camera.ProjectionMatrix, presentation.BackBufferWidth, presentation.BackBufferHeight,
                    pixel, IblSmoothSphereCentre, IblSphereRadius, out Vector3 reflection))
            {
                Logs.WriteWarning($"[ColorPipelineDemo] IBL reference pixel ({pixel.X}, {pixel.Y}) misses the mirror sphere.");
                continue;
            }

            Vector3 reference = ColorPipelineIblScene.ReferenceColor(
                _iblFacePixels[0], reflection, pipeline.Mode, pipeline.Tonemapper, pipeline.Exposure, settings.SpecularIntensity);
            Logs.WriteInfo(string.Create(
                CultureInfo.InvariantCulture,
                $"[ColorPipelineDemo] IBL reference pixel ({pixel.X}, {pixel.Y}) = {MathF.Round(reference.X * 255.0f)}, {MathF.Round(reference.Y * 255.0f)}, {MathF.Round(reference.Z * 255.0f)}."));
        }
    }

    private static float ReadLightIntensity()
    {
        var text = Environment.GetEnvironmentVariable(LightIntensityVariable);
        if (string.IsNullOrWhiteSpace(text))
        {
            return 1.0f;
        }

        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var intensity) && intensity >= 0.0f)
        {
            return intensity;
        }

        Logs.WriteWarning($"[ColorPipelineDemo] {LightIntensityVariable} '{text}' is not a non-negative number; 1 is used.");
        return 1.0f;
    }

    /// <summary>
    /// A font whose only glyph is a solid 8 x 12 block, so the text of the demo is a block of one known colour that a capture
    /// can read back without depending on a font file.
    /// </summary>
    private void CreateBlockFont(GraphicsDevice graphicsDevice)
    {
        const int glyphWidth = 8;
        const int glyphHeight = 12;
        var pixels = new Color[glyphWidth * glyphHeight];
        Array.Fill(pixels, Color.White);
        _fontTexture = new Texture2D(graphicsDevice, glyphWidth, glyphHeight);
        _fontTexture.SetData(pixels);

        var bounds = new Rectangle(0, 0, glyphWidth, glyphHeight);
        _font = new SpriteFont(
            _fontTexture,
            new List<Rectangle> { bounds },
            new List<Rectangle> { bounds },
            new List<char> { 'A' },
            glyphHeight,
            0.0f,
            new List<Vector3> { new(0.0f, glyphWidth, 0.0f) },
            'A');
    }

    private static void AddPrimitive(
        World world,
        GraphicsDevice graphicsDevice,
        string name,
        GeometricPrimitive primitive,
        Vector3 position,
        MaterialBase material)
    {
        var entity = new Entity { Name = name };
        var mesh = new StaticModelComponent();
        entity.RootComponent = mesh;
        mesh.StaticModel = StaticModel.CreateFromPrimitive(primitive);
        mesh.StaticModel.Meshes[0].Initialize(graphicsDevice);
        mesh.StaticModel.Meshes[0].Material = material;
        mesh.LocalPosition = position;
        world.AddEntity(entity);
    }
}
