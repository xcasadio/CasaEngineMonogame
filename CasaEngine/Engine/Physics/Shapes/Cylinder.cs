namespace CasaEngine.Engine.Physics.Shapes;

public class Cylinder : Shape
{
    public float Radius { get; set; }
    public float Length { get; set; }

    public Cylinder() : base(ShapeType.Cylinder)
    {
        Radius = 1f;
        Length = 1f;
    }
}