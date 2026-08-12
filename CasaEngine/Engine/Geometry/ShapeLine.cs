
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Engine.Geometry;

public class ShapeLine : Shape2d, IEquatable<ShapeLine>
{
    public Point Start { get; set; }
    public Point End { get; set; }

    public override BoundingBox BoundingBox
    {
        get
        {
            var start = new Vector3(Start.X, Start.Y, 0f);
            var end = new Vector3(End.X, End.Y, 0f);
            return new BoundingBox(Vector3.Min(start, end), Vector3.Max(start, end));
        }
    }

    public ShapeLine() : base(Shape2dType.Line)
    {

    }

    public bool Equals(ShapeLine other)
    {
        return Start.Equals(other.Start) && End.Equals(other.End);
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

        return Equals((ShapeLine)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Start, End);
    }

    public override string ToString() => $"{Enum.GetName(Type)} {{Start: {Start} End:{End}}}";

    public override void Load(JObject element)
    {
        base.Load(element);
        Start = element["start"].GetPoint();
        End = element["end"].GetPoint();
    }

}