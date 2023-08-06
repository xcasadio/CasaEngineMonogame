using System.Text.Json;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Shapes;

public class ShapeRectangle : Shape2d, IEquatable<ShapeRectangle>
{
    public int Width { get; set; }
    public int Height { get; set; }

    public ShapeRectangle() : base(Shape2dType.Rectangle)
    {

    }

    public ShapeRectangle(int x, int y, int w, int h)
        : this()
    {
        Position = new Vector2(x, y);
        Width = w;
        Height = h;
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

    public override void Load(JsonElement element)
    {
        base.Load(element);
        Width = element.GetProperty("w").GetInt32();
        Height = element.GetProperty("h").GetInt32();
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