using System.Diagnostics;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Size = CasaEngine.Core.Maths.Size;

namespace CasaEngine.Framework.Assets.TileMap;

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
    private Size _tileSize;
    private TileMapLayerData? _tileMapLayerData;
    private Size _mapSize;
    private AutoTileDrawingInfo[] _drawingInfos = new AutoTileDrawingInfo[4];
    private int _x, _y;
    private readonly Texture2D _texture2d;
    private AutoTileData _autoTileData;

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

    public AutoTile(Texture2D texture2d, AutoTileData tileData) : base(tileData)
    {
        _texture2d = texture2d;
        _autoTileData = tileData;

        for (int i = 0; i < 4; i++)
        {
            _drawingInfos[i] = new AutoTileDrawingInfo();
        }
    }

    public void RemovedTileFromLayer()
    {
        _tileMapLayerData.tiles[_x + _y * _mapSize.Width] = -1;
    }

    public override void Initialize(CasaEngineGame game)
    {
        base.Initialize(game);

        _drawingInfos[0].SetInfo(-1, 0, 0, 0, new Rectangle());
        _drawingInfos[1].SetInfo(-1, 0, 0, 0, new Rectangle());
        _drawingInfos[2].SetInfo(-1, 0, 0, 0, new Rectangle());
        _drawingInfos[3].SetInfo(-1, 0, 0, 0, new Rectangle());
    }

    uint getMask(Size mapSize, TileMapLayerData layer, int x, int y, int offset)
    {
        if (x >= mapSize.Width || x < 0 || y >= mapSize.Height || y < 0)
        {
            return 0;
        }

        var tileId = layer.tiles[x + y * mapSize.Width];
        if (tileId == _autoTileData.Id)
        {
            return (uint)1 << offset;
        }

        return 0;
    }
    public override void Update(float elapsedTime)
    {
        uint mask = 0;
        mask |= getMask(_mapSize, _tileMapLayerData, _x - 1, _y + 0, 0); // mask_left
        mask |= getMask(_mapSize, _tileMapLayerData, _x + 1, _y + 0, 1); // mask_right
        mask |= getMask(_mapSize, _tileMapLayerData, _x + 0, _y - 1, 2); // mask_top
        mask |= getMask(_mapSize, _tileMapLayerData, _x + 0, _y + 1, 3); // mask_bottom
        mask |= getMask(_mapSize, _tileMapLayerData, _x - 1, _y - 1, 4); // mask_left_top
        mask |= getMask(_mapSize, _tileMapLayerData, _x - 1, _y + 1, 5); // mask_left_bottom
        mask |= getMask(_mapSize, _tileMapLayerData, _x + 1, _y - 1, 6); // mask_right_top
        mask |= getMask(_mapSize, _tileMapLayerData, _x + 1, _y + 1, 7); // mask_right_bottom

        ComputeDrawingInfos(mask, 0, 0, 0, new Rectangle(0, 0, _tileSize.Width, _tileSize.Height));
    }

    public override void Draw(float x, float y, float z, Vector2 scale)
    {
        Draw(x, y, z, new Rectangle(0, 0, _tileSize.Width, _tileSize.Height), scale);
    }

    public override void Draw(float x, float y, float z, Rectangle uvOffset, Vector2 scale)
    {
        for (var index = 0; index < _drawingInfos.Length; index++)
        {
            var drawingInfo = _drawingInfos[index];
            if (drawingInfo.TileIndex != -1)
            {
                Draw(_texture2d,
                    _autoTileData.Locations[drawingInfo.TileIndex],
                    x + drawingInfo.XOffset * scale.X,
                    y + drawingInfo.YOffset * scale.Y,
                    z + drawingInfo.ZOffset,
                    drawingInfo.PosInTexture,
                    scale);
            }
        }
    }

    public void SetTileInfo(Size tileSize, Size mapSize, TileMapLayerData layer, int x, int y)
    {
        _tileSize = tileSize;
        _mapSize = mapSize;
        _tileMapLayerData = layer;
        _x = x;
        _y = y;
    }

    private void ComputeDrawingInfos(uint mask, float x, float y, float z, Rectangle uvOffset)
    {
        _drawingInfos[0].SetInfo(-1, x, y, z, uvOffset);
        _drawingInfos[1].SetInfo(-1, x, y, z, uvOffset);
        _drawingInfos[2].SetInfo(-1, x, y, z, uvOffset);
        _drawingInfos[3].SetInfo(-1, x, y, z, uvOffset);

        if (mask == mask_none)
        {
            _drawingInfos[0].SetInfo(0, x, y, z, uvOffset);
            return;
        }

        var width = uvOffset.Width / 2;
        var height = uvOffset.Height / 2;

        if (mask == mask_all)
        {
            _drawingInfos[0].SetInfo(2, x, y, z, new Rectangle(width, height, width, height));
            _drawingInfos[1].SetInfo(3, x + width, y, z, new Rectangle(0, height, width, height));
            _drawingInfos[2].SetInfo(4, x, y - height, z, new Rectangle(width, 0, width, height));
            _drawingInfos[3].SetInfo(5, x + width, y - height, z, new Rectangle(0, 0, width, height));
            return;
        }

        int index = 0;

        var mask1 = mask & (mask_left_top | mask_top | mask_left);
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
            _drawingInfos[index++].SetInfo(0, x, y, z, new Rectangle(0, 0, width, height));
        }
        else if (mask1 == (mask_top | mask_left))
        {
            _drawingInfos[index++].SetInfo(1, x, y, z, new Rectangle(0, 0, width, height));
        }
        else if (mask1 == mask_left_top || mask1 == 0)
        {
            _drawingInfos[index++].SetInfo(3, x, y, z, new Rectangle(width, 0, width, height));
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
            _drawingInfos[index++].SetInfo(0, x, y, z, new Rectangle(width, height, width, height));
        }
        else if (mask1 == (mask_top | mask_right))
        {
            _drawingInfos[index++].SetInfo(1, x, y, z, new Rectangle(width, 0, width, height));
        }
        else if (mask1 == mask_right_top || mask1 == 0)
        {
            _drawingInfos[index++].SetInfo(3, x, y, z, new Rectangle(width, 0, width, height));
        }
        else if (mask1 == mask_right)
        {
            _drawingInfos[index++].SetInfo(2, x, y, z, new Rectangle(width, 0, width, height));
        }

        x -= width;
        y -= height;
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
            _drawingInfos[index++].SetInfo(4, x, y, z, new Rectangle(0, height, width, height));
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
            _drawingInfos[index++].SetInfo(4, x, y, z, new Rectangle(width, height, width, height));
        }
        else if (mask1 == mask_right)
        {
            _drawingInfos[index++].SetInfo(0, x, y, z, new Rectangle(width, height, width, height));
        }
    }
}