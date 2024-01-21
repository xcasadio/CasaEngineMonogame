using System.Text.Json;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.SceneManagement.Components;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.SceneManagement;

//The base class of all UE objects.
public class UObject : ISerializable
{
    private bool _isInitialized;

    public Guid Id { get; private set; } = Guid.Empty;
    public string Name { get; set; }

    public UObject()
    {

    }

    public UObject(UObject other)
    {
        Name = other.Name;
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
        if (Id == Guid.Empty)
        {
            Id = Guid.NewGuid();
        }
    }

    public virtual void Load(JsonElement element)
    {
        Id = element.GetProperty("id").GetGuid();
        Name = element.GetProperty("name").GetString();
    }

#if EDITOR

    public virtual void Save(JObject node)
    {
        node.Add("id", Id.ToString());
        node.Add("name", Name);
    }

#endif
}






