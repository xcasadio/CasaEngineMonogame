using System;
using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Materials.Runtime;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos.Demos;

public sealed class EnvironmentShowcaseDemo : Demo
{
    private CasaEngineGame? _game;
    private KeyboardState _previousKeyboard;
    private TextureCube? _warmCubemap;
    private TextureCube? _studioCubemap;
    private int _environmentIndex;
    private bool _showEnvironmentBackground = true;
    private bool _environmentLightingEnabled = true;

    public override string Title => "Environment showcase";

    public override string Description =>
        "Global environment cubemap with explicit separation between visible background and environment-driven ambient/specular response. " +
        "B = toggle sky background, E = cycle environment cubemap, L = toggle environment lighting contribution.";

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;

        var world = game.GameManager.CurrentWorld;
        var graphicsDevice = game.GraphicsDevice;

        _warmCubemap ??= CreateEnvironmentCubemap(
            graphicsDevice,
            size: 32,
            positiveX: (new Color(235, 144, 86), new Color(255, 214, 162)),
            negativeX: (new Color(60, 112, 192), new Color(160, 211, 255)),
            positiveY: (new Color(255, 231, 194), Color.White),
            negativeY: (new Color(42, 48, 60), new Color(88, 94, 108)),
            positiveZ: (new Color(214, 112, 132), new Color(255, 198, 182)),
            negativeZ: (new Color(108, 72, 150), new Color(196, 168, 236)));

        _studioCubemap ??= CreateEnvironmentCubemap(
            graphicsDevice,
            size: 32,
            positiveX: (new Color(78, 150, 170), new Color(168, 240, 248)),
            negativeX: (new Color(44, 84, 124), new Color(118, 182, 224)),
            positiveY: (new Color(238, 247, 252), Color.White),
            negativeY: (new Color(26, 34, 48), new Color(62, 74, 90)),
            positiveZ: (new Color(48, 134, 110), new Color(172, 244, 210)),
            negativeZ: (new Color(98, 104, 198), new Color(186, 204, 255)));

