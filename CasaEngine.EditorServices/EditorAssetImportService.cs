using CasaEngine.Engine;
using CasaEngine.Framework;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Graphics;
using Newtonsoft.Json.Linq;

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

        EnsureAssetInfo(relativeFilePath, Path.GetFileNameWithoutExtension(filePath));
        return true;
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
}