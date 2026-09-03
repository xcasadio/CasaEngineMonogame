using CasaEngine.Core.Logging;
using System.ComponentModel;

using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Core.Serialization;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Engine.Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Framework.Scene.Entities.Components;

[DisplayName("Tile Map")]
public class TileMapComponent : SceneComponent, ICollideableComponent, IConditionalEntityUpdateSource
{
    /// <summary>
    /// One entry of the runtime-only sorted tile overlay (see <see cref="AddSortedOverlayTile"/>).
    /// Never serialized: it only ever lives in <see cref="_sortedOverlayTiles"/>.
    /// </summary>
    private readonly struct SortedOverlayTile
    {
        public SortedOverlayTile(TileMapTileReference tileReference, int gridX, int gridY, Tile tile, in RenderSortKey2D sortKey)
        {
            TileReference = tileReference;
            GridX = gridX;
            GridY = gridY;
            Tile = tile;
            SortKey = sortKey;
        }

        public readonly TileMapTileReference TileReference;
        public readonly int GridX;
        public readonly int GridY;

        /// <summary>
        /// The tile instance resolved at <see cref="AddSortedOverlayTile"/> time through the same creation
        /// path flat layers use. An <see cref="AnimatedTile"/> instance is registered in
        /// <see cref="_animatedTiles"/> so the component's regular per-frame update advances it exactly
        /// like a flat-layer tile of the same kind; its current source rectangle is then read every draw
        /// via <see cref="Assets.TileMap.Tile.GetCurrentSourceRectangle"/> instead of a static lookup.
        ///
        /// SHARED, NOT PER ENTRY: every live entry with the same <see cref="TileReference"/> holds the very
        /// same instance, handed out by <see cref="_overlayTileCache"/> — that is what keeps an animated
        /// overlay tile's phase across a rebuild, and what keeps sibling entries of one reference in phase
        /// with each other, as they are in the original renderer. Never give <see cref="Assets.TileMap.Tile"/>
        /// per-entry state on the strength of this field: writing through it would corrupt every sibling.
        /// Everything that legitimately differs between entries — position, sort key, and the texture the
        /// draw path resolves from <see cref="TileReference"/> — lives on the entry, not on the tile.
        /// </summary>
        public readonly Tile Tile;
        public readonly RenderSortKey2D SortKey;
    }

    private const float AxisAlignedWorldMatrixEpsilon = 1e-4f;
    private const float SlabClipEpsilon = 1e-6f;

    /// <summary>
    /// The twelve frustum edges as pairs of corner indices, in
    /// <see cref="BoundingFrustum.GetCorners(Vector3[])"/> order: near quad, far quad, then the
    /// four near/far connections.
    /// </summary>
    private static readonly int[] FrustumEdgeCornerIndices =
    {
        0, 1, 1, 2, 2, 3, 3, 0,
        4, 5, 5, 6, 6, 7, 7, 4,
        0, 4, 1, 5, 2, 6, 3, 7,
    };

    private List<PhysicsBody> _collisionObjects = new();

    /// <summary>
    /// Tracks the <see cref="TileCollisionManager"/> that backs each body created by
    /// <see cref="CreateCollisionObject(int, Rectangle, TileCollisionType)"/>/<see cref="CreateCollisionObject(int, int, int, Collision2d, TileCollisionType)"/>,
    /// so <see cref="RemoveCollisionObject"/> can detach the manager and clear the physics world's
    /// collision bookkeeping for it (not for this component) when the body is destroyed.
    /// </summary>
    private readonly Dictionary<PhysicsBody, TileCollisionManager> _collisionManagersByBody = new();
    private readonly List<AutoTile> _autoTiles = new();
    private readonly List<AnimatedTile> _animatedTiles = new();
    private readonly List<AutoTile> _dirtyAutoTiles = new();
    private readonly HashSet<AutoTile> _dirtyAutoTileSet = new();
    private readonly List<TileSetData> _tileSets = new();
    private readonly List<Texture2D> _tileSetTextures = new();
    private readonly List<SortedOverlayTile> _sortedOverlayTiles = new();

    /// <summary>
    /// Caches the <see cref="Tile"/> instance created for each distinct <see cref="TileMapTileReference"/>
    /// ever queued in the sorted overlay, keyed by reference so an overlay rebuild (<see cref="ClearSortedOverlayTiles"/>
    /// followed by <see cref="AddSortedOverlayTile"/> resubmitting the same references) reuses the same
    /// instance instead of creating a fresh one — the fix for animated overlay tiles restarting at frame 0
    /// on every rebuild (see <see cref="CreateOverlayTile"/>). Entries are never evicted except in
    /// <see cref="InitializeWithWorld"/>, where the overlay itself is already cleared.
    /// </summary>
    private readonly Dictionary<TileMapTileReference, CachedOverlayTile> _overlayTileCache = new();

    /// <summary>
    /// One cached overlay tile instance plus the number of currently live overlay entries referencing it.
    /// The reference count, not the cache entry's mere existence, drives <see cref="_animatedTiles"/>
    /// registration: a shared animated instance must be registered exactly once regardless of how many
    /// overlay entries use it (see <see cref="AddSortedOverlayTile"/>/<see cref="ClearSortedOverlayTiles"/>),
    /// or it would be updated once per entry and animate that many times too fast.
    /// </summary>
    private sealed class CachedOverlayTile
    {
        public CachedOverlayTile(Tile tile)
        {
            Tile = tile;
        }

        public readonly Tile Tile;
        public int RefCount;
    }

    private List<TileMapLayer> Layers { get; } = new();
    private int _chunkTileSize = 16;
    private bool _hasAnimatedTiles;
    private bool _needsAutoTileRefresh;
    private IPhysicsWorld _physicsWorldContext;
    private SpriteRendererComponent _spriteRendererComponent;
    private Texture2D _tileSetTexture;
    private BoundingFrustum _cullingFrustum;
    private Vector3[] _frustumCorners;

    public Guid TileMapDataAssetId { get; set; } = Guid.Empty;
    public TileMapData TileMapData { get; set; }
    public TileSetData TileSetData { get; set; }
    public PhysicsType PhysicsType { get; }
    public HashSet<Collision> Collisions { get; } = new();
    public int LastVisitedTileCount { get; private set; }
    public int LastDrawnTileCount { get; private set; }
    public int LastVisitedChunkCount { get; private set; }
    public int LastStaticBatchCount { get; private set; }
    public int LastStaticBatchTileCount { get; private set; }

    /// <summary>
    /// When true the component is skipped by the regular world draw pass. Set it when the tile map is
    /// routed to a <see cref="Rendering.TileMapSurfaceComponent"/> so that it is only painted offscreen.
    /// Runtime only: the flag is not serialized with the component.
    ///
    /// While it is set, <see cref="Draw"/> returns immediately without touching the draw counters, so
    /// <see cref="LastVisitedTileCount"/> and friends describe the last offscreen surface pass instead
    /// of the world pass — on a static map they simply stay at the values of that pass.
    /// </summary>
    public bool SkipMainPassDraw { get; set; }

    /// <summary>
    /// Counter incremented every time the visual content of the map changes (tile or flag mutation,
    /// auto tile refresh, reload). Consumers cache the last value they rendered and compare it to know
    /// whether they must redraw; a static map keeps the same value forever.
    /// </summary>
    public uint TileRevision { get; private set; }

    /// <summary>True when the map holds at least one animated tile, i.e. its visual changes every tick.</summary>
    public bool HasAnimatedTiles => _hasAnimatedTiles;

    /// <summary>True when auto tiles are waiting for a refresh in the next update.</summary>
    public bool HasDirtyAutoTiles => _dirtyAutoTiles.Count > 0;

    /// <summary>Number of tiles currently queued in the runtime sorted overlay. Exposed for tests and diagnostics.</summary>
    public int SortedOverlayTileCount => _sortedOverlayTiles.Count;

    public int ChunkTileSize
    {
        get => _chunkTileSize;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Chunk tile size must be positive.");
            }

