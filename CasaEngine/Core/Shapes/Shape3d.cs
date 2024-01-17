using System.Text.Json;
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Shapes;

public abstract class Shape3d
{
    public event EventHandler<string> PropertyChanged;

    private Vector3 _position;
    private Quaternion _orientation;

    public Shape3dType Type { get; }

    public Vector3 Position
    {
        get => _position;
        set
        {
            if (value != _position)
            {
                OnPropertyChange(nameof(Position));
            }

            _position = value;
        }
    }

    public Quaternion Orientation
    {
        get => _orientation;
        set
        {
            if (value != _orientation)
            {
                OnPropertyChange(nameof(Orientation));
            }

            _orientation = value;
        }
    }

    protected Shape3d(Shape3dType type)
    {
        Type = type;
    }

    public virtual void Load(JsonElement element)
    {
        Position = element.GetProperty("position").GetVector3();
        Orientation = element.GetProperty("orientation").GetQuaternion();
    }

    protected void OnPropertyChange(string propertyName)
    {
        PropertyChanged?.Invoke(this, propertyName);
    }

#if EDITOR
    public virtual void Save(JObject jObject)
    {
        jObject.Add("version", 1);
        jObject.Add("shape_type", Type.ConvertToString());

        var newObject = new JObject();
        Position.Save(newObject);
        jObject.Add("position", newObject);

        newObject = new JObject();
        Orientation.Save(newObject);
        jObject.Add("orientation", newObject);
    }
#endif
}