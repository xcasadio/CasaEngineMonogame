namespace CasaEngine.Engine.Physics.Shapes;

public class Sphere : Shape
{
    public float Radius { get; set; }

    public Sphere() : base(ShapeType.Sphere)
    {
        Radius = 1f;
    }
}