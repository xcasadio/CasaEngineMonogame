using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Shapes;

public abstract class Shape2d
{
    public Shape2dType Type { get; }
    public Vector2 Position { get; set; } = Vector2.Zero;
    public float Rotation { get; set; }
    public abstract BoundingBox BoundingBox { get; }

    protected Shape2d(Shape2dType type)
    {
        Type = type;
    }

    public virtual void Load(JsonElement element)
    {
        Position = element.GetProperty("location").GetVector2();
        Rotation = element.GetProperty("orientation").GetSingle();
    }

#if EDITOR
    public virtual void Save(JObject jObject)
    {
        jObject.Add("version", 1);
        jObject.Add("shape_type", Type.ConvertToString());

        var newObject = new JObject();
        Position.Save(newObject);
        jObject.Add("location", newObject);

        jObject.Add("orientation", Rotation);
    }
#endif
}