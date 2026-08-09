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

    // The arcade screen is centered on the camera target so that it sits in the middle of the image.
    private static readonly Vector3 ScreenCenter = new(0.0f, ScreenHeight / 2f + 1.0f, 0.0f);

    // The tile map lives in the world at pixel scale: it still generates its collision bodies, which
    // would fill the playable area with 960 x 352 units of invisible colliders. Park it far below the
    // scene — the offscreen frame is built relative to the map position, so the rendering is identical.
    private static readonly Vector3 OffscreenTileMapPosition = new(0.0f, -5000.0f, 0.0f);

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
        tileMapComponent.LocalPosition = OffscreenTileMapPosition;
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
        screenMesh.LocalPosition = ScreenCenter;
        screenMesh.LocalOrientation = Quaternion.CreateFromAxisAngle(Vector3.Right, MathHelper.PiOver2);
        world.AddEntity(screenEntity);

        _tileMapSurface = new TileMapSurfaceComponent(game.GraphicsDevice, tileMapComponent)
        {
            ClearColor = Color.Black,
        };
        _tileMapSurface.BindToMaterial(screenMaterial);
        world.RegisterTileMapSurface(_tileMapSurface);
    }

    // Framing verified numerically at 1920x1080 (FOV = PI/4, near 1, far 1000):
    //   screen: normal (0,0,1), dot(normal, center->camera) = 0.976, frustum = Contains,
    //           corners projecting inside x in [496, 1425] and y in [374, 698] px
    //   ground: normal (0,1,0), dot(normal, center->camera) = 0.427, center at (960, 833) px
    //           (the 24 x 24 ground is wider than the frustum, so it only intersects it)
    public override void InitializeCamera(CameraComponent camera)
    {
        camera.SetPositionAndTarget(new Vector3(0f, 5.487f, 12.687f), ScreenCenter);
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
