using CasaEngine.Core.Helpers;
using Newtonsoft.Json.Linq;
using System.Text.Json;

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

    public override void Load(JsonElement element)
    {
        base.Load(element);
        _width = element.GetProperty("width").GetSingle();
        _height = element.GetProperty("height").GetSingle();
        _length = element.GetProperty("length").GetSingle();
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);
        jObject.Add("width", _width);
        jObject.Add("height", _height);
        jObject.Add("length", _length);
    }
#endif
}