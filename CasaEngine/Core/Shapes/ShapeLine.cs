using System.Text.Json;
using CasaEngine.Core.Helpers;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Shapes;

public class ShapeLine : Shape2d, IEquatable<ShapeLine>
{
    public Point Start { get; set; }
    public Point End { get; set; }

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
        if (obj.GetType() != this.GetType()) return false;
        return Equals((ShapeLine)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Start, End);
    }

    public override string ToString() => $"{Enum.GetName(Type)} {{Start: {Start} End:{End}}}";

    public override void Load(JsonElement element)
    {
        base.Load(element);
        Start = element.GetProperty("start").GetPoint();
        End = element.GetProperty("end").GetPoint();
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