        SpawnScene(world, graphicsDevice);
        ApplyEnvironment(world.EnvironmentSettings);
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        if (camera is ArcBallCameraComponent arcBallCamera)
        {
            arcBallCamera.SetCamera(new Vector3(0f, 4.5f, 15.5f), new Vector3(0f, 1.2f, 0f), Vector3.Up);
        }
    }

    public override void Update(GameTime gameTime)
    {
        if (_game == null)
        {
            return;
        }

        var keyboard = Keyboard.GetState();
        var environmentSettings = _game.GameManager.CurrentWorld.EnvironmentSettings;

        if (IsNewKeyPress(keyboard, Keys.B))
        {
            _showEnvironmentBackground = !_showEnvironmentBackground;
            ApplyEnvironment(environmentSettings);
        }

        if (IsNewKeyPress(keyboard, Keys.E))
        {
            _environmentIndex = (_environmentIndex + 1) % 2;
            ApplyEnvironment(environmentSettings);
        }

        if (IsNewKeyPress(keyboard, Keys.L))
        {
            _environmentLightingEnabled = !_environmentLightingEnabled;
            ApplyEnvironment(environmentSettings);
        }

        _previousKeyboard = keyboard;
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

    private void ApplyEnvironment(WorldEnvironmentSettings environmentSettings)
    {
        var environmentCubemap = _environmentIndex == 0 ? _warmCubemap : _studioCubemap;
        var ambientColor = _environmentIndex == 0
            ? new Vector3(0.34f, 0.26f, 0.22f)
            : new Vector3(0.2f, 0.28f, 0.34f);
        var backgroundColor = _environmentIndex == 0
            ? new Color(22, 18, 18)
            : new Color(16, 22, 28);

        environmentSettings.Type = EnvironmentType.Cubemap;
        environmentSettings.BackgroundMode = _showEnvironmentBackground
            ? EnvironmentBackgroundMode.Environment
            : EnvironmentBackgroundMode.SolidColor;
        environmentSettings.BackgroundColor = backgroundColor;
        environmentSettings.EnvironmentAssetId = Guid.Empty;
        environmentSettings.BackgroundCubemapAssetId = Guid.Empty;
        environmentSettings.SpecularEnvironmentCubemapAssetId = Guid.Empty;
        environmentSettings.BackgroundCubemap = environmentCubemap;
        environmentSettings.SpecularEnvironmentCubemap = environmentCubemap;
        environmentSettings.AmbientColor = ambientColor;
        environmentSettings.AmbientIntensity = _environmentLightingEnabled ? 1.25f : 0.0f;
        environmentSettings.SpecularIntensity = _environmentLightingEnabled ? 1.15f : 0.0f;
        environmentSettings.MarkDirty();
    }

    private void SpawnScene(CasaEngine.Framework.Scene.World.World world, GraphicsDevice graphicsDevice)
    {
        var groundMaterial = new LitDiffuseMaterial
        {
            Name = "EnvironmentShowcaseGround",
            BasColor = CreateCheckerTexture(graphicsDevice, 128, new Color(166, 156, 142), new Color(112, 106, 96)),
            DiffuseColor = Color.White,
            AmbientColor = new Vector3(0.08f),
            SpecularColor = new Vector3(0.08f),
            SpecularPower = 12.0f,
        };

        var glossySphereMaterial = new LitDiffuseMaterial
        {
            Name = "EnvironmentShowcaseGlossySphere",
            DiffuseColor = new Color(230, 234, 240),
            AmbientColor = new Vector3(0.1f),
            SpecularColor = new Vector3(1.0f),
            SpecularPower = 96.0f,
        };

        var matteBoxMaterial = new LitDiffuseMaterial
        {
            Name = "EnvironmentShowcaseMatteBox",
            DiffuseColor = new Color(200, 154, 112),
            AmbientColor = new Vector3(0.06f),
            SpecularColor = new Vector3(0.18f),
            SpecularPower = 14.0f,
        };

        var reflectivePanelMaterial = new LitDiffuseMaterial
        {
            Name = "EnvironmentShowcaseReflectivePanel",
            DiffuseColor = new Color(214, 224, 236),
            AmbientColor = new Vector3(0.08f),
            SpecularColor = new Vector3(0.92f),
            SpecularPower = 72.0f,
        };

        SpawnStaticModel(
            "EnvironmentGround",
            world,
            graphicsDevice,
            new BoxPrimitive(18f, 0.3f, 18f),
            new Vector3(0f, -0.15f, 0f),
            Quaternion.Identity,
            groundMaterial);

        SpawnStaticModel(
            "EnvironmentSphere",
            world,
            graphicsDevice,
            new SpherePrimitive(1.35f, 28),
            new Vector3(-2.8f, 1.35f, 0.3f),
            Quaternion.Identity,
            glossySphereMaterial);

        SpawnStaticModel(
            "EnvironmentBox",
            world,
            graphicsDevice,
            new BoxPrimitive(2.3f, 2.3f, 2.3f),
            new Vector3(2.8f, 1.15f, 0f),
            Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(28f), MathHelper.ToRadians(-10f), 0f),
            matteBoxMaterial);

        SpawnStaticModel(
            "EnvironmentPanel",
            world,
            graphicsDevice,
            new PlanePrimitive(4.0f, 2.8f),
            new Vector3(0f, 2.0f, -4.6f),
            Quaternion.CreateFromYawPitchRoll(0f, MathHelper.PiOver2, 0f),
            reflectivePanelMaterial);
    }

    private bool IsNewKeyPress(KeyboardState keyboard, Keys key)
        => keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

    private static void SpawnStaticModel(
        string name,
        CasaEngine.Framework.Scene.World.World world,
        GraphicsDevice graphicsDevice,
        GeometricPrimitive primitive,
        Vector3 position,
        Quaternion rotation,
        LitDiffuseMaterial material)
    {
        var mesh = BuildMesh(name, primitive, graphicsDevice);
        mesh.Material = material;

        var model = new StaticModel { Name = name };
        model.Meshes.Add(mesh);
        model.RootNode = new StaticModelNode
        {
            Name = name,
            MeshIndex = 0,
            Position = position,
            Rotation = rotation,
            Scale = Vector3.One,
        };

        var entity = new Entity { Name = name };
        entity.RootComponent = new StaticModelComponent { StaticModel = model };
        world.AddEntity(entity);
    }

    private static StaticModelMesh BuildMesh(string name, GeometricPrimitive primitive, GraphicsDevice graphicsDevice)
    {
        var mesh = new StaticModelMesh { Name = name };
        mesh.SetData(primitive.Vertices.ToArray(), primitive.Indices.ToArray());
        mesh.Initialize(graphicsDevice);
        return mesh;
    }

    private static Texture2D CreateCheckerTexture(GraphicsDevice graphicsDevice, int size, Color a, Color b)
    {
        int cellSize = size / 8;
        var texture = new Texture2D(graphicsDevice, size, size);
        var data = new Color[size * size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int cellX = x / cellSize;
            int cellY = y / cellSize;
            data[y * size + x] = (cellX + cellY) % 2 == 0 ? a : b;
        }

        texture.SetData(data);
        return texture;
    }

    private static TextureCube CreateEnvironmentCubemap(
        GraphicsDevice graphicsDevice,
        int size,
        (Color start, Color end) positiveX,
        (Color start, Color end) negativeX,
        (Color start, Color end) positiveY,
        (Color start, Color end) negativeY,
        (Color start, Color end) positiveZ,
        (Color start, Color end) negativeZ)
    {
        var cubemap = new TextureCube(graphicsDevice, size, false, SurfaceFormat.Color);
        FillCubeFace(cubemap, CubeMapFace.PositiveX, size, positiveX.start, positiveX.end);
        FillCubeFace(cubemap, CubeMapFace.NegativeX, size, negativeX.start, negativeX.end);
        FillCubeFace(cubemap, CubeMapFace.PositiveY, size, positiveY.start, positiveY.end);
        FillCubeFace(cubemap, CubeMapFace.NegativeY, size, negativeY.start, negativeY.end);
        FillCubeFace(cubemap, CubeMapFace.PositiveZ, size, positiveZ.start, positiveZ.end);
        FillCubeFace(cubemap, CubeMapFace.NegativeZ, size, negativeZ.start, negativeZ.end);
        return cubemap;
    }

    private static void FillCubeFace(TextureCube cubemap, CubeMapFace face, int size, Color start, Color end)
    {
        var data = new Color[size * size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = size == 1 ? 0f : x / (size - 1f);
            float v = size == 1 ? 0f : y / (size - 1f);
            var horizontal = Color.Lerp(start, end, u);
            data[y * size + x] = Color.Lerp(horizontal, Color.White, v * 0.16f);
        }

        cubemap.SetData(face, data);
    }
}