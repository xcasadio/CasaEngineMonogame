using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Rendering.Shadows;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Validates forward directional shadows on static meshes. With <c>CASAENGINE_SHADOW_DEMO_VALIDATION=1</c> the scene
/// switches to a measurement configuration used to check the lighting maths pixel by pixel (see
/// <c>ai-agent/tasks/lighting-ambient-shadow-tasks.md</c>): world ambient (0.5, 0.5, 0.5) at intensity 1, the fill light
/// off, the grounds replaced by a textured receiver (a solid 128/255 texture generated in memory, white diffuse colour,
/// material ambient 1, no specular), and a reflective sphere with a material ambient of 0 that uses the scene reflection
/// cube. <c>CASAENGINE_SHADOW_DEMO_REFLECTION_CUBE=1</c> (in that configuration) binds a solid (0.2, 0.6, 1.0) cube
/// generated in memory as the world's specular environment cube. <c>CASAENGINE_SHADOW_DEMO_AMBIENT_SOURCE=Flat|Auto</c> (in that
/// configuration) sets <see cref="WorldEnvironmentSettings.AmbientSource"/>. <c>CASAENGINE_SHADOW_DEMO_DETAIL=1</c> (in that
/// configuration) binds a solid detail texture of value 64/255 with a tiling of 4 to the receiver
/// (<see cref="LitDiffuseMaterial.DetailTexture"/>). <c>CASAENGINE_SHADOW_DEMO_MODE=MultiplyLitColor</c> and <c>CASAENGINE_SHADOW_DEMO_COLOR=r,g,b</c> (in that configuration) set <see cref="ShadowSettings.Mode"/> and <see cref="ShadowSettings.ShadowColor"/>. <c>CASAENGINE_SHADOW_DEMO_FILTER_RADIUS</c> (in that configuration) sets <see cref="ShadowSettings.FilterRadius"/>. <c>CASAENGINE_SHADOW_DEMO_FADE_DISTANCE</c> and <c>CASAENGINE_SHADOW_DEMO_MAX_DISTANCE</c> (in that configuration) set <see cref="ShadowSettings.FadeDistance"/> and <see cref="ShadowSettings.MaxDistance"/> (48 by default). Without these variables the scene is unchanged.
/// <para><c>CASAENGINE_SHADOW_DEMO_MIPMAP_CHECK=1</c> additionally runs a real-device check of <see cref="Texture2DLoader.GenerateMipmaps"/>: it
/// writes a PNG (odd, non-square) and a TGA to a temporary folder, loads them with the option on, logs the level count and
/// sizes, compares level 0 with <c>Texture2D.FromStream</c> and every level with the CPU chain, logs a verdict, and deletes the files.</para>
/// </summary>
public sealed class StaticShadowValidationDemo : Demo
{
    private const string ValidationVariable = "CASAENGINE_SHADOW_DEMO_VALIDATION";
    private const string ReflectionCubeVariable = "CASAENGINE_SHADOW_DEMO_REFLECTION_CUBE";
    private const string AmbientSourceVariable = "CASAENGINE_SHADOW_DEMO_AMBIENT_SOURCE";
    private const string DetailVariable = "CASAENGINE_SHADOW_DEMO_DETAIL";
    private const string ModeVariable = "CASAENGINE_SHADOW_DEMO_MODE";
    private const string ColorVariable = "CASAENGINE_SHADOW_DEMO_COLOR";
    private const string FilterRadiusVariable = "CASAENGINE_SHADOW_DEMO_FILTER_RADIUS";
    private const string FadeDistanceVariable = "CASAENGINE_SHADOW_DEMO_FADE_DISTANCE";
    private const string MaxDistanceVariable = "CASAENGINE_SHADOW_DEMO_MAX_DISTANCE";
    private const string MipmapCheckVariable = "CASAENGINE_SHADOW_DEMO_MIPMAP_CHECK";

    private static readonly Color MeasurementReceiverTextureColor = new(128, 128, 128, 255);
    private static readonly Color MeasurementCubeColor = new(51, 153, 255, 255);
    private static readonly Color MeasurementDetailTextureColor = new(64, 64, 64, 255);
    private const float MeasurementDetailTiling = 4.0f;

