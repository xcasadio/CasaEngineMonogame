namespace CasaEngine.Engine.Physics.Shapes;

public class ShapeCompound : Shape
{
    public List<Shape> Shapes { get; } = new();

    public ShapeCompound() : base(ShapeType.Compound)
    {

    }
}