using System.ComponentModel;

using CasaEngine.Core.Math;
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Rendering.Geometry;

public class ShapePolygone : Shape2d, IEquatable<ShapePolygone>
{
    private readonly List<Vector2> _points = new();

    [Browsable(false)]
    public List<Vector2> Points => _points;

    private bool _isABox;

    [Browsable(false)]
    public bool IsABox => _isABox;

    public override BoundingBox BoundingBox
    {
        get
        {
            var position = Position.ToVector3();
            Vector2 min = new Vector2(float.MaxValue);
            Vector2 max = new Vector2(float.MinValue);

            foreach (var point in _points)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            return new BoundingBox(position - min.ToVector3(), position + max.ToVector3());
        }
    }

    public ShapePolygone() : base(Shape2dType.Polygone)
    {

    }
    public bool Equals(ShapePolygone other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return _isABox == other._isABox && _points.Equals(other._points);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals((ShapePolygone)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_isABox, _points);
    }

    public override void Load(JObject element)
    {
        base.Load(element);
        _isABox = element["isABox"].GetBoolean();

        var pointsElement = element["points"];
        _points.Clear();

        foreach (var pointElement in pointsElement)
        {
            var point = pointElement.GetVector2();
            _points.Add(point);
        }
    }

    public event EventHandler OnPointAdded;
    public event EventHandler OnPointDeleted;

    public ShapePolygone(Vector2 p1, Vector2 p2, Vector2 p3)
        : base(Shape2dType.Polygone)
    {
        _points.Add(p1);
        _points.Add(p2);
        _points.Add(p3);
    }

    public void AddPoint(Vector2 p)
    {
        _points.Add(p);

        OnPointAdded?.Invoke(this, EventArgs.Empty);
    }

    public void AddPoint(int index, Vector2 p)
    {
        _points.Insert(index, p);

        OnPointAdded?.Invoke(this, EventArgs.Empty);
    }

    public void ModifyPoint(int index, Vector2 p)
    {
        _points[index] = p;
    }

    public void RemovePoint(Vector2 p)
    {
        _points.Remove(p);

        OnPointDeleted?.Invoke(this, EventArgs.Empty);
    }

    public void RemovePointAt(int index)
    {
        _points.RemoveAt(index);

        OnPointDeleted?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteAllPoints()
    {
        _points.Clear();

        OnPointDeleted?.Invoke(this, EventArgs.Empty);
    }

    public override string ToString() => $"{Enum.GetName(Type)} {_points.Count} point(s)";
}