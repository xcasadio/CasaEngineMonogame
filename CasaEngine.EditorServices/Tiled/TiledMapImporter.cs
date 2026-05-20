using System.Globalization;
using System.Xml.Linq;
using CasaEngine.Framework.Assets.TileMap;

namespace CasaEngine.EditorServices.Tiled;

public sealed class TiledMapImporter
{
    public const uint HorizontalFlipFlag = 0x80000000u;
    public const uint VerticalFlipFlag = 0x40000000u;
    public const uint DiagonalFlipFlag = 0x20000000u;
    public const uint HexagonalRotationFlag = 0x10000000u;

    private const uint TileIdMask = 0x0FFFFFFFu;

    public bool IsFileSupported(string sourceFilePath)
    {
        return IsMapFileSupported(sourceFilePath);
    }

    public static bool IsMapFileSupported(string sourceFilePath)
    {
        return string.Equals(Path.GetExtension(sourceFilePath), ".tmx", StringComparison.OrdinalIgnoreCase);
    }

    public TiledMapImportDocument Import(string sourceFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);

        if (!IsMapFileSupported(sourceFilePath))
        {
            throw new NotSupportedException($"Tiled file '{sourceFilePath}' is not supported by this importer.");
        }

        return ImportTmx(sourceFilePath);
    }

    private static TiledMapImportDocument ImportTmx(string sourceFilePath)
    {
        var document = XDocument.Load(sourceFilePath, LoadOptions.SetLineInfo);
        var mapElement = document.Root ?? throw new InvalidDataException("Tiled map document is empty.");
        if (!string.Equals(mapElement.Name.LocalName, "map", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Tiled map root element must be 'map'.");
        }

        var orientation = ReadRequiredString(mapElement, "orientation");
        if (!string.Equals(orientation, "orthogonal", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Tiled orientation '{orientation}' is not supported. Only orthogonal finite maps are supported.");
        }

        if (ReadOptionalBool(mapElement, "infinite"))
        {
            throw new NotSupportedException("Infinite Tiled maps are not supported.");
        }

        var mapWidth = ReadRequiredInt(mapElement, "width");
        var mapHeight = ReadRequiredInt(mapElement, "height");
        var tileWidth = ReadRequiredInt(mapElement, "tilewidth");
        var tileHeight = ReadRequiredInt(mapElement, "tileheight");

        var tilesetReference = ReadSingleTileset(sourceFilePath, mapElement, tileWidth, tileHeight);
        var result = new TiledMapImportDocument(
            sourceFilePath,
            mapWidth,
            mapHeight,
            tileWidth,
            tileHeight,
            tilesetReference);

        var layerIndex = 0;
        foreach (var layerElement in mapElement.Elements("layer"))
        {
            var layerWidth = ReadOptionalInt(layerElement, "width", mapWidth);
            var layerHeight = ReadOptionalInt(layerElement, "height", mapHeight);
            if (layerWidth != mapWidth || layerHeight != mapHeight)
            {
                throw new NotSupportedException($"Tiled layer '{ReadOptionalString(layerElement, "name", string.Empty)}' size {layerWidth}x{layerHeight} does not match map size {mapWidth}x{mapHeight}.");
            }

            var dataElement = layerElement.Element("data")
                ?? throw new InvalidDataException($"Tiled layer '{ReadOptionalString(layerElement, "name", string.Empty)}' has no data element.");
            var tiles = ReadLayerTiles(dataElement, mapWidth * mapHeight, tilesetReference, result.Warnings);
            var layerName = ReadOptionalString(layerElement, "name", $"Layer {layerIndex + 1}");
            result.Layers.Add(new TiledTileLayer(layerName, layerIndex * 0.1f, tiles));
            layerIndex++;
        }

        if (result.Layers.Count == 0)
        {
            result.Warnings.Add("The Tiled map does not contain tile layers.");
        }

        return result;
    }

    private static TiledTilesetReference ReadSingleTileset(string mapFilePath, XElement mapElement, int mapTileWidth, int mapTileHeight)
    {
        var tilesetElements = mapElement.Elements("tileset").ToList();
        if (tilesetElements.Count != 1)
        {
            throw new NotSupportedException($"Tiled import v1 supports exactly one tileset per map, but found {tilesetElements.Count}.");
        }

        var tilesetElement = tilesetElements[0];
        var firstGid = ReadOptionalInt(tilesetElement, "firstgid", 1);
        var sourceAttribute = tilesetElement.Attribute("source")?.Value;
        var tilesetFilePath = mapFilePath;
        var tilesetRoot = tilesetElement;

        if (!string.IsNullOrWhiteSpace(sourceAttribute))
        {
            tilesetFilePath = ResolvePath(mapFilePath, sourceAttribute);
            var tilesetDocument = XDocument.Load(tilesetFilePath, LoadOptions.SetLineInfo);
            tilesetRoot = tilesetDocument.Root ?? throw new InvalidDataException($"Tiled tileset '{tilesetFilePath}' is empty.");
            if (!string.Equals(tilesetRoot.Name.LocalName, "tileset", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Tiled tileset '{tilesetFilePath}' root element must be 'tileset'.");
            }
        }

        var tileWidth = ReadRequiredInt(tilesetRoot, "tilewidth");
        var tileHeight = ReadRequiredInt(tilesetRoot, "tileheight");
        if (tileWidth != mapTileWidth || tileHeight != mapTileHeight)
        {
            throw new NotSupportedException($"Tileset tile size {tileWidth}x{tileHeight} does not match map tile size {mapTileWidth}x{mapTileHeight}.");
        }

        var imageElement = tilesetRoot.Element("image")
            ?? throw new NotSupportedException("Tiled import v1 requires an image-based tileset.");
        var imageSource = ReadRequiredString(imageElement, "source");
        var imageFilePath = ResolvePath(tilesetFilePath, imageSource);
        var imageWidth = ReadOptionalInt(imageElement, "width", 0);
        var imageHeight = ReadOptionalInt(imageElement, "height", 0);
        var columns = ReadOptionalInt(tilesetRoot, "columns", 0);
        var tileCount = ReadOptionalInt(tilesetRoot, "tilecount", 0);

        if (columns <= 0 && imageWidth > 0)
        {
            columns = imageWidth / tileWidth;
        }

        if (tileCount <= 0 && imageWidth > 0 && imageHeight > 0)
        {
            tileCount = (imageWidth / tileWidth) * (imageHeight / tileHeight);
        }

        if (columns <= 0 || tileCount <= 0)
        {
            throw new InvalidDataException("Tiled tileset must define columns/tilecount or image width/height.");
        }

        return new TiledTilesetReference(
            firstGid,
            ReadOptionalString(tilesetRoot, "name", Path.GetFileNameWithoutExtension(imageFilePath)),
            imageFilePath,
            tileWidth,
            tileHeight,
            columns,
            tileCount,
            imageWidth,
            imageHeight);
    }

    private static List<int> ReadLayerTiles(
        XElement dataElement,
        int expectedTileCount,
        TiledTilesetReference tilesetReference,
        List<string> warnings)
    {
        var compression = dataElement.Attribute("compression")?.Value;
        if (!string.IsNullOrWhiteSpace(compression))
        {
            throw new NotSupportedException($"Compressed Tiled layer data '{compression}' is not supported.");
        }

        var encoding = dataElement.Attribute("encoding")?.Value;
        var tiles = new List<int>(expectedTileCount);
        var flipWarningAdded = false;

        if (string.Equals(encoding, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var values = (dataElement.Value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var value in values)
            {
                tiles.Add(ConvertGid(ParseGid(value), tilesetReference, warnings, ref flipWarningAdded));
            }
        }
        else if (string.IsNullOrWhiteSpace(encoding))
        {
            foreach (var tileElement in dataElement.Elements("tile"))
            {
                tiles.Add(ConvertGid(ParseGid(ReadRequiredString(tileElement, "gid")), tilesetReference, warnings, ref flipWarningAdded));
            }
        }
        else
        {
            throw new NotSupportedException($"Tiled layer encoding '{encoding}' is not supported. Use CSV or XML tile data.");
        }

        if (tiles.Count != expectedTileCount)
        {
            throw new InvalidDataException($"Tiled layer has {tiles.Count} cells but expected {expectedTileCount}.");
        }

        return tiles;
    }

    private static int ConvertGid(uint rawGid, TiledTilesetReference tilesetReference, List<string> warnings, ref bool flipWarningAdded)
    {
        var cleanedGid = rawGid & TileIdMask;
        if (cleanedGid == 0)
        {
            return TileMapData.EmptyTileId;
        }

        if (cleanedGid != rawGid && !flipWarningAdded)
        {
            warnings.Add("Tiled flip/rotation flags were masked from GIDs; flipped rendering is not supported yet.");
            flipWarningAdded = true;
        }

        var firstGid = (uint)tilesetReference.FirstGid;
        var maxExclusiveGid = firstGid + (uint)tilesetReference.TileCount;
        if (cleanedGid < firstGid || cleanedGid >= maxExclusiveGid)
        {
            throw new InvalidDataException($"Tiled gid {cleanedGid} is outside the imported tileset range {firstGid}..{maxExclusiveGid - 1}.");
        }

        return (int)(cleanedGid - firstGid);
    }

    private static uint ParseGid(string value)
    {
        if (!uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var gid))
        {
            throw new InvalidDataException($"Invalid Tiled gid '{value}'.");
        }

        return gid;
    }

    private static string ResolvePath(string ownerFilePath, string source)
    {
        var normalizedSource = source.Replace('/', Path.DirectorySeparatorChar);
        var ownerDirectory = Path.GetDirectoryName(ownerFilePath) ?? string.Empty;
        return Path.GetFullPath(Path.Combine(ownerDirectory, normalizedSource));
    }

    private static string ReadRequiredString(XElement element, string attributeName)
    {
        var value = element.Attribute(attributeName)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"Tiled element '{element.Name.LocalName}' requires attribute '{attributeName}'.");
        }

        return value;
    }

    private static string ReadOptionalString(XElement element, string attributeName, string defaultValue)
    {
        var value = element.Attribute(attributeName)?.Value;
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private static int ReadRequiredInt(XElement element, string attributeName)
    {
        var value = ReadRequiredString(element, attributeName);
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            throw new InvalidDataException($"Tiled attribute '{attributeName}' has invalid integer value '{value}'.");
        }

        return result;
    }

    private static int ReadOptionalInt(XElement element, string attributeName, int defaultValue)
    {
        var value = element.Attribute(attributeName)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            throw new InvalidDataException($"Tiled attribute '{attributeName}' has invalid integer value '{value}'.");
        }

        return result;
    }

    private static bool ReadOptionalBool(XElement element, string attributeName)
    {
        var value = element.Attribute(attributeName)?.Value;
        return string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class TiledMapImportDocument
{
    public TiledMapImportDocument(
        string sourceFilePath,
        int width,
        int height,
        int tileWidth,
        int tileHeight,
        TiledTilesetReference tileset)
    {
        SourceFilePath = sourceFilePath;
        Width = width;
        Height = height;
        TileWidth = tileWidth;
        TileHeight = tileHeight;
        Tileset = tileset;
    }

    public string SourceFilePath { get; }
    public int Width { get; }
    public int Height { get; }
    public int TileWidth { get; }
    public int TileHeight { get; }
    public TiledTilesetReference Tileset { get; }
    public List<TiledTileLayer> Layers { get; } = new();
    public List<string> Warnings { get; } = new();
}

public sealed class TiledTilesetReference
{
    public TiledTilesetReference(
        int firstGid,
        string name,
        string imageFilePath,
        int tileWidth,
        int tileHeight,
        int columns,
        int tileCount,
        int imageWidth,
        int imageHeight)
    {
        FirstGid = firstGid;
        Name = name;
        ImageFilePath = imageFilePath;
        TileWidth = tileWidth;
        TileHeight = tileHeight;
        Columns = columns;
        TileCount = tileCount;
        ImageWidth = imageWidth;
        ImageHeight = imageHeight;
    }

    public int FirstGid { get; }
    public string Name { get; }
    public string ImageFilePath { get; }
    public int TileWidth { get; }
    public int TileHeight { get; }
    public int Columns { get; }
    public int TileCount { get; }
    public int ImageWidth { get; }
    public int ImageHeight { get; }
}

public sealed class TiledTileLayer
{
    public TiledTileLayer(string name, float zOffset, List<int> tiles)
    {
        Name = name;
        ZOffset = zOffset;
        Tiles = tiles;
    }

    public string Name { get; }
    public float ZOffset { get; }
    public List<int> Tiles { get; }
}

public sealed class TiledMapImportResult
{
    public TiledMapImportResult(IReadOnlyList<string> createdAssetFileNames, IReadOnlyList<string> warnings)
    {
        CreatedAssetFileNames = createdAssetFileNames;
        Warnings = warnings;
    }

    public IReadOnlyList<string> CreatedAssetFileNames { get; }
    public IReadOnlyList<string> Warnings { get; }
}