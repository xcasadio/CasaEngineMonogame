using System.Text.Json;
using CasaEngine.Core.Design;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets;

public class AssetInfo : ISaveLoad, IEquatable<AssetInfo>
{
    public long Id { get; private set; }

    public string FileName { get; set; }

    public string Name { get; set; }

    public AssetInfo()
    {
        Id = IdManager.GetId();
        Name = "Asset_" + Id;
    }

    public virtual void Load(JsonElement element, SaveOption option)
    {
        Id = element.GetProperty("id").GetInt64();
        IdManager.SetMax(Id);

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
            System.Diagnostics.Debugger.Break();
            Name = Path.GetFileNameWithoutExtension(FileName); //element.GetProperty("name").GetString();
        }
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
        return HashCode.Combine(Name, Id, FileName);
    }

#if EDITOR

    public virtual void Save(JObject jObject, SaveOption option)
    {
        var assetObject = new JObject(
            new JProperty("id", Id),
            new JProperty("name", Name),
            new JProperty("file_name", FileName));

        jObject.Add("asset", assetObject);
    }

#endif
}