using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Shapes;

public class ShapeCircle : Shape2d, IEquatable<ShapeCircle>
{
    public int Radius { get; set; }

    public ShapeCircle() : base(Shape2dType.Circle)
    {

    }

    public bool Equals(ShapeCircle? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Radius == other.Radius;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((ShapeCircle)obj);
    }

    public override int GetHashCode()
    {
        return Radius;
    }

    public override void Load(JsonElement element)
    {
        base.Load(element);
        Radius = element.GetProperty("radius").GetInt32();
    }

#if EDITOR
    public override void Save(JObject jObject)
    {
        base.Save(jObject);
        jObject.Add("radius", Radius);
    }

    public override string ToString()
    {
        return "Circle - " + Location + " - " + Radius;
    }
#endif
}