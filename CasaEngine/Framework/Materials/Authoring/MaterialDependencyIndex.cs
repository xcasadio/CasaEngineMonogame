using CasaEngine.Core.Serialization;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Materials.Authoring;

internal sealed class MaterialDependencyIndex
{
    private readonly Dictionary<Guid, Guid> _parentByMaterialId = new();
    private readonly Dictionary<Guid, HashSet<Guid>> _childrenByParentMaterialId = new();
    private string? _indexedProjectPath;
    private int _indexedMaterialAssetCount = -1;
    private bool _isInitialized;

    public HashSet<Guid> GetAffectedMaterialAssetIds(Guid materialAssetId)
    {
        EnsureInitialized();

        var affectedMaterialAssetIds = new HashSet<Guid> { materialAssetId };
        var pendingMaterialIds = new Queue<Guid>();
        pendingMaterialIds.Enqueue(materialAssetId);

        while (pendingMaterialIds.Count > 0)
        {
            Guid currentMaterialId = pendingMaterialIds.Dequeue();
            if (!_childrenByParentMaterialId.TryGetValue(currentMaterialId, out var childMaterialIds))
            {
                continue;
            }

            foreach (Guid childMaterialId in childMaterialIds)
            {
                if (!affectedMaterialAssetIds.Add(childMaterialId))
                {
                    continue;
                }

                pendingMaterialIds.Enqueue(childMaterialId);
            }
        }

        return affectedMaterialAssetIds;
    }

    public void RefreshMaterialDependency(Guid materialAssetId)
    {
        EnsureInitialized();

        var assetInfo = AssetCatalog.Get(materialAssetId);
        if (assetInfo == null
            || assetInfo.Id == Guid.Empty
            || !string.Equals(assetInfo.AssetType, "material", StringComparison.OrdinalIgnoreCase))
        {
            RemoveMaterialDependency(materialAssetId);
            return;
        }

        UpdateMaterialDependency(assetInfo.Id, TryReadParentMaterialAssetId(assetInfo.FileName));
    }

    private void EnsureInitialized()
    {
        int currentMaterialAssetCount = CountMaterialAssets();
        string currentProjectPath = EngineEnvironment.ProjectPath ?? string.Empty;

        if (_isInitialized
            && _indexedMaterialAssetCount == currentMaterialAssetCount
            && string.Equals(_indexedProjectPath, currentProjectPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        RebuildIndex(currentProjectPath, currentMaterialAssetCount);
    }

    private void RebuildIndex(string currentProjectPath, int currentMaterialAssetCount)
    {
        _parentByMaterialId.Clear();
        _childrenByParentMaterialId.Clear();

        foreach (var assetInfo in AssetCatalog.AssetInfos)
        {
            if (assetInfo.Id == Guid.Empty
                || !string.Equals(assetInfo.AssetType, "material", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            UpdateMaterialDependency(assetInfo.Id, TryReadParentMaterialAssetId(assetInfo.FileName));
        }

        _indexedProjectPath = currentProjectPath;
        _indexedMaterialAssetCount = currentMaterialAssetCount;
        _isInitialized = true;
    }

    private static int CountMaterialAssets()
    {
        int materialAssetCount = 0;

        foreach (var assetInfo in AssetCatalog.AssetInfos)
        {
            if (string.Equals(assetInfo.AssetType, "material", StringComparison.OrdinalIgnoreCase))
            {
                materialAssetCount++;
            }
        }

        return materialAssetCount;
    }

    private void UpdateMaterialDependency(Guid materialAssetId, Guid parentMaterialAssetId)
    {
        RemoveMaterialDependency(materialAssetId);

        if (parentMaterialAssetId == Guid.Empty)
        {
            return;
        }

        _parentByMaterialId[materialAssetId] = parentMaterialAssetId;
        if (!_childrenByParentMaterialId.TryGetValue(parentMaterialAssetId, out var childMaterialIds))
        {
            childMaterialIds = new HashSet<Guid>();
            _childrenByParentMaterialId.Add(parentMaterialAssetId, childMaterialIds);
        }

        childMaterialIds.Add(materialAssetId);
    }

    private void RemoveMaterialDependency(Guid materialAssetId)
    {
        if (!_parentByMaterialId.Remove(materialAssetId, out Guid previousParentMaterialId))
        {
            return;
        }

        if (!_childrenByParentMaterialId.TryGetValue(previousParentMaterialId, out var childMaterialIds))
        {
            return;
        }

        childMaterialIds.Remove(materialAssetId);
        if (childMaterialIds.Count == 0)
        {
            _childrenByParentMaterialId.Remove(previousParentMaterialId);
        }
    }

    private static Guid TryReadParentMaterialAssetId(string? relativeFileName)
    {
        if (string.IsNullOrWhiteSpace(relativeFileName)
            || string.IsNullOrWhiteSpace(EngineEnvironment.ProjectPath))
        {
            return Guid.Empty;
        }

        string normalizedRelativePath = relativeFileName.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        string fullPath = Path.Combine(EngineEnvironment.ProjectPath, normalizedRelativePath);
        if (!File.Exists(fullPath))
        {
            return Guid.Empty;
        }

        try
        {
            var document = JObject.Parse(File.ReadAllText(fullPath));
            return document["parent_material_asset_id"]?.GetGuid() ?? Guid.Empty;
        }
        catch
        {
            return Guid.Empty;
        }
    }
}