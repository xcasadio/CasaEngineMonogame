using CasaEngine.Engine.Primitives3D;
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
///   Glass cube          : <see cref="UnlitTextureMaterial"/>  — semi-transparent, <see cref="RenderQueue.Transparent"/>
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
        "transparent render queue.  L = cycle lights,  T = cycle sphere tints.";

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
        var sphereMat = (LitDiffuseMaterial)new MaterialCompiler().CompileRuntimeMaterial(_sphereMaterialAsset, game.AssetContentManager);

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

        // Glass cube (transparent)
        SpawnStaticModel("GlassCube", world, gd,
            new BoxPrimitive(1.5f, 1.5f, 1.5f),
            new Vector3(5.2f, 0.75f, 0f),
            Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(40f)),
            glassMat);
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
        MaterialBase       material)
    {
        var mesh = BuildMesh(name, primitive, gd);
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
    private static StaticModelMesh BuildMesh(string name, GeometricPrimitive primitive, GraphicsDevice gd)
    {
        var mesh = new StaticModelMesh { Name = name };
        mesh.SetData(primitive.Vertices.ToArray(), primitive.Indices.ToArray());
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
}
