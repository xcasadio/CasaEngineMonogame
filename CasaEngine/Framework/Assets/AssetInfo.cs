
using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets;

public class AssetInfo : ISerializable, IEquatable<AssetInfo>
{
    public Guid Id { get; private set; }
    public string Name { get; set; }
    public string FileName { get; set; }
    public string AssetType { get; set; }
    public string RelativeFileName
    {
        get => FileName;
        set => FileName = value;
    }

    public AssetInfo()
    {
        Id = Guid.NewGuid();
        Name = string.Empty;
        FileName = string.Empty;
        AssetType = string.Empty;
    }

    public bool Equals(AssetInfo? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Name == other.Name && Id == other.Id && FileName == other.FileName && AssetType == other.AssetType;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals((AssetInfo)obj);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public void Load(JObject element)
    {
        Id = element["id"].GetGuid();

        if (element.ContainsKey("file_name"))
        {
            FileName = element["file_name"].GetString();
        }

        if (element.ContainsKey("name"))
        {
            Name = element["name"].GetString();
        }
        else
        {
            Name = Path.GetFileNameWithoutExtension(FileName);
        }

        AssetType = element.ContainsKey("asset_type")
            ? element["asset_type"].GetString()
            : InferAssetType(FileName);
    }

    public AssetInfo(Guid id)
    {
        Id = id;
        Name = string.Empty;
        FileName = string.Empty;
        AssetType = string.Empty;
    }

    public void Save(JObject jObject)
    {
        jObject.Add("id", Id);
        jObject.Add("name", Name);
        jObject.Add("file_name", FileName);
        jObject.Add("asset_type", string.IsNullOrWhiteSpace(AssetType) ? InferAssetType(FileName) : AssetType);
    }

    public static string InferAssetType(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        return Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
    }
}