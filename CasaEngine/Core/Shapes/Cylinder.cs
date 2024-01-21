using System.Text.Json;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Shapes;

public class Cylinder : Shape3d, IEquatable<Cylinder>
{
    private float _radius;
    private float _length;

    public float Radius { get; set; }
    public float Length { get; set; }

    public override BoundingBox BoundingBox
    {
        get
        {
            var position = Position;
            var halfSize = new Vector3(Length / 2f + Radius, Radius, Radius); // X oriented
            return new BoundingBox(position - halfSize, position + halfSize);
        }
    }

    public Cylinder() : base(Shape3dType.Cylinder)
    {
        Radius = 1f;
        Length = 1f;
    }

    public bool Equals(Cylinder? other)
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
        return Equals((Cylinder)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine((int)Type, Position, Orientation, Radius, Length);
    }

    public override string ToString() => $"{Enum.GetName(Type)} {{Radius: {Radius} Length:{Length}}}";

    public override void Load(JsonElement element)
    {
        base.Load(element);
        _radius = element.GetProperty("radius").GetSingle();
        _length = element.GetProperty("length").GetSingle();
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);
        jObject.Add("radius", Radius);
        jObject.Add("length", _length);
    }
#endif
}