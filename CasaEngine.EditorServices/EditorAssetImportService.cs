using CasaEngine.Engine;
using CasaEngine.Framework;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Materials;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using System.Text;

namespace CasaEngine.EditorServices;

public static class EditorAssetImportService
{
    public static bool ImportFile(string sourceFilePath, string destinationFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFilePath);

        bool catalogChanged = EnsureFileAssetRegistered(destinationFilePath);

        var importer = new StaticModelImporter();
        if (!importer.IsFileSupported(sourceFilePath))
        {
            return catalogChanged;
        }

        var result = importer.ImportWithMetadata(sourceFilePath);
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

            if (!string.IsNullOrWhiteSpace(material.ReflectionTextureFilePath))
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
                Queue = importedMaterial.AlphaCutoutHint
                    ? RenderQueue.AlphaTest
                    : RenderQueue.Opaque,
                RasterizerStateName = importedMaterial.AlphaCutoutHint
                    ? "CullNone"
                    : MaterialAsset.DefaultRasterizerStateName,
                SamplerStateName = "AnisotropicWrap",
            };
            material.SetPropertyValue("diffuse_color", MaterialValue.FromColor(importedMaterial.DiffuseColor));
            material.SetPropertyValue("ambient_color", MaterialValue.FromVector3(ComputeImportedMaterialAmbientColor(importedMaterial)));
            material.SetPropertyValue("emissive_color", MaterialValue.FromVector3(ComputeImportedMaterialEmissiveColor(importedMaterial)));
            material.SetPropertyValue("specular_color", MaterialValue.FromVector3(importedMaterial.SpecularColor));
            material.SetPropertyValue("specular_power", MaterialValue.FromFloat(importedMaterial.SpecularPower));
            material.SetPropertyValue("alpha_cutoff", MaterialValue.FromFloat(
                importedMaterial.AlphaCutoutHint ? 0.35f : 0.5f));

            if (importedTextureAssets.DiffuseTextureAssetIdsByMaterialIndex.TryGetValue(importedMaterial.MaterialIndex, out var diffuseTextureAssetId))
            {
                material.SetPropertyValue("base_color_texture", MaterialValue.FromTextureId(diffuseTextureAssetId));
            }

            if (importedTextureAssets.NormalTextureAssetIdsByMaterialIndex.TryGetValue(importedMaterial.MaterialIndex, out var normalTextureAssetId))
            {
                material.SetPropertyValue("normal_texture", MaterialValue.FromTextureId(normalTextureAssetId));
            }

            if (importedTextureAssets.ReflectionTextureAssetIdsByMaterialIndex.TryGetValue(importedMaterial.MaterialIndex, out var reflectionTextureAssetId))
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

    private static Vector3 ComputeImportedMaterialAmbientColor(StaticModelImportedMaterial importedMaterial)
    {
        Vector3 ambientColor = importedMaterial.AmbientColor;
        if (importedMaterial.BrightAmbientHint)
        {
            const float signAmbient = 128f / 255f;
            Vector3 boostedAmbient = new(signAmbient, signAmbient, signAmbient);
            ambientColor = Vector3.Max(ambientColor, boostedAmbient);
        }

        return Vector3.Clamp(ambientColor, Vector3.Zero, Vector3.One);
    }

    private static Vector3 ComputeImportedMaterialEmissiveColor(StaticModelImportedMaterial importedMaterial)
    {
        return Vector3.Clamp(importedMaterial.EmissiveColor, Vector3.Zero, Vector3.One);
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