    private CasaEngineGame? _game;
    private bool _measurementConfiguration;
    private Texture2D? _measurementReceiverTexture;
    private Texture2D? _measurementDetailTexture;
    private TextureCube? _measurementReflectionCube;

    public override string Title => "Static shadow validation demo";

    public override string Description => "Validates forward directional shadows on static meshes with three columns: left keeps normal cast/receive, center disables ReceiveShadows on the receivers, and right disables CastShadows on the caster while keeping the same world shadow-map settings.";

    public override CameraComponent CreateCamera(CasaEngineGame game)
    {
        var entity = new Entity { Name = "StaticShadowValidationCamera" };
        var camera = new CameraLookAtComponent();
        entity.RootComponent = camera;
        entity.Initialize();
        game.GameManager.CurrentWorld.AddEntity(entity);
        return camera;
    }

    public override void ConfigureSceneLighting(World world)
    {
        var environmentSettings = world.EnvironmentSettings;
        environmentSettings.Type = EnvironmentType.None;
        environmentSettings.BackgroundMode = EnvironmentBackgroundMode.SolidColor;
        environmentSettings.BackgroundColor = new Color(30, 36, 44);
        environmentSettings.BackgroundCubemap = null;
        environmentSettings.SpecularEnvironmentCubemap = null;
        environmentSettings.AmbientColor = _measurementConfiguration ? new Vector3(0.5f) : new Vector3(0.1f, 0.11f, 0.13f);
        environmentSettings.AmbientIntensity = _measurementConfiguration ? 1.0f : 0.55f;
        environmentSettings.SpecularIntensity = 0.12f;
        if (_measurementReflectionCube is not null)
        {
            environmentSettings.SpecularEnvironmentCubemap = _measurementReflectionCube;
        }

        if (_measurementConfiguration)
        {
            environmentSettings.AmbientSource = ReadAmbientSource();
        }

        environmentSettings.Shadows.Enabled = true;
        environmentSettings.Shadows.Resolution = 2048;
        environmentSettings.Shadows.DepthBias = 0.001f;
        environmentSettings.Shadows.NormalBias = 0.002f;
        environmentSettings.Shadows.MaxDistance = 48.0f;

        if (_measurementConfiguration)
        {
            ApplyShadowStyleVariables(environmentSettings.Shadows);
        }

        environmentSettings.MarkDirty();

        AddDirectionalLight(
            world,
            "StaticShadowKeyLight",
            new Vector3(0.42f, -0.8f, -0.42f),
            new Color(255, 242, 214),
            Color.White,
            1.55f,
            castShadows: true);

        if (!_measurementConfiguration)
        {
            AddDirectionalLight(
                world,
                "StaticShadowFillLight",
                new Vector3(-0.2f, -0.55f, 0.82f),
                new Color(116, 132, 156),
                new Color(92, 102, 120),
                0.22f);
        }
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        camera.SetPositionAndTarget(new Vector3(0f, 11.0f, 28.0f), new Vector3(0f, 1.8f, 0f));
    }

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        _measurementConfiguration = IsVariableSet(ValidationVariable);

        if (IsVariableSet(MipmapCheckVariable))
        {
            RunMipmapCheck(game);
        }

        if (_measurementConfiguration)
        {
            _measurementReceiverTexture = CreateSolidTexture(game.GraphicsDevice, MeasurementReceiverTextureColor);
            if (IsVariableSet(DetailVariable))
            {
                _measurementDetailTexture = CreateSolidTexture(game.GraphicsDevice, MeasurementDetailTextureColor);
                _measurementDetailTexture.Name = "MeasurementDetailTexture";
            }

            if (IsVariableSet(ReflectionCubeVariable))
            {
                _measurementReflectionCube = CreateSolidCube(game.GraphicsDevice, MeasurementCubeColor);
            }
        }

