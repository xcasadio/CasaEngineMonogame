using System.Text;
using System.Text.Json;
using CasaEngine.Core.Logging;

namespace CasaEngine.Framework.Assets;

public static class AssetCatalog
{
    private static readonly Dictionary<Guid, AssetInfo> _assetInfos = new();
    private static readonly Dictionary<string, AssetInfo> _assetInfosByName = new();
    private static readonly Dictionary<string, AssetInfo> _assetInfosByFileName = new();

    public static bool IsLoaded { get; private set; }

    public static IEnumerable<AssetInfo> AssetInfos => _assetInfos.Values;

    internal static void AddInternal(AssetInfo assetInfo)
    {
        AddEntry(assetInfo);

        Logs.WriteTrace($"Add asset Id:{assetInfo.Id}, Name:{assetInfo.Name}, FileName:{assetInfo.FileName}");
    }

    private static void AddEntry(AssetInfo assetInfo)
    {
        NormalizeAssetInfo(assetInfo);
        _assetInfos.Add(assetInfo.Id, assetInfo);
        _assetInfosByName[assetInfo.Name] = assetInfo;
        _assetInfosByFileName[assetInfo.FileName] = assetInfo;
    }

    public static AssetInfo Get(Guid guid)
    {
        _assetInfos.TryGetValue(guid, out var assetInfo);
        return assetInfo;
    }

    public static AssetInfo Get(string name)
    {
        _assetInfosByName.TryGetValue(name, out var assetInfo);
        return assetInfo;
    }

    public static AssetInfo GetByFileName(string fileName)
    {
        _assetInfosByFileName.TryGetValue(fileName, out var assetInfo);
        return assetInfo;
    }

    public static void Load(string fileName)
    {
        ClearInternal();

        var count = ParseAssetInfos(File.ReadAllBytes(fileName), fileName);

        IsLoaded = true;
        Logs.WriteInfo($"Asset catalog loaded: {count} assets from {fileName}");
    }

    private static int ParseAssetInfos(byte[] fileBytes, string fileName)
    {
        var jsonBytes = fileBytes.AsSpan();
        if (jsonBytes.StartsWith(Encoding.UTF8.Preamble))
        {
            jsonBytes = jsonBytes[Encoding.UTF8.Preamble.Length..];
        }

        var readerOptions = new JsonReaderOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        var reader = new Utf8JsonReader(jsonBytes, readerOptions);

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            throw new InvalidDataException($"Asset catalog root must be a JSON object: {fileName}");
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (!reader.ValueTextEquals("asset_infos"u8))
            {
                reader.Skip();
                continue;
            }

            reader.Read();
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                return 0;
            }

            return ReadAssetInfoArray(ref reader, fileName);
        }

        return 0;
    }

    private static int ReadAssetInfoArray(ref Utf8JsonReader reader, string fileName)
    {
        var count = 0;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                continue;
            }

            AddEntry(ReadAssetInfo(ref reader, fileName));
            count++;
        }

        return count;
    }

    private static AssetInfo ReadAssetInfo(ref Utf8JsonReader reader, string fileName)
    {
        var id = Guid.Empty;
        var hasId = false;
        string name = null;
        string assetFileName = null;
        string assetType = null;

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (reader.ValueTextEquals("id"u8))
            {
                reader.Read();
                id = Guid.Parse(reader.GetString() ?? string.Empty);
                hasId = true;
            }
            else if (reader.ValueTextEquals("name"u8))
            {
                reader.Read();
                name = reader.GetString();
            }
            else if (reader.ValueTextEquals("file_name"u8))
            {
                reader.Read();
                assetFileName = reader.GetString();
            }
            else if (reader.ValueTextEquals("asset_type"u8))
            {
                reader.Read();
                assetType = reader.GetString();
            }
            else
            {
                reader.Skip();
            }
        }

        if (!hasId)
        {
            throw new InvalidDataException($"Asset info without 'id' in catalog: {fileName}");
        }

        return new AssetInfo(id)
        {
            Name = name ?? string.Empty,
            FileName = assetFileName ?? string.Empty,
            AssetType = assetType ?? string.Empty,
        };
    }

    internal static void AddInternal(Guid id, string name, string fileName)
    {
        var assetInfo = new AssetInfo(id)
        {
            Name = name,
            FileName = fileName,
            AssetType = AssetInfo.InferAssetType(fileName),
        };

        AddInternal(assetInfo);
    }

    internal static bool RemoveInternal(Guid id, out AssetInfo assetInfo)
    {
        if (!_assetInfos.TryGetValue(id, out var existingAssetInfo))
        {
            assetInfo = null;
            return false;
        }

        assetInfo = existingAssetInfo;

        Logs.WriteTrace($"Remove asset Id:{assetInfo.Id}, Name:{assetInfo.Name}, FileName:{assetInfo.FileName}");
        _assetInfos.Remove(id);
        _assetInfosByName.Remove(assetInfo.Name);
        _assetInfosByFileName.Remove(assetInfo.FileName);
        return true;
    }

    internal static void ClearInternal()
    {
        Logs.WriteTrace("Clear all assets");

        _assetInfos.Clear();
        _assetInfosByName.Clear();
        _assetInfosByFileName.Clear();
        IsLoaded = false;
    }

    internal static bool CanRenameInternal(string newName)
    {
        return !_assetInfos.Any(x => string.Equals(x.Value.Name, newName, StringComparison.InvariantCultureIgnoreCase));
    }

    internal static bool RenameInternal(Guid id, string newName, out AssetInfo assetInfo, out string oldName)
    {
        assetInfo = Get(id);
        oldName = null;

        if (assetInfo == null)
        {
            Logs.WriteError($"Rename Entity : The id '{id}' is not present in the catalog. (new name is {newName})");
            return false;
        }

        oldName = assetInfo.Name;
        _assetInfosByName.Remove(oldName);
        assetInfo.Name = newName;
        _assetInfosByName[newName] = assetInfo;

        return true;
    }

    internal static string RenameInternal(AssetInfo assetInfo, string newName)
    {
        var oldName = assetInfo.Name;
        _assetInfosByName.Remove(oldName);
        assetInfo.Name = newName;
        _assetInfosByName[newName] = assetInfo;

        return oldName;
    }

    private static void NormalizeAssetInfo(AssetInfo assetInfo)
    {
        assetInfo.Name = string.IsNullOrWhiteSpace(assetInfo.Name)
            ? Path.GetFileNameWithoutExtension(assetInfo.FileName)
            : assetInfo.Name;

        if (string.IsNullOrWhiteSpace(assetInfo.AssetType))
        {
            assetInfo.AssetType = AssetInfo.InferAssetType(assetInfo.FileName);
        }
    }
}