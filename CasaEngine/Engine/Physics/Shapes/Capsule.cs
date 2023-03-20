namespace CasaEngine.Engine.Physics.Shapes;

public class Capsule : Shape
{
    public float Radius { get; set; }
    public float Length { get; set; }

    public Capsule() : base(ShapeType.Capsule)
    {
        Radius = 1.0f;
        Length = 1.0f;
    }
}