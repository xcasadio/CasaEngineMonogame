
using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets;

public class AssetInfo : ISerializable, IEquatable<AssetInfo>
{
    public Guid Id { get; private set; }
    public string Name { get; set; }
    public string FileName { get; set; }

    public AssetInfo()
    {
        Id = Guid.NewGuid();
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

        return Name == other.Name && Id == other.Id && FileName == other.FileName;
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
    }

#if EDITOR

    public AssetInfo(Guid id)
    {
        Id = id;
    }

    public void Save(JObject jObject)
    {
        var assetObject = new JObject(
            new JProperty("id", Id),
            new JProperty("name", Name),
            new JProperty("file_name", FileName));

        jObject.Add("asset", assetObject);
    }

#endif
}