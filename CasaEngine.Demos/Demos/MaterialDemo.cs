using System;
using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Materials;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using DirLight = CasaEngine.Framework.Rendering.DirectionalLight;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Demonstrates the material system (Phases 0-10):
///
///   Ground              : <see cref="UnlitTextureMaterial"/>  — yellow tint, no lighting
///   Red cube            : <see cref="LitDiffuseMaterial"/>    — solid red, Lambert + specular
///   Textured cube       : <see cref="LitDiffuseMaterial"/>    — white, procedural checkerboard albedo
///   Sphere A (left)     : <see cref="LitDiffuseMaterial"/>    — shared material; BLUE  tint via <see cref="MaterialInstanceData"/> -> <see cref="MaterialPropertyBlock"/>
///   Sphere B (right)    : <see cref="LitDiffuseMaterial"/>    — shared material; GREEN tint via <see cref="MaterialInstanceData"/> -> <see cref="MaterialPropertyBlock"/>
///   Alpha-test panel    : <see cref="LitDiffuseMaterial"/>    — cutout texture + <see cref="RenderQueue.AlphaTest"/>
///   Glass cube          : <see cref="UnlitTextureMaterial"/>  — semi-transparent, <see cref="RenderQueue.Transparent"/>
///   Normal-map box      : <see cref="LitDiffuseMaterial"/>    — tangent-ready mesh with procedural normal map
///   Reflection sphere   : <see cref="LitDiffuseMaterial"/>    — procedural cubemap reflection path
///   Ambient / Emissive  : <see cref="LitDiffuseMaterial"/>    — side-by-side ambient and emissive comparison
///   Neutral profile refs: <see cref="LitDiffuseMaterial"/>    — metadata-only import profile samples (named opaque vs explicit reflection)
///   Hints-only import view: <see cref="LitDiffuseMaterial"/>   — shared imported-material presentation mapping driven only by imported hints
///
/// Keyboard shortcuts:
///   <c>L</c> — cycle directional light count  1 → 2 → 3 → 1
///   <c>T</c> — cycle per-sphere instance tints  (Blue/Green → Red/Cyan → Yellow/Magenta)
/// </summary>
public class MaterialDemo : Demo
{
    private CasaEngineGame? _game;

    public override string Title => "Material system demo";

    public override string Description =>
        "Unlit/Lit materials, LightingContext, MaterialInstanceData bridged to per-instance MaterialPropertyBlock, " +
        "alpha-test cutout, tangent-space normal map, transparent render queue, reflective cubemap path, ambient-vs-emissive reference, " +
        "neutral legacy import-profile samples, and hints-only imported-material presentation mapping.  " +
        "L = cycle lights,  T = cycle sphere tints.";

    // -----------------------------------------------------------------------
    //  Runtime state
    // -----------------------------------------------------------------------

    private StaticMeshRendererComponent? _renderer;
    private KeyboardState _prevKb;

    // Per-instance PropertyBlocks for the two demo spheres
    private readonly MaterialPropertyBlock _propBlockA = new();
    private readonly MaterialPropertyBlock _propBlockB = new();
    private readonly MaterialInstanceData _sphereInstanceDataA = new();
    private readonly MaterialInstanceData _sphereInstanceDataB = new();
    private MaterialAsset? _sphereMaterialAsset;

    // Tint pairs cycled with T key
    private static readonly (Color a, Color b)[] TintCycles =
    {
        (Color.CornflowerBlue, Color.LimeGreen),
        (Color.OrangeRed,      Color.Cyan),
        (Color.Yellow,         Color.Magenta),
    };
    private int _tintIndex;

    // Active light count (cycled with L key)
    private int _lightCount = 3;

    // -----------------------------------------------------------------------
    //  Demo.Initialize
    // -----------------------------------------------------------------------

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;

        var world = game.GameManager.CurrentWorld;
        var gd    = game.GraphicsDevice;

        _renderer = game.GetGameComponent<StaticMeshRendererComponent>();

