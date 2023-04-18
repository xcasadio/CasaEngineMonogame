using System.Diagnostics;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Assets.Map2d;

public class AutoTileDrawingInfo
{
    public int TileIndex { get; set; }
    public float XOffset { get; set; }
    public float YOffset { get; set; }
    public float ZOffset { get; set; }
    public Rectangle PosInTexture { get; set; }

    public void SetInfo(int tileIndex, float xOffset, float yOffset, float zOffset, Rectangle posInTexture)
    {
        TileIndex = tileIndex;
        XOffset = xOffset;
        YOffset = yOffset;
        ZOffset = zOffset;
        PosInTexture = posInTexture;
    }
}

public class AutoTile : Tile
{
    private Vector2 _tileSize;
    private TiledMapLayerData? _tiledMapLayerData;
    private Vector2 _mapSize;
    private AutoTileDrawingInfo[] _drawingInfos = new AutoTileDrawingInfo[4];
    private Tile[] _tiles = new Tile[6];
    private int _x, _y;

    public bool Hidden { get; }


    const uint mask_none = 0;
    const uint mask_left = 1 << 0;
    const uint mask_right = 1 << 1;
    const uint mask_top = 1 << 2;
    const uint mask_bottom = 1 << 3;
    const uint mask_left_top = 1 << 4;
    const uint mask_left_bottom = 1 << 5;
    const uint mask_right_top = 1 << 6;
    const uint mask_right_bottom = 1 << 7;
    const uint mask_all = mask_left | mask_right | mask_top | mask_bottom | mask_left_top | mask_left_bottom | mask_right_top | mask_right_bottom;

    public AutoTile(TileData tileData) : base(tileData)
    {
        for (int i = 0; i < 4; i++)
        {
            _drawingInfos[i] = new AutoTileDrawingInfo();
        }
    }

    public void RemovedTileFromLayer()
    {
        _tiledMapLayerData.tiles[_x + _y * (int)_mapSize.X] = -1;
    }

    public override void Initialize(CasaEngineGame game)
    {
        base.Initialize(game);

        foreach (var tile in _tiles)
        {
            tile.Initialize(game);
        }

        _drawingInfos[0].SetInfo(-1, 0, 0, 0, new Rectangle());
        _drawingInfos[1].SetInfo(-1, 0, 0, 0, new Rectangle());
        _drawingInfos[2].SetInfo(-1, 0, 0, 0, new Rectangle());
        _drawingInfos[3].SetInfo(-1, 0, 0, 0, new Rectangle());
    }

    uint getMask(Vector2 mapSize, TiledMapLayerData layer, int x, int y, int offset)
    {
        if (x >= mapSize.X || x < 0 || y >= mapSize.Y || y < 0)
        {
            return 0;
        }

        var tileId = layer.tiles[x + y * (int)mapSize.X];
        if (tileId != -1)
        {
            return (uint)1 << offset;
        }

        return 0;
    }
    public override void Update(float elapsedTime)
    {
        foreach (var tile in _tiles)
        {
            tile.Update(elapsedTime);
        }
        uint mask = 0;
        mask |= getMask(_mapSize, _tiledMapLayerData, _x - 1, _y, 0);
        mask |= getMask(_mapSize, _tiledMapLayerData, _x + 1, _y, 1);
        mask |= getMask(_mapSize, _tiledMapLayerData, _x, _y - 1, 2);
        mask |= getMask(_mapSize, _tiledMapLayerData, _x, _y + 1, 3);
        mask |= getMask(_mapSize, _tiledMapLayerData, _x - 1, _y - 1, 4);
        mask |= getMask(_mapSize, _tiledMapLayerData, _x - 1, _y + 1, 5);
        mask |= getMask(_mapSize, _tiledMapLayerData, _x + 1, _y - 1, 6);
        mask |= getMask(_mapSize, _tiledMapLayerData, _x + 1, _y + 1, 7);

        ComputeDrawingInfos(mask, 0, 0, 0, new Rectangle(0, 0, (int)_tileSize.X, (int)_tileSize.Y));
    }

    public override void Draw(float x, float y, float z)
    {
        Draw(x, y, z, new Rectangle(0, 0, (int)_tileSize.X, (int)_tileSize.Y));
    }

    public override void Draw(float x, float y, float z, Rectangle uvOffset)
    {
        foreach (var drawingInfo in _drawingInfos)
        {
            if (drawingInfo.TileIndex != -1)
            {
                _tiles[drawingInfo.TileIndex].Draw(
                    x + drawingInfo.XOffset,
                    y + drawingInfo.YOffset,
                    z + drawingInfo.ZOffset,
                    drawingInfo.PosInTexture);
            }
        }
    }

    public void SetTileInfo(List<Tile> tiles, Vector2 tileSize, Vector2 mapSize, TiledMapLayerData layer, int x, int y)
    {
        Debug.Assert(tiles.Count == 6, "SetTileInfo() : size is not 6");

        for (int i = 0; i < 6; ++i)
        {
            Debug.Assert(tiles[i] != null, "SetTileInfo() : ITile is null");

            _tiles[i] = tiles[i];
        }
        _tileSize = tileSize;
        _mapSize = mapSize;
        _tiledMapLayerData = layer;
        _x = x;
        _y = y;
    }

