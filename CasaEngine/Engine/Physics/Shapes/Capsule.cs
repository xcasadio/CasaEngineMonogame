using Newtonsoft.Json.Linq;

namespace CasaEngine.Engine.Physics.Shapes;

public class Capsule : Shape
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

    public Capsule() : base(ShapeType.Capsule)
    {
        Radius = 1.0f;
        Length = 1.0f;
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