using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets.TileMap;

public sealed class TileMapChunk
{
    private readonly List<TileMapStaticChunkBatch> _staticBatches = new();

    public TileMapChunk(int layerIndex, Point chunkIndex, Rectangle tileBounds)
    {
        if (tileBounds.Width <= 0 || tileBounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileBounds), "TileMap chunk bounds must be positive.");
        }

        LayerIndex = layerIndex;
        ChunkIndex = chunkIndex;
        TileBounds = tileBounds;
        DirtyVisual = true;
        DirtyCollision = true;
    }

    public int LayerIndex { get; }
    public Point ChunkIndex { get; }
    public Rectangle TileBounds { get; }
    public BoundingBox WorldBounds { get; private set; }
    public IReadOnlyList<TileMapStaticChunkBatch> StaticBatches => _staticBatches;
    public int StaticBatchCount => _staticBatches.Count;
    public int StaticTileCount { get; private set; }
    public bool HasStaticGeometry
    {
        get
        {
            for (var index = 0; index < _staticBatches.Count; index++)
            {
                if (_staticBatches[index].HasGeometry)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool DirtyVisual { get; private set; }
    public bool DirtyCollision { get; private set; }
    public bool ContainsAnimatedTiles { get; set; }
    public bool ContainsDynamicTiles { get; private set; }

    public TileMapStaticChunkBatch GetOrCreateStaticBatch(int batchIndex)
    {
        while (_staticBatches.Count <= batchIndex)
        {
            _staticBatches.Add(new TileMapStaticChunkBatch());
        }

        return _staticBatches[batchIndex];
    }

    public void TrimStaticBatchCount(int batchCount)
    {
        for (var index = _staticBatches.Count - 1; index >= batchCount; index--)
        {
            _staticBatches[index].DisposeGraphicsResources();
            _staticBatches.RemoveAt(index);
        }
    }

    public void SetStaticGeometryCounts(int staticTileCount, bool containsDynamicTiles)
    {
        StaticTileCount = staticTileCount;
        ContainsDynamicTiles = containsDynamicTiles;
    }

    public void UploadStaticGeometry(GraphicsDevice graphicsDevice)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);

        for (var index = 0; index < _staticBatches.Count; index++)
        {
            _staticBatches[index].UploadStaticGeometry(graphicsDevice);
        }
    }

    public bool HasGraphicsResourcesFor(GraphicsDevice graphicsDevice)
    {
        for (var index = 0; index < _staticBatches.Count; index++)
        {
            var staticBatch = _staticBatches[index];
            if (staticBatch.HasGeometry && !staticBatch.HasGraphicsResourcesFor(graphicsDevice))
            {
                return false;
            }
        }

        return true;
    }

    public bool IntersectsTileRange(int minTileX, int maxTileX, int minTileY, int maxTileY)
    {
        if (minTileX > maxTileX || minTileY > maxTileY)
        {
            return false;
        }

        return TileBounds.Left <= maxTileX
            && TileBounds.Right - 1 >= minTileX
            && TileBounds.Top <= maxTileY
            && TileBounds.Bottom - 1 >= minTileY;
    }

    public void UpdateWorldBounds(float mapPosX, float mapPosY, float tileWidth, float tileHeight, float minZ, float maxZ)
    {
        var left = mapPosX + tileWidth * TileBounds.Left;
        var right = mapPosX + tileWidth * TileBounds.Right;
        var top = mapPosY - tileHeight * TileBounds.Top;
        var bottom = mapPosY - tileHeight * TileBounds.Bottom;

        WorldBounds = new BoundingBox(
            new Vector3(Math.Min(left, right), Math.Min(top, bottom), Math.Min(minZ, maxZ)),
            new Vector3(Math.Max(left, right), Math.Max(top, bottom), Math.Max(minZ, maxZ)));
    }

    public void MarkDirty(bool visual = true, bool collision = true)
    {
        DirtyVisual |= visual;
        DirtyCollision |= collision;
    }

    public void MarkVisualClean()
    {
        DirtyVisual = false;
    }

    public void MarkCollisionClean()
    {
        DirtyCollision = false;
    }

    public void DisposeGraphicsResources()
    {
        for (var index = 0; index < _staticBatches.Count; index++)
        {
            _staticBatches[index].DisposeGraphicsResources();
        }
    }
}

public sealed class TileMapStaticChunkBatch
{
    private VertexPositionTexture[] _vertices = Array.Empty<VertexPositionTexture>();
    private short[] _indices = Array.Empty<short>();

    public int TileSetIndex { get; private set; }
    public VertexBuffer? VertexBuffer { get; private set; }
    public IndexBuffer? IndexBuffer { get; private set; }
    public int VertexCount { get; private set; }
    public int IndexCount { get; private set; }
    public int PrimitiveCount => IndexCount / 3;
    public int StaticTileCount { get; private set; }
    public bool HasGeometry => PrimitiveCount > 0;

    public void Reset(int tileSetIndex)
    {
        TileSetIndex = tileSetIndex;
        VertexCount = 0;
        IndexCount = 0;
        StaticTileCount = 0;
    }

    public VertexPositionTexture[] EnsureVertexCapacity(int vertexCount)
    {
        if (_vertices.Length < vertexCount)
        {
            Array.Resize(ref _vertices, vertexCount);
        }

        return _vertices;
    }

    public short[] EnsureIndexCapacity(int indexCount)
    {
        if (_indices.Length < indexCount)
        {
            Array.Resize(ref _indices, indexCount);
        }

        return _indices;
    }

    public void SetStaticGeometryCounts(int vertexCount, int indexCount, int staticTileCount)
    {
        VertexCount = vertexCount;
        IndexCount = indexCount;
        StaticTileCount = staticTileCount;
    }

    public void UploadStaticGeometry(GraphicsDevice graphicsDevice)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);

        if (VertexCount == 0 || IndexCount == 0)
        {
            DisposeGraphicsResources();
            return;
        }

        if (VertexBuffer == null
            || VertexBuffer.IsDisposed
            || !ReferenceEquals(VertexBuffer.GraphicsDevice, graphicsDevice)
            || VertexBuffer.VertexCount < VertexCount)
        {
            VertexBuffer?.Dispose();
            VertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionTexture), VertexCount, BufferUsage.WriteOnly);
        }

        if (IndexBuffer == null
            || IndexBuffer.IsDisposed
            || !ReferenceEquals(IndexBuffer.GraphicsDevice, graphicsDevice)
            || IndexBuffer.IndexCount < IndexCount)
        {
            IndexBuffer?.Dispose();
            IndexBuffer = new IndexBuffer(graphicsDevice, typeof(short), IndexCount, BufferUsage.WriteOnly);
        }

        VertexBuffer.SetData(_vertices, 0, VertexCount);
        IndexBuffer.SetData(_indices, 0, IndexCount);
    }

    public bool HasGraphicsResourcesFor(GraphicsDevice graphicsDevice)
    {
        return VertexBuffer != null
            && IndexBuffer != null
            && !VertexBuffer.IsDisposed
            && !IndexBuffer.IsDisposed
            && ReferenceEquals(VertexBuffer.GraphicsDevice, graphicsDevice)
            && ReferenceEquals(IndexBuffer.GraphicsDevice, graphicsDevice);
    }

    public void DisposeGraphicsResources()
    {
        VertexBuffer?.Dispose();
        IndexBuffer?.Dispose();
        VertexBuffer = null;
        IndexBuffer = null;
    }
}