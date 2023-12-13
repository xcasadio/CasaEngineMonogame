using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Shapes;

public abstract class Shape3d
{
    [Browsable(false)]
    public Shape3dType Type { get; }

    [Category("Coordinate")]
    public Vector3 Position { get; set; }

    [Category("Coordinate")]
    public Quaternion Orientation { get; set; }

    protected Shape3d(Shape3dType type)
    {
        Type = type;
    }

    public virtual void Load(JsonElement element)
    {
        Position = element.GetProperty("position").GetVector3();
        Orientation = element.GetProperty("orientation").GetQuaternion();
    }

#if EDITOR
    public virtual void Save(JObject jObject)
    {
        jObject.Add("version", 1);
        jObject.Add("shape_type", Type.ConvertToString());

        var newObject = new JObject();
        Position.Save(newObject);
        jObject.Add("position", newObject);

        newObject = new JObject();
        Orientation.Save(newObject);
        jObject.Add("orientation", newObject);
    }
#endif
}