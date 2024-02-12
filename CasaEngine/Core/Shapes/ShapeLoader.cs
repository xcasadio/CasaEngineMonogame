
using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Shapes;

public class ShapeLoader
{
    public static Shape2d LoadShape2d(JObject JObject)
    {
        var shapeType = JObject["shape_type"].GetEnum<Shape2dType>();

        Shape2d shape = shapeType switch
        {
            Shape2dType.Compound => new Shape2dCompound(),
            Shape2dType.Rectangle => new ShapeRectangle(),
            Shape2dType.Circle => new ShapeCircle(),
            Shape2dType.Line => new ShapeLine(),
            Shape2dType.Polygone => new ShapePolygone(),
            _ => throw new InvalidOperationException($"{Enum.GetName(shapeType)} is not supported")
        };

        shape.Load(JObject);

        return shape;
    }

    public static Shape3d LoadShape3d(JObject JObject)
    {
        var shapeType = JObject["shape_type"].GetEnum<Shape3dType>();

        Shape3d shape = shapeType switch
        {
            Shape3dType.Compound => new Shape3dCompound(),
            Shape3dType.Box => new Box(),
            Shape3dType.Capsule => new Capsule(),
            Shape3dType.Cylinder => new Cylinder(),
            Shape3dType.Sphere => new Sphere(),
            _ => throw new InvalidOperationException($"{Enum.GetName(shapeType)} is not supported")
        };

        shape.Load(JObject);

        return shape;
    }
}