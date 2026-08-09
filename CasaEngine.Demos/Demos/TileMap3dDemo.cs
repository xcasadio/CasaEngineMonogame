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
        // A tile map occupies x in [0, MapWidth] and y in [-MapHeight, 0] in its local space (rows grow
        // towards -Y). The -90 degrees rotation around X maps local (x, y, 0) to world (x, 0, -y), so the
        // map covers x in [0, MapWidth] and z in [0, MapHeight]; the offset below re-centers it on the
        // origin. The quad normal (local +Z) becomes world +Y, so the ground is seen from above.
        var groundEntity = new Entity { Name = "TileMapGround" };
        var groundTileMap = new TileMapComponent();
        groundEntity.RootComponent = groundTileMap;
        groundTileMap.TileMapData = tileMapData;
        groundTileMap.LocalScale = new Vector3(MapScale);
        groundTileMap.LocalOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathHelper.PiOver2);
        groundTileMap.LocalPosition = new Vector3(-MapWidth / 2f, 0f, -MapHeight / 2f);
        world.AddEntity(groundEntity);

        //============ wall tile map (rotated +90 degrees around Y, standing on the ground) ===============
        // Tile quads are single sided (front face towards local +Z, CullCounterClockwise). A +90 degrees
        // rotation around Y maps local (x, y, 0) to world (0, y, -x) and turns that normal towards world
        // +X, which faces the camera placed on the +X / +Z side in InitializeCamera; the opposite rotation
        // would leave the wall back-face culled. The offset puts the wall on the left edge of the ground
        // (x = -MapWidth / 2), standing on it (y in [0, MapHeight]) and centered in z.
        var wallEntity = new Entity { Name = "TileMapWall" };
        var wallTileMap = new TileMapComponent();
        wallEntity.RootComponent = wallTileMap;
        wallTileMap.TileMapData = tileMapData;
        wallTileMap.LocalScale = new Vector3(MapScale);
        wallTileMap.LocalOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathHelper.PiOver2);
        wallTileMap.LocalPosition = new Vector3(-MapWidth / 2f, MapHeight, MapWidth / 2f);
        world.AddEntity(wallEntity);
    }

    // The framing is expressed through the explicit arc ball orbit parameters instead of SetCamera /
    // SetPositionAndTarget: ArcBallCameraComponent.SetCamera negates the requested target and stores a
    // negative distance, which mirrors the resulting camera in Z whenever the target is not the origin.
    // With Target / Yaw / Pitch / Distance the placement is unambiguous:
    //   position = Target + (-sin(Yaw) * cos(Pitch), -sin(Pitch), cos(Yaw) * cos(Pitch)) * Distance
    // Values below verified numerically at 1920x1080 (FOV = PI/4, near 1, far 1000):
    //   real camera position = (34.686, 42.360, 50.701)
    //   ground: normal (0,1,0), dot(normal, center->camera) = 0.568, frustum = Contains, center at (960, 676) px
    //   wall  : normal (1,0,0), dot(normal, center->camera) = 0.694, frustum = Contains, center at (645, 437) px
    //   every corner of both maps projects inside [0,1920]x[0,1080].
    public override void InitializeCamera(CameraComponent camera)
    {
        var arcBall = (ArcBallCameraComponent)camera;
        arcBall.Target = new Vector3(0f, MapHeight / 2f, 0f);
        arcBall.Yaw = -0.6f;
        arcBall.Pitch = -0.5f;
        arcBall.Distance = 70f;
    }

    public override void Update(GameTime gameTime)
    {
    }

    public override void Clean()
    {
    }
}