    private void ComputeDrawingInfos(uint mask, float x, float y, float z, Rectangle uvOffset)
    {
        _drawingInfos[0].SetInfo(-1, x, y, z, uvOffset);
        _drawingInfos[1].SetInfo(-1, x, y, z, uvOffset);
        _drawingInfos[2].SetInfo(-1, x, y, z, uvOffset);
        _drawingInfos[3].SetInfo(-1, x, y, z, uvOffset);

        //trivial case
        if (mask == mask_none)
        {
            _drawingInfos[0].SetInfo(0, x, y, z, uvOffset);
            return;
        }

        var width = uvOffset.Width / 2;
        var height = uvOffset.Height / 2;

        //trivial case
        if (mask == mask_all)
        {
            _drawingInfos[0].SetInfo(2, x, y, z, new Rectangle(width, height, width, height));
            _drawingInfos[1].SetInfo(3, x + width, y, z, new Rectangle(0, height, width, height));
            _drawingInfos[2].SetInfo(4, x, y + height, z, new Rectangle(width, 0, width, height));
            _drawingInfos[3].SetInfo(5, x + width, y + height, z, new Rectangle(0, 0, width, height));
            return;
        }

        int index = 0;
        uint mask1 = 0;

        mask1 = mask & (mask_left_top | mask_top | mask_left);
        //left-top sub tile
        if (mask1 == (mask_left_top | mask_top | mask_left))
        {
            _drawingInfos[index++].SetInfo(2, x, y, z, new Rectangle(width, height, width, height));
        }
        else if (mask1 == (mask_left_top | mask_left))
        {
            _drawingInfos[index++].SetInfo(2, x, y, z, new Rectangle(width, 0, width, height));
        }
        else if (mask1 == (mask_left_top | mask_top)
                 || mask1 == mask_top)
        {
            _drawingInfos[index++].SetInfo(4, x, y, z, new Rectangle(0, 0, width, height));
        }
        else if (mask1 == (mask_top | mask_left))
        {
            _drawingInfos[index++].SetInfo(1, x, y, z, new Rectangle(0, 0, width, height));
        }
        else if (mask1 == mask_left_top || mask1 == 0)
        {
            _drawingInfos[index++].SetInfo(0, x, y, z, new Rectangle(0, 0, width, height));
        }
        else if (mask1 == mask_left)
        {
            _drawingInfos[index++].SetInfo(3, x, y, z, new Rectangle(0, 0, width, height));
        }

        x += width;
        mask1 = mask & (mask_right_top | mask_top | mask_right);
        //right-top sub tile
        if (mask1 == (mask_right_top | mask_top | mask_right))
        {
            _drawingInfos[index++].SetInfo(3, x, y, z, new Rectangle(0, height, width, height));
        }
        else if (mask1 == (mask_right_top | mask_right))
        {
            _drawingInfos[index++].SetInfo(3, x, y, z, new Rectangle(0, 0, width, height));
        }
        else if (mask1 == (mask_right_top | mask_top)
                 || mask1 == mask_top)
        {
            _drawingInfos[index++].SetInfo(3, x, y, z, new Rectangle(width, height, width, height));
        }
        else if (mask1 == (mask_top | mask_right))
        {
            _drawingInfos[index++].SetInfo(1, x, y, z, new Rectangle(width, 0, width, height));
        }
        else if (mask1 == mask_right_top || mask1 == 0)
        {
            _drawingInfos[index++].SetInfo(0, x, y, z, new Rectangle(width, 0, width, height));
        }
        else if (mask1 == mask_right)
        {
            _drawingInfos[index++].SetInfo(2, x, y, z, new Rectangle(width, 0, width, height));
        }

        x -= width;
        y += height;
        mask1 = mask & (mask_left_bottom | mask_bottom | mask_left);
        //left-bottom sub tile
        if (mask1 == (mask_left_bottom | mask_bottom | mask_left))
        {
            _drawingInfos[index++].SetInfo(4, x, y, z, new Rectangle(width, 0, width, height));
        }
        else if (mask1 == (mask_left_bottom | mask_left)
                 || mask1 == mask_left)
        {
            _drawingInfos[index++].SetInfo(4, x, y, z, new Rectangle(width, height, width, height));
        }
        else if (mask1 == (mask_left_bottom | mask_bottom)
                 || mask1 == mask_bottom)
        {
            _drawingInfos[index++].SetInfo(2, x, y, z, new Rectangle(0, height, width, height));
        }
        else if (mask1 == (mask_bottom | mask_left))
        {
            _drawingInfos[index++].SetInfo(1, x, y, z, new Rectangle(0, height, width, height));
        }
        else if (mask1 == mask_left_bottom || mask1 == 0)
        {
            _drawingInfos[index++].SetInfo(0, x, y, z, new Rectangle(0, height, width, height));
        }

        x += width;
        //y += height;
        mask1 = mask & (mask_right_bottom | mask_bottom | mask_right);
        //right-bottom sub tile
        if (mask1 == (mask_right_bottom | mask_bottom | mask_right))
        {
            _drawingInfos[index++].SetInfo(5, x, y, z, new Rectangle(0, 0, width, height));
        }
        else if (mask1 == (mask_right_bottom | mask_right))
        {
            _drawingInfos[index++].SetInfo(4, x, y, z, new Rectangle(width, height, width, height));
        }
        else if (mask1 == (mask_right_bottom | mask_bottom)
                 || mask1 == mask_bottom)
        {
            _drawingInfos[index++].SetInfo(3, x, y, z, new Rectangle(width, height, width, height));
        }
        else if (mask1 == (mask_bottom | mask_right))
        {
            _drawingInfos[index++].SetInfo(1, x, y, z, new Rectangle(width, height, width, height));
        }
        else if (mask1 == mask_right_bottom || mask1 == 0)
        {
            _drawingInfos[index++].SetInfo(0, x, y, z, new Rectangle(width, height, width, height));
        }
        else if (mask1 == mask_right)
        {
            _drawingInfos[index++].SetInfo(4, x, y, z, new Rectangle(width, height, width, height));
        }
    }
}