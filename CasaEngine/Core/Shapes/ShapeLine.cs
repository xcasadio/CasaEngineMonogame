
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Shapes;

public class ShapeLine : Shape2d, IEquatable<ShapeLine>
{
    public Point Start { get; set; }
    public Point End { get; set; }

    public override BoundingBox BoundingBox
    {
        get
        {
            var position = Position.ToVector3();
            return new BoundingBox(position - new Vector3(Start.X, Start.Y, 0f), position + new Vector3(End.X, End.Y, 0.1f));
        }
    }

    public ShapeLine() : base(Shape2dType.Line)
    {

    }

    public bool Equals(ShapeLine other)
    {
        return Start.Equals(other.Start) && End.Equals(other.End);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
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

#if EDITOR
    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        var newObject = new JObject();
        Start.Save(newObject);
        jObject.Add("start", newObject);

        newObject = new JObject();
        End.Save(newObject);
        jObject.Add("end", newObject);
    }
#endif
}