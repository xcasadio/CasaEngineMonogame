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

        var tilesetReferences = ReadTilesets(sourceFilePath, mapElement, tileWidth, tileHeight);
        var result = new TiledMapImportDocument(
            sourceFilePath,
            mapWidth,
            mapHeight,
            tileWidth,
            tileHeight,
            tilesetReferences);
        CopyCustomProperties(ReadCustomProperties(mapElement), result.CustomProperties);
        AddTilesetWarnings(tilesetReferences, result.Warnings);

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
            var tiles = ReadLayerTiles(dataElement, mapWidth * mapHeight, tilesetReferences, result.Warnings, out var tileSources, out var tileFlags);
            var layerName = ReadOptionalString(layerElement, "name", $"Layer {layerIndex + 1}");
            result.Layers.Add(new TiledTileLayer(layerName, layerIndex * 0.1f, tiles, tileSources, tileFlags, ReadCustomProperties(layerElement)));
            layerIndex++;
        }

        var objectLayerIndex = 0;
        foreach (var objectGroupElement in mapElement.Elements("objectgroup"))
        {
            result.ObjectLayers.Add(ReadObjectLayer(objectGroupElement, objectLayerIndex * 0.1f));
            objectLayerIndex++;
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

        var tilesetReferences = ReadTilesetsJson(sourceFilePath, mapObject, tileWidth, tileHeight);
        var result = new TiledMapImportDocument(
            sourceFilePath,
            mapWidth,
            mapHeight,
            tileWidth,
            tileHeight,
            tilesetReferences);
        CopyCustomProperties(ReadCustomProperties(mapObject), result.CustomProperties);
        AddTilesetWarnings(tilesetReferences, result.Warnings);

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

            var tiles = ReadLayerTilesJson(layerObject, mapWidth * mapHeight, tilesetReferences, result.Warnings, out var tileSources, out var tileFlags);
            var layerName = ReadOptionalString(layerObject, "name", $"Layer {layerIndex + 1}");
            result.Layers.Add(new TiledTileLayer(layerName, layerIndex * 0.1f, tiles, tileSources, tileFlags, ReadCustomProperties(layerObject)));
            layerIndex++;
        }

        var objectLayerIndex = 0;
        for (var index = 0; index < layers.Count; index++)
        {
            if (layers[index] is not JObject layerObject)
            {
                continue;
            }

            var layerType = ReadOptionalString(layerObject, "type", string.Empty);
            if (string.Equals(layerType, "objectgroup", StringComparison.OrdinalIgnoreCase))
            {
                result.ObjectLayers.Add(ReadObjectLayerJson(layerObject, objectLayerIndex * 0.1f));
                objectLayerIndex++;
            }
        }

        if (result.Layers.Count == 0)
        {
            result.Warnings.Add("The Tiled map does not contain tile layers.");
        }

        return result;
    }

    private static void AddTilesetWarnings(IReadOnlyList<TiledTilesetReference> tilesetReferences, List<string> warnings)
    {
        for (var index = 0; index < tilesetReferences.Count; index++)
        {
            warnings.AddRange(tilesetReferences[index].Warnings);
        }
    }

    private static List<TiledTilesetReference> ReadTilesets(string mapFilePath, XElement mapElement, int mapTileWidth, int mapTileHeight)
    {
        var tilesetElements = mapElement.Elements("tileset").ToList();
        if (tilesetElements.Count == 0)
        {
            throw new NotSupportedException("Tiled map must define at least one tileset.");
        }

        var tilesets = new List<TiledTilesetReference>(tilesetElements.Count);
        for (var index = 0; index < tilesetElements.Count; index++)
        {
            tilesets.Add(ReadTilesetReference(mapFilePath, tilesetElements[index], mapTileWidth, mapTileHeight));
        }

        return tilesets;
    }

    private static TiledTilesetReference ReadTilesetReference(string mapFilePath, XElement tilesetElement, int mapTileWidth, int mapTileHeight)
    {
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
        var customPropertiesByTileId = ReadTileCustomProperties(tilesetRoot);
        var animationsByTileId = ReadTileAnimations(tilesetRoot, warnings);

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
            customPropertiesByTileId,
            animationsByTileId,
            warnings);
    }

    private static List<TiledTilesetReference> ReadTilesetsJson(string mapFilePath, JObject mapObject, int mapTileWidth, int mapTileHeight)
    {
        var tilesetArray = mapObject["tilesets"] as JArray
            ?? throw new InvalidDataException("Tiled JSON map requires a 'tilesets' array.");
        if (tilesetArray.Count == 0)
        {
            throw new NotSupportedException("Tiled JSON map must define at least one tileset.");
        }

        var tilesets = new List<TiledTilesetReference>(tilesetArray.Count);
        for (var index = 0; index < tilesetArray.Count; index++)
        {
            if (tilesetArray[index] is not JObject tilesetObject)
            {
                throw new InvalidDataException("Tiled JSON tileset entry must be an object.");
            }

            tilesets.Add(ReadTilesetReferenceJson(mapFilePath, tilesetObject, mapTileWidth, mapTileHeight));
        }

        return tilesets;
    }

    private static TiledTilesetReference ReadTilesetReferenceJson(string mapFilePath, JObject tilesetObject, int mapTileWidth, int mapTileHeight)
    {
        if (tilesetObject == null)
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
        var customPropertiesByTileId = ReadTileCustomPropertiesJson(tilesetRoot);
        var animationsByTileId = ReadTileAnimationsJson(tilesetRoot, warnings);

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
            customPropertiesByTileId,
            animationsByTileId,
            warnings);
    }

    private static Dictionary<string, string> ReadCustomProperties(XElement element)
    {
        var customProperties = new Dictionary<string, string>(StringComparer.Ordinal);
        var propertiesElement = element.Element("properties");
        if (propertiesElement == null)
        {
            return customProperties;
        }

        foreach (var propertyElement in propertiesElement.Elements("property"))
        {
            var name = propertyElement.Attribute("name")?.Value;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var value = propertyElement.Attribute("value")?.Value ?? propertyElement.Value ?? string.Empty;
            customProperties[name] = value;
        }

        return customProperties;
    }

    private static Dictionary<string, string> ReadCustomProperties(JObject element)
    {
        var customProperties = new Dictionary<string, string>(StringComparer.Ordinal);
        if (element["properties"] is not JArray properties)
        {
            return customProperties;
        }

        for (var index = 0; index < properties.Count; index++)
        {
            if (properties[index] is not JObject propertyObject)
            {
                continue;
            }

            var name = ReadOptionalString(propertyObject, "name", string.Empty);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            customProperties[name] = ReadPropertyValue(propertyObject["value"]);
        }

        return customProperties;
    }

    private static Dictionary<int, Dictionary<string, string>> ReadTileCustomProperties(XElement tilesetRoot)
    {
        var customPropertiesByTileId = new Dictionary<int, Dictionary<string, string>>();
        foreach (var tileElement in tilesetRoot.Elements("tile"))
        {
            var customProperties = ReadCustomProperties(tileElement);
            if (customProperties.Count > 0)
            {
                customPropertiesByTileId[ReadRequiredInt(tileElement, "id")] = customProperties;
            }
        }

        return customPropertiesByTileId;
    }

    private static Dictionary<int, Dictionary<string, string>> ReadTileCustomPropertiesJson(JObject tilesetRoot)
    {
        var customPropertiesByTileId = new Dictionary<int, Dictionary<string, string>>();
        if (tilesetRoot["tiles"] is not JArray tiles)
        {
            return customPropertiesByTileId;
        }

        for (var index = 0; index < tiles.Count; index++)
        {
            if (tiles[index] is not JObject tileObject)
            {
                continue;
            }

            var customProperties = ReadCustomProperties(tileObject);
            if (customProperties.Count > 0)
            {
                customPropertiesByTileId[ReadRequiredInt(tileObject, "id", "tile")] = customProperties;
            }
        }

        return customPropertiesByTileId;
    }

    private static Dictionary<int, List<TiledTileAnimationFrame>> ReadTileAnimations(XElement tilesetRoot, List<string> warnings)
    {
        var animationsByTileId = new Dictionary<int, List<TiledTileAnimationFrame>>();
        foreach (var tileElement in tilesetRoot.Elements("tile"))
        {
            var animationElement = tileElement.Element("animation");
            if (animationElement == null)
            {
                continue;
            }

            var tileId = ReadRequiredInt(tileElement, "id");
            var frames = new List<TiledTileAnimationFrame>();
            foreach (var frameElement in animationElement.Elements("frame"))
            {
                var frameTileId = ReadRequiredInt(frameElement, "tileid");
                var durationMilliseconds = ReadOptionalInt(frameElement, "duration", 0);
                if (durationMilliseconds <= 0)
                {
                    warnings.Add($"Tile {tileId} has an animation frame with invalid duration; the frame was ignored.");
                    continue;
                }

                frames.Add(new TiledTileAnimationFrame(frameTileId, durationMilliseconds));
            }

            if (frames.Count > 0)
            {
                animationsByTileId[tileId] = frames;
            }
        }

        return animationsByTileId;
    }

    private static Dictionary<int, List<TiledTileAnimationFrame>> ReadTileAnimationsJson(JObject tilesetRoot, List<string> warnings)
    {
        var animationsByTileId = new Dictionary<int, List<TiledTileAnimationFrame>>();
        if (tilesetRoot["tiles"] is not JArray tiles)
        {
            return animationsByTileId;
        }

        for (var tileIndex = 0; tileIndex < tiles.Count; tileIndex++)
        {
            if (tiles[tileIndex] is not JObject tileObject)
            {
                continue;
            }

            var animationToken = tileObject["animation"];
            if (animationToken == null)
            {
                continue;
            }

            JArray? frameArray = animationToken as JArray;
            if (frameArray == null && animationToken is JObject animationObject)
            {
                frameArray = animationObject["frames"] as JArray;
            }

            if (frameArray == null)
            {
                continue;
            }

            var tileId = ReadRequiredInt(tileObject, "id", "tile");
            var frames = new List<TiledTileAnimationFrame>();
            for (var frameIndex = 0; frameIndex < frameArray.Count; frameIndex++)
            {
                if (frameArray[frameIndex] is not JObject frameObject)
                {
                    continue;
                }

                var frameTileId = ReadRequiredInt(frameObject, "tileid", "animation frame");
                var durationMilliseconds = ReadOptionalInt(frameObject, "duration", 0);
                if (durationMilliseconds <= 0)
                {
                    warnings.Add($"Tile {tileId} has an animation frame with invalid duration; the frame was ignored.");
                    continue;
                }

                frames.Add(new TiledTileAnimationFrame(frameTileId, durationMilliseconds));
            }

            if (frames.Count > 0)
            {
                animationsByTileId[tileId] = frames;
            }
        }

        return animationsByTileId;
    }

    private static TiledObjectLayer ReadObjectLayer(XElement objectGroupElement, float zOffset)
    {
        var objectLayer = new TiledObjectLayer(ReadOptionalString(objectGroupElement, "name", "Objects"), zOffset, ReadCustomProperties(objectGroupElement));
        foreach (var objectElement in objectGroupElement.Elements("object"))
        {
            objectLayer.Objects.Add(new TiledObject(
                ReadOptionalInt(objectElement, "id", 0),
                ReadOptionalString(objectElement, "name", string.Empty),
                ReadOptionalString(objectElement, "type", string.Empty),
                ReadOptionalFloat(objectElement, "x", 0f),
                ReadOptionalFloat(objectElement, "y", 0f),
                ReadOptionalFloat(objectElement, "width", 0f),
                ReadOptionalFloat(objectElement, "height", 0f),
                ReadCustomProperties(objectElement)));
        }

        return objectLayer;
    }

    private static TiledObjectLayer ReadObjectLayerJson(JObject layerObject, float zOffset)
    {
        var objectLayer = new TiledObjectLayer(ReadOptionalString(layerObject, "name", "Objects"), zOffset, ReadCustomProperties(layerObject));
        if (layerObject["objects"] is not JArray objects)
        {
            return objectLayer;
        }

        for (var index = 0; index < objects.Count; index++)
        {
            if (objects[index] is not JObject objectObject)
            {
                continue;
            }

            objectLayer.Objects.Add(new TiledObject(
                ReadOptionalInt(objectObject, "id", 0),
                ReadOptionalString(objectObject, "name", string.Empty),
                ReadOptionalString(objectObject, "type", string.Empty),
                ReadOptionalFloat(objectObject, "x", 0f),
                ReadOptionalFloat(objectObject, "y", 0f),
                ReadOptionalFloat(objectObject, "width", 0f),
                ReadOptionalFloat(objectObject, "height", 0f),
                ReadCustomProperties(objectObject)));
        }

        return objectLayer;
    }

    private static string ReadPropertyValue(JToken? valueToken)
    {
        if (valueToken == null || valueToken.Type == JTokenType.Null)
        {
            return string.Empty;
        }

        if (valueToken.Type == JTokenType.String)
        {
            return valueToken.Value<string>() ?? string.Empty;
        }

        if (valueToken is JValue { Value: IFormattable formattable })
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        }

        return valueToken.ToString();
    }

    private static void CopyCustomProperties(Dictionary<string, string> source, Dictionary<string, string> destination)
    {
        foreach (var customProperty in source)
        {
            destination[customProperty.Key] = customProperty.Value;
        }
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
        IReadOnlyList<TiledTilesetReference> tilesetReferences,
        List<string> warnings,
        out List<int> tileSources,
        out List<TileCellFlags> tileFlags)
    {
        var compression = dataElement.Attribute("compression")?.Value;
        if (!string.IsNullOrWhiteSpace(compression))
        {
            throw new NotSupportedException($"Compressed Tiled layer data '{compression}' is not supported.");
        }

        var encoding = dataElement.Attribute("encoding")?.Value;
        var tiles = new List<int>(expectedTileCount);
        tileSources = new List<int>(expectedTileCount);
        tileFlags = new List<TileCellFlags>(expectedTileCount);
        var flipWarningAdded = false;

        if (string.Equals(encoding, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var values = (dataElement.Value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var value in values)
            {
                var tileReference = ConvertGid(ParseGid(value), tilesetReferences, warnings, ref flipWarningAdded, out var flags);
                tiles.Add(tileReference.TileId);
                tileSources.Add(tileReference.TileSetIndex);
                tileFlags.Add(flags);
            }
        }
        else if (string.IsNullOrWhiteSpace(encoding))
        {
            foreach (var tileElement in dataElement.Elements("tile"))
            {
                var tileReference = ConvertGid(ParseGid(ReadRequiredString(tileElement, "gid")), tilesetReferences, warnings, ref flipWarningAdded, out var flags);
                tiles.Add(tileReference.TileId);
                tileSources.Add(tileReference.TileSetIndex);
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
        IReadOnlyList<TiledTilesetReference> tilesetReferences,
        List<string> warnings,
        out List<int> tileSources,
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
        tileSources = new List<int>(expectedTileCount);
        tileFlags = new List<TileCellFlags>(expectedTileCount);
        var flipWarningAdded = false;

        for (var index = 0; index < data.Count; index++)
        {
            var gidValue = data[index]!.Value<long>();
            if (gidValue < 0 || gidValue > uint.MaxValue)
            {
                throw new InvalidDataException($"Invalid Tiled gid '{gidValue}'.");
            }

            var tileReference = ConvertGid((uint)gidValue, tilesetReferences, warnings, ref flipWarningAdded, out var flags);
            tiles.Add(tileReference.TileId);
            tileSources.Add(tileReference.TileSetIndex);
            tileFlags.Add(flags);
        }

        if (tiles.Count != expectedTileCount)
        {
            throw new InvalidDataException($"Tiled layer has {tiles.Count} cells but expected {expectedTileCount}.");
        }

        return tiles;
    }

    private static TileMapTileReference ConvertGid(uint rawGid, IReadOnlyList<TiledTilesetReference> tilesetReferences, List<string> warnings, ref bool flipWarningAdded, out TileCellFlags flags)
    {
        var cleanedGid = rawGid & TileIdMask;
        flags = GetTileCellFlags(rawGid);
        if (cleanedGid == 0)
        {
            flags = TileCellFlags.None;
            return TileMapTileReference.Empty;
        }

        if (cleanedGid != rawGid && !flipWarningAdded)
        {
            warnings.Add("Tiled flip/rotation flags were imported; horizontal and vertical static-tile flips are rendered, diagonal rotation remains limited.");
            flipWarningAdded = true;
        }

        for (var tilesetIndex = 0; tilesetIndex < tilesetReferences.Count; tilesetIndex++)
        {
            var tilesetReference = tilesetReferences[tilesetIndex];
            var firstGid = (uint)tilesetReference.FirstGid;
            var maxExclusiveGid = firstGid + (uint)tilesetReference.TileCount;
            if (cleanedGid >= firstGid && cleanedGid < maxExclusiveGid)
            {
                return new TileMapTileReference(tilesetIndex, (int)(cleanedGid - firstGid));
            }
        }

        throw new InvalidDataException($"Tiled gid {cleanedGid} does not match any imported tileset range.");
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
        IReadOnlyList<TiledTilesetReference> tilesets)
    {
        if (tilesets.Count == 0)
        {
            throw new ArgumentException("Tiled map import document requires at least one tileset.", nameof(tilesets));
        }

        SourceFilePath = sourceFilePath;
        Width = width;
        Height = height;
        TileWidth = tileWidth;
        TileHeight = tileHeight;
        Tilesets = new List<TiledTilesetReference>(tilesets);
    }

    public string SourceFilePath { get; }
    public int Width { get; }
    public int Height { get; }
    public int TileWidth { get; }
    public int TileHeight { get; }
    public List<TiledTilesetReference> Tilesets { get; }
    public TiledTilesetReference Tileset => Tilesets[0];
    public List<TiledTileLayer> Layers { get; } = new();
    public List<TiledObjectLayer> ObjectLayers { get; } = new();
    public List<string> Warnings { get; } = new();
    public Dictionary<string, string> CustomProperties { get; } = new(StringComparer.Ordinal);
}

public sealed class TiledObjectLayer
{
    public TiledObjectLayer(string name, float zOffset, Dictionary<string, string> customProperties)
    {
        Name = name;
        ZOffset = zOffset;
        CustomProperties = customProperties;
    }

    public string Name { get; }
    public float ZOffset { get; }
    public List<TiledObject> Objects { get; } = new();
    public Dictionary<string, string> CustomProperties { get; }
}

public sealed class TiledObject
{
    public TiledObject(
        int id,
        string name,
        string type,
        float x,
        float y,
        float width,
        float height,
        Dictionary<string, string> customProperties)
    {
        Id = id;
        Name = name;
        Type = type;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        CustomProperties = customProperties;
    }

    public int Id { get; }
    public string Name { get; }
    public string Type { get; }
    public float X { get; }
    public float Y { get; }
    public float Width { get; }
    public float Height { get; }
    public Dictionary<string, string> CustomProperties { get; }
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
        Dictionary<int, Dictionary<string, string>> customPropertiesByTileId,
        Dictionary<int, List<TiledTileAnimationFrame>> animationsByTileId,
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
        CustomPropertiesByTileId = customPropertiesByTileId;
        AnimationsByTileId = animationsByTileId;
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
    public Dictionary<int, Dictionary<string, string>> CustomPropertiesByTileId { get; }
    public Dictionary<int, List<TiledTileAnimationFrame>> AnimationsByTileId { get; }
    public List<string> Warnings { get; }
}

public sealed class TiledTileAnimationFrame
{
    public TiledTileAnimationFrame(int tileId, int durationMilliseconds)
    {
        TileId = tileId;
        DurationMilliseconds = durationMilliseconds;
    }

    public int TileId { get; }
    public int DurationMilliseconds { get; }
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
    public TiledTileLayer(string name, float zOffset, List<int> tiles, List<int> tileSourceIndices, List<TileCellFlags> tileFlags, Dictionary<string, string> customProperties)
    {
        Name = name;
        ZOffset = zOffset;
        Tiles = tiles;
        TileSourceIndices = tileSourceIndices;
        TileFlags = tileFlags;
        CustomProperties = customProperties;
    }

    public string Name { get; }
    public float ZOffset { get; }
    public List<int> Tiles { get; }
    public List<int> TileSourceIndices { get; }
    public List<TileCellFlags> TileFlags { get; }
    public Dictionary<string, string> CustomProperties { get; }
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