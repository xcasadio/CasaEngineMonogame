using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Rendering.Geometry;
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

        var importer = new StaticModelImporter();
        if (!importer.IsFileSupported(sourceFilePath))
        {
            return catalogChanged;
        }

        var result = importer.ImportWithMetadata(sourceFilePath, legacyMaterialImportProfile);
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
        var importedAssetsDirectory = GetImportedAssetsDirectory(destinationFilePath);
        var texturesDirectory = Path.Combine(importedAssetsDirectory, "Textures");
        Directory.CreateDirectory(texturesDirectory);

        var importedTexturesBySourcePath = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var usedTextureFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var textureAssetId = ImportTexture(
            tiledMap.Tileset.ImageFilePath,
            texturesDirectory,
            mapBaseName,
            importedTexturesBySourcePath,
            usedTextureFileNames);

        if (textureAssetId == Guid.Empty)
        {
            throw new FileNotFoundException("Unable to import Tiled tileset image.", tiledMap.Tileset.ImageFilePath);
        }

        var tileSetFullPath = Path.Combine(importedAssetsDirectory, mapBaseName + Constants.FileNameExtensions.TileSet);
        var tileSetRelativePath = GetRelativeProjectPath(tileSetFullPath);
        var tileSetAssetInfo = EnsureAssetInfo(tileSetRelativePath, mapBaseName + "_TileSet");
        var tileSetData = CreateTileSetData(tiledMap, tileSetAssetInfo, textureAssetId);
        var tileSetDocument = SerializeAsset(tileSetData, tileSetAssetInfo.Id, tileSetAssetInfo.Name);
        EditorAssetWriterService.SaveDocument(tileSetRelativePath, tileSetDocument);

        var tileMapFullPath = Path.ChangeExtension(destinationFilePath, Constants.FileNameExtensions.TileMap);
        var tileMapRelativePath = GetRelativeProjectPath(tileMapFullPath);
        var tileMapAssetInfo = EnsureAssetInfo(tileMapRelativePath, mapBaseName);
        var tileMapData = CreateTileMapData(tiledMap, tileMapAssetInfo, tileSetAssetInfo.Id);
        var tileMapDocument = SerializeAsset(tileMapData, tileMapAssetInfo.Id, tileMapAssetInfo.Name);
        EditorAssetWriterService.SaveDocument(tileMapRelativePath, tileMapDocument);

        EditorAssetCatalogService.Save();

        var createdAssetFileNames = new List<string>
        {
            tileMapRelativePath,
            tileSetRelativePath,
        };

        var textureAssetInfo = AssetCatalog.Get(textureAssetId);
        if (textureAssetInfo != null)
        {
            createdAssetFileNames.Add(textureAssetInfo.FileName);
        }

        return new TiledMapImportResult(createdAssetFileNames, tiledMap.Warnings);
    }

    private static TileSetData CreateTileSetData(TiledMapImportDocument tiledMap, AssetInfo tileSetAssetInfo, Guid textureAssetId)
    {
        var tileSetData = new TileSetData
        {
            Name = tileSetAssetInfo.Name,
            FileName = tileSetAssetInfo.FileName,
            SpriteSheetAssetId = textureAssetId,
            TileSize = new Size(tiledMap.TileWidth, tiledMap.TileHeight),
        };

        for (var tileIndex = 0; tileIndex < tiledMap.Tileset.TileCount; tileIndex++)
        {
            var column = tileIndex % tiledMap.Tileset.Columns;
            var row = tileIndex / tiledMap.Tileset.Columns;
            var tileData = new StaticTileData
            {
                Id = tileIndex,
                CollisionType = TileCollisionType.None,
                IsBreakable = false,
                Location = new Rectangle(
                    column * tiledMap.TileWidth,
                    row * tiledMap.TileHeight,
                    tiledMap.TileWidth,
                    tiledMap.TileHeight),
            };

            if (tiledMap.Tileset.CollisionByTileId.TryGetValue(tileIndex, out var collision))
            {
                tileData.CollisionType = TileCollisionType.Blocked;
                tileData.CollisionShape = new Collision2d
                {
                    CollisionHitType = CollisionHitType.Defense,
                    Shape = new ShapeRectangle(collision.X, collision.Y, collision.Width, collision.Height),
                };
            }

            if (tiledMap.Tileset.CustomPropertiesByTileId.TryGetValue(tileIndex, out var customProperties))
            {
                CopyCustomProperties(customProperties, tileData.CustomProperties);
            }

            tileSetData.AddTile(tileData);
        }

        return tileSetData;
    }

    private static TileMapData CreateTileMapData(TiledMapImportDocument tiledMap, AssetInfo tileMapAssetInfo, Guid tileSetAssetId)
    {
        var tileMapData = new TileMapData
        {
            Name = tileMapAssetInfo.Name,
            FileName = tileMapAssetInfo.FileName,
            MapSize = new Size(tiledMap.Width, tiledMap.Height),
            TileSetDataAssetId = tileSetAssetId,
        };
        CopyCustomProperties(tiledMap.CustomProperties, tileMapData.CustomProperties);

        foreach (var tiledLayer in tiledMap.Layers)
        {
            var layerData = new TileMapLayerData
            {
                Name = tiledLayer.Name,
                zOffset = tiledLayer.ZOffset,
                tiles = new List<int>(tiledLayer.Tiles),
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

    private static bool TryImportSeparatedAnimationAssets(string sourceFilePath, string destinationFilePath)
    {
        var riggedModelLoader = new RiggedModelLoader(null, null);
        RiggedModel riggedModel;

        try
        {
            riggedModel = riggedModelLoader.LoadAsset(sourceFilePath);
        }
        catch
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

    private static void ImportSeparatedAnimationAssets(string destinationFilePath, RiggedModel riggedModel)
    {
        ArgumentNullException.ThrowIfNull(riggedModel);

        string modelBaseName = Path.GetFileNameWithoutExtension(destinationFilePath);
        string rawAssetRelativePath = GetRelativeProjectPath(destinationFilePath);
        var rawAssetInfo = AssetCatalog.GetByFileName(rawAssetRelativePath)
            ?? EnsureAssetInfo(rawAssetRelativePath, modelBaseName);

        Guid skeletonAssetId = Guid.Empty;
        string animationDirectory = Path.Combine(GetImportedAssetsDirectory(destinationFilePath), "Animation");
        Directory.CreateDirectory(animationDirectory);

        if (riggedModel.SkeletonDefinition != null)
        {
            string skeletonFullPath = Path.Combine(animationDirectory, modelBaseName + Constants.FileNameExtensions.Skeleton);
            string skeletonRelativePath = GetRelativeProjectPath(skeletonFullPath);
            string skeletonAssetName = modelBaseName + "_Skeleton";
            var skeletonAssetInfo = EnsureAssetInfo(skeletonRelativePath, skeletonAssetName);
            var skeletonAsset = AnimationAssetDataConverter.CreateSkeletonAsset(riggedModel.SkeletonDefinition);
            skeletonAsset.Name = skeletonAssetInfo.Name;
            skeletonAsset.FileName = skeletonRelativePath;

            var skeletonDocument = SerializeAsset(skeletonAsset, skeletonAssetInfo.Id, skeletonAssetInfo.Name);
            EditorAssetWriterService.SaveDocument(skeletonRelativePath, skeletonDocument);
            skeletonAssetId = skeletonAssetInfo.Id;
        }

        var clipAssetIds = new List<Guid>(riggedModel.AnimationClips.Count);
        var usedClipFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var clipIndex = 0; clipIndex < riggedModel.AnimationClips.Count; clipIndex++)
        {
            var animationClip = riggedModel.AnimationClips[clipIndex];
            string clipBaseName = string.IsNullOrWhiteSpace(animationClip.Name)
                ? $"{modelBaseName}_{clipIndex + 1}"
                : $"{modelBaseName}_{SanitizeFileName(animationClip.Name)}";
            string clipFileName = CreateUniqueImportedFileName(
                clipBaseName + Constants.FileNameExtensions.SkeletonAnimation,
                usedClipFileNames);
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
        string texturesDirectory = Path.Combine(GetImportedAssetsDirectory(destinationSourceFilePath), "Textures");
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
                    usedTextureFileNames);

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
                    usedTextureFileNames);

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
                    usedTextureFileNames);

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
        string materialsDirectory = Path.Combine(GetImportedAssetsDirectory(destinationSourceFilePath), "Materials");
        Directory.CreateDirectory(materialsDirectory);

        var materialAssetIdsByMaterialIndex = new Dictionary<int, Guid>();
        var usedMaterialFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var importedMaterial in materials)
        {
            LegacyImportedMaterialPresentation presentation = LegacyImportedMaterialPresentationResolver.Resolve(importedMaterial);
            string materialFileName = CreateUniqueImportedFileName(
                SanitizeFileName(importedMaterial.DisplayName) + Constants.FileNameExtensions.Material,
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
        HashSet<string> usedTextureFileNames)
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

        string copiedTextureFileName = CreateUniqueImportedFileName(Path.GetFileName(sourceTexturePath), usedTextureFileNames);
        string copiedTextureFullPath = Path.Combine(texturesDirectory, copiedTextureFileName);
        File.Copy(sourceTexturePath, copiedTextureFullPath, true);

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
        HashSet<string> usedTextureFileNames)
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

        string copiedTextureFileName = CreateUniqueImportedFileName(Path.GetFileName(sourceTexturePath), usedTextureFileNames);
        string copiedTextureFullPath = Path.Combine(texturesDirectory, copiedTextureFileName);
        File.Copy(sourceTexturePath, copiedTextureFullPath, true);

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

    private static string GetImportedAssetsDirectory(string destinationSourceFilePath)
    {
        string targetDirectory = Path.GetDirectoryName(destinationSourceFilePath)!;
        string modelBaseName = Path.GetFileNameWithoutExtension(destinationSourceFilePath);
        return Path.Combine(targetDirectory, modelBaseName + "_Imported");
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