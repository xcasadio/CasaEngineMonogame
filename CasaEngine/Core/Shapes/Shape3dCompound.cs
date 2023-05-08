using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Shapes;

public class Shape3dCompound : Shape3d
{
    public List<Shape3d> Shapes { get; } = new();

    public Shape3dCompound() : base(Shape3dType.Compound)
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