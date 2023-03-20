namespace CasaEngine.Engine.Physics.Shapes;

public class Box : Shape
{
    public float Width { get; set; }
    public float Height { get; set; }
    public float Length { get; set; }

    public Box() : base(ShapeType.Box)
    {
        Width = 1.0f;
        Height = 1.0f;
        Length = 1.0f;
    }
}