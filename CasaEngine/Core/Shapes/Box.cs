using System.Text.Json;
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
            var position = Position;
            var halfSize = Size / 2f;
            return new BoundingBox(position - halfSize, position + halfSize);
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
        return Type == other.Type && Position.Equals(other.Position) && Orientation.Equals(other.Orientation) && Size.Equals(other.Size);
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
        return HashCode.Combine((int)Type, Position, Orientation, Size);
    }

    public override string ToString() => $"{Enum.GetName(Type)} {{Width: {Size.X} Height:{Size.Y} Length:{Size}}}";

    public override void Load(JsonElement element)
    {
        base.Load(element);
        var w = element.GetProperty("w").GetSingle();
        var h = element.GetProperty("h").GetSingle();
        var l = element.GetProperty("l").GetSingle();

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