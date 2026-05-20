using System.ComponentModel;
using BulletSharp;

using CasaEngine.Core.Serialization;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Framework.Scene.Entities.Components;

[DisplayName("Tile Map")]
public class TileMapComponent : SceneComponent, ICollideableComponent, IConditionalEntityUpdateSource
{
    private List<CollisionObject> _collisionObjects = new();
    private readonly List<AutoTile> _autoTiles = new();
    private List<TileMapLayer> Layers { get; } = new();
    private bool _hasAnimatedTiles;
    private bool _needsAutoTileRefresh;
    private IPhysicsWorldContext? _physicsWorldContext;

    public Guid TileMapDataAssetId { get; set; } = Guid.Empty;
    public TileMapData TileMapData { get; set; }
    public TileSetData TileSetData { get; set; }
    public PhysicsType PhysicsType { get; }
    public HashSet<Collision> Collisions { get; } = new();
    public int LastVisitedTileCount { get; private set; }
    public int LastDrawnTileCount { get; private set; }

    public TileMapComponent()
    {
        //Do nothing
    }

    public TileMapComponent(TileMapComponent other) : base(other)
    {
        TileMapData = other.TileMapData;
        TileSetData = other.TileSetData;
        TileMapDataAssetId = other.TileMapDataAssetId;
    }

    public override void InitializeWithWorld(CasaEngine.Framework.Scene.World.World world)
    {
        base.InitializeWithWorld(world);

        Layers.Clear();
        _autoTiles.Clear();
        _collisionObjects.Clear();
        _hasAnimatedTiles = false;
        _needsAutoTileRefresh = false;
        _physicsWorldContext = Owner.World.PhysicsWorldContext;

        if (TileMapDataAssetId != Guid.Empty)
        {
            TileMapData = Owner.World.Game.AssetContentManager.Load<TileMapData>(TileMapDataAssetId);
        }

        if (TileMapData == null)
        {
            return;
        }

        TileMapData.Validate();

        TileSetData = Owner.World.Game.AssetContentManager.Load<TileSetData>(TileMapData.TileSetDataAssetId);

        var texture = Owner.World.Game.AssetContentManager.Load<Texture>(TileSetData.SpriteSheetAssetId);
        texture.Load(Owner.World.Game.AssetContentManager);

        for (var layerIndex = 0; layerIndex < TileMapData.Layers.Count; layerIndex++)
        {
            var tileMapLayerData = TileMapData.Layers[layerIndex];
            var tileMapLayer = new TileMapLayer(tileMapLayerData);
            Layers.Add(tileMapLayer);
            var mapWidth = TileMapData.MapSize.Width;
            var mapHeight = TileMapData.MapSize.Height;

            for (var y = 0; y < mapHeight; y++)
            {
                for (var x = 0; x < mapWidth; x++)
                {
                    tileMapLayer.Tiles.Add(CreateRuntimeTile(texture.Resource, tileMapLayerData, layerIndex, x, y));
                    tileMapLayer.CollisionObjects.Add(CreateCollisionObject(layerIndex, x, y));
                }
            }
        }

        IsBoundingBoxDirty = true;

        if (_needsAutoTileRefresh)
        {
            Owner?.Policies.RequestConditionalUpdate();
        }
    }

    public override TileMapComponent Clone()
    {
        return new TileMapComponent(this);
    }

    public bool ShouldUpdateWhenConditional(Entity owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return _hasAnimatedTiles || _needsAutoTileRefresh;
    }

    public override void Update(float elapsedTime)
    {
        if (_needsAutoTileRefresh)
        {
            for (var index = 0; index < _autoTiles.Count; index++)
            {
                _autoTiles[index].Update(elapsedTime);
            }
        }

        _needsAutoTileRefresh = false;

        base.Update(elapsedTime);
    }

    public override BoundingBox GetBoundingBox()
    {
        if (TileMapData != null)
        {
            var p0 = Vector3.Zero;
            var p1 = new Vector3(
                TileMapData.MapSize.Width * TileSetData.TileSize.Width,
                -TileMapData.MapSize.Height * TileSetData.TileSize.Height,
                0f);

            var minZ = TileMapData.Layers.Count > 0 ? TileMapData.Layers[0].zOffset : 0f;
            var maxZ = minZ;

            for (var layerIndex = 1; layerIndex < TileMapData.Layers.Count; layerIndex++)
            {
                var zOffset = TileMapData.Layers[layerIndex].zOffset;
                if (zOffset < minZ)
                {
                    minZ = zOffset;
                }

                if (zOffset > maxZ)
                {
                    maxZ = zOffset;
                }
            }

            var min = new Vector3(
                Math.Min(p0.X, p1.X),
                Math.Min(p0.Y, p1.Y),
                minZ);
            var max = new Vector3(
                Math.Max(p0.X, p1.X),
                Math.Max(p0.Y, p1.Y),
                maxZ);

            return new BoundingBox(min, max).Transform(WorldMatrixWithScale);
        }

        const float length = 0.5f;
        return new BoundingBox(Vector3.One * -length, Vector3.One * length).Transform(WorldMatrixWithScale);
    }

