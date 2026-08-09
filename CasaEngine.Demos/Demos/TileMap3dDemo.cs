using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Shows a tile map used as a plain 3D object: the same map is placed twice, once rotated flat on the
/// ground and once standing as a wall, and is viewed through the regular perspective arc ball camera.
/// Both instances take the rotated draw path (full world matrix + per chunk frustum culling).
/// </summary>
public class TileMap3dDemo : Demo
{
    // The map is authored in pixels (30 x 11 tiles of 32 pixels): scale it down to the
    // metric scale used by the other 3D demos.
    private const float MapScale = 0.05f;
    private const float MapWidth = 30 * 32 * MapScale;
    private const float MapHeight = 11 * 32 * MapScale;

    public override string Title => "Tile map 3d demo";
    public override string Description => "Places the same tile map as a rotated ground plane and as a vertical wall under a free perspective camera.";

    public override void Initialize(CasaEngineGame game)
    {
        var world = game.GameManager.CurrentWorld;

        var assetInfo = AssetCatalog.GetByFileName(@"Maps\map_1_1.tileMap");
        var tileMapData = game.AssetContentManager.Load<TileMapData>(assetInfo.Id);

        //============ ground tile map (rotated -90 degrees around X) ===============
        var groundEntity = new Entity { Name = "TileMapGround" };
        var groundTileMap = new TileMapComponent();
        groundEntity.RootComponent = groundTileMap;
        groundTileMap.TileMapData = tileMapData;
        groundTileMap.LocalScale = new Vector3(MapScale);
        groundTileMap.LocalOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathHelper.PiOver2);
        groundTileMap.LocalPosition = Vector3.Zero;
        world.AddEntity(groundEntity);

        //============ wall tile map (rotated +90 degrees around Y, standing on the ground) ===============
        // Tile quads are single sided (front face towards local +Z, CullCounterClockwise). A +90 degrees
        // rotation around Y turns that normal towards world +X, which faces the default camera placed at
        // positive X in InitializeCamera; the opposite rotation would leave the wall back-face culled.
        var wallEntity = new Entity { Name = "TileMapWall" };
        var wallTileMap = new TileMapComponent();
        wallEntity.RootComponent = wallTileMap;
        wallTileMap.TileMapData = tileMapData;
        wallTileMap.LocalScale = new Vector3(MapScale);
        wallTileMap.LocalOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathHelper.PiOver2);
        wallTileMap.LocalPosition = new Vector3(0f, MapHeight, 0f);
        world.AddEntity(wallEntity);
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        var target = new Vector3(MapWidth / 2f, MapHeight / 2f, MapHeight / 2f);
        ((ArcBallCameraComponent)camera).SetCamera(target + new Vector3(0f, 30f, 45f), target, Vector3.Up);
    }

    public override void Update(GameTime gameTime)
    {
    }

    public override void Clean()
    {
    }
}
