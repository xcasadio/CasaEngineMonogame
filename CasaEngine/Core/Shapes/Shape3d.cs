using System.Text.Json;
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Shapes;

public abstract class Shape3d
{
    public event EventHandler<string> PropertyChanged;

    public Shape3dType Type { get; }

    public abstract BoundingBox BoundingBox { get; }

    protected Shape3d(Shape3dType type)
    {
        Type = type;
    }

    public virtual void Load(JsonElement element)
    {
        //Do nothing
    }

    protected void OnPropertyChange(string propertyName)
    {
        PropertyChanged?.Invoke(this, propertyName);
    }

#if EDITOR
    public virtual void Save(JObject jObject)
    {
        jObject.Add("shape_type", Type.ConvertToString());
    }
#endif
}