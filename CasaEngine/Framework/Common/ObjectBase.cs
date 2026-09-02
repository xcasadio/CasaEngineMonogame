using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Common;

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

    /// <summary>
    /// Additive constructor letting a caller assign a deterministic <see cref="Id"/> at construction
    /// (for example a converter deriving stable ids from <c>Ids.For</c>) instead of the random one the
    /// parameterless constructor generates. Mirrors its default <see cref="Name"/> so nothing
    /// serializes a null name; <see cref="Load"/> still overrides both afterwards.
    /// </summary>
    protected ObjectBase(Guid id)
    {
        Id = id;
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
}
