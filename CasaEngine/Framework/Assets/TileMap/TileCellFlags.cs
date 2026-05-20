namespace CasaEngine.Framework.Assets.TileMap;

[Flags]
public enum TileCellFlags
{
    None = 0,
    FlipHorizontal = 1,
    FlipVertical = 2,
    FlipDiagonal = 4,
    HexagonalRotation = 8,
}