using Newtonsoft.Json.Linq;
using System.Text.Json;

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

    public override void Load(JsonElement element)
    {
        base.Load(element);
        _radius = element.GetProperty("radius").GetSingle();
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);
        jObject.Add("radius", Radius);
    }
#endif
}