    public override void Draw(float elapsedTime)
    {
        LastVisitedTileCount = 0;
        LastDrawnTileCount = 0;

        if (TileMapData == null)
        {
            return;
        }

        var translation = Position;
        var scale = Scale.ToVector2();

        var mapWidth = TileMapData.MapSize.Width;
        var mapHeight = TileMapData.MapSize.Height;

        if (mapWidth <= 0 || mapHeight <= 0)
        {
            return;
        }

        var tileWidth = TileSetData.TileSize.Width * scale.X;
        var tileHeight = TileSetData.TileSize.Height * scale.Y;
        var mapPosX = translation.X;
        var mapPosY = translation.Y;
        var minTileX = 0;
        var maxTileX = mapWidth - 1;
        var minTileY = 0;
        var maxTileY = mapHeight - 1;

        if (TryGetVisibleTileRange(tileWidth, tileHeight, mapPosX, mapPosY, mapWidth, mapHeight,
                out var visibleMinTileX, out var visibleMaxTileX, out var visibleMinTileY, out var visibleMaxTileY))
        {
            if (visibleMinTileX > visibleMaxTileX || visibleMinTileY > visibleMaxTileY)
            {
                return;
            }

            minTileX = visibleMinTileX;
            maxTileX = visibleMaxTileX;
            minTileY = visibleMinTileY;
            maxTileY = visibleMaxTileY;
        }

        foreach (var layer in Layers)
        {
            var layerZ = layer.TileMapLayerData.zOffset;

            for (var y = minTileY; y <= maxTileY; y++)
            {
                var rowOffset = y * mapWidth;

                for (var x = minTileX; x <= maxTileX; x++)
                {
                    var tileIndex = rowOffset + x;
                    LastVisitedTileCount++;

                    if (layer.TileMapLayerData.tiles[tileIndex] == TileMapData.EmptyTileId)
                    {
                        continue;
                    }

                    LastDrawnTileCount++;
                    layer.Tiles[tileIndex].Draw(mapPosX + tileWidth * x, mapPosY - tileHeight * y, translation.Z + layerZ, scale);
                }
            }
        }
    }

    public void RemoveTile(int layer, int x, int y)
    {
        SetTile(layer, x, y, TileMapData.EmptyTileId);
    }

    public int GetTileId(int layerIndex, int x, int y)
    {
        EnsureTileMapLoaded();
        return TileMapData.GetTileId(layerIndex, x, y);
    }

    public void SetTile(int layerIndex, int x, int y, int tileId)
    {
        EnsureTileMapLoaded();
        EnsureValidTileId(tileId);

        var tileIndex = TileMapData.GetTileIndex(x, y);
        var layerData = TileMapData.Layers[layerIndex];
        var layerRuntime = Layers[layerIndex];

        if (layerData.tiles[tileIndex] == tileId)
        {
            return;
        }

        layerData.tiles[tileIndex] = tileId;
        ReplaceRuntimeTile(layerRuntime, layerData, layerIndex, x, y, tileIndex);

        _needsAutoTileRefresh = true;
        IsBoundingBoxDirty = true;
        Owner?.Policies.RequestConditionalUpdate();
    }

    public override void Load(JObject element)
    {
        base.Load(element);
        TileMapDataAssetId = element["tile_map_data_asset_id"].GetGuid();
    }

    private void EnsureTileMapLoaded()
    {
        if (TileMapData == null)
        {
            throw new InvalidOperationException("TileMapData must be loaded before accessing tile data.");
        }

        if (TileSetData == null)
        {
            throw new InvalidOperationException("TileSetData must be loaded before accessing tile data.");
        }
    }

    private void EnsureValidTileId(int tileId)
    {
        if (tileId == TileMapData.EmptyTileId)
        {
            return;
        }

        if (!TileSetData.IsKnownTileId(tileId))
        {
            throw new ArgumentException($"Unknown tile id '{tileId}'.", nameof(tileId));
        }
    }

