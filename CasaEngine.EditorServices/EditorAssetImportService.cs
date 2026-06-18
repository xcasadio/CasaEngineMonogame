using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Rendering.Geometry;
using CasaEngine.EditorServices.Import;
using CasaEngine.EditorServices.Tiled;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using System.Text;
using Size = CasaEngine.Core.Math.Size;

namespace CasaEngine.EditorServices;

public static class EditorAssetImportService
{
    public static TiledMapImportResult? LastTiledMapImportResult { get; private set; }

    public static bool ImportFile(
        string sourceFilePath,
        string destinationFilePath,
        ILegacyMaterialImportProfile? legacyMaterialImportProfile = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFilePath);

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

        var importedTextureAssets = ImportTextureAssets(result.Materials, destinationFilePath);
        ApplyDiffuseTextureSlots(model, importedTextureAssets.DiffuseTextureAssetIdsByMaterialIndex);
        ApplyMaterialImports(model, result.Materials, importedTextureAssets, destinationFilePath);

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
                true);

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
                    CollisionHitType = CollisionHitType.Defense,
                    Shape = new ShapeRectangle(collision.X, collision.Y, collision.Width, collision.Height),
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
        if (!riggedModelReader.IsFileSupported(sourceFilePath) && !AssimpToGltfConverter.RequiresConversion(sourceFilePath))
        {
            return false;
        }

        RiggedModel riggedModel;

        try
        {
            riggedModel = ReadRiggedModelWithConversion(riggedModelReader, sourceFilePath);
        }
        catch
        {
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

        ImportSeparatedAnimationAssets(destinationFilePath, riggedModel);
        return true;
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

    /// <summary>
    /// Reads a rigged model, converting non-glTF sources to a temporary <c>.glb</c> first.
    /// </summary>
    private static RiggedModel ReadRiggedModelWithConversion(GltfRiggedModelReader reader, string sourceFilePath)
    {
        if (reader.IsFileSupported(sourceFilePath))
        {
            return reader.LoadAsset(sourceFilePath);
        }

        string temporaryGlbPath = CreateTemporaryGlbPath();
        try
        {
            AssimpToGltfConverter.Convert(sourceFilePath, temporaryGlbPath);
            return reader.LoadAsset(temporaryGlbPath);
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

    private static void ImportSeparatedAnimationAssets(string destinationFilePath, RiggedModel riggedModel)
    {
        ArgumentNullException.ThrowIfNull(riggedModel);

        string modelBaseName = Path.GetFileNameWithoutExtension(destinationFilePath);
        string rawAssetRelativePath = GetRelativeProjectPath(destinationFilePath);
        var rawAssetInfo = AssetCatalog.GetByFileName(rawAssetRelativePath)
            ?? EnsureAssetInfo(rawAssetRelativePath, modelBaseName);

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
            GeometryAssetId = rawAssetInfo.Id,
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
        string destinationSourceFilePath)
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
            reflectionTextureAssetIdsByMaterialIndex);
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
        string destinationSourceFilePath)
    {
        string modelBaseName = Path.GetFileNameWithoutExtension(destinationSourceFilePath);
        string materialsDirectory = Path.GetDirectoryName(destinationSourceFilePath)!;
        Directory.CreateDirectory(materialsDirectory);

        var materialAssetIdsByMaterialIndex = new Dictionary<int, Guid>();
        var usedMaterialFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var importedMaterial in materials)
        {
            LegacyImportedMaterialPresentation presentation = LegacyImportedMaterialPresentationResolver.Resolve(importedMaterial);
            string materialFileName = CreateUniqueImportedFileName(
                SanitizeFileName(importedMaterial.DisplayName) + Constants.FileNameExtensions.Material,
                materialsDirectory,
                usedMaterialFileNames);
            string materialFullPath = Path.Combine(materialsDirectory, materialFileName);
            string materialRelativePath = GetRelativeProjectPath(materialFullPath);
            string materialAssetName = $"{modelBaseName}_{Path.GetFileNameWithoutExtension(materialFileName)}";
            var materialAssetInfo = EnsureAssetInfo(materialRelativePath, materialAssetName);

            var material = new MaterialAsset("lit-diffuse")
            {
                Name = materialAssetInfo.Name,
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

    private static Guid ImportTexture(
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

        if (!File.Exists(sourceTexturePath))
        {
            return Guid.Empty;
        }

        if (!Texture2DLoader.IsTextureFile(sourceTexturePath))
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
            Dictionary<int, Guid> reflectionTextureAssetIdsByMaterialIndex)
        {
            DiffuseTextureAssetIdsByMaterialIndex = diffuseTextureAssetIdsByMaterialIndex;
            NormalTextureAssetIdsByMaterialIndex = normalTextureAssetIdsByMaterialIndex;
            ReflectionTextureAssetIdsByMaterialIndex = reflectionTextureAssetIdsByMaterialIndex;
        }

        public Dictionary<int, Guid> DiffuseTextureAssetIdsByMaterialIndex { get; }

        public Dictionary<int, Guid> NormalTextureAssetIdsByMaterialIndex { get; }

        public Dictionary<int, Guid> ReflectionTextureAssetIdsByMaterialIndex { get; }
    }
}