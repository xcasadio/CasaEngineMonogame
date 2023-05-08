using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Shapes;

public class Sphere : Shape3d
{
    public float Radius { get; set; }

    public Sphere() : base(Shape3dType.Sphere)
    {
        Radius = 1f;
    }

    public bool Equals(Sphere? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Type == other.Type && Position.Equals(other.Position) && Orientation.Equals(other.Orientation) && Radius.Equals(other.Radius);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((Sphere)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine((int)Type, Position, Orientation, Radius);
    }

    public override void Load(JsonElement element)
    {
        base.Load(element);
        Radius = element.GetProperty("radius").GetSingle();
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);
        jObject.Add("radius", Radius);
    }
#endif
}