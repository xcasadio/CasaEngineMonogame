using System;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.Models;
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
/// configuration) sets <see cref="WorldEnvironmentSettings.AmbientSource"/>. Without these variables the scene is unchanged.
/// </summary>
public sealed class StaticShadowValidationDemo : Demo
{
    private const string ValidationVariable = "CASAENGINE_SHADOW_DEMO_VALIDATION";
    private const string ReflectionCubeVariable = "CASAENGINE_SHADOW_DEMO_REFLECTION_CUBE";
    private const string AmbientSourceVariable = "CASAENGINE_SHADOW_DEMO_AMBIENT_SOURCE";

    private static readonly Color MeasurementReceiverTextureColor = new(128, 128, 128, 255);
    private static readonly Color MeasurementCubeColor = new(51, 153, 255, 255);

    private CasaEngineGame? _game;
    private bool _measurementConfiguration;
    private Texture2D? _measurementReceiverTexture;
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

        if (_measurementConfiguration)
        {
            _measurementReceiverTexture = CreateSolidTexture(game.GraphicsDevice, MeasurementReceiverTextureColor);
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
        _measurementReflectionCube?.Dispose();
        _measurementReflectionCube = null;
        _measurementConfiguration = false;
        _game = null;
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