    private Tile CreateRuntimeTile(Texture2D texture, TileMapLayerData tileMapLayerData, int layerIndex, int x, int y)
    {
        var tileId = tileMapLayerData.tiles[TileMapData.GetTileIndex(x, y)];
        Tile tile;

        if (tileId == TileMapData.EmptyTileId)
        {
            tile = new EmptyTile();
        }
        else if (TileSetData.TryGetTileData(tileId, out var tileData) && tileData != null)
        {
            switch (tileData.Type)
            {
                case TileType.Auto:
                    var autoTileData = tileData as AutoTileData ?? throw new InvalidOperationException($"Tile {tileId} is not a valid auto tile.");
                    var autoTile = new AutoTile(texture, autoTileData);
                    autoTile.SetTileInfo(TileSetData.TileSize, TileMapData.MapSize, tileMapLayerData, x, y);
                    tile = autoTile;
                    _autoTiles.Add(autoTile);
                    _needsAutoTileRefresh = true;
                    break;

                case TileType.Static:
                    tile = new StaticTile(texture, tileData as StaticTileData);
                    break;

                default:
                    throw new ArgumentException($"tile type not supported {tileData.Type}");
            }
        }
        else
        {
            throw new InvalidOperationException($"Unknown tile id '{tileId}' in layer {layerIndex} at ({x}, {y}).");
        }

        tile.Initialize(Owner.World.Game);
        return tile;
    }

    private CollisionObject? CreateCollisionObject(int layerIndex, int x, int y)
    {
        var tileId = TileMapData.GetTileId(layerIndex, x, y);
        if (tileId == TileMapData.EmptyTileId)
        {
            return null;
        }

        if (!TileSetData.TryGetTileData(tileId, out var tileData) || tileData == null)
        {
            return null;
        }

        if (tileData.CollisionType != TileCollisionType.NoContactResponse && tileData.CollisionType != TileCollisionType.Blocked)
        {
            return null;
        }

        if (_physicsWorldContext == null)
        {
            return null;
        }

        var tileSize = TileSetData.TileSize;
        var worldMatrix = WorldMatrixNoScale;
        worldMatrix.Translation += new Vector3(
            x * tileSize.Width + tileSize.Width / 2f,
            -y * tileSize.Height - tileSize.Height / 2f,
            0f);
        var box = new BoxShape(tileSize.Width / 2f, tileSize.Height / 2f, 0.5f)
        {
            LocalScaling = LocalScale,
            UserObject = this,
        };

        var tileCollisionManager = new TileCollisionManager(this, layerIndex, x, y);

        CollisionObject collisionObject;
        if (tileData.CollisionType == TileCollisionType.NoContactResponse)
        {
            collisionObject = _physicsWorldContext.AddGhostObject(box, ref worldMatrix, tileCollisionManager);
        }
        else
        {
            collisionObject = _physicsWorldContext.AddStaticObject(box, LocalScale, ref worldMatrix, tileCollisionManager,
                new PhysicsDefinition { Friction = 0f });
        }

        _collisionObjects.Add(collisionObject);
        return collisionObject;
    }

    private void ReplaceRuntimeTile(TileMapLayer layerRuntime, TileMapLayerData layerData, int layerIndex, int x, int y, int tileIndex)
    {
        if (layerRuntime.Tiles[tileIndex] is AutoTile oldAutoTile)
        {
            _autoTiles.Remove(oldAutoTile);
        }

        RemoveCollisionObject(layerRuntime.CollisionObjects[tileIndex]);
        layerRuntime.CollisionObjects[tileIndex] = null;

        var texture = Owner.World.Game.AssetContentManager.Load<Texture>(TileSetData.SpriteSheetAssetId);
        texture.Load(Owner.World.Game.AssetContentManager);

        layerRuntime.Tiles[tileIndex] = CreateRuntimeTile(texture.Resource, layerData, layerIndex, x, y);
        layerRuntime.CollisionObjects[tileIndex] = CreateCollisionObject(layerIndex, x, y);
    }

