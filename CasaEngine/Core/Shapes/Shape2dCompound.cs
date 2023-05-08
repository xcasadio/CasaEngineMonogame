using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Shapes;

public class Shape2dCompound : Shape2d, IEquatable<Shape2dCompound>
{
    public List<Shape2d> Shapes { get; } = new();

    public Shape2dCompound() : base(Shape2dType.Compound)
    {

    }
    public bool Equals(Shape2dCompound? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Shapes.Equals(other.Shapes);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((Shape2dCompound)obj);
    }

    public override int GetHashCode()
    {
        return Shapes.GetHashCode();
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);
        var shapesJArray = new JArray();

        foreach (var entity in Shapes)
        {
            JObject entityObject = new();
            entity.Save(entityObject);
            shapesJArray.Add(entity);
        }

        jObject.Add("shapes", shapesJArray);
    }
#endif
}