using System;
using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering.Models;

using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Demonstrates the material system (Phases 0-10):
///
///   Ground              : <see cref="LitDiffuseMaterial"/>    — sandy receiver for projected shadows
///   Red cube            : <see cref="LitDiffuseMaterial"/>    — solid red, Lambert + specular
///   Textured cube       : <see cref="LitDiffuseMaterial"/>    — white, procedural checkerboard albedo
///   Sphere A (left)     : <see cref="LitDiffuseMaterial"/>    — shared material; BLUE  tint via <see cref="MaterialInstanceData"/> -> <see cref="MaterialPropertyBlock"/>
///   Sphere B (right)    : <see cref="LitDiffuseMaterial"/>    — shared material; GREEN tint via <see cref="MaterialInstanceData"/> -> <see cref="MaterialPropertyBlock"/>
///   Alpha-test panel    : <see cref="LitDiffuseMaterial"/>    — cutout texture + <see cref="RenderQueue.AlphaTest"/>
///   Glass cube          : <see cref="UnlitTextureMaterial"/>  — semi-transparent, <see cref="RenderQueue.Transparent"/>
///   Normal-map box      : <see cref="LitDiffuseMaterial"/>    — tangent-ready mesh with procedural normal map
///   Reflection sphere   : <see cref="LitDiffuseMaterial"/>    — shared scene-sky reflection path
///   Ambient / Emissive  : <see cref="LitDiffuseMaterial"/>    — side-by-side ambient and emissive comparison
///   Neutral profile refs: <see cref="LitDiffuseMaterial"/>    — metadata-only import profile samples (named opaque vs explicit reflection)
///   Hints-only import view: <see cref="LitDiffuseMaterial"/>   — shared imported-material presentation mapping driven only by imported hints
///
/// Keyboard shortcuts:
///   <c>T</c> — cycle per-sphere instance tints  (Blue/Green → Red/Cyan → Yellow/Magenta)
/// </summary>
public class MaterialDemo : Demo
{
    private CasaEngineGame? _game;

    public override void ConfigureSceneLighting(CasaEngine.Framework.Scene.World.World world)
    {
    }

    public override string Title => "Material system demo";

    public override string Description =>
        "Unlit/Lit materials, LightingContext, MaterialInstanceData bridged to per-instance MaterialPropertyBlock, " +
        "alpha-test cutout, tangent-space normal map, transparent render queue, shared sky background plus scene reflection cube, ambient-vs-emissive reference, " +
        "neutral legacy import-profile samples, hints-only imported-material presentation mapping, and an explicit LightComponent rig (directional + point + spot).  " +
        "T = cycle sphere tints.";

    // -----------------------------------------------------------------------
    //  Runtime state
    // -----------------------------------------------------------------------

    private KeyboardState _prevKb;

    // Per-instance PropertyBlocks for the two demo spheres
    private readonly MaterialPropertyBlock _propBlockA = new();
    private readonly MaterialPropertyBlock _propBlockB = new();
    private readonly MaterialInstanceData _sphereInstanceDataA = new();
    private readonly MaterialInstanceData _sphereInstanceDataB = new();
    private MaterialAsset? _sphereMaterialAsset;
    private SkyBackgroundViewPipeline? _skyPipeline;

    // Tint pairs cycled with T key
    private static readonly (Color a, Color b)[] TintCycles =
    {
        (Color.CornflowerBlue, Color.LimeGreen),
        (Color.OrangeRed,      Color.Cyan),
        (Color.Yellow,         Color.Magenta),
    };
    private static readonly SkySettings StudioSky = new()
    {
        ZenithColor = new Color(48, 80, 129),
        HorizonColor = new Color(227, 206, 176),
        GroundColor = new Color(96, 88, 96),
        SunColor = new Color(255, 244, 214),
        SunDirection = Vector3.Normalize(new Vector3(-0.5f, -0.8f, -0.3f)),
        SunSize = 0.04f,
        ReflectionCubeSize = 32,
    };
    private const float DemoRingRadius = 6.0f;
    private const float DemoRingStartAngle = -MathHelper.PiOver2;
    private int _tintIndex;

