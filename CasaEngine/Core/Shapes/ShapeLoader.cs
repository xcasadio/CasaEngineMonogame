using System.Text.Json;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Serialization;

namespace CasaEngine.Core.Shapes;

public class ShapeLoader
{
    public static Shape2d LoadShape2d(JsonElement jsonElement)
    {
        var shapeType = jsonElement.GetProperty("shape_type").GetEnum<Shape2dType>();

        Shape2d shape = shapeType switch
        {
            Shape2dType.Compound => new Shape2dCompound(),
            Shape2dType.Rectangle => new ShapeRectangle(),
            Shape2dType.Circle => new ShapeCircle(),
            Shape2dType.Line => new ShapeLine(),
            Shape2dType.Polygone => new ShapePolygone(),
            _ => throw new InvalidOperationException($"{Enum.GetName(shapeType)} is not supported")
        };

        shape.Load(jsonElement);

        return shape;
    }

    public static Shape3d LoadShape3d(JsonElement jsonElement)
    {
        var shapeType = jsonElement.GetProperty("shape_type").GetEnum<Shape3dType>();

        Shape3d shape = shapeType switch
        {
            Shape3dType.Compound => new Shape3dCompound(),
            Shape3dType.Box => new Box(),
            Shape3dType.Capsule => new Capsule(),
            Shape3dType.Cylinder => new Cylinder(),
            Shape3dType.Sphere => new Sphere(),
            _ => throw new InvalidOperationException($"{Enum.GetName(shapeType)} is not supported")
        };

        shape.Load(jsonElement);

        return shape;
    }
}