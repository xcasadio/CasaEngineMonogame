using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Shapes;

public class Capsule : Shape3d, IEquatable<Capsule>
{
    public float Radius { get; set; }
    public float Length { get; set; }

    public Capsule() : base(Shape3dType.Capsule)
    {
        Radius = 1.0f;
        Length = 1.0f;
    }

    public bool Equals(Capsule? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Type == other.Type && Position.Equals(other.Position) && Orientation.Equals(other.Orientation) && Radius.Equals(other.Radius) && Length.Equals(other.Length);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((Capsule)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine((int)Type, Position, Orientation, Radius, Length);
    }

    public override string ToString() => $"{Enum.GetName(Type)} {{Radius: {Radius} Length:{Length}}}";

    public override void Load(JsonElement element)
    {
        base.Load(element);
        Radius = element.GetProperty("radius").GetSingle();
        Length = element.GetProperty("length").GetSingle();
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);
        jObject.Add("radius", Radius);
        jObject.Add("length", Length);
    }
#endif
}