    // -----------------------------------------------------------------------
    //  Demo.Initialize
    // -----------------------------------------------------------------------

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;

        var world = game.GameManager.CurrentWorld;
        var gd    = game.GraphicsDevice;
        AddDemoLights(world);

        // ------------------------------------------------------------------
        //  Material instances
        // ------------------------------------------------------------------

        var materialCompiler = new MaterialCompiler();
        TextureCube studioReflectionCube = ProceduralSkyCubeFactory.CreateReflectionCube(gd, StudioSky, 32);
        world.EnvironmentSettings.SpecularEnvironmentCubemap = studioReflectionCube;
        world.EnvironmentSettings.MarkDirty();

        // Ground — lit sandy receiver so directional shadows remain visible in the sample.
        var groundMat = new LitDiffuseMaterial
        {
            Name = "LitGround",
            DiffuseColor = new Color(210, 195, 150),
            AmbientColor = new Vector3(0.16f, 0.15f, 0.12f),
            SpecularColor = new Vector3(0.08f),
            SpecularPower = 8.0f,
        };

        // Red cube — lit, no texture
        var redMat = new LitDiffuseMaterial
        {
            Name          = "LitRed",
            DiffuseColor  = new Color(200, 50, 50),
            SpecularColor = new Vector3(0.8f),
            SpecularPower = 32f,
        };

        // White textured cube — lit, with a procedural checkerboard albedo
        var whiteMat = new LitDiffuseMaterial
        {
            Name          = "LitWhiteTextured",
            DiffuseColor  = Color.White,
            SpecularColor = new Vector3(0.6f),
            SpecularPower = 64f,
            BasColor      = CreateCheckerTexture(gd, 128, Color.White, new Color(170, 170, 170)),
        };

        // Shared sphere material — authoring asset compiled once, then overridden per-instance
        _sphereMaterialAsset = new MaterialAsset("lit-diffuse")
        {
            Name = "LitSphereBase",
        };
        _sphereMaterialAsset.SetPropertyValue("diffuse_color", MaterialValue.FromColor(Color.White));
        _sphereMaterialAsset.SetPropertyValue("specular_color", MaterialValue.FromVector3(new Vector3(1.0f)));
        _sphereMaterialAsset.SetPropertyValue("specular_power", MaterialValue.FromFloat(64f));
        var sphereMat = (LitDiffuseMaterial)materialCompiler.CompileRuntimeMaterial(_sphereMaterialAsset, game.AssetContentManager);

        var alphaTestAsset = new MaterialAsset("lit-diffuse")
        {
            Name = "LitAlphaCutout",
            Queue = RenderQueue.AlphaTest,
        };
        alphaTestAsset.SetPropertyValue("diffuse_color", MaterialValue.FromColor(Color.White));
        alphaTestAsset.SetPropertyValue("specular_color", MaterialValue.FromVector3(new Vector3(0.15f)));
        alphaTestAsset.SetPropertyValue("specular_power", MaterialValue.FromFloat(12f));
        alphaTestAsset.SetPropertyValue("alpha_cutoff", MaterialValue.FromFloat(0.5f));
        var alphaTestMat = (LitDiffuseMaterial)materialCompiler.CompileRuntimeMaterial(alphaTestAsset, game.AssetContentManager);
        alphaTestMat.BasColor = CreateAlphaCutoutTexture(gd, 128);

        var normalMapAsset = new MaterialAsset("lit-diffuse")
        {
            Name = "LitNormalMapBox",
        };
        normalMapAsset.SetPropertyValue("diffuse_color", MaterialValue.FromColor(new Color(235, 240, 255)));
        normalMapAsset.SetPropertyValue("specular_color", MaterialValue.FromVector3(new Vector3(0.8f)));
        normalMapAsset.SetPropertyValue("specular_power", MaterialValue.FromFloat(48f));
        var normalMapMat = (LitDiffuseMaterial)materialCompiler.CompileRuntimeMaterial(normalMapAsset, game.AssetContentManager);
        normalMapMat.BasColor = CreateCheckerTexture(gd, 128, new Color(215, 219, 230), new Color(122, 126, 145));
        normalMapMat.NormalMap = CreateWaveNormalMap(gd, 128);

