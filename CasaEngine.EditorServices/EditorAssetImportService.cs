using CasaEngine.Core.Logging;
using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Engine.Geometry;
using CasaEngine.EditorServices.Import;
using CasaEngine.EditorServices.Tiled;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using System.Text;
using ColorSpace = CasaEngine.Framework.Rendering.ColorSpace;
using Size = CasaEngine.Core.Math.Size;

namespace CasaEngine.EditorServices;

public static class EditorAssetImportService
{
    public static TiledMapImportResult? LastTiledMapImportResult { get; private set; }

    public static bool ImportFile(
        string sourceFilePath,
        string destinationFilePath,
        ILegacyMaterialImportProfile? legacyMaterialImportProfile = null)
        => ImportFile(sourceFilePath, destinationFilePath, legacyMaterialImportProfile, new EditorAssetImportOptions());

    /// <summary>
    /// Imports a file like <see cref="ImportFile(string, string, ILegacyMaterialImportProfile?)"/>, with import options.
    /// The material template of the options only applies to the materials of static models (ADR-0109).
    /// </summary>
    public static bool ImportFile(
        string sourceFilePath,
        string destinationFilePath,
        ILegacyMaterialImportProfile? legacyMaterialImportProfile,
        EditorAssetImportOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFilePath);
        ArgumentNullException.ThrowIfNull(options);

        bool catalogChanged = EnsureFileAssetRegistered(destinationFilePath);
        LastTiledMapImportResult = null;

        if (TiledMapImporter.IsMapFileSupported(sourceFilePath))
        {
            LastTiledMapImportResult = ImportTiledMap(sourceFilePath, destinationFilePath);
            return true;
        }

        if (TryImportSeparatedAnimationAssets(sourceFilePath, destinationFilePath))
        {
            return true;
        }

        var staticModelReader = new GltfStaticModelReader();
        if (!staticModelReader.IsFileSupported(sourceFilePath) && !AssimpToGltfConverter.RequiresConversion(sourceFilePath))
        {
            return catalogChanged;
        }

        var result = ReadStaticModelWithConversion(staticModelReader, sourceFilePath, legacyMaterialImportProfile);
        var model = result.Model;
        if (model.RootNode == null && model.Meshes.Count == 0)
        {
            return catalogChanged;
        }

        var importedTextureAssets = ImportTextureAssets(result.Materials, destinationFilePath, options.MaterialTemplate);
        ApplyDiffuseTextureSlots(model, importedTextureAssets.DiffuseTextureAssetIdsByMaterialIndex);
        ApplyMaterialImports(model, result.Materials, importedTextureAssets, destinationFilePath, options.MaterialTemplate);

        string staticModelFullPath = Path.ChangeExtension(destinationFilePath, Constants.FileNameExtensions.StaticModel);
        string staticModelRelativePath = GetRelativeProjectPath(staticModelFullPath);
        string staticModelName = Path.GetFileNameWithoutExtension(staticModelFullPath);

        var modelAssetInfo = EnsureAssetInfo(staticModelRelativePath, staticModelName);
        model.Name = modelAssetInfo.Name;
        model.FileName = staticModelRelativePath;

