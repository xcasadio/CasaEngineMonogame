
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Shapes;

public class Cylinder : Shape3d, IEquatable<Cylinder>
{
    private float _length;

    public float Radius { get; set; }
    public float Length { get; set; }

    public override BoundingBox BoundingBox
    {
        get
        {
            var halfSize = new Vector3(Length / 2f, Radius, Radius); // X oriented
            return new BoundingBox(
                new Vector3(-Length / 2f, Radius, Radius),
                new Vector3(Length / 2f, Radius, Radius));
        }
    }

    public Cylinder() : base(Shape3dType.Cylinder)
    {
        Radius = 0.5f;
        Length = 1f;
    }

    public bool Equals(Cylinder? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Type == other.Type && Radius.Equals(other.Radius) && Length.Equals(other.Length);
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
        return HashCode.Combine((int)Type, Radius, Length);
    }

    public override string ToString() => $"{Enum.GetName(Type)} {{Radius: {Radius} Length:{Length}}}";

    public override void Load(JObject element)
    {
        base.Load(element);
        Radius = element["radius"].GetSingle();
        _length = element["length"].GetSingle();
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