        var reflectiveMat = new LitDiffuseMaterial
        {
            Name = "ReflectiveSphere",
            DiffuseColor = new Color(214, 224, 236),
            AmbientColor = new Vector3(0.18f, 0.18f, 0.2f),
            SpecularColor = new Vector3(1.0f),
            SpecularPower = 96f,
            UseSceneReflectionCube = true,
        };

        var ambientOnlyAsset = new MaterialAsset("lit-diffuse")
        {
            Name = "AmbientReference",
        };
        ambientOnlyAsset.SetPropertyValue("diffuse_color", MaterialValue.FromColor(new Color(178, 190, 210)));
        ambientOnlyAsset.SetPropertyValue("ambient_color", MaterialValue.FromVector3(new Vector3(0.9f, 0.75f, 0.55f)));
        ambientOnlyAsset.SetPropertyValue("specular_color", MaterialValue.FromVector3(new Vector3(0.15f)));
        ambientOnlyAsset.SetPropertyValue("specular_power", MaterialValue.FromFloat(10f));
        var ambientOnlyMat = (LitDiffuseMaterial)materialCompiler.CompileRuntimeMaterial(ambientOnlyAsset, game.AssetContentManager);

        var emissiveOnlyAsset = new MaterialAsset("lit-diffuse")
        {
            Name = "EmissiveReference",
        };
        emissiveOnlyAsset.SetPropertyValue("diffuse_color", MaterialValue.FromColor(new Color(178, 190, 210)));
        emissiveOnlyAsset.SetPropertyValue("emissive_color", MaterialValue.FromVector3(new Vector3(0.35f, 0.22f, 0.08f)));
        emissiveOnlyAsset.SetPropertyValue("specular_color", MaterialValue.FromVector3(new Vector3(0.15f)));
        emissiveOnlyAsset.SetPropertyValue("specular_power", MaterialValue.FromFloat(10f));
        var emissiveOnlyMat = (LitDiffuseMaterial)materialCompiler.CompileRuntimeMaterial(emissiveOnlyAsset, game.AssetContentManager);

        var neutralImportProfile = NeutralLegacyMaterialImportProfile.Instance;
        var namedOpaqueInterpretation = neutralImportProfile.Interpret(new LegacyMaterialImportContext(
            SourceAssetPath: @"D:\virtual\AlphaPalm.X",
            SourceAssetName: "AlphaPalm",
            ImportedMaterial: new StaticModelImportedMaterial()));
        var namedOpaqueMat = new LitDiffuseMaterial
        {
            Name = "NeutralProfileNamedOpaque",
            DiffuseColor = new Color(179, 196, 182),
            AmbientColor = new Vector3(0.08f, 0.1f, 0.08f),
            SpecularColor = new Vector3(0.18f),
            SpecularPower = 14f,
            Queue = namedOpaqueInterpretation.AlphaCutout ? RenderQueue.AlphaTest : RenderQueue.Opaque,
        };

        var explicitReflectionInterpretation = neutralImportProfile.Interpret(new LegacyMaterialImportContext(
            SourceAssetPath: @"D:\virtual\MirrorPlate.X",
            SourceAssetName: "MirrorPlate",
            ImportedMaterial: new StaticModelImportedMaterial
            {
                UsesReflection = true,
            }));
        var explicitReflectionMat = new LitDiffuseMaterial
        {
            Name = "NeutralProfileExplicitReflection",
            DiffuseColor = new Color(209, 216, 226),
            AmbientColor = new Vector3(0.1f, 0.1f, 0.12f),
            SpecularColor = new Vector3(0.9f),
            SpecularPower = 72f,
            UseSceneReflectionCube = explicitReflectionInterpretation.Reflection,
        };