        var modelDocument = SerializeAsset(model, modelAssetInfo.Id, modelAssetInfo.Name);
        Directory.CreateDirectory(Path.GetDirectoryName(staticModelFullPath)!);
        EditorAssetWriterService.SaveDocument(staticModelRelativePath, modelDocument);
        return true;
    }

    public static TiledMapImportResult ImportTiledMap(string sourceFilePath, string destinationFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFilePath);

        EnsureFileAssetRegistered(destinationFilePath);

        var tiledMap = new TiledMapImporter().Import(sourceFilePath);
        return ImportTiledMapDocument(tiledMap, destinationFilePath, null);
    }

    /// <summary>
    /// Imports a Tiled JSON map (.tmj) from its text, without reading any file of the source. Produces the same assets as
    /// <see cref="ImportTiledMap"/> given the same map, tilesets and images.
    /// The bytes returned by <paramref name="readImage"/> are always written to the destination texture file, overwriting an existing
    /// file, even when the logical image path is that destination path (the path entry skips the copy in that case).
    /// The resolver must return a non-null <see cref="TiledTilesetReference"/> whose tile size and FirstGid match the request: a null
    /// return throws <see cref="InvalidOperationException"/> ("The resolver returned no tileset for '&lt;source&gt;'."), a FirstGid
    /// mismatch throws <see cref="InvalidOperationException"/>, a tile size mismatch <see cref="NotSupportedException"/>. An exception
    /// thrown by the resolver propagates unchanged (a caller that wants the path entry's <see cref="FileNotFoundException"/> throws it itself).
    /// </summary>
    /// <param name="mapJsonText">The text of the .tmj.</param>
    /// <param name="mapFilePath">The logical path of the map, used to resolve the relative tileset and image paths.</param>
    /// <param name="resolveExternalTileset">Returns the import model of an external tileset (see <see cref="TiledMapImporter.ReadTilesetJson"/>).</param>
    /// <param name="readImage">Receives <see cref="TiledTilesetReference.ImageFilePath"/> and returns the image bytes, or null when the image does not exist.</param>
    /// <param name="destinationFilePath">The destination .tmj path in the project; the imported assets are written next to it.</param>
    public static TiledMapImportResult ImportTiledMapContent(
        string mapJsonText,
        string mapFilePath,
        Func<TiledExternalTilesetRequest, TiledTilesetReference> resolveExternalTileset,
        Func<string, byte[]?> readImage,
        string destinationFilePath)
    {
        ArgumentNullException.ThrowIfNull(mapJsonText);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapFilePath);
        ArgumentNullException.ThrowIfNull(resolveExternalTileset);
        ArgumentNullException.ThrowIfNull(readImage);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFilePath);

        EnsureFileAssetRegistered(destinationFilePath);

        var tiledMap = TiledMapImporter.ImportJson(mapJsonText, mapFilePath, resolveExternalTileset);
        return ImportTiledMapDocument(tiledMap, destinationFilePath, readImage);
    }

    // readImage null: the images are files, read and copied from disc. Otherwise the bytes come from readImage.
    private static TiledMapImportResult ImportTiledMapDocument(
        TiledMapImportDocument tiledMap,
        string destinationFilePath,
        Func<string, byte[]?>? readImage)
    {
        var mapBaseName = Path.GetFileNameWithoutExtension(destinationFilePath);
        var importedAssetsDirectory = Path.GetDirectoryName(destinationFilePath)!;
        Directory.CreateDirectory(importedAssetsDirectory);

        var importedTexturesBySourcePath = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var usedTextureFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedTileSetFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tileSetAssetIds = new List<Guid>(tiledMap.Tilesets.Count);
        var createdAssetFileNames = new List<string>(tiledMap.Tilesets.Count + 4);

        for (var tilesetIndex = 0; tilesetIndex < tiledMap.Tilesets.Count; tilesetIndex++)
        {
            var tiledTileset = tiledMap.Tilesets[tilesetIndex];
            var textureAssetId = ImportTexture(
                tiledTileset.ImageFilePath,
                importedAssetsDirectory,
                mapBaseName,
                importedTexturesBySourcePath,
                usedTextureFileNames,
                true,
                readImage);

            if (textureAssetId == Guid.Empty)
            {
                throw new FileNotFoundException("Unable to import Tiled tileset image.", tiledTileset.ImageFilePath);
            }

            var tileSetFileName = CreateTileSetFileName(mapBaseName, tiledTileset.Name, tiledMap.Tilesets.Count == 1, usedTileSetFileNames);
            var tileSetFullPath = Path.Combine(importedAssetsDirectory, tileSetFileName);
            var tileSetRelativePath = GetRelativeProjectPath(tileSetFullPath);
            var tileSetAssetInfo = EnsureAssetInfo(tileSetRelativePath, CreateTileSetAssetName(mapBaseName, tiledTileset.Name, tiledMap.Tilesets.Count == 1, tilesetIndex));
            var tileSetData = CreateTileSetData(tiledTileset, tileSetAssetInfo, textureAssetId);
            var tileSetDocument = SerializeAsset(tileSetData, tileSetAssetInfo.Id, tileSetAssetInfo.Name);
            EditorAssetWriterService.SaveDocument(tileSetRelativePath, tileSetDocument);
            tileSetAssetIds.Add(tileSetAssetInfo.Id);
            createdAssetFileNames.Add(tileSetRelativePath);
        }

        var tileMapFullPath = Path.ChangeExtension(destinationFilePath, Constants.FileNameExtensions.TileMap);
        var tileMapRelativePath = GetRelativeProjectPath(tileMapFullPath);
        var tileMapAssetInfo = EnsureAssetInfo(tileMapRelativePath, mapBaseName);
        var tileMapData = CreateTileMapData(tiledMap, tileMapAssetInfo, tileSetAssetIds);
        var tileMapDocument = SerializeAsset(tileMapData, tileMapAssetInfo.Id, tileMapAssetInfo.Name);
        EditorAssetWriterService.SaveDocument(tileMapRelativePath, tileMapDocument);

        EditorAssetCatalogService.Save();

        createdAssetFileNames.Insert(0, tileMapRelativePath);

        foreach (var importedTextureAssetId in importedTexturesBySourcePath.Values)
        {
            var textureAssetInfo = AssetCatalog.Get(importedTextureAssetId);
            if (textureAssetInfo != null)
            {
                createdAssetFileNames.Add(textureAssetInfo.FileName);
            }
        }

        return new TiledMapImportResult(createdAssetFileNames, tiledMap.Warnings);
    }

    private static TileSetData CreateTileSetData(TiledTilesetReference tiledTileset, AssetInfo tileSetAssetInfo, Guid textureAssetId)
    {
        var tileSetData = new TileSetData
        {
            Name = tileSetAssetInfo.Name,
            FileName = tileSetAssetInfo.FileName,
            SpriteSheetAssetId = textureAssetId,
            TileSize = new Size(tiledTileset.TileWidth, tiledTileset.TileHeight),
        };

        for (var tileIndex = 0; tileIndex < tiledTileset.TileCount; tileIndex++)
        {
            var column = tileIndex % tiledTileset.Columns;
            var row = tileIndex / tiledTileset.Columns;
            TileData tileData;
            if (tiledTileset.AnimationsByTileId.TryGetValue(tileIndex, out var animationFrames))
            {
                var animatedTileData = new AnimatedTileData();
                for (var frameIndex = 0; frameIndex < animationFrames.Count; frameIndex++)
                {
                    var animationFrame = animationFrames[frameIndex];
                    animatedTileData.Frames.Add(new AnimatedTileFrameData
                    {
                        TileId = animationFrame.TileId,
                        DurationMilliseconds = animationFrame.DurationMilliseconds,
                    });
                }

                tileData = animatedTileData;
            }
            else
            {
                tileData = new StaticTileData
                {
                    Location = new Rectangle(
                        column * tiledTileset.TileWidth,
                        row * tiledTileset.TileHeight,
                        tiledTileset.TileWidth,
                        tiledTileset.TileHeight),
                };
            }

            tileData.Id = tileIndex;
            tileData.CollisionType = TileCollisionType.None;
            tileData.IsBreakable = false;

            if (tileData is AnimatedTileData animatedTileDataWithLocation)
            {
                animatedTileDataWithLocation.Location = new Rectangle(
                    column * tiledTileset.TileWidth,
                    row * tiledTileset.TileHeight,
                    tiledTileset.TileWidth,
                    tiledTileset.TileHeight);
            }

            if (tiledTileset.CollisionByTileId.TryGetValue(tileIndex, out var collision))
            {
                tileData.CollisionType = TileCollisionType.Blocked;
                tileData.CollisionShape = new Collision2d
                {
                    //The tile runtime picks its own profile from the tile collision type.
                    ProfileName = string.Empty,
                    LocalPosition = new Vector2(collision.X, collision.Y),
                    Shape = new ShapeRectangle(collision.Width, collision.Height),
                };
            }

            if (tiledTileset.CustomPropertiesByTileId.TryGetValue(tileIndex, out var customProperties))
            {
                CopyCustomProperties(customProperties, tileData.CustomProperties);
            }

            tileSetData.AddTile(tileData);
        }

        return tileSetData;
    }

    private static TileMapData CreateTileMapData(TiledMapImportDocument tiledMap, AssetInfo tileMapAssetInfo, IReadOnlyList<Guid> tileSetAssetIds)
    {
        var tileMapData = new TileMapData
        {
            Name = tileMapAssetInfo.Name,
            FileName = tileMapAssetInfo.FileName,
            MapSize = new Size(tiledMap.Width, tiledMap.Height),
        };
        for (var tileSetIndex = 0; tileSetIndex < tileSetAssetIds.Count; tileSetIndex++)
        {
            tileMapData.TileSetDataAssetIds.Add(tileSetAssetIds[tileSetIndex]);
        }

        CopyCustomProperties(tiledMap.CustomProperties, tileMapData.CustomProperties);

        foreach (var tiledLayer in tiledMap.Layers)
        {
            var layerData = new TileMapLayerData
            {
                Name = tiledLayer.Name,
                zOffset = tiledLayer.ZOffset,
                tiles = new List<int>(tiledLayer.Tiles),
                tileSources = new List<int>(tiledLayer.TileSourceIndices),
                tileFlags = new List<TileCellFlags>(tiledLayer.TileFlags),
            };
            CopyCustomProperties(tiledLayer.CustomProperties, layerData.CustomProperties);
            tileMapData.Layers.Add(layerData);
        }

        foreach (var tiledObjectLayer in tiledMap.ObjectLayers)
        {
            var objectLayerData = new TileMapObjectLayerData
            {
                Name = tiledObjectLayer.Name,
                zOffset = tiledObjectLayer.ZOffset,
            };
            CopyCustomProperties(tiledObjectLayer.CustomProperties, objectLayerData.CustomProperties);

            for (var objectIndex = 0; objectIndex < tiledObjectLayer.Objects.Count; objectIndex++)
            {
                var tiledObject = tiledObjectLayer.Objects[objectIndex];
                var objectData = new TileMapObjectData
                {
                    Id = tiledObject.Id,
                    Name = string.IsNullOrWhiteSpace(tiledObject.Name) ? null : tiledObject.Name,
                    Type = string.IsNullOrWhiteSpace(tiledObject.Type) ? null : tiledObject.Type,
                    X = tiledObject.X,
                    Y = tiledObject.Y,
                    Width = tiledObject.Width,
                    Height = tiledObject.Height,
                };
                CopyCustomProperties(tiledObject.CustomProperties, objectData.CustomProperties);
                objectLayerData.Objects.Add(objectData);
            }

            tileMapData.ObjectLayers.Add(objectLayerData);
        }

        tileMapData.Validate();
        return tileMapData;
    }

    private static void CopyCustomProperties(Dictionary<string, string> source, Dictionary<string, string> destination)
    {
        foreach (var customProperty in source)
        {
            destination[customProperty.Key] = customProperty.Value;
        }
    }

    private static string CreateTileSetFileName(string mapBaseName, string tiledTilesetName, bool isSingleTileset, HashSet<string> usedTileSetFileNames)
    {
        if (isSingleTileset)
        {
            return CreateUniqueImportedFileName(mapBaseName + Constants.FileNameExtensions.TileSet, usedTileSetFileNames);
        }

        return CreateUniqueImportedFileName(SanitizeFileName(tiledTilesetName) + Constants.FileNameExtensions.TileSet, usedTileSetFileNames);
    }

    private static string CreateTileSetAssetName(string mapBaseName, string tiledTilesetName, bool isSingleTileset, int tilesetIndex)
    {
        if (isSingleTileset)
        {
            return mapBaseName + "_TileSet";
        }

        var sanitizedTilesetName = SanitizeFileName(tiledTilesetName);
        if (string.IsNullOrWhiteSpace(sanitizedTilesetName) || string.Equals(sanitizedTilesetName, "Material", StringComparison.Ordinal))
        {
            sanitizedTilesetName = $"Tileset_{tilesetIndex + 1}";
        }

        return $"{mapBaseName}_{sanitizedTilesetName}";
    }

    private static bool TryImportSeparatedAnimationAssets(string sourceFilePath, string destinationFilePath)
    {
        var riggedModelReader = new GltfRiggedModelReader(null, null);
        bool requiresConversion = !riggedModelReader.IsFileSupported(sourceFilePath);
        if (requiresConversion && !AssimpToGltfConverter.RequiresConversion(sourceFilePath))
        {
            return false;
        }

        string? temporaryGlbPath = requiresConversion ? CreateTemporaryGlbPath() : null;
        try
        {
            RiggedModel riggedModel;

            try
            {
                if (temporaryGlbPath == null)
                {
                    riggedModel = riggedModelReader.LoadAsset(sourceFilePath);
                }
                else
                {
                    AssimpToGltfConverter.Convert(sourceFilePath, temporaryGlbPath);
                    riggedModel = riggedModelReader.LoadAsset(temporaryGlbPath);
                }
            }
            catch (Exception exception)
            {
                // Not every model can be read as a rig: the caller then imports it as a static model.
                Logs.WriteWarning($"Model '{sourceFilePath}' could not be read as a rigged model, importing it as a static model: {exception.Message}");
                return false;
            }

            if (riggedModel == null)
            {
                return false;
            }

            bool hasRealBones = riggedModel.NumberOfBonesInUse > 1;
            bool hasAnimations = riggedModel.AnimationClips.Count > 0 || riggedModel.OriginalAnimations.Count > 0;
            if (!hasRealBones && !hasAnimations)
            {
                return false;
            }

            ImportSeparatedAnimationAssets(destinationFilePath, riggedModel, temporaryGlbPath);
            return true;
        }
        finally
        {
            if (temporaryGlbPath != null)
            {
                TryDeleteFile(temporaryGlbPath);
            }
        }
    }

    /// <summary>
    /// Reads a static model, converting non-glTF sources to a temporary <c>.glb</c> first.
    /// </summary>
    private static StaticModelImportResult ReadStaticModelWithConversion(
        GltfStaticModelReader reader,
        string sourceFilePath,
        ILegacyMaterialImportProfile? legacyMaterialImportProfile)
    {
        if (reader.IsFileSupported(sourceFilePath))
        {
            return reader.ReadWithMetadata(sourceFilePath, legacyMaterialImportProfile);
        }

        string temporaryGlbPath = CreateTemporaryGlbPath();
        try
        {
            AssimpToGltfConverter.Convert(sourceFilePath, temporaryGlbPath);
            return reader.ReadWithMetadata(temporaryGlbPath, legacyMaterialImportProfile);
        }
        finally
        {
            TryDeleteFile(temporaryGlbPath);
        }
    }

    private static string CreateTemporaryGlbPath()
    {
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), "CasaEngineImport");
        Directory.CreateDirectory(temporaryDirectory);
        return Path.Combine(temporaryDirectory, Guid.NewGuid().ToString("N") + ".glb");
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Best-effort cleanup of the temporary conversion artifact.
        }
    }

    /// <summary>
    /// Writes the skeleton, animation clip and skinned mesh assets of <paramref name="riggedModel"/> next to
    /// <paramref name="destinationFilePath"/>. The runtime reads the geometry as glTF only: when the source was
    /// converted, <paramref name="convertedGlbPath"/> is kept next to the source as <c>&lt;name&gt;.glb</c>, registered
    /// in the catalog, and becomes the geometry of the skinned mesh; otherwise the source itself is the geometry.
    /// </summary>
    private static void ImportSeparatedAnimationAssets(string destinationFilePath, RiggedModel riggedModel, string? convertedGlbPath)
    {
        ArgumentNullException.ThrowIfNull(riggedModel);

        string modelBaseName = Path.GetFileNameWithoutExtension(destinationFilePath);
        string geometryFullPath = destinationFilePath;
        if (convertedGlbPath != null)
        {
            geometryFullPath = Path.ChangeExtension(destinationFilePath, ".glb");
            Directory.CreateDirectory(Path.GetDirectoryName(geometryFullPath)!);
            File.Copy(convertedGlbPath, geometryFullPath, overwrite: true);
            EnsureFileAssetRegistered(geometryFullPath);
        }

        string geometryRelativePath = GetRelativeProjectPath(geometryFullPath);
        var geometryAssetInfo = AssetCatalog.GetByFileName(geometryRelativePath)
            ?? EnsureAssetInfo(geometryRelativePath, modelBaseName);

        Guid skeletonAssetId = Guid.Empty;
        string animationDirectory = Path.GetDirectoryName(destinationFilePath)!;
        Directory.CreateDirectory(animationDirectory);
        var usedAnimationFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (riggedModel.SkeletonDefinition != null)
        {
            string skeletonFileName = CreateUniqueImportedFileName(
                modelBaseName + Constants.FileNameExtensions.Skeleton,
                animationDirectory,
                usedAnimationFileNames);
            string skeletonFullPath = Path.Combine(animationDirectory, skeletonFileName);
            string skeletonRelativePath = GetRelativeProjectPath(skeletonFullPath);
            string skeletonAssetName = Path.GetFileNameWithoutExtension(skeletonFileName);
            var skeletonAssetInfo = EnsureAssetInfo(skeletonRelativePath, skeletonAssetName);
            var skeletonAsset = AnimationAssetDataConverter.CreateSkeletonAsset(riggedModel.SkeletonDefinition);
            skeletonAsset.Name = skeletonAssetInfo.Name;
            skeletonAsset.FileName = skeletonRelativePath;

            var skeletonDocument = SerializeAsset(skeletonAsset, skeletonAssetInfo.Id, skeletonAssetInfo.Name);
            EditorAssetWriterService.SaveDocument(skeletonRelativePath, skeletonDocument);
            skeletonAssetId = skeletonAssetInfo.Id;
        }

        var clipAssetIds = new List<Guid>(riggedModel.AnimationClips.Count);
        for (var clipIndex = 0; clipIndex < riggedModel.AnimationClips.Count; clipIndex++)
        {
            var animationClip = riggedModel.AnimationClips[clipIndex];
            string clipBaseName = string.IsNullOrWhiteSpace(animationClip.Name)
                ? $"{modelBaseName}_{clipIndex + 1}"
                : $"{modelBaseName}_{SanitizeFileName(animationClip.Name)}";
            string clipFileName = CreateUniqueImportedFileName(
                clipBaseName + Constants.FileNameExtensions.SkeletonAnimation,
                animationDirectory,
                usedAnimationFileNames);
            string clipFullPath = Path.Combine(animationDirectory, clipFileName);
            string clipRelativePath = GetRelativeProjectPath(clipFullPath);
            string clipAssetName = Path.GetFileNameWithoutExtension(clipFileName);
            var clipAssetInfo = EnsureAssetInfo(clipRelativePath, clipAssetName);
            var compressedClip = AnimationClipCompressor.Compress(animationClip, AnimationClipCompressionSettings.Default);
            var clipAsset = AnimationAssetDataConverter.CreateAnimationClipAsset(compressedClip, skeletonAssetId);
            clipAsset.Name = clipAssetInfo.Name;
            clipAsset.FileName = clipRelativePath;

            var clipDocument = SerializeAsset(clipAsset, clipAssetInfo.Id, clipAssetInfo.Name);
            EditorAssetWriterService.SaveDocument(clipRelativePath, clipDocument);
            clipAssetIds.Add(clipAssetInfo.Id);
        }

        string modelFullPath = Path.ChangeExtension(destinationFilePath, Constants.FileNameExtensions.Model);
        string modelRelativePath = GetRelativeProjectPath(modelFullPath);
        string modelAssetName = Path.GetFileNameWithoutExtension(modelFullPath);
        var modelAssetInfo = EnsureAssetInfo(modelRelativePath, modelAssetName);
        var skinnedMeshAsset = new SkinnedMeshAsset
        {
            Name = modelAssetInfo.Name,
            FileName = modelRelativePath,
            SkeletonAssetId = skeletonAssetId,
            GeometryAssetId = geometryAssetInfo.Id,
            DefaultAnimationClipAssetId = clipAssetIds.Count > 0 ? clipAssetIds[0] : Guid.Empty,
        };

        for (var clipIndex = 0; clipIndex < clipAssetIds.Count; clipIndex++)
        {
            skinnedMeshAsset.AnimationClipAssetIds.Add(clipAssetIds[clipIndex]);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(modelFullPath)!);
        var skinnedMeshDocument = SerializeAsset(skinnedMeshAsset, modelAssetInfo.Id, modelAssetInfo.Name);
        EditorAssetWriterService.SaveDocument(modelRelativePath, skinnedMeshDocument);
    }

    public static bool EnsureFileAssetRegistered(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string relativeFilePath = GetRelativeProjectPath(filePath);
        if (AssetCatalog.GetByFileName(relativeFilePath) != null)
        {
            return false;
        }

        EnsureAssetInfo(relativeFilePath, Path.GetFileName(filePath));
        return true;
    }

    private static ImportedTextureAssets ImportTextureAssets(
        IReadOnlyList<StaticModelImportedMaterial> materials,
        string destinationSourceFilePath,
        ImportedMaterialTemplate materialTemplate)
    {
        string modelBaseName = Path.GetFileNameWithoutExtension(destinationSourceFilePath);
        string texturesDirectory = Path.GetDirectoryName(destinationSourceFilePath)!;
        Directory.CreateDirectory(texturesDirectory);

        var importedTexturesBySourcePath = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var importedCubeTexturesBySourcePath = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var usedTextureFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var diffuseTextureAssetIdsByMaterialIndex = new Dictionary<int, Guid>();
        var normalTextureAssetIdsByMaterialIndex = new Dictionary<int, Guid>();
        var reflectionTextureAssetIdsByMaterialIndex = new Dictionary<int, Guid>();
        var metallicRoughnessTextureAssetIdsByMaterialIndex = new Dictionary<int, Guid>();
        var occlusionTextureAssetIdsByMaterialIndex = new Dictionary<int, Guid>();
        var emissiveTextureAssetIdsByMaterialIndex = new Dictionary<int, Guid>();

        void ImportPbrTexture(string? sourceTexturePath, int materialIndex, Dictionary<int, Guid> textureAssetIdsByMaterialIndex)
        {
            if (string.IsNullOrWhiteSpace(sourceTexturePath))
            {
                return;
            }

            Guid textureAssetId = ImportTexture(
                sourceTexturePath,
                texturesDirectory,
                modelBaseName,
                importedTexturesBySourcePath,
                usedTextureFileNames,
                true);

            if (textureAssetId != Guid.Empty)
            {
                textureAssetIdsByMaterialIndex[materialIndex] = textureAssetId;
            }
        }

        foreach (var material in materials)
        {
            if (!string.IsNullOrWhiteSpace(material.DiffuseTextureFilePath))
            {
                Guid textureAssetId = ImportTexture(
                    material.DiffuseTextureFilePath,
                    texturesDirectory,
                    modelBaseName,
                    importedTexturesBySourcePath,
                    usedTextureFileNames,
                    true);

                if (textureAssetId != Guid.Empty)
                {
                    diffuseTextureAssetIdsByMaterialIndex[material.MaterialIndex] = textureAssetId;
                }
            }

            if (!string.IsNullOrWhiteSpace(material.NormalTextureFilePath))
            {
                Guid textureAssetId = ImportTexture(
                    material.NormalTextureFilePath,
                    texturesDirectory,
                    modelBaseName,
                    importedTexturesBySourcePath,
                    usedTextureFileNames,
                    true);

                if (textureAssetId != Guid.Empty)
                {
                    normalTextureAssetIdsByMaterialIndex[material.MaterialIndex] = textureAssetId;
                }
            }

            if (materialTemplate == ImportedMaterialTemplate.LitPbr)
            {
                ImportPbrTexture(material.MetallicRoughnessTextureFilePath, material.MaterialIndex, metallicRoughnessTextureAssetIdsByMaterialIndex);
                ImportPbrTexture(material.OcclusionTextureFilePath, material.MaterialIndex, occlusionTextureAssetIdsByMaterialIndex);
                ImportPbrTexture(material.EmissiveTextureFilePath, material.MaterialIndex, emissiveTextureAssetIdsByMaterialIndex);
            }

            if (material.UsesReflection
                && !string.IsNullOrWhiteSpace(material.ReflectionTextureFilePath))
            {
                Guid textureAssetId = ImportTextureCube(
                    material.ReflectionTextureFilePath,
                    texturesDirectory,
                    modelBaseName,
                    importedCubeTexturesBySourcePath,
                    usedTextureFileNames,
                    true);

                if (textureAssetId != Guid.Empty)
                {
                    reflectionTextureAssetIdsByMaterialIndex[material.MaterialIndex] = textureAssetId;
                }
            }
        }

        return new ImportedTextureAssets(
            diffuseTextureAssetIdsByMaterialIndex,
            normalTextureAssetIdsByMaterialIndex,
            reflectionTextureAssetIdsByMaterialIndex,
            metallicRoughnessTextureAssetIdsByMaterialIndex,
            occlusionTextureAssetIdsByMaterialIndex,
            emissiveTextureAssetIdsByMaterialIndex);
    }

    private static void ApplyDiffuseTextureSlots(StaticModel model, Dictionary<int, Guid> diffuseTextureAssetIdsByMaterialIndex)
    {
        foreach (var mesh in model.Meshes)
        {
            if (mesh.MaterialIndex >= 0
                && diffuseTextureAssetIdsByMaterialIndex.TryGetValue(mesh.MaterialIndex, out var textureAssetId))
            {
                mesh.TextureAssetId = textureAssetId;
            }
        }
    }

    private static void ApplyMaterialImports(
        StaticModel model,
        IReadOnlyList<StaticModelImportedMaterial> materials,
        ImportedTextureAssets importedTextureAssets,
        string destinationSourceFilePath,
        ImportedMaterialTemplate materialTemplate)
    {
        string modelBaseName = Path.GetFileNameWithoutExtension(destinationSourceFilePath);
        string materialsDirectory = Path.GetDirectoryName(destinationSourceFilePath)!;
        Directory.CreateDirectory(materialsDirectory);

        var materialAssetIdsByMaterialIndex = new Dictionary<int, Guid>();
        var usedMaterialFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var importedMaterial in materials)
        {
            string materialFileName = CreateUniqueImportedFileName(
                SanitizeFileName(importedMaterial.DisplayName) + Constants.FileNameExtensions.Material,
                materialsDirectory,
                usedMaterialFileNames);
            string materialFullPath = Path.Combine(materialsDirectory, materialFileName);
            string materialRelativePath = GetRelativeProjectPath(materialFullPath);
            string materialAssetName = $"{modelBaseName}_{Path.GetFileNameWithoutExtension(materialFileName)}";
            var materialAssetInfo = EnsureAssetInfo(materialRelativePath, materialAssetName);

            MaterialAsset material = materialTemplate == ImportedMaterialTemplate.LitPbr
                ? CreateLitPbrMaterial(importedMaterial, importedTextureAssets, materialAssetInfo.Name)
                : CreateLitDiffuseMaterial(importedMaterial, importedTextureAssets, materialAssetInfo.Name);

            var materialDocument = SerializeAsset(material, materialAssetInfo.Id, materialAssetInfo.Name);
            EditorAssetWriterService.SaveDocument(materialRelativePath, materialDocument);
            materialAssetIdsByMaterialIndex[importedMaterial.MaterialIndex] = materialAssetInfo.Id;
        }

        foreach (var mesh in model.Meshes)
        {
            if (mesh.MaterialIndex >= 0
                && materialAssetIdsByMaterialIndex.TryGetValue(mesh.MaterialIndex, out var materialAssetId))
            {
                mesh.MaterialAssetId = materialAssetId;
            }
        }
    }

    private static MaterialAsset CreateLitDiffuseMaterial(
        StaticModelImportedMaterial importedMaterial,
        ImportedTextureAssets importedTextureAssets,
        string assetName)
    {
        LegacyImportedMaterialPresentation presentation = LegacyImportedMaterialPresentationResolver.Resolve(importedMaterial);
        var material = new MaterialAsset("lit-diffuse")
        {
            Name = assetName,
            Queue = presentation.Queue,
            RasterizerStateName = presentation.DisableBackfaceCulling
                ? "CullNone"
                : MaterialAsset.DefaultRasterizerStateName,
            SamplerStateName = "AnisotropicWrap",
        };
        material.SetPropertyValue("diffuse_color", MaterialValue.FromColor(importedMaterial.DiffuseColor));
        material.SetPropertyValue("ambient_color", MaterialValue.FromVector3(presentation.AmbientColor));
        material.SetPropertyValue("emissive_color", MaterialValue.FromVector3(presentation.EmissiveColor));
        material.SetPropertyValue("specular_color", MaterialValue.FromVector3(importedMaterial.SpecularColor));
        material.SetPropertyValue("specular_power", MaterialValue.FromFloat(importedMaterial.SpecularPower));
        material.SetPropertyValue("alpha_cutoff", MaterialValue.FromFloat(presentation.AlphaCutoff));

        if (importedTextureAssets.DiffuseTextureAssetIdsByMaterialIndex.TryGetValue(importedMaterial.MaterialIndex, out var diffuseTextureAssetId))
        {
            material.SetPropertyValue("base_color_texture", MaterialValue.FromTextureId(diffuseTextureAssetId));
        }

        if (importedTextureAssets.NormalTextureAssetIdsByMaterialIndex.TryGetValue(importedMaterial.MaterialIndex, out var normalTextureAssetId))
        {
            material.SetPropertyValue("normal_texture", MaterialValue.FromTextureId(normalTextureAssetId));
        }

        if (importedMaterial.UsesReflection
            && importedTextureAssets.ReflectionTextureAssetIdsByMaterialIndex.TryGetValue(importedMaterial.MaterialIndex, out var reflectionTextureAssetId))
        {
            material.SetPropertyValue("reflection_texture", MaterialValue.FromTextureId(reflectionTextureAssetId));
        }

        return material;
    }

    // Every key of the lit-pbr definition that the glTF material carries is written explicitly (ADR-0109); a texture the material
    // does not have produces no texture key. The base colour and the emissive colour are encoded to display colours.
    private static MaterialAsset CreateLitPbrMaterial(
        StaticModelImportedMaterial importedMaterial,
        ImportedTextureAssets importedTextureAssets,
        string assetName)
    {
        RenderQueue queue = importedMaterial.AlphaMode switch
        {
            StaticModelAlphaMode.Mask => RenderQueue.AlphaTest,
            StaticModelAlphaMode.Blend => RenderQueue.Transparent,
            _ => RenderQueue.Opaque,
        };

        var material = new MaterialAsset("lit-pbr")
        {
            Name = assetName,
            Queue = queue,
            RasterizerStateName = importedMaterial.DoubleSided
                ? "CullNone"
                : MaterialAsset.DefaultRasterizerStateName,
            SamplerStateName = "AnisotropicWrap",
        };

        Color linearBaseColor = importedMaterial.DiffuseColor;
        material.SetPropertyValue("base_color", MaterialValue.FromColor(new Color(
            EncodeToDisplay(linearBaseColor.R / 255.0f),
            EncodeToDisplay(linearBaseColor.G / 255.0f),
            EncodeToDisplay(linearBaseColor.B / 255.0f),
            linearBaseColor.A / 255.0f)));
        material.SetPropertyValue("metallic", MaterialValue.FromFloat(importedMaterial.MetallicFactor));
        material.SetPropertyValue("roughness", MaterialValue.FromFloat(importedMaterial.RoughnessFactor));
        material.SetPropertyValue("emissive_color", MaterialValue.FromVector3(new Vector3(
            EncodeToDisplay(importedMaterial.EmissiveColor.X),
            EncodeToDisplay(importedMaterial.EmissiveColor.Y),
            EncodeToDisplay(importedMaterial.EmissiveColor.Z))));
        material.SetPropertyValue("occlusion_strength", MaterialValue.FromFloat(importedMaterial.OcclusionStrength));
        material.SetPropertyValue("normal_scale", MaterialValue.FromFloat(importedMaterial.NormalScale));
        material.SetPropertyValue("alpha_cutoff", MaterialValue.FromFloat(importedMaterial.AlphaCutoff));

        int materialIndex = importedMaterial.MaterialIndex;
        SetTextureIfImported(material, "base_color_texture", importedTextureAssets.DiffuseTextureAssetIdsByMaterialIndex, materialIndex);
        SetTextureIfImported(material, "normal_texture", importedTextureAssets.NormalTextureAssetIdsByMaterialIndex, materialIndex);
        SetTextureIfImported(material, "metallic_roughness_texture", importedTextureAssets.MetallicRoughnessTextureAssetIdsByMaterialIndex, materialIndex);
        SetTextureIfImported(material, "occlusion_texture", importedTextureAssets.OcclusionTextureAssetIdsByMaterialIndex, materialIndex);
        SetTextureIfImported(material, "emissive_texture", importedTextureAssets.EmissiveTextureAssetIdsByMaterialIndex, materialIndex);
        return material;
    }

    private static void SetTextureIfImported(
        MaterialAsset material,
        string propertyKey,
        Dictionary<int, Guid> textureAssetIdsByMaterialIndex,
        int materialIndex)
    {
        if (textureAssetIdsByMaterialIndex.TryGetValue(materialIndex, out var textureAssetId))
        {
            material.SetPropertyValue(propertyKey, MaterialValue.FromTextureId(textureAssetId));
        }
    }

    // glTF factors are linear; lit-pbr stores display colours that the shader decodes with the 2.2 power (ColorSpace).
    private static float EncodeToDisplay(float linearValue)
        => MathF.Pow(Math.Clamp(linearValue, 0.0f, 1.0f), 1.0f / ColorSpace.DisplayGamma);

    private static Guid ImportTexture(
        string sourceTexturePath,
        string texturesDirectory,
        string modelBaseName,
        Dictionary<string, Guid> importedTexturesBySourcePath,
        HashSet<string> usedTextureFileNames,
        bool avoidExistingDestinationFileCollisions = false,
        Func<string, byte[]?>? readImage = null)
    {
        if (importedTexturesBySourcePath.TryGetValue(sourceTexturePath, out var existingTextureAssetId))
        {
            return existingTextureAssetId;
        }

        // readImage null: the source is a file; otherwise its bytes come from readImage.
        byte[]? imageBytes = null;
        if (readImage == null)
        {
            if (!File.Exists(sourceTexturePath))
            {
                return Guid.Empty;
            }
        }
        else
        {
            imageBytes = readImage(sourceTexturePath);
            if (imageBytes == null)
            {
                return Guid.Empty;
            }
        }

        if (!Texture2DLoader.IsTextureFile(sourceTexturePath))
        {
            return Guid.Empty;
        }

        string copiedTextureFileName = avoidExistingDestinationFileCollisions
            ? CreateUniqueImportedFileName(Path.GetFileName(sourceTexturePath), texturesDirectory, sourceTexturePath, usedTextureFileNames)
            : CreateUniqueImportedFileName(Path.GetFileName(sourceTexturePath), usedTextureFileNames);
        string copiedTextureFullPath = Path.Combine(texturesDirectory, copiedTextureFileName);
        if (imageBytes != null)
        {
            // Content mode: the returned bytes are the source of truth, always written (an existing file is overwritten),
            // even when the logical image path is the destination path.
            File.WriteAllBytes(copiedTextureFullPath, imageBytes);
        }
        else if (!IsSameFullPath(sourceTexturePath, copiedTextureFullPath))
        {
            File.Copy(sourceTexturePath, copiedTextureFullPath, !avoidExistingDestinationFileCollisions);
        }

        string copiedTextureRelativePath = GetRelativeProjectPath(copiedTextureFullPath);
        string rawTextureAssetName = $"{modelBaseName}_{copiedTextureFileName}";
        var rawTextureAssetInfo = EnsureAssetInfo(copiedTextureRelativePath, rawTextureAssetName);

        string wrapperRelativePath = Path.ChangeExtension(copiedTextureRelativePath, Constants.FileNameExtensions.Texture);
        string wrapperAssetName = $"{modelBaseName}_{Path.GetFileNameWithoutExtension(copiedTextureFileName)}";
        var wrapperAssetInfo = EnsureAssetInfo(wrapperRelativePath, wrapperAssetName);

        var wrapperDocument = CreateTextureWrapperDocument(wrapperAssetInfo.Id, wrapperAssetInfo.Name, rawTextureAssetInfo.Id);
        EditorAssetWriterService.SaveDocument(wrapperRelativePath, wrapperDocument);

        importedTexturesBySourcePath[sourceTexturePath] = wrapperAssetInfo.Id;
        return wrapperAssetInfo.Id;
    }

    private static Guid ImportTextureCube(
        string sourceTexturePath,
        string texturesDirectory,
        string modelBaseName,
        Dictionary<string, Guid> importedTexturesBySourcePath,
        HashSet<string> usedTextureFileNames,
        bool avoidExistingDestinationFileCollisions = false)
    {
        if (importedTexturesBySourcePath.TryGetValue(sourceTexturePath, out var existingTextureAssetId))
        {
            return existingTextureAssetId;
        }

        if (!File.Exists(sourceTexturePath)
            || !TextureCubeLoader.IsTextureCubeFile(sourceTexturePath))
        {
            return Guid.Empty;
        }

        string copiedTextureFileName = avoidExistingDestinationFileCollisions
            ? CreateUniqueImportedFileName(Path.GetFileName(sourceTexturePath), texturesDirectory, sourceTexturePath, usedTextureFileNames)
            : CreateUniqueImportedFileName(Path.GetFileName(sourceTexturePath), usedTextureFileNames);
        string copiedTextureFullPath = Path.Combine(texturesDirectory, copiedTextureFileName);
        if (!IsSameFullPath(sourceTexturePath, copiedTextureFullPath))
        {
            File.Copy(sourceTexturePath, copiedTextureFullPath, !avoidExistingDestinationFileCollisions);
        }

        string copiedTextureRelativePath = GetRelativeProjectPath(copiedTextureFullPath);
        string assetName = $"{modelBaseName}_{Path.GetFileNameWithoutExtension(copiedTextureFileName)}";
        var assetInfo = EnsureAssetInfo(copiedTextureRelativePath, assetName);

        importedTexturesBySourcePath[sourceTexturePath] = assetInfo.Id;
        return assetInfo.Id;
    }

    private static JObject CreateTextureWrapperDocument(Guid assetId, string assetName, Guid rawTextureAssetId)
    {
        var rootObject = new JObject
        {
            ["id"] = assetId.ToString(),
            ["name"] = assetName,
            ["texture_asset_id"] = rawTextureAssetId.ToString(),
        };

        var samplerStateObject = new JObject();
        SamplerState.AnisotropicWrap.Save(samplerStateObject);
        rootObject["sampler_state"] = samplerStateObject;
        return rootObject;
    }

    private static string CreateUniqueImportedFileName(string fileName, HashSet<string> usedFileNames)
    {
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        string candidate = fileName;
        int suffix = 2;

        while (!usedFileNames.Add(candidate))
        {
            candidate = $"{baseName}_{suffix++}{extension}";
        }

        return candidate;
    }

    private static string CreateUniqueImportedFileName(string fileName, string targetDirectory, HashSet<string> usedFileNames)
    {
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        string candidate = fileName;
        int suffix = 2;

        while (!usedFileNames.Add(candidate) || File.Exists(Path.Combine(targetDirectory, candidate)) || Directory.Exists(Path.Combine(targetDirectory, candidate)))
        {
            candidate = $"{baseName}_{suffix++}{extension}";
        }

        return candidate;
    }

    private static string CreateUniqueImportedFileName(string fileName, string targetDirectory, string sourceFilePath, HashSet<string> usedFileNames)
    {
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        string candidate = fileName;
        int suffix = 2;

        while (!usedFileNames.Add(candidate) || IsExistingDifferentPath(Path.Combine(targetDirectory, candidate), sourceFilePath))
        {
            candidate = $"{baseName}_{suffix++}{extension}";
        }

        return candidate;
    }

    private static bool IsExistingDifferentPath(string candidatePath, string sourceFilePath)
    {
        return (File.Exists(candidatePath) || Directory.Exists(candidatePath))
            && !IsSameFullPath(candidatePath, sourceFilePath);
    }

    private static bool IsSameFullPath(string firstPath, string secondPath)
    {
        return string.Equals(Path.GetFullPath(firstPath), Path.GetFullPath(secondPath), StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Material";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            bool isInvalid = false;
            foreach (char invalidChar in invalidChars)
            {
                if (character == invalidChar)
                {
                    isInvalid = true;
                    break;
                }
            }

            builder.Append(isInvalid || char.IsWhiteSpace(character)
                ? '_'
                : character);
        }

        return builder.ToString();
    }

    private static AssetInfo EnsureAssetInfo(string relativeFilePath, string assetName)
    {
        var existing = AssetCatalog.GetByFileName(relativeFilePath);
        if (existing != null)
        {
            return existing;
        }

        var assetInfo = new AssetInfo(Guid.NewGuid())
        {
            Name = string.IsNullOrWhiteSpace(assetName)
                ? Path.GetFileNameWithoutExtension(relativeFilePath)
                : assetName,
            FileName = relativeFilePath,
            AssetType = AssetInfo.InferAssetType(relativeFilePath),
        };

        EditorAssetCatalogService.Add(assetInfo);
        return assetInfo;
    }

    private static JObject SerializeAsset(object asset, Guid assetId, string assetName)
    {
        if (!EditorAssetJsonSerializer.TrySerialize(asset, out var rootObject))
        {
            throw new InvalidOperationException($"Asset type '{asset.GetType().FullName}' is not supported by the editor serializer.");
        }

        rootObject["id"] = assetId.ToString();
        rootObject["name"] = assetName;
        return rootObject;
    }

    private static string GetRelativeProjectPath(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(EngineEnvironment.ProjectPath))
        {
            throw new InvalidOperationException("Project path is not configured.");
        }

        return Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);
    }

    private sealed class ImportedTextureAssets
    {
        public ImportedTextureAssets(
            Dictionary<int, Guid> diffuseTextureAssetIdsByMaterialIndex,
            Dictionary<int, Guid> normalTextureAssetIdsByMaterialIndex,
            Dictionary<int, Guid> reflectionTextureAssetIdsByMaterialIndex,
            Dictionary<int, Guid> metallicRoughnessTextureAssetIdsByMaterialIndex,
            Dictionary<int, Guid> occlusionTextureAssetIdsByMaterialIndex,
            Dictionary<int, Guid> emissiveTextureAssetIdsByMaterialIndex)
        {
            DiffuseTextureAssetIdsByMaterialIndex = diffuseTextureAssetIdsByMaterialIndex;
            NormalTextureAssetIdsByMaterialIndex = normalTextureAssetIdsByMaterialIndex;
            ReflectionTextureAssetIdsByMaterialIndex = reflectionTextureAssetIdsByMaterialIndex;
            MetallicRoughnessTextureAssetIdsByMaterialIndex = metallicRoughnessTextureAssetIdsByMaterialIndex;
            OcclusionTextureAssetIdsByMaterialIndex = occlusionTextureAssetIdsByMaterialIndex;
            EmissiveTextureAssetIdsByMaterialIndex = emissiveTextureAssetIdsByMaterialIndex;
        }

        public Dictionary<int, Guid> MetallicRoughnessTextureAssetIdsByMaterialIndex { get; }

        public Dictionary<int, Guid> OcclusionTextureAssetIdsByMaterialIndex { get; }

        public Dictionary<int, Guid> EmissiveTextureAssetIdsByMaterialIndex { get; }

        public Dictionary<int, Guid> DiffuseTextureAssetIdsByMaterialIndex { get; }

        public Dictionary<int, Guid> NormalTextureAssetIdsByMaterialIndex { get; }

        public Dictionary<int, Guid> ReflectionTextureAssetIdsByMaterialIndex { get; }
    }
}