    private bool TryGetVisibleTileRange(
        float tileWidth,
        float tileHeight,
        float mapPosX,
        float mapPosY,
        int mapWidth,
        int mapHeight,
        out int minTileX,
        out int maxTileX,
        out int minTileY,
        out int maxTileY)
    {
        minTileX = 0;
        maxTileX = mapWidth - 1;
        minTileY = 0;
        maxTileY = mapHeight - 1;

        if (tileWidth <= 0f || tileHeight <= 0f)
        {
            return false;
        }

        var currentRenderFrame = Owner.World.CurrentRenderFrame;
        if (!currentRenderFrame.HasValue)
        {
            return false;
        }

        if (!TryGetWorldViewBounds(currentRenderFrame.Value, Position.Z, out var viewMinX, out var viewMaxX, out var viewMinY, out var viewMaxY))
        {
            return false;
        }

        var rawMinTileX = (int)Math.Floor((viewMinX - mapPosX) / tileWidth) - 1;
        var rawMaxTileX = (int)Math.Floor((viewMaxX - mapPosX) / tileWidth) + 1;
        var rawMinTileY = (int)Math.Floor((mapPosY - viewMaxY) / tileHeight) - 1;
        var rawMaxTileY = (int)Math.Floor((mapPosY - viewMinY) / tileHeight) + 1;

        if (rawMaxTileX < 0 || rawMaxTileY < 0 || rawMinTileX >= mapWidth || rawMinTileY >= mapHeight)
        {
            minTileX = 0;
            maxTileX = -1;
            minTileY = 0;
            maxTileY = -1;
            return true;
        }

        minTileX = Math.Clamp(rawMinTileX, 0, mapWidth - 1);
        maxTileX = Math.Clamp(rawMaxTileX, 0, mapWidth - 1);
        minTileY = Math.Clamp(rawMinTileY, 0, mapHeight - 1);
        maxTileY = Math.Clamp(rawMaxTileY, 0, mapHeight - 1);
        return true;
    }

    private static bool TryGetWorldViewBounds(
        in RenderFrame frame,
        float planeZ,
        out float minX,
        out float maxX,
        out float minY,
        out float maxY)
    {
        var viewportRect = frame.ViewportRect;
        minX = float.MaxValue;
        maxX = float.MinValue;
        minY = float.MaxValue;
        maxY = float.MinValue;

        if (viewportRect.Width <= 0 || viewportRect.Height <= 0)
        {
            return false;
        }

        var viewport = new Viewport(viewportRect.X, viewportRect.Y, viewportRect.Width, viewportRect.Height)
        {
            MinDepth = 0f,
            MaxDepth = 1f,
        };

        return IncludeViewportCorner(viewport, frame.View, frame.Projection, viewportRect.Left, viewportRect.Top, planeZ, ref minX, ref maxX, ref minY, ref maxY)
            && IncludeViewportCorner(viewport, frame.View, frame.Projection, viewportRect.Right, viewportRect.Top, planeZ, ref minX, ref maxX, ref minY, ref maxY)
            && IncludeViewportCorner(viewport, frame.View, frame.Projection, viewportRect.Left, viewportRect.Bottom, planeZ, ref minX, ref maxX, ref minY, ref maxY)
            && IncludeViewportCorner(viewport, frame.View, frame.Projection, viewportRect.Right, viewportRect.Bottom, planeZ, ref minX, ref maxX, ref minY, ref maxY);
    }

    private static bool IncludeViewportCorner(
        Viewport viewport,
        Matrix view,
        Matrix projection,
        float screenX,
        float screenY,
        float planeZ,
        ref float minX,
        ref float maxX,
        ref float minY,
        ref float maxY)
    {
        var nearPoint = viewport.Unproject(new Vector3(screenX, screenY, 0f), projection, view, Matrix.Identity);
        var farPoint = viewport.Unproject(new Vector3(screenX, screenY, 1f), projection, view, Matrix.Identity);
        var direction = farPoint - nearPoint;

        if (Math.Abs(direction.Z) < 0.0001f)
        {
            return false;
        }

        var distance = (planeZ - nearPoint.Z) / direction.Z;
        var worldPoint = nearPoint + direction * distance;

        minX = Math.Min(minX, worldPoint.X);
        maxX = Math.Max(maxX, worldPoint.X);
        minY = Math.Min(minY, worldPoint.Y);
        maxY = Math.Max(maxY, worldPoint.Y);
        return true;
    }

    private void RemoveCollisionObject(CollisionObject? collisionObject)
    {
        if (collisionObject == null)
        {
            return;
        }

        if (_physicsWorldContext == null)
        {
            return;
        }

        _physicsWorldContext.ClearCollisionDataFrom(this);

        if (collisionObject is RigidBody rigidBody)
        {
            _physicsWorldContext.RemoveRigidBody(rigidBody);
        }
        else
        {
            _physicsWorldContext.RemoveCollisionObject(collisionObject);
        }

        _collisionObjects.Remove(collisionObject);
        collisionObject.Dispose();
    }

}