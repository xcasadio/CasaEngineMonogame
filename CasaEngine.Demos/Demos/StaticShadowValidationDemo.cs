using System;
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

public sealed class StaticShadowValidationDemo : Demo
{
    private CasaEngineGame? _game;

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
        environmentSettings.AmbientColor = new Vector3(0.1f, 0.11f, 0.13f);
        environmentSettings.AmbientIntensity = 0.55f;
        environmentSettings.SpecularIntensity = 0.12f;
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

        AddDirectionalLight(
            world,
            "StaticShadowFillLight",
            new Vector3(-0.2f, -0.55f, 0.82f),
            new Color(116, 132, 156),
            new Color(92, 102, 120),
            0.22f);
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        camera.SetPositionAndTarget(new Vector3(0f, 11.0f, 28.0f), new Vector3(0f, 1.8f, 0f));
    }

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;

        var world = game.GameManager.CurrentWorld;
        CreateValidationColumn(world, game.GraphicsDevice, "CastReceive", -9.0f, casterCastShadows: true, receiverReceiveShadows: true);
        CreateValidationColumn(world, game.GraphicsDevice, "NoReceive", 0.0f, casterCastShadows: true, receiverReceiveShadows: false);
        CreateValidationColumn(world, game.GraphicsDevice, "NoCast", 9.0f, casterCastShadows: false, receiverReceiveShadows: true);
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

        _game = null;
    }

    private static void CreateValidationColumn(World world, GraphicsDevice graphicsDevice, string columnName, float centerX, bool casterCastShadows, bool receiverReceiveShadows)
    {
        var groundMaterial = new LitDiffuseMaterial
        {
            Name = $"{columnName}Ground",
            DiffuseColor = new Color(184, 176, 154),
            AmbientColor = new Vector3(0.18f, 0.18f, 0.16f),
            SpecularColor = new Vector3(0.06f),
            SpecularPower = 6.0f,
        };

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