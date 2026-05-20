using System.Globalization;
using System.Xml.Linq;
using CasaEngine.Framework.Assets.TileMap;
using Newtonsoft.Json.Linq;

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
        var extension = Path.GetExtension(sourceFilePath);
        return string.Equals(extension, ".tmx", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".tmj", StringComparison.OrdinalIgnoreCase);
    }

    public TiledMapImportDocument Import(string sourceFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);

        if (!IsMapFileSupported(sourceFilePath))
        {
            throw new NotSupportedException($"Tiled file '{sourceFilePath}' is not supported by this importer.");
        }

        if (string.Equals(Path.GetExtension(sourceFilePath), ".tmj", StringComparison.OrdinalIgnoreCase))
        {
            return ImportTmj(sourceFilePath);
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
        result.Warnings.AddRange(tilesetReference.Warnings);

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
            var tiles = ReadLayerTiles(dataElement, mapWidth * mapHeight, tilesetReference, result.Warnings, out var tileFlags);
            var layerName = ReadOptionalString(layerElement, "name", $"Layer {layerIndex + 1}");
            result.Layers.Add(new TiledTileLayer(layerName, layerIndex * 0.1f, tiles, tileFlags));
            layerIndex++;
        }

        if (result.Layers.Count == 0)
        {
            result.Warnings.Add("The Tiled map does not contain tile layers.");
        }

        return result;
    }

    private static TiledMapImportDocument ImportTmj(string sourceFilePath)
    {
        var mapObject = JObject.Parse(File.ReadAllText(sourceFilePath));

        var orientation = ReadRequiredString(mapObject, "orientation", "map");
        if (!string.Equals(orientation, "orthogonal", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Tiled orientation '{orientation}' is not supported. Only orthogonal finite maps are supported.");
        }

        if (ReadOptionalBool(mapObject, "infinite"))
        {
            throw new NotSupportedException("Infinite Tiled maps are not supported.");
        }

        var mapWidth = ReadRequiredInt(mapObject, "width", "map");
        var mapHeight = ReadRequiredInt(mapObject, "height", "map");
        var tileWidth = ReadRequiredInt(mapObject, "tilewidth", "map");
        var tileHeight = ReadRequiredInt(mapObject, "tileheight", "map");

        var tilesetReference = ReadSingleTilesetJson(sourceFilePath, mapObject, tileWidth, tileHeight);
        var result = new TiledMapImportDocument(
            sourceFilePath,
            mapWidth,
            mapHeight,
            tileWidth,
            tileHeight,
            tilesetReference);
        result.Warnings.AddRange(tilesetReference.Warnings);

        var layers = mapObject["layers"] as JArray
            ?? throw new InvalidDataException("Tiled JSON map requires a 'layers' array.");
        var layerIndex = 0;
        for (var index = 0; index < layers.Count; index++)
        {
            if (layers[index] is not JObject layerObject)
            {
                continue;
            }

            var layerType = ReadOptionalString(layerObject, "type", string.Empty);
            if (!string.Equals(layerType, "tilelayer", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var layerWidth = ReadOptionalInt(layerObject, "width", mapWidth);
            var layerHeight = ReadOptionalInt(layerObject, "height", mapHeight);
            if (layerWidth != mapWidth || layerHeight != mapHeight)
            {
                throw new NotSupportedException($"Tiled layer '{ReadOptionalString(layerObject, "name", string.Empty)}' size {layerWidth}x{layerHeight} does not match map size {mapWidth}x{mapHeight}.");
            }

            var tiles = ReadLayerTilesJson(layerObject, mapWidth * mapHeight, tilesetReference, result.Warnings, out var tileFlags);
            var layerName = ReadOptionalString(layerObject, "name", $"Layer {layerIndex + 1}");
            result.Layers.Add(new TiledTileLayer(layerName, layerIndex * 0.1f, tiles, tileFlags));
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

        var warnings = new List<string>();
        var collisionShapes = ReadTileCollisionShapes(tilesetRoot, warnings);

        return new TiledTilesetReference(
            firstGid,
            ReadOptionalString(tilesetRoot, "name", Path.GetFileNameWithoutExtension(imageFilePath)),
            imageFilePath,
            tileWidth,
            tileHeight,
            columns,
            tileCount,
            imageWidth,
            imageHeight,
            collisionShapes,
            warnings);
    }

    private static TiledTilesetReference ReadSingleTilesetJson(string mapFilePath, JObject mapObject, int mapTileWidth, int mapTileHeight)
    {
        var tilesetArray = mapObject["tilesets"] as JArray
            ?? throw new InvalidDataException("Tiled JSON map requires a 'tilesets' array.");
        if (tilesetArray.Count != 1)
        {
            throw new NotSupportedException($"Tiled import v1 supports exactly one tileset per map, but found {tilesetArray.Count}.");
        }

        if (tilesetArray[0] is not JObject tilesetObject)
        {
            throw new InvalidDataException("Tiled JSON tileset entry must be an object.");
        }

        var firstGid = ReadOptionalInt(tilesetObject, "firstgid", 1);
        var source = ReadOptionalString(tilesetObject, "source", string.Empty);
        var tilesetFilePath = mapFilePath;
        var tilesetRoot = tilesetObject;

        if (!string.IsNullOrWhiteSpace(source))
        {
            tilesetFilePath = ResolvePath(mapFilePath, source);
            tilesetRoot = JObject.Parse(File.ReadAllText(tilesetFilePath));
        }

        var tileWidth = ReadRequiredInt(tilesetRoot, "tilewidth", "tileset");
        var tileHeight = ReadRequiredInt(tilesetRoot, "tileheight", "tileset");
        if (tileWidth != mapTileWidth || tileHeight != mapTileHeight)
        {
            throw new NotSupportedException($"Tileset tile size {tileWidth}x{tileHeight} does not match map tile size {mapTileWidth}x{mapTileHeight}.");
        }

        var imageSource = ReadRequiredString(tilesetRoot, "image", "tileset");
        var imageFilePath = ResolvePath(tilesetFilePath, imageSource);
        var imageWidth = ReadOptionalInt(tilesetRoot, "imagewidth", 0);
        var imageHeight = ReadOptionalInt(tilesetRoot, "imageheight", 0);
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

        var warnings = new List<string>();
        var collisionShapes = ReadTileCollisionShapesJson(tilesetRoot, warnings);

        return new TiledTilesetReference(
            firstGid,
            ReadOptionalString(tilesetRoot, "name", Path.GetFileNameWithoutExtension(imageFilePath)),
            imageFilePath,
            tileWidth,
            tileHeight,
            columns,
            tileCount,
            imageWidth,
            imageHeight,
            collisionShapes,
            warnings);
    }

    private static Dictionary<int, TiledTileCollision> ReadTileCollisionShapes(XElement tilesetRoot, List<string> warnings)
    {
        var collisions = new Dictionary<int, TiledTileCollision>();

        foreach (var tileElement in tilesetRoot.Elements("tile"))
        {
            var tileId = ReadRequiredInt(tileElement, "id");
            var objectGroup = tileElement.Element("objectgroup");
            if (objectGroup == null)
            {
                continue;
            }

            var acceptedCollision = false;
            foreach (var objectElement in objectGroup.Elements("object"))
            {
                if (objectElement.Element("polygon") != null
                    || objectElement.Element("polyline") != null
                    || objectElement.Element("ellipse") != null)
                {
                    warnings.Add($"Tile {tileId} has a non-rectangle collision object; only rectangle collisions are imported for now.");
                    continue;
                }

                var width = ReadOptionalFloat(objectElement, "width", 0f);
                var height = ReadOptionalFloat(objectElement, "height", 0f);
                if (width <= 0f || height <= 0f)
                {
                    continue;
                }

                if (acceptedCollision)
                {
                    warnings.Add($"Tile {tileId} has multiple collision objects; only the first rectangle is imported for now.");
                    continue;
                }

                collisions[tileId] = new TiledTileCollision(
                    ReadOptionalFloat(objectElement, "x", 0f),
                    ReadOptionalFloat(objectElement, "y", 0f),
                    width,
                    height);
                acceptedCollision = true;
            }
        }

        return collisions;
    }

    private static Dictionary<int, TiledTileCollision> ReadTileCollisionShapesJson(JObject tilesetRoot, List<string> warnings)
    {
        var collisions = new Dictionary<int, TiledTileCollision>();
        if (tilesetRoot["tiles"] is not JArray tiles)
        {
            return collisions;
        }

        for (var tileIndex = 0; tileIndex < tiles.Count; tileIndex++)
        {
            if (tiles[tileIndex] is not JObject tileObject || tileObject["objectgroup"] is not JObject objectGroup)
            {
                continue;
            }

            var tileId = ReadRequiredInt(tileObject, "id", "tile");
            if (objectGroup["objects"] is not JArray objects)
            {
                continue;
            }

            var acceptedCollision = false;
            for (var objectIndex = 0; objectIndex < objects.Count; objectIndex++)
            {
                if (objects[objectIndex] is not JObject objectElement)
                {
                    continue;
                }

                if (objectElement["polygon"] != null
                    || objectElement["polyline"] != null
                    || ReadOptionalBool(objectElement, "ellipse"))
                {
                    warnings.Add($"Tile {tileId} has a non-rectangle collision object; only rectangle collisions are imported for now.");
                    continue;
                }

                var width = ReadOptionalFloat(objectElement, "width", 0f);
                var height = ReadOptionalFloat(objectElement, "height", 0f);
                if (width <= 0f || height <= 0f)
                {
                    continue;
                }

                if (acceptedCollision)
                {
                    warnings.Add($"Tile {tileId} has multiple collision objects; only the first rectangle is imported for now.");
                    continue;
                }

                collisions[tileId] = new TiledTileCollision(
                    ReadOptionalFloat(objectElement, "x", 0f),
                    ReadOptionalFloat(objectElement, "y", 0f),
                    width,
                    height);
                acceptedCollision = true;
            }
        }

        return collisions;
    }

    private static List<int> ReadLayerTiles(
        XElement dataElement,
        int expectedTileCount,
        TiledTilesetReference tilesetReference,
        List<string> warnings,
        out List<TileCellFlags> tileFlags)
    {
        var compression = dataElement.Attribute("compression")?.Value;
        if (!string.IsNullOrWhiteSpace(compression))
        {
            throw new NotSupportedException($"Compressed Tiled layer data '{compression}' is not supported.");
        }

        var encoding = dataElement.Attribute("encoding")?.Value;
        var tiles = new List<int>(expectedTileCount);
        tileFlags = new List<TileCellFlags>(expectedTileCount);
        var flipWarningAdded = false;

        if (string.Equals(encoding, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var values = (dataElement.Value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var value in values)
            {
                tiles.Add(ConvertGid(ParseGid(value), tilesetReference, warnings, ref flipWarningAdded, out var flags));
                tileFlags.Add(flags);
            }
        }
        else if (string.IsNullOrWhiteSpace(encoding))
        {
            foreach (var tileElement in dataElement.Elements("tile"))
            {
                tiles.Add(ConvertGid(ParseGid(ReadRequiredString(tileElement, "gid")), tilesetReference, warnings, ref flipWarningAdded, out var flags));
                tileFlags.Add(flags);
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

    private static List<int> ReadLayerTilesJson(
        JObject layerObject,
        int expectedTileCount,
        TiledTilesetReference tilesetReference,
        List<string> warnings,
        out List<TileCellFlags> tileFlags)
    {
        var compression = ReadOptionalString(layerObject, "compression", string.Empty);
        if (!string.IsNullOrWhiteSpace(compression))
        {
            throw new NotSupportedException($"Compressed Tiled layer data '{compression}' is not supported.");
        }

        var data = layerObject["data"] as JArray
            ?? throw new NotSupportedException("Tiled JSON layer data must be an array of gids.");
        var tiles = new List<int>(expectedTileCount);
        tileFlags = new List<TileCellFlags>(expectedTileCount);
        var flipWarningAdded = false;

        for (var index = 0; index < data.Count; index++)
        {
            var gidValue = data[index]!.Value<long>();
            if (gidValue < 0 || gidValue > uint.MaxValue)
            {
                throw new InvalidDataException($"Invalid Tiled gid '{gidValue}'.");
            }

            tiles.Add(ConvertGid((uint)gidValue, tilesetReference, warnings, ref flipWarningAdded, out var flags));
            tileFlags.Add(flags);
        }

        if (tiles.Count != expectedTileCount)
        {
            throw new InvalidDataException($"Tiled layer has {tiles.Count} cells but expected {expectedTileCount}.");
        }

        return tiles;
    }

    private static int ConvertGid(uint rawGid, TiledTilesetReference tilesetReference, List<string> warnings, ref bool flipWarningAdded, out TileCellFlags flags)
    {
        var cleanedGid = rawGid & TileIdMask;
        flags = GetTileCellFlags(rawGid);
        if (cleanedGid == 0)
        {
            flags = TileCellFlags.None;
            return TileMapData.EmptyTileId;
        }

        if (cleanedGid != rawGid && !flipWarningAdded)
        {
            warnings.Add("Tiled flip/rotation flags were imported; horizontal and vertical static-tile flips are rendered, diagonal rotation remains limited.");
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

    private static TileCellFlags GetTileCellFlags(uint rawGid)
    {
        var flags = TileCellFlags.None;
        if ((rawGid & HorizontalFlipFlag) != 0u)
        {
            flags |= TileCellFlags.FlipHorizontal;
        }

        if ((rawGid & VerticalFlipFlag) != 0u)
        {
            flags |= TileCellFlags.FlipVertical;
        }

        if ((rawGid & DiagonalFlipFlag) != 0u)
        {
            flags |= TileCellFlags.FlipDiagonal;
        }

        if ((rawGid & HexagonalRotationFlag) != 0u)
        {
            flags |= TileCellFlags.HexagonalRotation;
        }

        return flags;
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

    private static float ReadOptionalFloat(XElement element, string attributeName, float defaultValue)
    {
        var value = element.Attribute(attributeName)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
        {
            throw new InvalidDataException($"Tiled attribute '{attributeName}' has invalid float value '{value}'.");
        }

        return result;
    }

    private static bool ReadOptionalBool(XElement element, string attributeName)
    {
        var value = element.Attribute(attributeName)?.Value;
        return string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadRequiredString(JObject element, string propertyName, string elementName)
    {
        var value = element[propertyName]?.Value<string>();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"Tiled {elementName} requires property '{propertyName}'.");
        }

        return value;
    }

    private static string ReadOptionalString(JObject element, string propertyName, string defaultValue)
    {
        var value = element[propertyName]?.Value<string>();
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private static int ReadRequiredInt(JObject element, string propertyName, string elementName)
    {
        if (element[propertyName] == null)
        {
            throw new InvalidDataException($"Tiled {elementName} requires property '{propertyName}'.");
        }

        return ReadOptionalInt(element, propertyName, 0);
    }

    private static int ReadOptionalInt(JObject element, string propertyName, int defaultValue)
    {
        var token = element[propertyName];
        if (token == null || token.Type == JTokenType.Null)
        {
            return defaultValue;
        }

        if (token.Type == JTokenType.Integer)
        {
            return token.Value<int>();
        }

        var value = token.Value<string>();
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            throw new InvalidDataException($"Tiled property '{propertyName}' has invalid integer value '{value}'.");
        }

        return result;
    }

    private static float ReadOptionalFloat(JObject element, string propertyName, float defaultValue)
    {
        var token = element[propertyName];
        if (token == null || token.Type == JTokenType.Null)
        {
            return defaultValue;
        }

        if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
        {
            return token.Value<float>();
        }

        var value = token.Value<string>();
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
        {
            throw new InvalidDataException($"Tiled property '{propertyName}' has invalid float value '{value}'.");
        }

        return result;
    }

    private static bool ReadOptionalBool(JObject element, string propertyName)
    {
        var token = element[propertyName];
        if (token == null || token.Type == JTokenType.Null)
        {
            return false;
        }

        if (token.Type == JTokenType.Boolean)
        {
            return token.Value<bool>();
        }

        if (token.Type == JTokenType.Integer)
        {
            return token.Value<int>() != 0;
        }

        var value = token.Value<string>();
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
        int imageHeight,
        Dictionary<int, TiledTileCollision> collisionByTileId,
        List<string> warnings)
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
        CollisionByTileId = collisionByTileId;
        Warnings = warnings;
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
    public Dictionary<int, TiledTileCollision> CollisionByTileId { get; }
    public List<string> Warnings { get; }
}

public sealed class TiledTileCollision
{
    public TiledTileCollision(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public float X { get; }
    public float Y { get; }
    public float Width { get; }
    public float Height { get; }
}

public sealed class TiledTileLayer
{
    public TiledTileLayer(string name, float zOffset, List<int> tiles, List<TileCellFlags> tileFlags)
    {
        Name = name;
        ZOffset = zOffset;
        Tiles = tiles;
        TileFlags = tileFlags;
    }

    public string Name { get; }
    public float ZOffset { get; }
    public List<int> Tiles { get; }
    public List<TileCellFlags> TileFlags { get; }
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