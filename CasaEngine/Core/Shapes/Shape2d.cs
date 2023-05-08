using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Core.Helpers;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Shapes;

public abstract class Shape2d
{
    [Browsable(false)]
    public Shape2dType Type { get; }
    public Point Location { get; set; } = Point.Zero;
    public float Rotation { get; set; }

    protected Shape2d(Shape2dType type)
    {
        Type = type;
    }

    public virtual void Load(JsonElement element)
    {
        //Location = new Point(
        //    element.GetProperty("x").GetInt32(),
        //    element.GetProperty("y").GetInt32());
        Location = element.GetProperty("location").GetPoint();
        Rotation = element.GetProperty("orientation").GetSingle();
    }

#if EDITOR
    public virtual void Save(JObject jObject)
    {
        jObject.Add("version", 1);
        jObject.Add("shape_type", Type.ConvertToString());

        var newObject = new JObject();
        Location.Save(newObject);
        jObject.Add("location", newObject);

        jObject.Add("orientation", Rotation);
    }
#endif
}