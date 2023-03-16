using System.ComponentModel;
using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Extension;
using Genbox.VelcroPhysics.Shared;
using Genbox.VelcroPhysics.Tools.PolygonManipulation;
using Microsoft.Xna.Framework;

namespace CasaEngine.Core.Maths.Shape2D
{
    public class ShapePolygone : Shape2DObject
    {

#if EDITOR
        private readonly List<Vector2> _points = new();
#else
        Vector2[] _points;
#endif

        private bool _isABox;



#if !EDITOR
        public Vector2[] Points => _points;
#endif

#if EDITOR
        [Browsable(false)]
#endif
        public bool IsABox => _isABox;


        public ShapePolygone() { }

        public ShapePolygone(ShapePolygone o)
            : base(o)
        { }



        public override void Load(XmlElement el, SaveOption option)
        {
            base.Load(el, option);

            var version = int.Parse(el.Attributes["version"].Value);

            if (version > 2)
            {
                _isABox = bool.Parse(el.Attributes["isABox"].Value);
            }

            var pointList = (XmlElement)el.SelectSingleNode("PointList");

#if EDITOR
            _points.Clear();
#else
            _points = new Vector2[pointList.ChildNodes.Count];
            int i = 0;
#endif

            foreach (XmlNode node in pointList.ChildNodes)
            {
                var p = new Vector2();
                ((XmlElement)node).Read(ref p);
#if EDITOR
                _points.Add(p);
#else
                _points[i++] = p;
#endif
            }
        }

        public override Shape2DObject Clone()
        {
            return new ShapePolygone(this);
        }

        public override void CopyFrom(Shape2DObject ob)
        {
            if (ob is ShapePolygone == false)
            {
                throw new ArgumentException("ShapePolygone.CopyFrom() : Shape2DObject is not a ShapePolygone");
            }

            base.CopyFrom(ob);

            var s = (ShapePolygone)ob;

#if EDITOR
            _points.AddRange(s._points);
#else
            _points = new Vector2[s._points.Length];
            s._points.CopyTo(_points, 0);
#endif
        }

        public override void FlipHorizontally()
        {
#if EDITOR
            var c = _points.Count;
#else
            int c = _points.Length;
#endif

            for (var i = 0; i < c; i++)
            {
#if EDITOR
                _points[i] = new Vector2(-_points[i].X, _points[i].Y);
#else
                _points[i].X = -_points[i].X;
#endif
            }

            _points.Reverse();
        }

        public override void FlipVertically()
        {
#if EDITOR
            var c = _points.Count;
#else
            int c = _points.Length;
#endif

            for (var i = 0; i < c; i++)
            {
#if EDITOR
                _points[i] = new Vector2(_points[i].X, -_points[i].Y);
#else
                _points[i].Y = -_points[i].Y;
#endif
            }
        }

#if EDITOR
        public event EventHandler? OnPointAdded;
        public event EventHandler? OnPointDeleted;

#if EDITOR
        [Browsable(false)]
#endif
        public List<Vector2> PointList
        {
            get { return _points; }
        }

#if EDITOR
        [Browsable(false)]
#endif
        public Vector2[] Points
        {
            get { return _points.ToArray(); }
        }

        public ShapePolygone(Vector2 p1, Vector2 p2, Vector2 p3)
            : base(Shape2DType.Polygone)
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

            /*if (OnPointAdded != null)
            {
                OnPointAdded(this, EventArgs.Empty);
            }*/
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
            var v = new Vertices(_points);
            v = SimplifyTools.MergeIdenticalPoints(v);
            v = SimplifyTools.CollinearSimplify(v);
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

        public override bool CompareTo(Shape2DObject o)
        {
            if (o is ShapePolygone polygone)
            {
                if (_isABox != polygone._isABox)
                {
                    return false;
                }

                if (_points.Count != polygone._points.Count)
                {
                    return false;
                }

                for (var i = 0; i < _points.Count; i++)
                {
                    if (_points[i] != polygone._points[i])
                    {
                        return false;
                    }
                }

                return base.CompareTo(polygone);
            }

            return false;
        }

        public override void Save(XmlElement el, SaveOption option)
        {
            base.Save(el, option);

            var box = el.OwnerDocument.CreateElementWithText("IsABox", _isABox.ToString());
            el.AppendChild(box);

            var pointList = el.OwnerDocument.CreateElement("PointList");
            el.AppendChild(pointList);

            foreach (var p in _points)
            {
                var point = el.OwnerDocument.CreateElement("Point", p);
                pointList.AppendChild(point);
            }
        }

        public override void Save(BinaryWriter bw, SaveOption option)
        {
            base.Save(bw, option);

            //VerticesCorrection();

            bw.Write(_isABox);
            bw.Write(_points.Count);

            foreach (var p in _points)
            {
                bw.Write(p);
            }
        }

        public override string ToString()
        {
            return "Polygone";
        }
#endif
    }
}
