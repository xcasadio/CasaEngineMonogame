using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Materials.Runtime;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Shows the render-to-texture tile map mode: the map is painted offscreen by a
/// <see cref="TileMapSurfaceComponent"/> in its natural orthographic pixel space, and the resulting
/// texture is displayed on an arcade screen quad inside a perspective 3D scene. The map itself is
/// never drawn in the world pass (<see cref="TileMapComponent.SkipMainPassDraw"/>).
/// </summary>
public sealed class TileMapSurfaceScreenDemo : Demo
{
    // map_1_1 is 30 x 11 tiles of 32 pixels: keep the screen quad at the map aspect ratio so the
    // texels stay square.
    private const int MapPixelWidth = 30 * 32;
    private const int MapPixelHeight = 11 * 32;
    private const float ScreenWidth = 9.0f;
    private const float ScreenHeight = ScreenWidth * MapPixelHeight / MapPixelWidth;

    private static readonly Vector3 InitialCameraTarget = new(0.0f, 3.5f, 0.0f);

    private CasaEngineGame? _game;
    private TileMapSurfaceComponent? _tileMapSurface;

    public override string Title => "Tile map surface screen demo";
    public override string Description => "Renders a tile map offscreen in orthographic pixel space and displays the texture on a 3D arcade screen.";

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        var world = game.GameManager.CurrentWorld;

        var groundEntity = new Entity { Name = "Ground" };
        var groundMesh = new StaticModelComponent();
        groundEntity.RootComponent = groundMesh;
        groundMesh.StaticModel = StaticModel.CreateFromPrimitive(new BoxPrimitive(24, 1, 24));
        groundMesh.StaticModel.Meshes[0].Initialize(game.GraphicsDevice);
        groundMesh.StaticModel.Meshes[0].Material = new LitDiffuseMaterial { DiffuseColor = new Color(160, 150, 135) };
        groundMesh.LocalPosition = new Vector3(0, -0.5f, 0);
        world.AddEntity(groundEntity);

        //============ tile map rendered offscreen only ===============
        var assetInfo = AssetCatalog.GetByFileName(@"Maps\map_1_1.tileMap");
        var tileMapData = game.AssetContentManager.Load<TileMapData>(assetInfo.Id);

        var tileMapEntity = new Entity { Name = "OffscreenTileMap" };
        var tileMapComponent = new TileMapComponent
        {
            TileMapData = tileMapData,
            SkipMainPassDraw = true,
        };
        tileMapEntity.RootComponent = tileMapComponent;
        world.AddEntity(tileMapEntity);

        //============ arcade screen showing the produced texture ===============
        var screenEntity = new Entity { Name = "TileMapScreen" };
        var screenMesh = new StaticModelComponent();
        screenEntity.RootComponent = screenMesh;
        screenMesh.StaticModel = StaticModel.CreateFromPrimitive(new PlanePrimitive(ScreenWidth, ScreenHeight));
        screenMesh.StaticModel.Meshes[0].Initialize(game.GraphicsDevice);

        // PointClamp is what keeps the tiles crisp whatever the 3d camera does: the surface only
        // exposes the texture, the sampling policy belongs to the material displaying it.
        var screenMaterial = new UnlitTextureMaterial
        {
            Tint = Color.White,
            Alpha = 1.0f,
            SamplerState = SamplerState.PointClamp,
        };

        screenMesh.StaticModel.Meshes[0].Material = screenMaterial;
        screenMesh.LocalPosition = new Vector3(0, ScreenHeight / 2f + 1.0f, 0);
        screenMesh.LocalOrientation = Quaternion.CreateFromAxisAngle(Vector3.Right, MathHelper.PiOver2);
        world.AddEntity(screenEntity);

        _tileMapSurface = new TileMapSurfaceComponent(game.GraphicsDevice, tileMapComponent)
        {
            ClearColor = Color.Black,
        };
        _tileMapSurface.BindToMaterial(screenMaterial);
        world.RegisterTileMapSurface(_tileMapSurface);
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        camera.SetPositionAndTarget(new Vector3(0, 5, 14), InitialCameraTarget);
    }

    public override void Update(GameTime gameTime)
    {
    }

    public override void Clean()
    {
        if (_tileMapSurface != null)
        {
            _game?.GameManager.CurrentWorld?.UnregisterTileMapSurface(_tileMapSurface);
            _tileMapSurface.Dispose();
            _tileMapSurface = null;
        }

        _game = null;
    }
}
