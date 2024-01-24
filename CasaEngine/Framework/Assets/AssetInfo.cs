using System.Text.Json;
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

    //To remove : used only for retro compatibility
    public static readonly Dictionary<int, Guid> GuidsById = new();

    public void Load(JsonElement element)
    {
        var id = element.GetProperty("id").GetInt32();

        GuidsById.Add(id, Guid.NewGuid());
        Id = GuidsById[id];
        //Id = element.GetProperty("id").GetGuid();

        if (element.TryGetProperty("file_name", out var fileNameNode))
        {
            FileName = fileNameNode.GetString();
        }

        if (element.TryGetProperty("name", out var nameNode))
        {
            Name = nameNode.GetString();
        }
        else
        {
            //TODO : remove this else
            //System.Diagnostics.Debugger.Break();
            Name = Path.GetFileNameWithoutExtension(FileName); //element.GetProperty("name").GetString();
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