﻿using System.ComponentModel;

using CasaEngine.Core.Helpers;
using CasaEngine.Core.Maths;
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Shapes;

public class ShapePolygone : Shape2d, IEquatable<ShapePolygone>
{
#if !EDITOR
    private Vector2[] _points;

    public Vector2[] Points => _points;
#endif

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
    public bool Equals(ShapePolygone? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return _isABox == other._isABox && _points.Equals(other._points);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
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

#if EDITOR
        _points.Clear();
#else
        _points = new Vector2[pointsElement.Children().Count()];
        int i = 0;
#endif

        foreach (var pointElement in pointsElement)
        {
            var point = pointElement.GetVector2();

#if EDITOR
            _points.Add(point);
#else
            _points[i++] = point;
#endif
        }
    }

#if EDITOR
    private readonly List<Vector2> _points = new();

    public event EventHandler? OnPointAdded;
    public event EventHandler? OnPointDeleted;

    [Browsable(false)]
    public List<Vector2> Points => _points;

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

    private void VerticesCorrection()
    {
        var v = new List<Vector2>(_points);
        v = PolygonSimplifyTools.MergeIdenticalPoints(v);
        v = PolygonSimplifyTools.CollinearSimplify(v);
        _points.Clear();
        _points.AddRange(v);

        var p = new List<int>();

        // Ensure the polygon is convex and the interior
        // is to the left of each edge.
        for (var i = 0; i < _points.Count; ++i)
        {
            var i1 = i;
            var i2 = i + 1 < _points.Count ? i + 1 : 0;

            var edge = _points[i2] - _points[i1];

            for (var j = 0; j < _points.Count; ++j)
            {
                // Don't check vertices on the current edge.
                if (j == i1 || j == i2)
                {
                    continue;
                }

                var r = _points[j] - _points[i1];

                // Your polygon is non-convex (it has an indentation) or
                // has colinear edges.
                var s = edge.X * r.Y - edge.Y * r.X;

                if (s > 0.0f && p.Contains(i1) == false)
                {
                    p.Add(i1);
                }
            }
        }

        //normal a gauche a l'interieur du polygone
        if (p.Count == _points.Count)
        {
            _points.Reverse();
        }
        else
        {
            /*Vector2 tmp;

            foreach (int i in p)
            {
                i1 = i == 0 ? _Points.Count - 1 : i - 1;
                i2 = i == _Points.Count - 1 ? 0 : i + 1;

                tmp = _Points[i1];
                _Points[i1] = _Points[i2];
                _Points[i2] = tmp;
            }*/
        }
    }

    public override void Save(JObject jObject)
    {
        base.Save(jObject);
        jObject.Add("isABox", _isABox);

        var pointListNode = new JArray();

        foreach (var point in _points)
        {
            var newObject = new JObject();
            point.Save(newObject);
            pointListNode.Add(newObject);
        }

        jObject.Add("points", pointListNode);
    }

    public override string ToString() => $"{Enum.GetName(Type)} {_points.Count} point(s)";
#endif
}