            _chunkTileSize = value;
        }
    }

    public TileMapComponent()
    {
        //Do nothing
    }

    public TileMapComponent(TileMapComponent other) : base(other)
    {
        TileMapData = other.TileMapData;
        TileSetData = other.TileSetData;
        TileMapDataAssetId = other.TileMapDataAssetId;
        SkipMainPassDraw = other.SkipMainPassDraw;
    }

    public override void InitializeWithWorld(CasaEngine.Framework.Scene.World.World world)
    {
        base.InitializeWithWorld(world);

        DisposeChunkGraphicsResources();
        RemoveAllCollisionObjects();
        Layers.Clear();
        _autoTiles.Clear();
        _animatedTiles.Clear();
        _dirtyAutoTiles.Clear();
        _dirtyAutoTileSet.Clear();
        _collisionObjects.Clear();
        _tileSets.Clear();
        _tileSetTextures.Clear();
        _sortedOverlayTiles.Clear();
        _overlayTileCache.Clear();
        _hasAnimatedTiles = false;
        _needsAutoTileRefresh = false;
        _physicsWorldContext = Owner.World.PhysicsWorld;
        _spriteRendererComponent = Owner.World.Game.GetGameComponent<SpriteRendererComponent>();
        _tileSetTexture = null;

        if (TileMapDataAssetId != Guid.Empty)
        {
            TileMapData = Owner.World.Game.AssetContentManager.Load<TileMapData>(TileMapDataAssetId);
        }

        if (TileMapData == null)
        {
            return;
        }

        TileMapData.Validate();
        LoadTileSets();

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
                    tileMapLayer.Tiles.Add(CreateRuntimeTile(tileMapLayerData, layerIndex, x, y));
                    tileMapLayer.CollisionObjects.Add(null);
                }
            }

            BuildChunks(tileMapLayer, layerIndex);
            RebuildLayerCollisionChunks(layerIndex);
        }

        _hasAnimatedTiles = _animatedTiles.Count > 0;

        IsBoundingBoxDirty = true;
        TileRevision++;

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
        return _hasAnimatedTiles || _dirtyAutoTiles.Count > 0;
    }

    public override void Update(float elapsedTime)
    {
        if (_animatedTiles.Count > 0)
        {
            for (var index = 0; index < _animatedTiles.Count; index++)
            {
                _animatedTiles[index].Update(elapsedTime);
            }
        }

        if (_dirtyAutoTiles.Count > 0)
        {
            for (var index = 0; index < _dirtyAutoTiles.Count; index++)
            {
                _dirtyAutoTiles[index].Update(elapsedTime);
            }

            _dirtyAutoTiles.Clear();
            _dirtyAutoTileSet.Clear();
            TileRevision++;
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
        if (SkipMainPassDraw)
        {
            return;
        }

        DrawTileMap();
    }

    /// <summary>
    /// Draws the tile map with the render frame currently set on the world, ignoring
    /// <see cref="SkipMainPassDraw"/>. Used by the offscreen tile map surface pass.
    /// </summary>
    internal void DrawTileMap()
    {
        LastVisitedTileCount = 0;
        LastDrawnTileCount = 0;
        LastVisitedChunkCount = 0;
        LastStaticBatchCount = 0;
        LastStaticBatchTileCount = 0;

        if (TileMapData == null)
        {
            return;
        }

        // The rotation test is done once per draw, never per tile: when the world matrix carries no
        // rotation (the 2D case) the axis-aligned fast path below runs exactly as before.
        var worldMatrix = WorldMatrixWithScale;
        if (!IsAxisAlignedWorldMatrix(in worldMatrix))
        {
            DrawWithWorldMatrix(in worldMatrix);
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

        GetRenderedLayerWorldZRange(translation.Z, out var minPlaneZ, out var maxPlaneZ);

        if (TryGetVisibleTileRange(tileWidth, tileHeight, mapPosX, mapPosY, mapWidth, mapHeight, minPlaneZ, maxPlaneZ,
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

        for (var layerIndex = 0; layerIndex < Layers.Count; layerIndex++)
        {
            var layer = Layers[layerIndex];
            if (!layer.TileMapLayerData.Depth.ShouldRenderTiles)
            {
                continue;
            }

            var layerZ = layer.TileMapLayerData.zOffset;
            var worldZ = translation.Z + layerZ;
            var staticBatchWorld = Matrix.CreateScale(scale.X, scale.Y, 1f) * Matrix.CreateTranslation(translation.X, translation.Y, worldZ);

            for (var chunkIndex = 0; chunkIndex < layer.Chunks.Count; chunkIndex++)
            {
                var chunk = layer.Chunks[chunkIndex];
                if (!chunk.IntersectsTileRange(minTileX, maxTileX, minTileY, maxTileY))
                {
                    continue;
                }

                LastVisitedChunkCount++;
                chunk.UpdateWorldBounds(mapPosX, mapPosY, tileWidth, tileHeight, worldZ, worldZ);

                var staticBatchDrawn = TryDrawStaticChunkBatch(layer, chunk, in staticBatchWorld);
                if (staticBatchDrawn && !chunk.ContainsDynamicTiles)
                {
                    chunk.MarkVisualClean();
                    continue;
                }

                var chunkMinTileX = Math.Max(minTileX, chunk.TileBounds.Left);
                var chunkMaxTileX = Math.Min(maxTileX, chunk.TileBounds.Right - 1);
                var chunkMinTileY = Math.Max(minTileY, chunk.TileBounds.Top);
                var chunkMaxTileY = Math.Min(maxTileY, chunk.TileBounds.Bottom - 1);

                for (var y = chunkMinTileY; y <= chunkMaxTileY; y++)
                {
                    var rowOffset = y * mapWidth;

                    for (var x = chunkMinTileX; x <= chunkMaxTileX; x++)
                    {
                        var tileIndex = rowOffset + x;
                        LastVisitedTileCount++;

                        if (layer.TileMapLayerData.tiles[tileIndex] == TileMapData.EmptyTileId)
                        {
                            continue;
                        }

                        if (staticBatchDrawn && IsStaticTile(layer.TileMapLayerData, tileIndex))
                        {
                            continue;
                        }

                        LastDrawnTileCount++;
                        var flags = layer.TileMapLayerData.GetTileFlags(tileIndex);
                        layer.Tiles[tileIndex].Draw(mapPosX + tileWidth * x, mapPosY - tileHeight * y, worldZ, scale, flags);
                    }
                }

                chunk.MarkVisualClean();
            }
        }

        DrawSortedOverlayTiles(mapPosX, mapPosY, tileWidth, tileHeight, translation.Z, scale, minTileX, maxTileX, minTileY, maxTileY);
    }

    /// <summary>
    /// Draws every tile queued in the runtime sorted overlay (see <see cref="AddSortedOverlayTile"/>)
    /// through the keyed sprite path. Only reachable from the axis-aligned draw path: overlay tiles are
    /// meant for standard 2D top-down tile maps, the case the sorted-tile-vs-entity interleaving problem
    /// targets, so the rotated <see cref="DrawWithWorldMatrix"/> path does not draw them.
    /// </summary>
    private void DrawSortedOverlayTiles(
        float mapPosX,
        float mapPosY,
        float tileWidth,
        float tileHeight,
        float worldZ,
        Vector2 scale,
        int minTileX,
        int maxTileX,
        int minTileY,
        int maxTileY)
    {
        if (_sortedOverlayTiles.Count == 0 || _spriteRendererComponent == null)
        {
            return;
        }

        for (var index = 0; index < _sortedOverlayTiles.Count; index++)
        {
            var overlayTile = _sortedOverlayTiles[index];

            // Culled consistently with the regular tile grid: an overlay tile outside the range visible
            // this frame is skipped exactly like a normal tile at the same grid cell would be, reusing
            // the plane based visible tile range already computed above instead of duplicating it.
            if (overlayTile.GridX < minTileX || overlayTile.GridX > maxTileX
                || overlayTile.GridY < minTileY || overlayTile.GridY > maxTileY)
            {
                continue;
            }

            var texture = _tileSetTextures[overlayTile.TileReference.TileSetIndex];
            var sourceRectangle = overlayTile.Tile.GetCurrentSourceRectangle();

            var worldX = mapPosX + tileWidth * overlayTile.GridX;
            var worldY = mapPosY - tileHeight * overlayTile.GridY;

            // Z POLICY: every overlay tile is submitted at the tile map entity's world Z (translation.Z),
            // never at a layer's zOffset. Every sorted participant (overlay tiles and Y-sorted entity
            // sprites alike) is expected to share that same coplanar Z, so the LessEqual depth test can
            // never override the RenderSortKey2D ordering between them.
            //
            // Scissor rectangle: an empty rectangle, not the device's current one. The sprite renderer's
            // fixed rasterizer state (RasterizerState.CullCounterClockwise) never enables scissor testing,
            // so the stored value is inert; skipping the GraphicsDevice read keeps this hot loop from
            // touching the device once per overlay tile.
            _spriteRendererComponent.DrawSprite(
                texture,
                sourceRectangle,
                Point.Zero,
                new Vector2(worldX, worldY),
                0.0f,
                scale,
                Color.White,
                worldZ,
                in overlayTile.SortKey,
                SpriteEffects.None,
                Rectangle.Empty);
        }
    }

    /// <summary>
    /// Draw path used when the component world matrix carries a rotation: tile quads and chunk
    /// geometry stay in tile map local space and are transformed by the full world matrix.
    /// </summary>
    private void DrawWithWorldMatrix(in Matrix worldMatrix)
    {
        var mapWidth = TileMapData.MapSize.Width;
        var mapHeight = TileMapData.MapSize.Height;

        if (mapWidth <= 0 || mapHeight <= 0)
        {
            return;
        }

        var tileWidth = (float)TileSetData.TileSize.Width;
        var tileHeight = (float)TileSetData.TileSize.Height;

        // The plane based visible tile range is only valid for an axis-aligned map: with a rotation
        // the map is culled chunk by chunk against the view frustum, built once per draw.
        var currentRenderFrame = Owner.World.CurrentRenderFrame;
        BoundingFrustum cullingFrustum = null;
        if (currentRenderFrame.HasValue)
        {
            _cullingFrustum ??= new BoundingFrustum(Matrix.Identity);
            _cullingFrustum.Matrix = currentRenderFrame.Value.ViewProjection;
            cullingFrustum = _cullingFrustum;
        }

        for (var layerIndex = 0; layerIndex < Layers.Count; layerIndex++)
        {
            var layer = Layers[layerIndex];
            if (!layer.TileMapLayerData.Depth.ShouldRenderTiles)
            {
                continue;
            }

            var layerZ = layer.TileMapLayerData.zOffset;
            var staticBatchWorld = Matrix.CreateTranslation(0f, 0f, layerZ) * worldMatrix;

            for (var chunkIndex = 0; chunkIndex < layer.Chunks.Count; chunkIndex++)
            {
                var chunk = layer.Chunks[chunkIndex];
                chunk.UpdateWorldBounds(0f, 0f, tileWidth, tileHeight, layerZ, layerZ, in worldMatrix);

                if (cullingFrustum != null && !cullingFrustum.Intersects(chunk.WorldBounds))
                {
                    continue;
                }

                LastVisitedChunkCount++;

                var staticBatchDrawn = TryDrawStaticChunkBatch(layer, chunk, in staticBatchWorld);
                if (staticBatchDrawn && !chunk.ContainsDynamicTiles)
                {
                    chunk.MarkVisualClean();
                    continue;
                }

                for (var y = chunk.TileBounds.Top; y < chunk.TileBounds.Bottom; y++)
                {
                    var rowOffset = y * mapWidth;

                    for (var x = chunk.TileBounds.Left; x < chunk.TileBounds.Right; x++)
                    {
                        var tileIndex = rowOffset + x;
                        LastVisitedTileCount++;

                        if (layer.TileMapLayerData.tiles[tileIndex] == TileMapData.EmptyTileId)
                        {
                            continue;
                        }

                        if (staticBatchDrawn && IsStaticTile(layer.TileMapLayerData, tileIndex))
                        {
                            continue;
                        }

                        LastDrawnTileCount++;
                        var flags = layer.TileMapLayerData.GetTileFlags(tileIndex);
                        layer.Tiles[tileIndex].Draw(tileWidth * x, -tileHeight * y, layerZ, Vector2.One, flags, in worldMatrix);
                    }
                }

                chunk.MarkVisualClean();
            }
        }
    }

    /// <summary>
    /// Returns true when the world matrix is a pure scale + translation (no rotation, no shear),
    /// which is the condition for the axis-aligned tile map draw and culling fast path.
    /// </summary>
    internal static bool IsAxisAlignedWorldMatrix(in Matrix world)
    {
        var scaleMagnitude = Math.Max(
            Math.Abs(world.M11),
            Math.Max(Math.Abs(world.M22), Math.Abs(world.M33)));
        // The epsilon stays relative to the matrix scale: an absolute floor would classify a real
        // rotation as axis-aligned on a small scaled map and silently drop it from the render.
        var epsilon = AxisAlignedWorldMatrixEpsilon * Math.Max(1e-6f, scaleMagnitude);

        return Math.Abs(world.M12) <= epsilon
            && Math.Abs(world.M13) <= epsilon
            && Math.Abs(world.M21) <= epsilon
            && Math.Abs(world.M23) <= epsilon
            && Math.Abs(world.M31) <= epsilon
            && Math.Abs(world.M32) <= epsilon
            && world.M11 > 0f
            && world.M22 > 0f
            && world.M33 > 0f;
    }

    public void RemoveTile(int layer, int x, int y)
    {
        SetTileReference(layer, x, y, TileMapTileReference.Empty);
    }

    public int GetTileId(int layerIndex, int x, int y)
    {
        EnsureTileMapLoaded();
        return TileMapData.GetTileId(layerIndex, x, y);
    }

    public TileMapTileReference GetTileReference(int layerIndex, int x, int y)
    {
        EnsureTileMapLoaded();
        return TileMapData.GetTileReference(layerIndex, x, y);
    }

    public TileCellFlags GetTileFlags(int layerIndex, int x, int y)
    {
        EnsureTileMapLoaded();
        return TileMapData.GetTileFlags(layerIndex, x, y);
    }

    public void SetTile(int layerIndex, int x, int y, int tileId)
    {
        SetTile(layerIndex, x, y, tileId, TileCellFlags.None);
    }

    public void SetTile(int layerIndex, int x, int y, int tileId, TileCellFlags flags)
    {
        EnsureTileMapLoaded();

        var tileIndex = TileMapData.GetTileIndex(x, y);
        var layerData = TileMapData.Layers[layerIndex];
        var tileSourceIndex = tileId == TileMapData.EmptyTileId
            ? TileMapLayerData.DefaultTileSourceIndex
            : layerData.GetTileSourceIndex(tileIndex);

        SetTileReference(layerIndex, x, y, new TileMapTileReference(tileSourceIndex, tileId), flags);
    }

    public void SetTileReference(int layerIndex, int x, int y, TileMapTileReference tileReference)
    {
        SetTileReference(layerIndex, x, y, tileReference, TileCellFlags.None);
    }

    public void SetTileReference(int layerIndex, int x, int y, TileMapTileReference tileReference, TileCellFlags flags)
    {
        EnsureTileMapLoaded();
        if (tileReference.IsEmpty)
        {
            tileReference = TileMapTileReference.Empty;
        }

        EnsureValidTileReference(tileReference);

        var tileIndex = TileMapData.GetTileIndex(x, y);
        var layerData = TileMapData.Layers[layerIndex];
        var layerRuntime = Layers[layerIndex];
        var previousTileReference = new TileMapTileReference(layerData.GetTileSourceIndex(tileIndex), layerData.tiles[tileIndex]);
        var tileChanged = previousTileReference.TileId != tileReference.TileId
            || previousTileReference.TileSetIndex != tileReference.TileSetIndex;
        var flagsChanged = layerData.GetTileFlags(tileIndex) != flags;

        if (!tileChanged && !flagsChanged)
        {
            return;
        }

        layerData.tiles[tileIndex] = tileReference.TileId;
        if (tileReference.TileSetIndex != TileMapLayerData.DefaultTileSourceIndex || layerData.tileSources.Count > 0)
        {
            layerData.SetTileSourceIndex(tileIndex, tileReference.TileSetIndex);
        }

        layerData.SetTileFlags(tileIndex, flags);

        if (tileChanged)
        {
            ReplaceRuntimeTile(layerRuntime, layerData, layerIndex, x, y, tileIndex);
            RebuildCollisionChunkForTile(layerIndex, x, y);
            MarkAffectedAutoTilesDirty(layerIndex, x, y);
        }

        MarkAffectedChunksDirty(layerIndex, x, y);

        IsBoundingBoxDirty = true;
        TileRevision++;
    }

    public void SetTileFlags(int layerIndex, int x, int y, TileCellFlags flags)
    {
        EnsureTileMapLoaded();

        var tileReference = TileMapData.GetTileReference(layerIndex, x, y);
        SetTileReference(layerIndex, x, y, tileReference, flags);
    }

    /// <summary>
    /// Adds a tile to the runtime-only sorted overlay. Overlay tiles are not part of any
    /// <see cref="TileMapLayer"/>: they are meant for cases where a normal layer tile (typically a wall
    /// tile) must interleave, draw order wise, with Y-sorted entity sprites instead of always drawing
    /// flat with the rest of the map. Each call queues one tile at grid cell
    /// (<paramref name="gridX"/>, <paramref name="gridY"/>) — the same grid the tile map layers use —
    /// drawn every frame at that cell's world position through the keyed sprite path, ordered against
    /// every other keyed sprite (other overlay tiles and Y-sorted entities alike) by
    /// <paramref name="sortKey"/>.
    ///
    /// Overlay entries are runtime-only: they are never written by <see cref="Load"/> or by the editor
    /// serializer, and <see cref="ClearSortedOverlayTiles"/> is the only way to remove them. Gameplay
    /// code (e.g. a world proxy) is expected to populate the overlay once at load time and to call
    /// <see cref="ClearSortedOverlayTiles"/> before repopulating it.
    /// </summary>
    public void AddSortedOverlayTile(TileMapTileReference tileReference, int gridX, int gridY, in RenderSortKey2D sortKey)
    {
        EnsureTileMapLoaded();

        if (tileReference.IsEmpty)
        {
            throw new ArgumentException("An overlay tile cannot reference the empty tile.", nameof(tileReference));
        }

        EnsureValidTileReference(tileReference);

        if (!_overlayTileCache.TryGetValue(tileReference, out var cachedTile))
        {
            cachedTile = new CachedOverlayTile(CreateOverlayTile(tileReference));
            _overlayTileCache.Add(tileReference, cachedTile);
        }

        cachedTile.RefCount++;
        if (cachedTile.RefCount == 1 && cachedTile.Tile is AnimatedTile animatedTile)
        {
            // First live overlay entry for this reference (either the very first use, or a re-Add after a
            // Clear brought a previously cached instance's ref count back from 0): register it for the
            // regular per-frame update. CreateOverlayTile itself never registers an AnimatedTile it creates
            // — this is the only place that does, so a cache hit re-registers exactly like a cache miss.
            _animatedTiles.Add(animatedTile);
            _hasAnimatedTiles = true;
        }

        _sortedOverlayTiles.Add(new SortedOverlayTile(tileReference, gridX, gridY, cachedTile.Tile, in sortKey));
    }

    /// <summary>Removes every tile previously queued with <see cref="AddSortedOverlayTile"/>.</summary>
    public void ClearSortedOverlayTiles()
    {
        if (_sortedOverlayTiles.Count == 0)
        {
            return;
        }

        // Overlay entries share their Tile instance with every other live entry of the same reference (see
        // AddSortedOverlayTile's cache); an animated one was registered in _animatedTiles for the regular
        // per-frame update and must be unregistered once its last live entry disappears here, or it would
        // keep being updated after every overlay entry referencing it is gone. The cached instance itself
        // is NOT dropped (see _overlayTileCache): that is what lets a later resubmit of the same reference
        // pick up the same instance, phase and all, instead of restarting animation at frame 0.
        for (var index = 0; index < _sortedOverlayTiles.Count; index++)
        {
            var tileReference = _sortedOverlayTiles[index].TileReference;
            if (!_overlayTileCache.TryGetValue(tileReference, out var cachedTile))
            {
                continue;
            }

            cachedTile.RefCount--;
            if (cachedTile.RefCount == 0 && cachedTile.Tile is AnimatedTile animatedTile)
            {
                _animatedTiles.Remove(animatedTile);
            }
        }

        _sortedOverlayTiles.Clear();
        _hasAnimatedTiles = _animatedTiles.Count > 0;
    }

    public override void Load(JObject element)
    {
        base.Load(element);
        TileMapDataAssetId = element["tile_map_data_asset_id"].GetGuid();
    }

    public override void Detach()
    {
        DisposeChunkGraphicsResources();
        RemoveAllCollisionObjects();
        base.Detach();
    }

    private void EnsureTileMapLoaded()
    {
        if (TileMapData == null)
        {
            throw new InvalidOperationException("TileMapData must be loaded before accessing tile data.");
        }

        if (TileSetData == null || _tileSets.Count == 0 || _tileSetTextures.Count == 0)
        {
            throw new InvalidOperationException("TileSetData must be loaded before accessing tile data.");
        }
    }

    private void EnsureValidTileReference(TileMapTileReference tileReference)
    {
        if (tileReference.IsEmpty)
        {
            return;
        }

        if (tileReference.TileSetIndex < 0 || tileReference.TileSetIndex >= _tileSets.Count)
        {
            throw new ArgumentException($"Unknown tileset source index '{tileReference.TileSetIndex}'.", nameof(tileReference));
        }

        if (!_tileSets[tileReference.TileSetIndex].IsKnownTileId(tileReference.TileId))
        {
            throw new ArgumentException($"Unknown tile id '{tileReference.TileId}' for tileset source '{tileReference.TileSetIndex}'.", nameof(tileReference));
        }
    }

    private void LoadTileSets()
    {
        _tileSets.Clear();
        _tileSetTextures.Clear();
        _tileSetTexture = null;

        for (var tileSetIndex = 0; tileSetIndex < TileMapData.TileSetDataAssetIds.Count; tileSetIndex++)
        {
            var tileSetData = Owner.World.Game.AssetContentManager.Load<TileSetData>(TileMapData.TileSetDataAssetIds[tileSetIndex]);
            if (tileSetIndex == 0)
            {
                TileSetData = tileSetData;
            }
            else if (tileSetData.TileSize.Width != TileSetData.TileSize.Width || tileSetData.TileSize.Height != TileSetData.TileSize.Height)
            {
                throw new NotSupportedException("TileMap runtime requires every tileset used by a map to have the same tile size.");
            }

            var texture = Owner.World.Game.AssetContentManager.Load<Texture>(tileSetData.SpriteSheetAssetId);
            texture.Load(Owner.World.Game.AssetContentManager);

            _tileSets.Add(tileSetData);
            _tileSetTextures.Add(texture.Resource);
        }

        if (_tileSets.Count == 0)
        {
            throw new InvalidOperationException("TileMapData must reference at least one TileSetData asset.");
        }

        _tileSetTexture = _tileSetTextures[0];
    }

    private bool TryGetTileData(TileMapLayerData layerData, int tileIndex, out TileData tileData)
    {
        var tileId = layerData.tiles[tileIndex];
        if (tileId == TileMapData.EmptyTileId)
        {
            tileData = null;
            return false;
        }

        return TryGetTileData(layerData.GetTileSourceIndex(tileIndex), tileId, out tileData);
    }

    private bool TryGetTileData(int tileSourceIndex, int tileId, out TileData tileData)
    {
        tileData = null;
        return tileSourceIndex >= 0
            && tileSourceIndex < _tileSets.Count
            && _tileSets[tileSourceIndex].TryGetTileData(tileId, out tileData);
    }

    private Tile CreateRuntimeTile(TileMapLayerData tileMapLayerData, int layerIndex, int x, int y)
    {
        var tileIndex = TileMapData.GetTileIndex(x, y);
        var tileId = tileMapLayerData.tiles[tileIndex];
        Tile tile;

        if (tileId == TileMapData.EmptyTileId)
        {
            tile = new EmptyTile();
        }
        else if (TryGetTileData(tileMapLayerData, tileIndex, out var tileData) && tileData != null)
        {
            var tileSourceIndex = tileMapLayerData.GetTileSourceIndex(tileIndex);
            var tileSetData = _tileSets[tileSourceIndex];
            var texture = _tileSetTextures[tileSourceIndex];
            switch (tileData.Type)
            {
                case TileType.Auto:
                    var autoTileData = tileData as AutoTileData ?? throw new InvalidOperationException($"Tile {tileId} is not a valid auto tile.");
                    var autoTile = new AutoTile(texture, autoTileData);
                    autoTile.SetTileInfo(tileSetData.TileSize, TileMapData.MapSize, tileMapLayerData, tileSourceIndex, x, y);
                    tile = autoTile;
                    _autoTiles.Add(autoTile);
                    QueueAutoTileRefresh(autoTile);
                    break;

                case TileType.Static:
                    tile = new StaticTile(texture, tileData as StaticTileData);
                    break;

                case TileType.Animated:
                    var animatedTileData = tileData as AnimatedTileData ?? throw new InvalidOperationException($"Tile {tileId} is not a valid animated tile.");
                    var animatedTile = new AnimatedTile(texture, tileSetData, animatedTileData);
                    tile = animatedTile;
                    _animatedTiles.Add(animatedTile);
                    _hasAnimatedTiles = true;
                    break;

                default:
                    throw new ArgumentException($"tile type not supported {tileData.Type}");
            }
        }
        else
        {
            var tileSourceIndex = tileMapLayerData.GetTileSourceIndex(tileIndex);
            throw new InvalidOperationException($"Unknown tile id '{tileId}' for tileset source '{tileSourceIndex}' in layer {layerIndex} at ({x}, {y}).");
        }

        tile.Initialize(Owner.World.Game);
        return tile;
    }

    /// <summary>
    /// Resolves the <see cref="Tile"/> instance backing one sorted overlay entry (see
    /// <see cref="AddSortedOverlayTile"/>), through the same Static/Animated creation the flat-layer path
    /// uses in <see cref="CreateRuntimeTile"/>. Overlay tiles have no grid cell of their own for auto-tile
    /// neighbor resolution, so <see cref="TileType.Auto"/> is not supported here.
    ///
    /// Called only on the first use of a reference (see <see cref="_overlayTileCache"/>): does NOT register
    /// an <see cref="AnimatedTile"/> it creates in <see cref="_animatedTiles"/> — <see cref="AddSortedOverlayTile"/>
    /// does that for both a cache miss and a cache hit, since a hit skips this method entirely.
    /// </summary>
    private Tile CreateOverlayTile(TileMapTileReference tileReference)
    {
        var tileSetIndex = tileReference.TileSetIndex;
        var tileId = tileReference.TileId;

        if (!TryGetTileData(tileSetIndex, tileId, out var tileData) || tileData == null)
        {
            throw new InvalidOperationException($"Unknown tile id '{tileId}' for tileset source '{tileSetIndex}'.");
        }

        var tileSetData = _tileSets[tileSetIndex];
        var texture = _tileSetTextures[tileSetIndex];
        Tile tile;

        switch (tileData.Type)
        {
            case TileType.Static:
                tile = new StaticTile(texture, tileData as StaticTileData);
                break;

            case TileType.Animated:
                var animatedTileData = tileData as AnimatedTileData ?? throw new InvalidOperationException($"Tile {tileId} is not a valid animated tile.");
                tile = new AnimatedTile(texture, tileSetData, animatedTileData);
                break;

            default:
                throw new NotSupportedException($"Sorted overlay tiles do not support tile type '{tileData.Type}' (tile id {tileId}).");
        }

        // Unlike CreateRuntimeTile, this tile is never drawn through Tile.Draw (DrawSortedOverlayTiles only
        // calls Update and GetCurrentSourceRectangle on it, then submits the sprite itself), so it has no
        // need for the SpriteRendererComponent Tile.Initialize resolves — skipping it also keeps overlay
        // entries usable before the owning entity is attached to a world.
        return tile;
    }

    /// <summary>
    /// Computes the world position of a tile collision body. The tile rectangle is expressed in map local
    /// pixels (<paramref name="left"/>/<paramref name="top"/> being its top left corner), so its centre must go
    /// through the full world transform, scale included, to stay aligned with the rendered map.
    /// </summary>
    internal static Vector3 ComputeCollisionWorldPosition(ref Matrix worldMatrixWithScale, float left, float top, float width, float height)
    {
        var localCenter = new Vector3(left + width / 2f, -top - height / 2f, 0f);
        return Vector3.Transform(localCenter, worldMatrixWithScale);
    }

    private PhysicsBody CreateCollisionObject(int layerIndex, Rectangle tileBounds, TileCollisionType collisionType)
    {
        if (_physicsWorldContext == null)
        {
            return null;
        }

        var tileSize = TileSetData.TileSize;
        var width = tileBounds.Width * tileSize.Width;
        var height = tileBounds.Height * tileSize.Height;
        var worldMatrixWithScale = WorldMatrixWithScale;
        var worldMatrix = WorldMatrixNoScale;
        worldMatrix.Translation = ComputeCollisionWorldPosition(
            ref worldMatrixWithScale,
            tileBounds.X * tileSize.Width,
            tileBounds.Y * tileSize.Height,
            width,
            height);
        var box = new Box { Size = new Vector3(width, height, 1f) };

        var tileCollisionManager = new TileCollisionManager(this, layerIndex, tileBounds.X, tileBounds.Y);

        PhysicsBody collisionObject;
        if (collisionType == TileCollisionType.NoContactResponse)
        {
            //A trigger tile is a static sensor: the Trigger profile blocks nothing (IsSensor), so the
            //narrow phase still records contacts without adding constraints, same as the former ghost.
            var physicsDefinition = new PhysicsDefinition { Friction = 0f, PhysicsType = PhysicsType.Static, ProfileName = CollisionProfileNames.Trigger };
            collisionObject = _physicsWorldContext.AddStaticObject(box, LocalScale, ref worldMatrix, tileCollisionManager,
                physicsDefinition, GameSettings.PhysicsEngineSettings.CollisionProfiles.ResolveProfileId(physicsDefinition));
        }
        else
        {
            var physicsDefinition = new PhysicsDefinition { Friction = 0f };
            collisionObject = _physicsWorldContext.AddStaticObject(box, LocalScale, ref worldMatrix, tileCollisionManager,
                physicsDefinition, GameSettings.PhysicsEngineSettings.CollisionProfiles.ResolveProfileId(physicsDefinition));
        }

        _collisionObjects.Add(collisionObject);
        _collisionManagersByBody[collisionObject] = tileCollisionManager;
        return collisionObject;
    }

    /// <summary>
    /// World position of the collision body of a tile carrying its own collision volume: the tile origin
    /// offset by the local pose of the volume, both expressed in map local pixels.
    /// </summary>
    internal static Vector3 ComputeTileCollisionWorldPosition(ref Matrix worldMatrixWithScale, Collision2d collision2d, int tileX, int tileY, CasaEngine.Core.Math.Size tileSize)
    {
        var rectangle = (ShapeRectangle)collision2d.Shape;
        return ComputeCollisionWorldPosition(
            ref worldMatrixWithScale,
            tileX * tileSize.Width + collision2d.LocalPosition.X,
            tileY * tileSize.Height + collision2d.LocalPosition.Y,
            rectangle.Width,
            rectangle.Height);
    }

    private PhysicsBody CreateCollisionObject(int layerIndex, int tileX, int tileY, Collision2d collision2d, TileCollisionType collisionType)
    {
        if (_physicsWorldContext == null)
        {
            return null;
        }

        var rectangle = (ShapeRectangle)collision2d.Shape;
        var tileSize = TileSetData.TileSize;
        var width = rectangle.Width;
        var height = rectangle.Height;
        if (width <= 0f || height <= 0f)
        {
            return null;
        }

        var worldMatrixWithScale = WorldMatrixWithScale;
        var worldMatrix = WorldMatrixNoScale;
        worldMatrix.Translation = ComputeTileCollisionWorldPosition(ref worldMatrixWithScale, collision2d, tileX, tileY, tileSize);
        var box = new Box { Size = new Vector3(width, height, 1f) };

        var tileCollisionManager = new TileCollisionManager(this, layerIndex, tileX, tileY);

        PhysicsBody collisionObject;
        if (collisionType == TileCollisionType.NoContactResponse)
        {
            //A trigger tile is a static sensor: the Trigger profile blocks nothing (IsSensor), so the
            //narrow phase still records contacts without adding constraints, same as the former ghost.
            var physicsDefinition = new PhysicsDefinition { Friction = 0f, PhysicsType = PhysicsType.Static, ProfileName = CollisionProfileNames.Trigger };
            collisionObject = _physicsWorldContext.AddStaticObject(box, LocalScale, ref worldMatrix, tileCollisionManager,
                physicsDefinition, GameSettings.PhysicsEngineSettings.CollisionProfiles.ResolveProfileId(physicsDefinition));
        }
        else
        {
            var physicsDefinition = new PhysicsDefinition { Friction = 0f };
            collisionObject = _physicsWorldContext.AddStaticObject(box, LocalScale, ref worldMatrix, tileCollisionManager,
                physicsDefinition, GameSettings.PhysicsEngineSettings.CollisionProfiles.ResolveProfileId(physicsDefinition));
        }

        _collisionObjects.Add(collisionObject);
        _collisionManagersByBody[collisionObject] = tileCollisionManager;
        return collisionObject;
    }

    private void ReplaceRuntimeTile(TileMapLayer layerRuntime, TileMapLayerData layerData, int layerIndex, int x, int y, int tileIndex)
    {
        if (layerRuntime.Tiles[tileIndex] is AutoTile oldAutoTile)
        {
            _autoTiles.Remove(oldAutoTile);
            if (_dirtyAutoTileSet.Remove(oldAutoTile))
            {
                _dirtyAutoTiles.Remove(oldAutoTile);
            }
        }
        else if (layerRuntime.Tiles[tileIndex] is AnimatedTile oldAnimatedTile)
        {
            _animatedTiles.Remove(oldAnimatedTile);
            _hasAnimatedTiles = _animatedTiles.Count > 0;
        }

        RemoveCollisionObject(layerRuntime.CollisionObjects[tileIndex]);
        layerRuntime.CollisionObjects[tileIndex] = null;

        layerRuntime.Tiles[tileIndex] = CreateRuntimeTile(layerData, layerIndex, x, y);
        _hasAnimatedTiles = _animatedTiles.Count > 0;
    }

    private void BuildChunks(TileMapLayer layer, int layerIndex)
    {
        layer.Chunks.Clear();
        layer.CollisionChunks.Clear();

        var mapWidth = TileMapData.MapSize.Width;
        var mapHeight = TileMapData.MapSize.Height;
        var chunkSize = ChunkTileSize;
        layer.ChunkColumns = (mapWidth + chunkSize - 1) / chunkSize;
        layer.ChunkRows = (mapHeight + chunkSize - 1) / chunkSize;

        for (var chunkY = 0; chunkY < layer.ChunkRows; chunkY++)
        {
            var tileY = chunkY * chunkSize;
            var tileHeight = Math.Min(chunkSize, mapHeight - tileY);

            for (var chunkX = 0; chunkX < layer.ChunkColumns; chunkX++)
            {
                var tileX = chunkX * chunkSize;
                var tileWidth = Math.Min(chunkSize, mapWidth - tileX);
                var tileBounds = new Rectangle(tileX, tileY, tileWidth, tileHeight);
                var chunkIndex = new Point(chunkX, chunkY);
                layer.Chunks.Add(new TileMapChunk(layerIndex, chunkIndex, tileBounds));
                layer.CollisionChunks.Add(new TileMapCollisionChunk(layerIndex, chunkIndex, tileBounds));
            }
        }
    }

    private void MarkAffectedChunksDirty(int layerIndex, int x, int y)
    {
        for (var offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                MarkChunkDirty(layerIndex, x + offsetX, y + offsetY);
            }
        }
    }

    private void MarkAffectedAutoTilesDirty(int layerIndex, int x, int y)
    {
        var layer = Layers[layerIndex];

        for (var offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                var tileX = x + offsetX;
                var tileY = y + offsetY;
                if (!TileMapData.IsInside(tileX, tileY))
                {
                    continue;
                }

                var tileIndex = TileMapData.GetTileIndex(tileX, tileY);
                if (layer.Tiles[tileIndex] is AutoTile autoTile)
                {
                    QueueAutoTileRefresh(autoTile);
                }
            }
        }
    }

    private void QueueAutoTileRefresh(AutoTile autoTile)
    {
        if (_dirtyAutoTileSet.Add(autoTile))
        {
            _dirtyAutoTiles.Add(autoTile);
        }

        _needsAutoTileRefresh = true;
        Owner?.Policies.RequestConditionalUpdate();
    }

    private void MarkChunkDirty(int layerIndex, int x, int y)
    {
        if (!TileMapData.IsInside(x, y))
        {
            return;
        }

        var layer = Layers[layerIndex];
        if (layer.Chunks.Count == 0)
        {
            return;
        }

        var chunkX = x / ChunkTileSize;
        var chunkY = y / ChunkTileSize;
        var chunkIndex = chunkX + chunkY * layer.ChunkColumns;
        if (chunkIndex >= 0 && chunkIndex < layer.Chunks.Count)
        {
            layer.Chunks[chunkIndex].MarkDirty();
        }
    }

    private void RebuildLayerCollisionChunks(int layerIndex)
    {
        var layer = Layers[layerIndex];
        for (var chunkIndex = 0; chunkIndex < layer.CollisionChunks.Count; chunkIndex++)
        {
            RebuildCollisionChunk(layer, layer.CollisionChunks[chunkIndex]);
        }
    }

    private void RebuildCollisionChunkForTile(int layerIndex, int x, int y)
    {
        if (!TileMapData.IsInside(x, y))
        {
            return;
        }

        var layer = Layers[layerIndex];
        if (layer.CollisionChunks.Count == 0)
        {
            return;
        }

        var chunkX = x / ChunkTileSize;
        var chunkY = y / ChunkTileSize;
        var chunkIndex = chunkX + chunkY * layer.ChunkColumns;
        if (chunkIndex >= 0 && chunkIndex < layer.CollisionChunks.Count)
        {
            layer.CollisionChunks[chunkIndex].MarkDirty();
            RebuildCollisionChunk(layer, layer.CollisionChunks[chunkIndex]);
        }
    }

    private void RebuildCollisionChunk(TileMapLayer layer, TileMapCollisionChunk collisionChunk)
    {
        RemoveCollisionObjects(collisionChunk.CollisionObjects);
        collisionChunk.CollisionObjects.Clear();

        if (_physicsWorldContext == null)
        {
            collisionChunk.MarkClean();
            return;
        }

        RebuildCollisionChunkForType(layer, collisionChunk, TileCollisionType.Blocked);
        RebuildCollisionChunkForType(layer, collisionChunk, TileCollisionType.NoContactResponse);
        collisionChunk.MarkClean();
    }

    private void RebuildCollisionChunkForType(TileMapLayer layer, TileMapCollisionChunk collisionChunk, TileCollisionType collisionType)
    {
        var tileBounds = collisionChunk.TileBounds;
        var cellCount = tileBounds.Width * tileBounds.Height;
        var solidCells = collisionChunk.EnsureSolidCapacity(cellCount);

        for (var y = 0; y < tileBounds.Height; y++)
        {
            var mapY = tileBounds.Y + y;
            var rowOffset = mapY * TileMapData.MapSize.Width;

            for (var x = 0; x < tileBounds.Width; x++)
            {
                var mapX = tileBounds.X + x;
                var tileId = layer.TileMapLayerData.tiles[rowOffset + mapX];
                if (tileId == TileMapData.EmptyTileId)
                {
                    continue;
                }

                if (TryGetTileData(layer.TileMapLayerData, rowOffset + mapX, out var tileData) && tileData?.CollisionType == collisionType)
                {
                    if (tileData.CollisionShape?.Shape is ShapeRectangle)
                    {
                        var collisionObject = CreateCollisionObject(collisionChunk.LayerIndex, mapX, mapY, tileData.CollisionShape, collisionType);
                        if (collisionObject != null)
                        {
                            collisionChunk.CollisionObjects.Add(collisionObject);
                        }
                    }
                    else
                    {
                        solidCells[x + y * tileBounds.Width] = true;
                    }
                }
            }
        }

        var rectangles = collisionChunk.BuildMergedTileRectangles(tileBounds.Width, tileBounds.Height, tileBounds.X, tileBounds.Y, solidCells);
        for (var rectangleIndex = 0; rectangleIndex < rectangles.Count; rectangleIndex++)
        {
            var collisionObject = CreateCollisionObject(collisionChunk.LayerIndex, rectangles[rectangleIndex], collisionType);
            if (collisionObject != null)
            {
                collisionChunk.CollisionObjects.Add(collisionObject);
            }
        }
    }

    // Logged at most once per component, per reason: these two bail-outs are how a chunk silently stops
    // being drawn when its GPU buffers are missing or the batch draw is refused, which is what growing
    // black patches over a map look like from the outside. Once-only, because this is a per-chunk,
    // per-frame path.
    private bool _loggedMissingStaticBatchResources;
    private bool _loggedStaticBatchDrawRefused;

    private bool TryDrawStaticChunkBatch(TileMapLayer layer, TileMapChunk chunk, in Matrix world)
    {
        var currentFrame = Owner.World.CurrentRenderFrame;
        if (!currentFrame.HasValue || _spriteRendererComponent == null || _tileSetTextures.Count == 0)
        {
            return false;
        }

        var graphicsDevice = Owner.World.Game.GraphicsDevice;
        if (chunk.DirtyVisual)
        {
            RebuildStaticChunkBuffer(layer, chunk, graphicsDevice);
        }

        if (!chunk.HasStaticGeometry)
        {
            return false;
        }

        if (!chunk.HasGraphicsResourcesFor(graphicsDevice))
        {
            RebuildStaticChunkBuffer(layer, chunk, graphicsDevice);
        }

        for (var batchIndex = 0; batchIndex < chunk.StaticBatches.Count; batchIndex++)
        {
            var staticBatch = chunk.StaticBatches[batchIndex];
            if (!staticBatch.HasGeometry)
            {
                continue;
            }

            if (staticBatch.TileSetIndex < 0
                || staticBatch.TileSetIndex >= _tileSetTextures.Count
                || staticBatch.VertexBuffer == null
                || staticBatch.IndexBuffer == null)
            {
                if (!_loggedMissingStaticBatchResources)
                {
                    _loggedMissingStaticBatchResources = true;
                    Logs.WriteWarning(
                        $"TileMapComponent '{Owner?.Name}': a static chunk batch has no usable graphics "
                        + $"resources (tile set index {staticBatch.TileSetIndex} of {_tileSetTextures.Count}, "
                        + $"vertex buffer {(staticBatch.VertexBuffer == null ? "null" : "present")}, "
                        + $"index buffer {(staticBatch.IndexBuffer == null ? "null" : "present")}). "
                        + "Its tiles will not be drawn. Logged once per component.");
                }

                return false;
            }
        }

        var drawnBatchCount = 0;
        var drawnTileCount = 0;
        for (var batchIndex = 0; batchIndex < chunk.StaticBatches.Count; batchIndex++)
        {
            var staticBatch = chunk.StaticBatches[batchIndex];
            if (!staticBatch.HasGeometry)
            {
                continue;
            }

            if (!_spriteRendererComponent.DrawStaticBatch(
                    _tileSetTextures[staticBatch.TileSetIndex],
                    staticBatch.VertexBuffer!,
                    staticBatch.IndexBuffer!,
                    staticBatch.PrimitiveCount,
                    in world,
                    currentFrame.Value))
            {
                if (!_loggedStaticBatchDrawRefused)
                {
                    _loggedStaticBatchDrawRefused = true;
                    Logs.WriteWarning(
                        $"TileMapComponent '{Owner?.Name}': the sprite renderer refused a static chunk "
                        + "batch draw. Its tiles will not be drawn. Logged once per component.");
                }

                return false;
            }

            drawnBatchCount++;
            drawnTileCount += staticBatch.StaticTileCount;
        }

        LastStaticBatchCount += drawnBatchCount;
        LastStaticBatchTileCount += drawnTileCount;
        LastDrawnTileCount += drawnTileCount;
        return drawnBatchCount > 0;
    }

    private void RebuildStaticChunkBuffer(TileMapLayer layer, TileMapChunk chunk, GraphicsDevice graphicsDevice)
    {
        for (var tileSetIndex = 0; tileSetIndex < _tileSets.Count; tileSetIndex++)
        {
            chunk.GetOrCreateStaticBatch(tileSetIndex).Reset(tileSetIndex);
        }

        chunk.TrimStaticBatchCount(_tileSets.Count);

        var staticTileCount = 0;
        var containsDynamicTiles = false;

        for (var y = chunk.TileBounds.Top; y < chunk.TileBounds.Bottom; y++)
        {
            var rowOffset = y * TileMapData.MapSize.Width;

            for (var x = chunk.TileBounds.Left; x < chunk.TileBounds.Right; x++)
            {
                var tileId = layer.TileMapLayerData.tiles[rowOffset + x];
                if (tileId == TileMapData.EmptyTileId)
                {
                    continue;
                }

                var tileSourceIndex = layer.TileMapLayerData.GetTileSourceIndex(rowOffset + x);
                if (!TryGetTileData(tileSourceIndex, tileId, out var tileData) || tileData is not StaticTileData staticTileData)
                {
                    containsDynamicTiles = true;
                    continue;
                }

                AddStaticTileQuad(
                    chunk.GetOrCreateStaticBatch(tileSourceIndex),
                    _tileSets[tileSourceIndex].TileSize,
                    _tileSetTextures[tileSourceIndex],
                    x,
                    y,
                    staticTileData.Location,
                    layer.TileMapLayerData.GetTileFlags(rowOffset + x));
                staticTileCount++;
            }
        }

        chunk.SetStaticGeometryCounts(staticTileCount, containsDynamicTiles);
        chunk.UploadStaticGeometry(graphicsDevice);
        chunk.MarkVisualClean();
    }

    private static void AddStaticTileQuad(
        TileMapStaticChunkBatch staticBatch,
        CasaEngine.Core.Math.Size tileSize,
        Texture2D texture,
        int tileX,
        int tileY,
        Rectangle sourceRectangle,
        TileCellFlags flags)
    {
        var nextStaticTileCount = staticBatch.StaticTileCount + 1;
        var vertices = staticBatch.EnsureVertexCapacity(nextStaticTileCount * 4);
        var indices = staticBatch.EnsureIndexCapacity(nextStaticTileCount * 6);
        var vertexCount = staticBatch.VertexCount;
        var indexCount = staticBatch.IndexCount;
        var textureWidth = texture.Width;
        var textureHeight = texture.Height;
        var left = tileX * tileSize.Width;
        var right = left + tileSize.Width;
        var top = -tileY * tileSize.Height;
        var bottom = top - tileSize.Height;

        var uvLeft = sourceRectangle.Left / (float)textureWidth;
        var uvRight = sourceRectangle.Right / (float)textureWidth;
        var uvTop = sourceRectangle.Top / (float)textureHeight;
        var uvBottom = sourceRectangle.Bottom / (float)textureHeight;
        if ((flags & TileCellFlags.FlipHorizontal) != 0)
        {
            (uvLeft, uvRight) = (uvRight, uvLeft);
        }

        if ((flags & TileCellFlags.FlipVertical) != 0)
        {
            (uvTop, uvBottom) = (uvBottom, uvTop);
        }

        var baseVertex = vertexCount;

        vertices[vertexCount++] = new VertexPositionTexture(new Vector3(left, top, 0f), new Vector2(uvLeft, uvTop));
        vertices[vertexCount++] = new VertexPositionTexture(new Vector3(right, top, 0f), new Vector2(uvRight, uvTop));
        vertices[vertexCount++] = new VertexPositionTexture(new Vector3(right, bottom, 0f), new Vector2(uvRight, uvBottom));
        vertices[vertexCount++] = new VertexPositionTexture(new Vector3(left, bottom, 0f), new Vector2(uvLeft, uvBottom));

        indices[indexCount++] = (short)baseVertex;
        indices[indexCount++] = (short)(baseVertex + 1);
        indices[indexCount++] = (short)(baseVertex + 2);
        indices[indexCount++] = (short)baseVertex;
        indices[indexCount++] = (short)(baseVertex + 2);
        indices[indexCount++] = (short)(baseVertex + 3);

        staticBatch.SetStaticGeometryCounts(vertexCount, indexCount, nextStaticTileCount);
    }

    private bool IsStaticTile(TileMapLayerData layerData, int tileIndex)
    {
        return TryGetTileData(layerData, tileIndex, out var tileData) && tileData is StaticTileData;
    }

    private void DisposeChunkGraphicsResources()
    {
        for (var layerIndex = 0; layerIndex < Layers.Count; layerIndex++)
        {
            var layer = Layers[layerIndex];
            for (var chunkIndex = 0; chunkIndex < layer.Chunks.Count; chunkIndex++)
            {
                layer.Chunks[chunkIndex].DisposeGraphicsResources();
            }
        }
    }

    private bool TryGetVisibleTileRange(
        float tileWidth,
        float tileHeight,
        float mapPosX,
        float mapPosY,
        int mapWidth,
        int mapHeight,
        float minPlaneZ,
        float maxPlaneZ,
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

        if (!TryGetWorldViewBounds(currentRenderFrame.Value, minPlaneZ, maxPlaneZ, out var viewMinX, out var viewMaxX, out var viewMinY, out var viewMaxY))
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

    /// <summary>
    /// World space Z range covered by the rendered layers, used as the plane slab the visible tile
    /// range is computed on. The component position is always part of the range so the slab stays
    /// valid when no layer renders tiles.
    /// </summary>
    private void GetRenderedLayerWorldZRange(float translationZ, out float minZ, out float maxZ)
    {
        minZ = translationZ;
        maxZ = translationZ;

        for (var layerIndex = 0; layerIndex < Layers.Count; layerIndex++)
        {
            var layer = Layers[layerIndex];
            if (!layer.TileMapLayerData.Depth.ShouldRenderTiles)
            {
                continue;
            }

            var worldZ = translationZ + layer.TileMapLayerData.zOffset;

            if (worldZ < minZ)
            {
                minZ = worldZ;
            }

            if (worldZ > maxZ)
            {
                maxZ = worldZ;
            }
        }
    }

    /// <summary>
    /// World space X/Y extent covered by the view frustum on the tile map plane slab
    /// [<paramref name="minPlaneZ"/>, <paramref name="maxPlaneZ"/>].
    ///
    /// The extent comes from the frustum edges clipped to the slab, never from the four viewport
    /// corner rays: as soon as the camera pitches towards the horizon, corner rays either stop
    /// crossing the plane in front of the camera or cross it arbitrarily far away, and the quad they
    /// describe no longer contains the visible part of the map. The frustum is bounded by its far
    /// plane, so clipping its edges always yields a finite and conservative extent.
    ///
    /// Returns false when the frustum does not reach the slab at all; the caller then keeps the full
    /// tile range, entity level frustum culling already discarding a fully offscreen tile map.
    /// </summary>
    private bool TryGetWorldViewBounds(
        in RenderFrame frame,
        float minPlaneZ,
        float maxPlaneZ,
        out float minX,
        out float maxX,
        out float minY,
        out float maxY)
    {
        _cullingFrustum ??= new BoundingFrustum(Matrix.Identity);
        _cullingFrustum.Matrix = frame.ViewProjection;

        _frustumCorners ??= new Vector3[BoundingFrustum.CornerCount];
        _cullingFrustum.GetCorners(_frustumCorners);

        return TryGetFrustumSlabBounds(_frustumCorners, minPlaneZ, maxPlaneZ, out minX, out maxX, out minY, out maxY);
    }

    /// <summary>
    /// Axis aligned X/Y bounds of the intersection between a frustum, given by its eight corners in
    /// <see cref="BoundingFrustum.GetCorners(Vector3[])"/> order, and the Z slab
    /// [<paramref name="minPlaneZ"/>, <paramref name="maxPlaneZ"/>].
    ///
    /// That intersection is a bounded convex polytope whose vertices are either frustum corners
    /// inside the slab or crossings between a frustum edge and a slab plane, so clipping the twelve
    /// edges to the slab gives the exact bounds.
    /// </summary>
    internal static bool TryGetFrustumSlabBounds(
        Vector3[] frustumCorners,
        float minPlaneZ,
        float maxPlaneZ,
        out float minX,
        out float maxX,
        out float minY,
        out float maxY)
    {
        minX = float.MaxValue;
        maxX = float.MinValue;
        minY = float.MaxValue;
        maxY = float.MinValue;

        if (frustumCorners == null || frustumCorners.Length < BoundingFrustum.CornerCount)
        {
            return false;
        }

        if (minPlaneZ > maxPlaneZ)
        {
            (minPlaneZ, maxPlaneZ) = (maxPlaneZ, minPlaneZ);
        }

        var found = false;

        for (var index = 0; index < FrustumEdgeCornerIndices.Length; index += 2)
        {
            var start = frustumCorners[FrustumEdgeCornerIndices[index]];
            var end = frustumCorners[FrustumEdgeCornerIndices[index + 1]];

            if (!TryClipSegmentToSlab(in start, in end, minPlaneZ, maxPlaneZ, out var clippedStart, out var clippedEnd))
            {
                continue;
            }

            minX = Math.Min(minX, Math.Min(clippedStart.X, clippedEnd.X));
            maxX = Math.Max(maxX, Math.Max(clippedStart.X, clippedEnd.X));
            minY = Math.Min(minY, Math.Min(clippedStart.Y, clippedEnd.Y));
            maxY = Math.Max(maxY, Math.Max(clippedStart.Y, clippedEnd.Y));
            found = true;
        }

        return found && minX <= maxX && minY <= maxY;
    }

    private static bool TryClipSegmentToSlab(
        in Vector3 start,
        in Vector3 end,
        float minZ,
        float maxZ,
        out Vector3 clippedStart,
        out Vector3 clippedEnd)
    {
        clippedStart = start;
        clippedEnd = end;

        var deltaZ = end.Z - start.Z;
        var enter = 0f;
        var exit = 1f;

        if (Math.Abs(deltaZ) < SlabClipEpsilon)
        {
            if (!(start.Z >= minZ) || !(start.Z <= maxZ))
            {
                return false;
            }
        }
        else
        {
            var enterCandidate = (minZ - start.Z) / deltaZ;
            var exitCandidate = (maxZ - start.Z) / deltaZ;

            if (enterCandidate > exitCandidate)
            {
                (enterCandidate, exitCandidate) = (exitCandidate, enterCandidate);
            }

            enter = Math.Max(enter, enterCandidate);
            exit = Math.Min(exit, exitCandidate);

            if (!(enter <= exit))
            {
                return false;
            }
        }

        var direction = end - start;
        clippedStart = start + direction * enter;
        clippedEnd = start + direction * exit;
        return true;
    }

    private void RemoveCollisionObjects(List<PhysicsBody> collisionObjects)
    {
        for (var index = 0; index < collisionObjects.Count; index++)
        {
            RemoveCollisionObject(collisionObjects[index]);
        }
    }

    private void RemoveAllCollisionObjects()
    {
        for (var index = _collisionObjects.Count - 1; index >= 0; index--)
        {
            RemoveCollisionObject(_collisionObjects[index]);
        }

        _collisionObjects.Clear();
    }

    private void RemoveCollisionObject(PhysicsBody collisionObject)
    {
        if (collisionObject == null)
        {
            return;
        }

        if (_physicsWorldContext == null)
        {
            return;
        }

        // The physics collider for a tile body is its per-body TileCollisionManager, not this component
        // (see CreateCollisionObject) - clear the world's collision bookkeeping for that actual collider,
        // and detach it so any collision still queued for dispatch this frame answers safely instead of
        // touching a cell that may have been rebuilt/removed already.
        if (_collisionManagersByBody.Remove(collisionObject, out var tileCollisionManager))
        {
            tileCollisionManager.Detach();
            _physicsWorldContext.ClearCollisionDataFrom(tileCollisionManager);
        }
        else
        {
            _physicsWorldContext.ClearCollisionDataFrom(this);
        }

        if (collisionObject.IsRigidBody)
        {
            _physicsWorldContext.RemoveRigidBody(collisionObject);
        }
        else
        {
            _physicsWorldContext.RemoveCollisionObject(collisionObject);
        }

        _collisionObjects.Remove(collisionObject);
        collisionObject.Dispose();
    }

}