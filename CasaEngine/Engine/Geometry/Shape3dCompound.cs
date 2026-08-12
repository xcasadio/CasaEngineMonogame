
using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Geometry;

public class Shape3dCompound : Shape3d
{
    public List<Shape3d> Shapes { get; } = new();

    public override BoundingBox BoundingBox
    {
        get
        {
            var boundingBox = new BoundingBox();

            foreach (var shape in Shapes)
            {
                boundingBox.ExpandBy(shape.BoundingBox);
            }

            return boundingBox;
        }
    }

    public Shape3dCompound() : base(Shape3dType.Compound)
    {

    }

}