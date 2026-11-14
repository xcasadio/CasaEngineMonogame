using System;
using System.Collections.Generic;
using System.Globalization;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// The measurement scene of the colour pipeline (ADR-0095, <c>ai-agent/tasks/pbr-rendering-tasks.md</c>, phase 1). A fixed
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
/// included. Keys: M changes the mode, T the operator, = / + raises the exposure by 0.25 and - lowers it.
/// </para>
/// </summary>
public sealed class ColorPipelineDemo : Demo
{
    private const string LightIntensityVariable = "CASAENGINE_COLOR_DEMO_LIGHT_INTENSITY";
    private const string BufferedVariable = "CASAENGINE_COLOR_DEMO_BUFFERED";
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

    private static readonly Vector2 TextBlockPosition = new(24.0f, 112.0f);

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
    private SpriteFont? _font;
    private KeyboardState _previousKeyboard;

    public override string Title => "Colour pipeline demo";

    public override string Description =>
        "Measurement scene of the colour pipeline: a grey ramp, three lit spheres, an emissive plane, an interface panel and a screen text. Choose the pipeline with CASAENGINE_DEMO_COLOR_PIPELINE, CASAENGINE_DEMO_TONEMAPPER and CASAENGINE_DEMO_EXPOSURE, or with M, T and +/- in the demo.";

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

        CreateBlockFont(game.GraphicsDevice);
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
        var camera = new CameraLookAtComponent();
        entity.RootComponent = camera;
        entity.Initialize();
        game.GameManager.CurrentWorld.AddEntity(entity);
        return camera;
    }

    public override void InitializeCamera(CameraComponent camera)
    {
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

        KeyboardState keyboard = DemoKeyboard.Read(_game);
        var settings = _game.GameManager.CurrentWorld.EnvironmentSettings;
        var colorPipeline = settings.ColorPipeline;
        bool changed = false;

        if (WasPressed(keyboard, Keys.M))
        {
            colorPipeline.Mode = colorPipeline.Mode == ColorPipelineMode.Legacy ? ColorPipelineMode.LinearHdr : ColorPipelineMode.Legacy;
            changed = true;
        }

        if (WasPressed(keyboard, Keys.T))
        {
            colorPipeline.Tonemapper = colorPipeline.Tonemapper == TonemapOperator.AcesApprox ? TonemapOperator.Reinhard : TonemapOperator.AcesApprox;
            changed = true;
        }

        if (WasPressed(keyboard, Keys.OemPlus) || WasPressed(keyboard, Keys.Add))
        {
            colorPipeline.Exposure += ExposureStep;
            changed = true;
        }

        if (WasPressed(keyboard, Keys.OemMinus) || WasPressed(keyboard, Keys.Subtract))
        {
            colorPipeline.Exposure -= ExposureStep;
            changed = true;
        }

        _previousKeyboard = keyboard;

        if (changed)
        {
            settings.MarkDirty();
            Logs.WriteInfo($"[ColorPipelineDemo] {colorPipeline.Mode}, {colorPipeline.Tonemapper}, exposure {colorPipeline.Exposure}.");
        }
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
        _previousVirtualResolution = null;
        _virtualResolutionChanged = false;
        _game = null;
    }

    private bool WasPressed(KeyboardState keyboard, Keys key)
        => keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

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