        // ------------------------------------------------------------------
        //  Lighting setup  (three-light studio rig)
        // ------------------------------------------------------------------
        if (_renderer is not null)
        {
            var lit = _renderer.DefaultLighting;
            lit.AmbientColor = new Vector3(0.08f);
            lit.ActiveDirectionalLightCount = _lightCount;

            // Key — warm sun from upper-right
            lit.DirectionalLights[0] = new DirLight(
                new Vector3(-0.5f, -0.8f, -0.3f),
                new Vector3(1.0f,  0.92f, 0.75f),
                new Vector3(1.0f,  0.92f, 0.75f));

            // Fill — cool blue from the left
            lit.DirectionalLights[1] = new DirLight(
                new Vector3(0.8f, -0.2f, 0.5f),
                new Vector3(0.3f, 0.4f, 0.65f),
                Vector3.Zero);

            // Rim — from behind
            lit.DirectionalLights[2] = new DirLight(
                new Vector3(0.1f, -0.4f, 0.9f),
                new Vector3(0.4f, 0.35f, 0.3f),
                new Vector3(0.2f, 0.2f, 0.2f));
        }

        // ------------------------------------------------------------------
        //  Material instances
        // ------------------------------------------------------------------

        var materialCompiler = new MaterialCompiler();

        // Ground — unlit, sandy tint
        var groundMat = new UnlitTextureMaterial
        {
            Name  = "UnlitGround",
            Tint  = new Color(210, 195, 150),
            Alpha = 1.0f,
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
            ReflectionCube = CreateDebugReflectionCube(gd, 16),
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
                ReflectionTextureFilePath = "SkyCubeMap.dds",
            }));
        var explicitReflectionMat = new LitDiffuseMaterial
        {
            Name = "NeutralProfileExplicitReflection",
            DiffuseColor = new Color(209, 216, 226),
            AmbientColor = new Vector3(0.1f, 0.1f, 0.12f),
            SpecularColor = new Vector3(0.9f),
            SpecularPower = 72f,
            ReflectionCube = explicitReflectionInterpretation.Reflection ? CreateDebugReflectionCube(gd, 16) : null,
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

        // Ground
        SpawnStaticModel("Ground", world, gd,
            new BoxPrimitive(16f, 0.3f, 16f),
            new Vector3(0f, -0.15f, 0f),
            Quaternion.Identity,
            groundMat);

        // Red cube
        SpawnStaticModel("RedCube", world, gd,
            new BoxPrimitive(1.5f, 1.5f, 1.5f),
            new Vector3(-4.5f, 0.75f, 0f),
            Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(25f)),
            redMat);

        // Textured cube
        SpawnStaticModel("TexturedCube", world, gd,
            new BoxPrimitive(1.5f, 1.5f, 1.5f),
            new Vector3(-1.8f, 0.75f, 0f),
            Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(-15f)),
            whiteMat);

        // Sphere A — PropertyBlock (placed via standalone StaticModelSubMeshComponent)
        SpawnSphereWithPropertyBlock("SphereA", world, gd,
            sphereMat, _propBlockA,
            new Vector3(0.9f, 0.9f, 0f));

        // Sphere B — PropertyBlock
        SpawnSphereWithPropertyBlock("SphereB", world, gd,
            sphereMat, _propBlockB,
            new Vector3(3.0f, 0.9f, 0f));

        // Alpha-test panel (cutout)
        SpawnStaticModel("AlphaTestPanel", world, gd,
            new PlanePrimitive(2.3f, 2.3f),
            new Vector3(4.8f, 1.15f, -3.0f),
            Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(-18f), MathHelper.PiOver2, 0f),
            alphaTestMat);

        // Glass cube (transparent)
        SpawnStaticModel("GlassCube", world, gd,
            new BoxPrimitive(1.5f, 1.5f, 1.5f),
            new Vector3(5.2f, 0.75f, 0f),
            Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(40f)),
            glassMat);

        // Normal-mapped box (tangent-ready mesh)
        SpawnStaticModel("NormalMapBox", world, gd,
            new BoxPrimitive(1.8f, 1.8f, 1.8f),
            new Vector3(-1.2f, 0.9f, -3.2f),
            Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(28f), MathHelper.ToRadians(-16f), 0f),
            normalMapMat,
            useTangents: true);

        SpawnStaticModel("ReflectionSphere", world, gd,
            new SpherePrimitive(1.0f, 24),
            new Vector3(1.8f, 1.0f, -3.4f),
            Quaternion.Identity,
            reflectiveMat);

        SpawnStaticModel("AmbientReference", world, gd,
            new BoxPrimitive(1.1f, 1.1f, 1.1f),
            new Vector3(4.2f, 0.55f, 2.6f),
            Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(18f), MathHelper.ToRadians(-10f), 0f),
            ambientOnlyMat);

        SpawnStaticModel("NeutralProfileNamedOpaque", world, gd,
            new BoxPrimitive(1.1f, 1.1f, 1.1f),
            new Vector3(-5.9f, 0.55f, 2.6f),
            Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(12f), MathHelper.ToRadians(-10f), 0f),
            namedOpaqueMat);

        SpawnStaticModel("NeutralProfileExplicitReflection", world, gd,
            new BoxPrimitive(1.1f, 1.1f, 1.1f),
            new Vector3(-4.1f, 0.55f, 2.6f),
            Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(-12f), MathHelper.ToRadians(-10f), 0f),
            explicitReflectionMat);

        SpawnStaticModel("HintsOnlyImportedPresentation", world, gd,
            new PlanePrimitive(1.2f, 1.2f),
            new Vector3(-2.3f, 0.8f, 2.6f),
            Quaternion.CreateFromYawPitchRoll(0f, MathHelper.PiOver2, 0f),
            sharedImportedPresentationMat);

        SpawnStaticModel("EmissiveReference", world, gd,
            new BoxPrimitive(1.1f, 1.1f, 1.1f),
            new Vector3(6.0f, 0.55f, 2.6f),
            Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(-18f), MathHelper.ToRadians(-10f), 0f),
            emissiveOnlyMat);
    }

    // -----------------------------------------------------------------------
    //  Demo.InitializeCamera
    // -----------------------------------------------------------------------

    public override void InitializeCamera(CameraComponent camera)
    {
        ((ArcBallCameraComponent)camera).SetCamera(
            new Vector3(0f, 6f, 15f), Vector3.Zero, Vector3.Up);
    }

    // -----------------------------------------------------------------------
    //  Demo.Update
    // -----------------------------------------------------------------------

    public override void Update(GameTime gameTime)
    {
        var kb = _game?.IsActive == true ? Keyboard.GetState() : new KeyboardState();

        // L — cycle directional light count  1 → 2 → 3 → 1
        if (kb.IsKeyDown(Keys.L) && !_prevKb.IsKeyDown(Keys.L) && _renderer is not null)
        {
            _lightCount = (_lightCount % 3) + 1;
            _renderer.DefaultLighting.ActiveDirectionalLightCount = _lightCount;
        }

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
        _game = null;
        _renderer = null;
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

    /// <summary>
    /// Creates an entity with a single-mesh <see cref="StaticModelComponent"/>
    /// positioned at <paramref name="position"/> with <paramref name="rotation"/>.
    /// </summary>
    private static void SpawnStaticModel(
        string             name,
        Framework.World.World world,
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
        Framework.World.World world,
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

    private static TextureCube CreateDebugReflectionCube(GraphicsDevice gd, int size)
    {
        var textureCube = new TextureCube(gd, size, false, SurfaceFormat.Color);
        FillCubeFace(textureCube, CubeMapFace.PositiveX, size, new Color(210, 118, 82), new Color(255, 222, 180));
        FillCubeFace(textureCube, CubeMapFace.NegativeX, size, new Color(58, 116, 196), new Color(162, 214, 255));
        FillCubeFace(textureCube, CubeMapFace.PositiveY, size, new Color(248, 244, 210), Color.White);
        FillCubeFace(textureCube, CubeMapFace.NegativeY, size, new Color(58, 74, 88), new Color(130, 146, 160));
        FillCubeFace(textureCube, CubeMapFace.PositiveZ, size, new Color(78, 176, 150), new Color(210, 246, 226));
        FillCubeFace(textureCube, CubeMapFace.NegativeZ, size, new Color(188, 90, 170), new Color(246, 204, 238));
        return textureCube;
    }

    private static void FillCubeFace(TextureCube textureCube, CubeMapFace face, int size, Color start, Color end)
    {
        var data = new Color[size * size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = size == 1 ? 0.0f : x / (size - 1f);
            float v = size == 1 ? 0.0f : y / (size - 1f);
            var horizontal = Color.Lerp(start, end, u);
            data[y * size + x] = Color.Lerp(horizontal, Color.White, v * 0.18f);
        }

        textureCube.SetData(face, data);
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