        var sharedImportedPresentation = LegacyImportedMaterialPresentationResolver.Resolve(new StaticModelImportedMaterial
        {
            AlphaCutoutHint = true,
            BrightAmbientHint = true,
            AmbientColor = new Vector3(0.08f, 0.12f, 0.08f),
            EmissiveColor = new Vector3(0.2f, 0.05f, -0.15f),
        });
        var sharedImportedPresentationMat = new LitDiffuseMaterial
        {
            Name = "HintsOnlyImportedPresentation",
            BasColor = alphaTestMat.BasColor,
            DiffuseColor = new Color(188, 205, 173),
            AmbientColor = sharedImportedPresentation.AmbientColor,
            EmissiveColor = sharedImportedPresentation.EmissiveColor,
            SpecularColor = new Vector3(0.16f),
            SpecularPower = 10f,
            Queue = sharedImportedPresentation.Queue,
            AlphaCutoff = sharedImportedPresentation.AlphaCutoff,
            RasterizerState = sharedImportedPresentation.DisableBackfaceCulling ? RasterizerState.CullNone : null,
        };

        // Semi-transparent glass cube — unlit
        var glassMat = new UnlitTextureMaterial
        {
            Name       = "UnlitGlass",
            Tint       = new Color(200, 230, 255, 160),
            Alpha      = 0.55f,
            IsTransparent = true,
            Queue      = RenderQueue.Transparent,
            BlendState = BlendState.AlphaBlend,
        };

        // Initialise sphere PropertyBlocks with the first tint pair through the new instance-data bridge
        ApplyTints();

        // ------------------------------------------------------------------
        //  Scene objects — each gets its own Entity + StaticModelComponent
        // ------------------------------------------------------------------

        const int demoObjectCount = 13;

        // Ground
        SpawnStaticModel("Ground", world, gd,
            new BoxPrimitive(16f, 0.3f, 16f),
            new Vector3(0f, -0.15f, 0f),
            Quaternion.Identity,
            groundMat);

        // Red cube
        SpawnStaticModel("RedCube", world, gd,
            new BoxPrimitive(1.5f, 1.5f, 1.5f),
            GetDemoRingPosition(0, demoObjectCount, 0.75f),
            CreateDemoRingRotation(0, demoObjectCount, yawOffsetDegrees: 25f),
            redMat);

        // Textured cube
        SpawnStaticModel("TexturedCube", world, gd,
            new BoxPrimitive(1.5f, 1.5f, 1.5f),
            GetDemoRingPosition(1, demoObjectCount, 0.75f),
            CreateDemoRingRotation(1, demoObjectCount, yawOffsetDegrees: -15f),
            whiteMat);

        // Sphere A — PropertyBlock (placed via standalone StaticModelSubMeshComponent)
        SpawnSphereWithPropertyBlock("SphereA", world, gd,
            sphereMat, _propBlockA,
            GetDemoRingPosition(2, demoObjectCount, 0.9f));

        // Sphere B — PropertyBlock
        SpawnSphereWithPropertyBlock("SphereB", world, gd,
            sphereMat, _propBlockB,
            GetDemoRingPosition(3, demoObjectCount, 0.9f));

        // Alpha-test panel (cutout)
        SpawnStaticModel("AlphaTestPanel", world, gd,
            new PlanePrimitive(2.3f, 2.3f),
            GetDemoRingPosition(4, demoObjectCount, 1.15f),
            CreateDemoRingRotation(4, demoObjectCount, yawOffsetDegrees: -18f, pitchDegrees: 90f),
            alphaTestMat);

        // Glass cube (transparent)
        SpawnStaticModel("GlassCube", world, gd,
            new BoxPrimitive(1.5f, 1.5f, 1.5f),
            GetDemoRingPosition(5, demoObjectCount, 0.75f),
            CreateDemoRingRotation(5, demoObjectCount, yawOffsetDegrees: 40f),
            glassMat);

        // Normal-mapped box (tangent-ready mesh)
        SpawnStaticModel("NormalMapBox", world, gd,
            new BoxPrimitive(1.8f, 1.8f, 1.8f),
            GetDemoRingPosition(6, demoObjectCount, 0.9f),
            CreateDemoRingRotation(6, demoObjectCount, yawOffsetDegrees: 28f, pitchDegrees: -16f),
            normalMapMat,
            useTangents: true);

        SpawnStaticModel("ReflectionSphere", world, gd,
            new SpherePrimitive(1.0f, 24),
            GetDemoRingPosition(7, demoObjectCount, 1.0f),
            Quaternion.Identity,
            reflectiveMat);

