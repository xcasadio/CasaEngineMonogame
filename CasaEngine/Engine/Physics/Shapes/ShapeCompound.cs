using Newtonsoft.Json.Linq;

namespace CasaEngine.Engine.Physics.Shapes;

public class ShapeCompound : Shape
{
    public List<Shape> Shapes { get; } = new();

    public ShapeCompound() : base(ShapeType.Compound)
    {

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