        var world = game.GameManager.CurrentWorld;
        CreateValidationColumn(world, game.GraphicsDevice, "CastReceive", -9.0f, casterCastShadows: true, receiverReceiveShadows: true);
        CreateValidationColumn(world, game.GraphicsDevice, "NoReceive", 0.0f, casterCastShadows: true, receiverReceiveShadows: false);
        CreateValidationColumn(world, game.GraphicsDevice, "NoCast", 9.0f, casterCastShadows: false, receiverReceiveShadows: true);

        if (_measurementConfiguration)
        {
            CreateMeasurementReflectiveSphere(world, game.GraphicsDevice);
        }
    }

    public override void Update(GameTime gameTime)
    {
    }

    public override void Clean()
    {
        if (_game?.GameManager.CurrentWorld is { } world)
        {
            world.EnvironmentSettings.ResetToDefaults();
            world.EnvironmentSettings.MarkDirty();
        }

        _measurementReceiverTexture?.Dispose();
        _measurementReceiverTexture = null;
        _measurementDetailTexture?.Dispose();
        _measurementDetailTexture = null;
        _measurementReflectionCube?.Dispose();
        _measurementReflectionCube = null;
        _measurementConfiguration = false;
        _game = null;
    }


    private static void RunMipmapCheck(CasaEngineGame game)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "casaengine-mipmap-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var allPassed = true;

        try
        {
            // One PNG with odd, non-square dimensions and one TGA, both generated here and deleted afterwards.
            var pngPath = Path.Combine(tempDirectory, "odd.png");
            var tgaPath = Path.Combine(tempDirectory, "even.tga");
            WriteMipmapCheckPng(game.GraphicsDevice, pngPath, 37, 19);
            WriteMipmapCheckTga(tgaPath, 64, 32);

            allPassed &= CheckMipmapLoad(game, pngPath);
            allPassed &= CheckMipmapLoad(game, tgaPath);
        }
        catch (Exception ex)
        {
            allPassed = false;
            Logs.WriteException(ex);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }

        Logs.WriteInfo($"[MipmapCheck] verdict={(allPassed ? "PASS" : "FAIL")}");
    }

    private static Color[] CreateMipmapCheckPixels(int width, int height)
    {
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                pixels[y * width + x] = new Color((x * 7 + y * 3) % 256, (x * 13 + y * 5) % 256, (x * 3 + y * 11) % 256, 255 - (x * 5 + y * 9) % 200);
            }
        }

        return pixels;
    }

    private static void WriteMipmapCheckPng(GraphicsDevice graphicsDevice, string path, int width, int height)
    {
        using var texture = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
        texture.SetData(CreateMipmapCheckPixels(width, height));
        using var stream = File.Create(path);
        texture.SaveAsPng(stream, width, height);
    }

    private static void WriteMipmapCheckTga(string path, int width, int height)
    {
        var pixels = CreateMipmapCheckPixels(width, height);
        using var writer = new BinaryWriter(File.Create(path));
        // Uncompressed true-colour image, 32 bits per pixel, origin at the top left, 8 alpha bits.
        writer.Write(new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
        writer.Write((short)width);
        writer.Write((short)height);
        writer.Write((byte)32);
        writer.Write((byte)0x28);
        for (int i = 0; i < pixels.Length; i++)
        {
            writer.Write(new[] { pixels[i].B, pixels[i].G, pixels[i].R, pixels[i].A });
        }
    }

    private static bool CheckMipmapLoad(CasaEngineGame game, string path)
    {
        var name = Path.GetFileName(path);
        Texture2D reference;
        using (var referenceStream = File.OpenRead(path))
        {
            reference = Texture2D.FromStream(game.GraphicsDevice, referenceStream);
        }
        var referencePixels = new Color[reference.Width * reference.Height];
        reference.GetData(referencePixels);
        var referenceWidth = reference.Width;
        var referenceHeight = reference.Height;
        var referenceFormat = reference.Format;
        reference.Dispose();


        var defaultLoaded = (Texture2D)new Texture2DLoader().LoadAsset(path, game.AssetContentManager);
        var defaultLevelCount = defaultLoaded.LevelCount;
        defaultLoaded.Dispose();

        var mipmapped = (Texture2D)new Texture2DLoader { GenerateMipmaps = true }.LoadAsset(path, game.AssetContentManager);
        try
        {
            int width = mipmapped.Width;
            int height = mipmapped.Height;

            // Expected level count and sizes, computed here without MipmapChain: floor(log2(max(w, h))) + 1, max(1, dim >> i).
            int expectedLevelCount = 1;
            for (int size = Math.Max(width, height); size > 1; size >>= 1)
            {
                expectedLevelCount++;
            }

            var sizes = new List<string>();
            var levelsMatchCpu = true;
            var cpuLevels = MipmapChain.ComputeLevels(referencePixels, width, height);
            for (int level = 0; level < mipmapped.LevelCount; level++)
            {
                int levelWidth = Math.Max(1, width >> level);
                int levelHeight = Math.Max(1, height >> level);
                sizes.Add($"{levelWidth}x{levelHeight}");

                var readBack = new Color[levelWidth * levelHeight];
                mipmapped.GetData(level, null, readBack, 0, readBack.Length);
                levelsMatchCpu &= level < cpuLevels.Length && readBack.AsSpan().SequenceEqual(cpuLevels[level]);
            }

            var level0Level = new Color[width * height];
            mipmapped.GetData(0, null, level0Level, 0, level0Level.Length);
            var level0Identical = level0Level.AsSpan().SequenceEqual(referencePixels)
                && mipmapped.Width == referenceWidth && mipmapped.Height == referenceHeight && mipmapped.Format == referenceFormat;

            var passed = mipmapped.LevelCount == expectedLevelCount && defaultLevelCount == 1 && level0Identical && levelsMatchCpu;
            Logs.WriteInfo(
                $"[MipmapCheck] file={name} size={width}x{height} LevelCount={mipmapped.LevelCount} expected={expectedLevelCount} " +
                $"levels={string.Join(",", sizes)} level0IdenticalToFromStream={level0Identical} levelsMatchCpuChain={levelsMatchCpu} " +
                $"defaultLoaderLevelCount={defaultLevelCount} passed={passed}");
            return passed;
        }
        finally
        {
            mipmapped.Dispose();
        }
    }

    private static AmbientSource ReadAmbientSource()
    {
        var value = Environment.GetEnvironmentVariable(AmbientSourceVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            return AmbientSource.Auto;
        }

        if (Enum.TryParse(value, true, out AmbientSource ambientSource))
        {
            return ambientSource;
        }

        Logs.WriteWarning($"[StaticShadowValidationDemo] {AmbientSourceVariable}='{value}' is not Auto or Flat; using Auto.");
        return AmbientSource.Auto;
    }

    private static void ApplyShadowStyleVariables(ShadowSettings shadows)
    {
        var modeValue = Environment.GetEnvironmentVariable(ModeVariable);
        if (!string.IsNullOrWhiteSpace(modeValue))
        {
            if (Enum.TryParse(modeValue, true, out ShadowMode mode))
            {
                shadows.Mode = mode;
            }
            else
            {
                Logs.WriteWarning($"[StaticShadowValidationDemo] {ModeVariable}='{modeValue}' is not OccludeDirectLight or MultiplyLitColor; using OccludeDirectLight.");
            }
        }

        var colorValue = Environment.GetEnvironmentVariable(ColorVariable);
        if (!string.IsNullOrWhiteSpace(colorValue))
        {
            if (TryReadVector3(colorValue, out var color))
            {
                shadows.ShadowColor = color;
            }
            else
            {
                Logs.WriteWarning($"[StaticShadowValidationDemo] {ColorVariable}='{colorValue}' is not three comma-separated numbers; using black.");
            }
        }

        var filterRadiusValue = Environment.GetEnvironmentVariable(FilterRadiusVariable);
        if (!string.IsNullOrWhiteSpace(filterRadiusValue))
        {
            if (float.TryParse(filterRadiusValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var filterRadius))
            {
                shadows.FilterRadius = filterRadius;
            }
            else
            {
                Logs.WriteWarning($"[StaticShadowValidationDemo] {FilterRadiusVariable}='{filterRadiusValue}' is not a number; using 0.");
            }
        }

        if (TryReadFloatVariable(MaxDistanceVariable, out var maxDistance))
        {
            shadows.MaxDistance = maxDistance;
        }

        if (TryReadFloatVariable(FadeDistanceVariable, out var fadeDistance))
        {
            shadows.FadeDistance = fadeDistance;
        }
    }

    private static bool TryReadFloatVariable(string name, out float value)
    {
        value = 0.0f;
        var text = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        Logs.WriteWarning($"[StaticShadowValidationDemo] {name}='{text}' is not a number; ignored.");
        return false;
    }

    private static bool TryReadVector3(string text, out Vector3 value)
    {
        value = Vector3.Zero;
        var parts = text.Split(',');
        if (parts.Length != 3
            || !float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)
            || !float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y)
            || !float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z))
        {
            return false;
        }

        value = new Vector3(x, y, z);
        return true;
    }

    private static bool IsVariableSet(string name)
    {
        return Environment.GetEnvironmentVariable(name) == "1";
    }

    private static Texture2D CreateSolidTexture(GraphicsDevice graphicsDevice, Color color)
    {
        var texture = new Texture2D(graphicsDevice, 2, 2, false, SurfaceFormat.Color) { Name = "MeasurementReceiverTexture" };
        texture.SetData(new[] { color, color, color, color });
        return texture;
    }

    private static TextureCube CreateSolidCube(GraphicsDevice graphicsDevice, Color color)
    {
        var cube = new TextureCube(graphicsDevice, 2, false, SurfaceFormat.Color) { Name = "MeasurementReflectionCube" };
        var faceData = new[] { color, color, color, color };
        for (int face = 0; face < 6; face++)
        {
            cube.SetData((CubeMapFace)face, faceData);
        }

        return cube;
    }

    private void CreateMeasurementReflectiveSphere(World world, GraphicsDevice graphicsDevice)
    {
        var reflectiveMaterial = new LitDiffuseMaterial
        {
            Name = "MeasurementReflective",
            DiffuseColor = new Color(200, 200, 200),
            AmbientColor = Vector3.Zero,
            SpecularColor = new Vector3(0.5f),
            SpecularPower = 16.0f,
            UseSceneReflectionCube = true,
        };

        SpawnStaticPrimitive(
            world,
            graphicsDevice,
            "MeasurementReflectiveEntity",
            new SpherePrimitive(2.4f, 24),
            new Vector3(0.0f, 1.2f, 8.5f),
            Quaternion.Identity,
            reflectiveMaterial,
            castShadows: false,
            receiveShadows: true);
    }

    private LitDiffuseMaterial CreateGroundMaterial(string columnName)
    {
        if (_measurementConfiguration)
        {
            // Measurement receiver: texture x white diffuse x (light + world ambient x 1), no specular.
            return new LitDiffuseMaterial
            {
                Name = $"{columnName}Ground",
                BasColor = _measurementReceiverTexture,
                DetailTexture = _measurementDetailTexture,
                DetailTiling = MeasurementDetailTiling,
                DiffuseColor = Color.White,
                AmbientColor = Vector3.One,
                SpecularColor = Vector3.Zero,
            };
        }

        return new LitDiffuseMaterial
        {
            Name = $"{columnName}Ground",
            DiffuseColor = new Color(184, 176, 154),
            AmbientColor = new Vector3(0.18f, 0.18f, 0.16f),
            SpecularColor = new Vector3(0.06f),
            SpecularPower = 6.0f,
        };
    }

    private void CreateValidationColumn(World world, GraphicsDevice graphicsDevice, string columnName, float centerX, bool casterCastShadows, bool receiverReceiveShadows)
    {
        var groundMaterial = CreateGroundMaterial(columnName);

        var casterMaterial = new LitDiffuseMaterial
        {
            Name = $"{columnName}Caster",
            DiffuseColor = new Color(172, 118, 92),
            AmbientColor = new Vector3(0.12f, 0.1f, 0.09f),
            SpecularColor = new Vector3(0.28f),
            SpecularPower = 22.0f,
        };

        var receiverMaterial = new LitDiffuseMaterial
        {
            Name = $"{columnName}Receiver",
            DiffuseColor = new Color(110, 138, 182),
            AmbientColor = new Vector3(0.11f, 0.12f, 0.16f),
            SpecularColor = new Vector3(0.18f),
            SpecularPower = 16.0f,
        };

        SpawnStaticPrimitive(
            world,
            graphicsDevice,
            $"{columnName}GroundEntity",
            new BoxPrimitive(6.0f, 0.25f, 6.0f),
            new Vector3(centerX, -0.125f, 0.0f),
            Quaternion.Identity,
            groundMaterial,
            castShadows: false,
            receiveShadows: receiverReceiveShadows);

        SpawnStaticPrimitive(
            world,
            graphicsDevice,
            $"{columnName}CasterEntity",
            new BoxPrimitive(1.8f, 2.4f, 1.8f),
            new Vector3(centerX - 1.1f, 2.25f, 0.8f),
            Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(28.0f)),
            casterMaterial,
            castShadows: casterCastShadows,
            receiveShadows: true);

        SpawnStaticPrimitive(
            world,
            graphicsDevice,
            $"{columnName}ReceiverEntity",
            new BoxPrimitive(1.35f, 1.35f, 1.35f),
            new Vector3(centerX + 1.45f, 0.675f, -1.1f),
            Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(-18.0f)),
            receiverMaterial,
            castShadows: false,
            receiveShadows: receiverReceiveShadows);
    }

    private static void SpawnStaticPrimitive(
        World world,
        GraphicsDevice graphicsDevice,
        string entityName,
        GeometricPrimitive primitive,
        Vector3 localPosition,
        Quaternion localOrientation,
        LitDiffuseMaterial material,
        bool castShadows,
        bool receiveShadows)
    {
        var mesh = new StaticModelMesh
        {
            Name = entityName + "Mesh",
            Material = material,
        };
        mesh.SetData(primitive.Vertices.ToArray(), primitive.Indices.ToArray());
        mesh.Initialize(graphicsDevice);

        var model = new StaticModel { Name = entityName + "Model" };
        model.Meshes.Add(mesh);
        model.RootNode = new StaticModelNode
        {
            Name = entityName + "Root",
            MeshIndex = 0,
            Position = Vector3.Zero,
            Rotation = Quaternion.Identity,
            Scale = Vector3.One,
        };

        var component = new StaticModelComponent
        {
            StaticModel = model,
            CastShadows = castShadows,
            ReceiveShadows = receiveShadows,
        };
        component.LocalPosition = localPosition;
        component.LocalOrientation = localOrientation;

        var entity = new Entity { Name = entityName, RootComponent = component };
        world.AddEntity(entity);
    }

    private static void AddDirectionalLight(
        World world,
        string entityName,
        Vector3 direction,
        Color diffuseColor,
        Color specularColor,
        float intensity,
        bool castShadows = false)
    {
        var entity = new Entity { Name = entityName };
        entity.RootComponent = new LightComponent
        {
            Type = LightType.Directional,
            LocalOrientation = CreateOrientationFromForward(direction),
            Color = diffuseColor,
            SpecularColor = specularColor,
            Intensity = intensity,
            CastShadows = castShadows,
        };

        world.AddEntity(entity);
    }

    private static Quaternion CreateOrientationFromForward(Vector3 forward)
    {
        if (forward.LengthSquared() <= 0.0001f)
        {
            return Quaternion.Identity;
        }

        forward = Vector3.Normalize(forward);
        float dot = MathHelper.Clamp(Vector3.Dot(Vector3.Forward, forward), -1.0f, 1.0f);

        if (dot >= 0.9999f)
        {
            return Quaternion.Identity;
        }

        if (dot <= -0.9999f)
        {
            return Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.Pi);
        }

        var axis = Vector3.Normalize(Vector3.Cross(Vector3.Forward, forward));
        float angle = MathF.Acos(dot);
        return Quaternion.CreateFromAxisAngle(axis, angle);
    }
}