
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities;

public class ObjectBase : ISerializable
{
    private bool _isInitialized;

    public Guid Id { get; private set; }

    public string Name { get; set; }

    public string FileName { get; set; }

    //if this object comes from an asset (example an actor created in the content browser)
    public Guid AssetId { get; set; }

    public ObjectBase()
    {
        Id = Guid.NewGuid();
        Name = "Object " + Id;
    }

    public ObjectBase(ObjectBase other)
    {
        Id = Guid.NewGuid();
        Name = other.Name;
        FileName = other.FileName;
    }

    public void Initialize()
    {
        if (!_isInitialized)
        {
            InitializePrivate();
            _isInitialized = true;
        }
    }

    protected virtual void InitializePrivate()
    {
        //Do nothing
    }

    public virtual void Load(JObject element)
    {
        Id = element["id"].GetGuid();
        Name = element["name"].GetString();
    }

#if EDITOR

    public virtual void Save(JObject node)
    {
        node.Add("id", Id.ToString());
        node.Add("name", Name);
    }

#endif
}