        SpawnStaticModel("AmbientReference", world, gd,
            new BoxPrimitive(1.1f, 1.1f, 1.1f),
            GetDemoRingPosition(8, demoObjectCount, 0.55f),
            CreateDemoRingRotation(8, demoObjectCount, yawOffsetDegrees: 18f, pitchDegrees: -10f),
            ambientOnlyMat);

        SpawnStaticModel("NeutralProfileNamedOpaque", world, gd,
            new BoxPrimitive(1.1f, 1.1f, 1.1f),
            GetDemoRingPosition(9, demoObjectCount, 0.55f),
            CreateDemoRingRotation(9, demoObjectCount, yawOffsetDegrees: 12f, pitchDegrees: -10f),
            namedOpaqueMat);

        SpawnStaticModel("NeutralProfileExplicitReflection", world, gd,
            new BoxPrimitive(1.1f, 1.1f, 1.1f),
            GetDemoRingPosition(10, demoObjectCount, 0.55f),
            CreateDemoRingRotation(10, demoObjectCount, yawOffsetDegrees: -12f, pitchDegrees: -10f),
            explicitReflectionMat);

        SpawnStaticModel("HintsOnlyImportedPresentation", world, gd,
            new PlanePrimitive(1.2f, 1.2f),
            GetDemoRingPosition(11, demoObjectCount, 0.8f),
            CreateDemoRingRotation(11, demoObjectCount, pitchDegrees: 90f),
            sharedImportedPresentationMat);

