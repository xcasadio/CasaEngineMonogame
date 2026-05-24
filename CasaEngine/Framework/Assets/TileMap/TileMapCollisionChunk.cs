using CasaEngine.Engine.Physics;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Assets.TileMap;

public sealed class TileMapCollisionChunk
{
    private bool[] _consumedCells = Array.Empty<bool>();
    private bool[] _solidCells = Array.Empty<bool>();
    private readonly List<Rectangle> _mergedTileRectangles = new();

    public TileMapCollisionChunk(int layerIndex, Point chunkIndex, Rectangle tileBounds)
    {
        LayerIndex = layerIndex;
        ChunkIndex = chunkIndex;
        TileBounds = tileBounds;
        Dirty = true;
    }

    public int LayerIndex { get; }
    public Point ChunkIndex { get; }
    public Rectangle TileBounds { get; }
    public bool Dirty { get; private set; }
    public List<PhysicsBody> CollisionObjects { get; } = new();
    public IReadOnlyList<Rectangle> MergedTileRectangles => _mergedTileRectangles;

    public bool[] EnsureSolidCapacity(int cellCount)
    {
        if (_solidCells.Length < cellCount)
        {
            _solidCells = new bool[cellCount];
        }

        Array.Clear(_solidCells, 0, cellCount);
        return _solidCells;
    }

    public List<Rectangle> BuildMergedTileRectangles(int width, int height, int originX, int originY, bool[] solidCells)
    {
        var cellCount = width * height;
        if (_consumedCells.Length < cellCount)
        {
            _consumedCells = new bool[cellCount];
        }

        Array.Clear(_consumedCells, 0, cellCount);
        _mergedTileRectangles.Clear();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = x + y * width;
                if (!solidCells[index] || _consumedCells[index])
                {
                    continue;
                }

                var rectangleWidth = 1;
                while (x + rectangleWidth < width)
                {
                    var nextIndex = x + rectangleWidth + y * width;
                    if (!solidCells[nextIndex] || _consumedCells[nextIndex])
                    {
                        break;
                    }

                    rectangleWidth++;
                }

                var rectangleHeight = 1;
                var canExtend = true;
                while (y + rectangleHeight < height && canExtend)
                {
                    for (var testX = 0; testX < rectangleWidth; testX++)
                    {
                        var testIndex = x + testX + (y + rectangleHeight) * width;
                        if (!solidCells[testIndex] || _consumedCells[testIndex])
                        {
                            canExtend = false;
                            break;
                        }
                    }

                    if (canExtend)
                    {
                        rectangleHeight++;
                    }
                }

                for (var markY = 0; markY < rectangleHeight; markY++)
                {
                    for (var markX = 0; markX < rectangleWidth; markX++)
                    {
                        _consumedCells[x + markX + (y + markY) * width] = true;
                    }
                }

                _mergedTileRectangles.Add(new Rectangle(originX + x, originY + y, rectangleWidth, rectangleHeight));
            }
        }

        return _mergedTileRectangles;
    }

    public void MarkDirty()
    {
        Dirty = true;
    }

    public void MarkClean()
    {
        Dirty = false;
    }
}