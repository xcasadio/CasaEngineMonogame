
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Shapes;

public class ShapeRectangle : Shape2d, IEquatable<ShapeRectangle>
{
    public int Width { get; set; }
    public int Height { get; set; }

    public override BoundingBox BoundingBox
    {
        get
        {
            var position = Position.ToVector3();
            var halfSize = new Vector3(Width / 2f, Height / 2f, 0f);
            return new BoundingBox(position - halfSize, position + halfSize);
        }
    }

    public ShapeRectangle(int width = 1, int height = 1) : base(Shape2dType.Rectangle)
    {
        Width = width;
        Height = height;
    }

    public ShapeRectangle(int x, int y, int w, int h)
        : this(w, h)
    {
        Position = new Vector2(x, y);
    }

    public bool Equals(ShapeRectangle? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Width == other.Width && Height == other.Height;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((ShapeRectangle)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Width, Height);
    }

    public override void Load(JObject element)
    {
        base.Load(element);
        Width = element["w"].GetInt32();
        Height = element["h"].GetInt32();
    }

#if EDITOR
    public override void Save(JObject jObject)
    {
        base.Save(jObject);
        jObject.Add("w", Width);
        jObject.Add("h", Height);
    }

    public override string ToString() => $"{Enum.GetName(Type)} {{X: {Position.X} Y:{Position.Y} Width:{Width} Height:{Height}}}";
#endif
}