
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Shapes;

public class Box : Shape3d, IEquatable<Box>
{
    private Vector3 _size;

    public Vector3 Size
    {
        get => _size;
        set
        {
            if (value != _size)
            {
                OnPropertyChange(nameof(Size));
            }
            _size = value;
        }
    }

    public override BoundingBox BoundingBox
    {
        get
        {
            var halfSize = Size / 2f;
            return new BoundingBox(halfSize, halfSize);
        }
    }

    public Box() : base(Shape3dType.Box)
    {
        Size = Vector3.One;
    }

    public bool Equals(Box? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Type == other.Type && Size.Equals(other.Size);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((Box)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine((int)Type, Size);
    }

    public override string ToString() => $"{Enum.GetName(Type)} {{Width: {Size.X} Height:{Size.Y} Length:{Size}}}";

    public override void Load(JObject element)
    {
        base.Load(element);
        var w = element["w"].GetSingle();
        var h = element["h"].GetSingle();
        var l = element["l"].GetSingle();

        Size = new Vector3(w, h, l);
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);
        jObject.Add("w", Size.X);
        jObject.Add("h", Size.Y);
        jObject.Add("l", Size.Z);
    }
#endif
}