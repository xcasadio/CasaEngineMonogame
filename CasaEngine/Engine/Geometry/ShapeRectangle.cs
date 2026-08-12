
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Engine.Geometry;

public class ShapeRectangle : Shape2d, IEquatable<ShapeRectangle>
{
    public float Width { get; set; }
    public float Height { get; set; }

    public override BoundingBox BoundingBox
    {
        get
        {
            var halfSize = new Vector3(Width / 2f, Height / 2f, 0f);
            return new BoundingBox(-halfSize, halfSize);
        }
    }

    public ShapeRectangle(float width = 1, float height = 1) : base(Shape2dType.Rectangle)
    {
        Width = width;
        Height = height;
    }

    public bool Equals(ShapeRectangle other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Width == other.Width && Height == other.Height;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals((ShapeRectangle)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Width, Height);
    }

    public override void Load(JObject element)
    {
        base.Load(element);
        Width = element["w"].GetSingle();
        Height = element["h"].GetSingle();
    }

    public override string ToString() => $"{Enum.GetName(Type)} {{Width: {Width} Height:{Height}}}";
}