using Newtonsoft.Json.Linq;

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

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);
        jObject.Add("radius", Radius);
    }
#endif
}