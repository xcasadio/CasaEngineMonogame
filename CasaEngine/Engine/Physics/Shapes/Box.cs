namespace CasaEngine.Engine.Physics.Shapes;

public class Box : Shape
{
    private float _width;
    private float _height;
    private float _length;

    public float Width
    {
        get => _width;
        set
        {
            _width = value;
#if EDITOR
            OnPropertyChanged();
#endif
        }
    }

    public float Height
    {
        get => _height;
        set
        {
            _height = value;
#if EDITOR
            OnPropertyChanged();
#endif
        }
    }

    public float Length
    {
        get => _length;
        set
        {
            _length = value;
#if EDITOR
            OnPropertyChanged();
#endif
        }
    }

    public Box() : base(ShapeType.Box)
    {
        Width = 1.0f;
        Height = 1.0f;
        Length = 1.0f;
    }
}