        SpawnStaticModel("EmissiveReference", world, gd,
            new BoxPrimitive(1.1f, 1.1f, 1.1f),
            GetDemoRingPosition(12, demoObjectCount, 0.55f),
            CreateDemoRingRotation(12, demoObjectCount, yawOffsetDegrees: -18f, pitchDegrees: -10f),
            emissiveOnlyMat);
    }

    // -----------------------------------------------------------------------
    //  Demo.InitializeCamera
    // -----------------------------------------------------------------------

    public override void InitializeCamera(CameraComponent camera)
    {
        ((ArcBallCameraComponent)camera).SetCamera(
            new Vector3(0f, 6f, 15f), Vector3.Zero, Vector3.Up);

        if (_game == null)
        {
            return;
        }

        _skyPipeline ??= new SkyBackgroundViewPipeline(StudioSky);
        foreach (var view in _game.GameManager.ViewManager.Views)
        {
            view.Pipeline = _skyPipeline;
            view.ClearColor = StudioSky.HorizonColor;
            view.Invalidate();
        }
    }

    // -----------------------------------------------------------------------
    //  Demo.Update
    // -----------------------------------------------------------------------

    public override void Update(GameTime gameTime)
    {
        var kb = _game?.IsActive == true ? Keyboard.GetState() : new KeyboardState();

        // T — cycle per-sphere instance tints
        if (kb.IsKeyDown(Keys.T) && !_prevKb.IsKeyDown(Keys.T))
        {
            _tintIndex = (_tintIndex + 1) % TintCycles.Length;
            ApplyTints();
        }

        _prevKb = kb;

    }

    // -----------------------------------------------------------------------
    //  Demo.Clean
    // -----------------------------------------------------------------------

    public override void Clean()
    {
        if (_game != null)
        {
            _game.GameManager.CurrentWorld.EnvironmentSettings.SpecularEnvironmentCubemap = null;
            _game.GameManager.CurrentWorld.EnvironmentSettings.MarkDirty();

            foreach (var view in _game.GameManager.ViewManager.Views)
            {
                if (ReferenceEquals(view.Pipeline, _skyPipeline))
                {
                    view.Pipeline = null;
                    view.ClearColor = Color.CornflowerBlue;
                    view.Invalidate();
                }
            }
        }

        _game = null;
        _skyPipeline = null;
    }

    // -----------------------------------------------------------------------
    //  Private helpers
    // -----------------------------------------------------------------------

    /// <summary>Writes the current tint pair into both PropertyBlocks via MaterialInstanceData.</summary>
    private void ApplyTints()
    {
        if (_sphereMaterialAsset == null)
        {
            return;
        }

        var (a, b) = TintCycles[_tintIndex];

        _sphereInstanceDataA.SetPropertyOverride("diffuse_color", MaterialValue.FromColor(a));
        _sphereInstanceDataB.SetPropertyOverride("diffuse_color", MaterialValue.FromColor(b));

        MaterialInstancePropertyBlockMapper.Apply(_propBlockA, _sphereMaterialAsset, _sphereInstanceDataA);
        MaterialInstancePropertyBlockMapper.Apply(_propBlockB, _sphereMaterialAsset, _sphereInstanceDataB);
    }

    private static Vector3 GetDemoRingPosition(int index, int totalCount, float height)
    {
        float angle = GetDemoRingAngle(index, totalCount);
        return new Vector3(MathF.Cos(angle) * DemoRingRadius, height, MathF.Sin(angle) * DemoRingRadius);
    }

    private static Quaternion CreateDemoRingRotation(
        int index,
        int totalCount,
        float yawOffsetDegrees = 0f,
        float pitchDegrees = 0f,
        float rollDegrees = 0f)
    {
        float angle = GetDemoRingAngle(index, totalCount);
        float yaw = -angle - MathHelper.PiOver2 + MathHelper.ToRadians(yawOffsetDegrees);
        return Quaternion.CreateFromYawPitchRoll(yaw, MathHelper.ToRadians(pitchDegrees), MathHelper.ToRadians(rollDegrees));
    }

    private static float GetDemoRingAngle(int index, int totalCount)
        => DemoRingStartAngle + (index * MathHelper.TwoPi / totalCount);

    /// <summary>
    /// Creates an entity with a single-mesh <see cref="StaticModelComponent"/>
    /// positioned at <paramref name="position"/> with <paramref name="rotation"/>.
    /// </summary>
    private static void SpawnStaticModel(
        string             name,
        CasaEngine.Framework.Scene.World.World world,
        GraphicsDevice     gd,
        GeometricPrimitive primitive,
        Vector3            position,
        Quaternion         rotation,
        MaterialBase       material,
        bool               useTangents = false)
    {
        var mesh = BuildMesh(name, primitive, gd, useTangents);
        mesh.Material = material;

        var model = new StaticModel { Name = name };
        model.Meshes.Add(mesh);   // index 0
        model.RootNode = new StaticModelNode
        {
            Name      = name,
            MeshIndex = 0,
            Position  = position,
            Rotation  = rotation,
            Scale     = Vector3.One,
        };

        var entity    = new Entity { Name = name };
        var component = new StaticModelComponent { StaticModel = model };
        entity.RootComponent = component;
        world.AddEntity(entity);
    }

    /// <summary>
    /// Creates an entity where a standalone <see cref="StaticModelSubMeshComponent"/>
    /// is used directly as the root component. This allows assigning a
    /// <see cref="MaterialPropertyBlock"/> for per-instance parameter overrides.
    /// </summary>
    private static void SpawnSphereWithPropertyBlock(
        string                name,
        CasaEngine.Framework.Scene.World.World world,
        GraphicsDevice        gd,
        LitDiffuseMaterial    material,
        MaterialPropertyBlock propertyBlock,
        Vector3               position)
    {
        var mesh = BuildMesh(name, new SpherePrimitive(0.9f, 24), gd);
        mesh.Material = material;

        var component = new StaticModelSubMeshComponent
        {
            ModelMesh         = mesh,
            PropertyOverrides = propertyBlock,
        };
        component.LocalPosition = position;

        var entity = new Entity { Name = name };
        entity.RootComponent = component;
        world.AddEntity(entity);
    }

    private static void AddDemoLights(CasaEngine.Framework.Scene.World.World world)
    {
        SpawnLight(
            world,
            "DemoDirectionalLight",
            LightType.Directional,
            Vector3.Zero,
            new Vector3(-0.5f, -0.8f, -0.3f),
            new Color(255, 244, 214),
            Color.White,
            1.0f,
            0.0f,
            0.0f,
            0.0f,
            castShadows: true);

        SpawnLight(
            world,
            "DemoPointLight",
            LightType.Point,
            new Vector3(2.6f, 1.9f, 2.3f),
            Vector3.Forward,
            new Color(104, 140, 214),
            new Color(104, 140, 214),
            0.55f,
            8.5f,
            0.0f,
            0.0f);

        SpawnLight(
            world,
            "DemoSpotLight",
            LightType.Spot,
            new Vector3(-2.8f, 3.2f, 1.4f),
            Vector3.Zero - new Vector3(-2.8f, 3.2f, 1.4f),
            new Color(255, 214, 168),
            new Color(255, 214, 168),
            0.9f,
            10.0f,
            18.0f,
            32.0f);
    }

    private static void SpawnLight(
        CasaEngine.Framework.Scene.World.World world,
        string name,
        LightType type,
        Vector3 position,
        Vector3 forward,
        Color color,
        Color specularColor,
        float intensity,
        float range,
        float innerConeAngleDegrees,
        float outerConeAngleDegrees,
        bool castShadows = false)
    {
        var lightComponent = new LightComponent
        {
            Type = type,
            LocalPosition = position,
            LocalOrientation = CreateOrientationFromForward(forward),
            Color = color,
            SpecularColor = specularColor,
            Intensity = intensity,
            Range = range,
            InnerConeAngleDegrees = innerConeAngleDegrees,
            OuterConeAngleDegrees = outerConeAngleDegrees,
            CastShadows = castShadows,
        };

        var entity = new Entity { Name = name };
        entity.RootComponent = lightComponent;
        world.AddEntity(entity);
    }

    private static Quaternion CreateOrientationFromForward(Vector3 forward)
    {
        if (forward.LengthSquared() <= 0.0001f)
        {
            return Quaternion.Identity;
        }

        forward = Vector3.Normalize(forward);
        float dot = Math.Clamp(Vector3.Dot(Vector3.Forward, forward), -1.0f, 1.0f);

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

    /// <summary>Builds and uploads a <see cref="StaticModelMesh"/> from a geometric primitive.</summary>
    private static StaticModelMesh BuildMesh(string name, GeometricPrimitive primitive, GraphicsDevice gd, bool useTangents = false)
    {
        var mesh = new StaticModelMesh { Name = name };

        if (useTangents)
        {
            mesh.SetData(BuildTangentVertices(primitive), primitive.Indices.ToArray());
        }
        else
        {
            mesh.SetData(primitive.Vertices.ToArray(), primitive.Indices.ToArray());
        }

        mesh.Initialize(gd);
        return mesh;
    }

    /// <summary>Generates a procedural two-colour checkerboard <see cref="Texture2D"/>.</summary>
    private static Texture2D CreateCheckerTexture(GraphicsDevice gd, int size, Color a, Color b)
    {
        int cellSize = size / 8;
        var tex      = new Texture2D(gd, size, size);
        var data     = new Color[size * size];

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

    private static Texture2D CreateAlphaCutoutTexture(GraphicsDevice gd, int size)
    {
        var tex = new Texture2D(gd, size, size);
        var data = new Color[size * size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float nx = x / (size - 1f) * 2f - 1f;
            float ny = y / (size - 1f) * 2f - 1f;
            float distance = MathF.Sqrt(nx * nx + ny * ny);

            float alpha = 0.0f;
            if (distance < 0.68f)
            {
                alpha = 1.0f;
            }
            else if (distance < 0.82f)
            {
                alpha = 1.0f - (distance - 0.68f) / 0.14f;
            }

            float shade = Math.Clamp((ny + 1.0f) * 0.5f, 0.0f, 1.0f);
            var fill = Color.Lerp(new Color(44, 92, 55), new Color(187, 222, 118), shade);
            data[y * size + x] = new Color(fill.R, fill.G, fill.B, (byte)(Math.Clamp(alpha, 0.0f, 1.0f) * 255f));
        }

        tex.SetData(data);
        return tex;
    }

    private static Texture2D CreateWaveNormalMap(GraphicsDevice gd, int size)
    {
        var tex = new Texture2D(gd, size, size);
        var data = new Color[size * size];
        float texel = 1.0f / size;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = x / (size - 1f);
            float v = y / (size - 1f);
            float du = SampleWaveHeight(u + texel, v) - SampleWaveHeight(u - texel, v);
            float dv = SampleWaveHeight(u, v + texel) - SampleWaveHeight(u, v - texel);

            var normal = Vector3.Normalize(new Vector3(-du * 8.0f, -dv * 8.0f, 1.0f));
            data[y * size + x] = new Color(new Vector4(
                normal.X * 0.5f + 0.5f,
                normal.Y * 0.5f + 0.5f,
                normal.Z * 0.5f + 0.5f,
                1.0f));
        }

        tex.SetData(data);
        return tex;
    }

    private static float SampleWaveHeight(float u, float v)
    {
        float wrappedU = u - MathF.Floor(u);
        float wrappedV = v - MathF.Floor(v);
        float waveA = MathF.Sin(wrappedU * MathHelper.TwoPi * 4.0f);
        float waveB = MathF.Cos(wrappedV * MathHelper.TwoPi * 3.0f);
        float waveC = MathF.Sin((wrappedU + wrappedV) * MathHelper.TwoPi * 2.0f);
        return waveA * 0.30f + waveB * 0.25f + waveC * 0.20f;
    }

    private static VertexPositionNormalTextureTangent[] BuildTangentVertices(GeometricPrimitive primitive)
    {
        var vertices = primitive.Vertices;
        var indices = primitive.Indices;
        var tangents = new Vector3[vertices.Count];
        var bitangents = new Vector3[vertices.Count];

        for (int i = 0; i <= indices.Count - 3; i += 3)
        {
            int index0 = (int)indices[i];
            int index1 = (int)indices[i + 1];
            int index2 = (int)indices[i + 2];

            var vertex0 = vertices[index0];
            var vertex1 = vertices[index1];
            var vertex2 = vertices[index2];

            var edge1 = vertex1.Position - vertex0.Position;
            var edge2 = vertex2.Position - vertex0.Position;
            var deltaUv1 = vertex1.TextureCoordinate - vertex0.TextureCoordinate;
            var deltaUv2 = vertex2.TextureCoordinate - vertex0.TextureCoordinate;
            float determinant = deltaUv1.X * deltaUv2.Y - deltaUv2.X * deltaUv1.Y;

            if (MathF.Abs(determinant) < 1e-6f)
            {
                continue;
            }

            float inverseDeterminant = 1.0f / determinant;
            var tangent = (edge1 * deltaUv2.Y - edge2 * deltaUv1.Y) * inverseDeterminant;
            var bitangent = (edge2 * deltaUv1.X - edge1 * deltaUv2.X) * inverseDeterminant;

            tangents[index0] += tangent;
            tangents[index1] += tangent;
            tangents[index2] += tangent;
            bitangents[index0] += bitangent;
            bitangents[index1] += bitangent;
            bitangents[index2] += bitangent;
        }

        var tangentVertices = new VertexPositionNormalTextureTangent[vertices.Count];

        for (int i = 0; i < vertices.Count; i++)
        {
            var sourceVertex = vertices[i];
            var normal = Vector3.Normalize(sourceVertex.Normal);
            var tangent = tangents[i] - normal * Vector3.Dot(normal, tangents[i]);

            if (tangent.LengthSquared() < 1e-6f)
            {
                tangent = BuildFallbackTangent(normal);
            }
            else
            {
                tangent.Normalize();
            }

            float handedness = Vector3.Dot(Vector3.Cross(normal, tangent), bitangents[i]) < 0.0f ? -1.0f : 1.0f;
            tangentVertices[i] = new VertexPositionNormalTextureTangent(
                sourceVertex.Position,
                normal,
                sourceVertex.TextureCoordinate,
                new Vector4(tangent, handedness));
        }

        return tangentVertices;
    }

    private static Vector3 BuildFallbackTangent(Vector3 normal)
    {
        var referenceAxis = MathF.Abs(normal.Y) > 0.999f ? Vector3.Right : Vector3.Up;
        var tangent = Vector3.Cross(referenceAxis, normal);

        if (tangent.LengthSquared() < 1e-6f)
        {
            tangent = Vector3.Right;
        }
        else
        {
            tangent.Normalize();
        }

        return tangent;
    }
}
