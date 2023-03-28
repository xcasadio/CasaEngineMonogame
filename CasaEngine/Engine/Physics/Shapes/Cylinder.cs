using Newtonsoft.Json.Linq;

namespace CasaEngine.Engine.Physics.Shapes;

public class Cylinder : Shape
{
    private float _radius;
    private float _length;

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

    public Cylinder() : base(ShapeType.Cylinder)
    {
        Radius = 1f;
        Length = 1f;
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);
        jObject.Add("radius", Radius);
        jObject.Add("length", _length);
    }
#endif
}