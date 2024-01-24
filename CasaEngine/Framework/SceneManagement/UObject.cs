using System.Text.Json;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.SceneManagement;

//The base class of all UE objects.
public class UObject : ISerializable
{
    private bool _isInitialized;

    public Guid Id { get; private set; } = Guid.Empty;

    public string Name { get; set; }

    public string FileName { get; set; }

    public UObject()
    {

    }

    public UObject(UObject other)
    {
#if EDITOR
        Name = other.Name;
#endif
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
        //TODO : remove
        if (element.GetProperty("id").ValueKind == JsonValueKind.Number)
        {
            var id = element.GetProperty("id").GetInt32();
            if (AssetInfo.GuidsById.TryGetValue(id, out var guid))
            {
                Id = guid;
            }
            else
            {
                Id = Guid.NewGuid();
                AssetInfo.GuidsById.Add(id, Id);
            }
        }
        else
        {
            Id = element.GetProperty("id").GetGuid();
        }

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
