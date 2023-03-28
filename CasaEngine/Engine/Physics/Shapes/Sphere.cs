namespace CasaEngine.Engine.Physics.Shapes;

public class Sphere : Shape
{
    private float _radius;

    public float Radius
    {
        get => _radius;
        set
        {
            _radius = value;
#if EDITOR
            OnPropertyChanged();
#endif
        }
    }

    public Sphere() : base(ShapeType.Sphere)
    {
        Radius = 1f;
    }
}