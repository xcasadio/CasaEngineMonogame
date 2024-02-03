using System.Text.Json;
using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Shapes;

public class ShapeCircle : Shape2d, IEquatable<ShapeCircle>
{
    public float Radius { get; set; } = 1f;

    public override BoundingBox BoundingBox
    {
        get
        {
            var position = Position.ToVector3();
            var radiusVector = new Vector3(Radius);
            return new BoundingBox(position - radiusVector, position + radiusVector);
        }
    }

    public ShapeCircle() : base(Shape2dType.Circle)
    {

    }

    public ShapeCircle(int radius) : this()
    {
        Radius = radius;
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
        if (obj.GetType() != GetType()) return false;
        return Equals((ShapeCircle)obj);
    }

    public override string ToString() => $"{Enum.GetName(Type)} {{Radius: {Radius}}}";

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