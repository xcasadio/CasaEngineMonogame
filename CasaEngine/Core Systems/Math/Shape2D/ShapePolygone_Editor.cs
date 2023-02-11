using Microsoft.Xna.Framework;
using System.Xml;
using CasaEngineCommon.Extension;
using FarseerPhysics.Common.PolygonManipulation;
using FarseerPhysics.Common;
using CasaEngineCommon.Design;
using System.ComponentModel;

namespace CasaEngine.Math.Shape2D
{
    public partial class ShapePolygone
        : Shape2DObject
    {

        public event EventHandler OnPointAdded;
        public event EventHandler OnPointDeleted;



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

            if (OnPointAdded != null)
            {
                OnPointAdded(this, EventArgs.Empty);
            }
        }

        public void AddPoint(int index, Vector2 p)
        {
            _points.Insert(index, p);

            if (OnPointAdded != null)
            {
                OnPointAdded(this, EventArgs.Empty);
            }
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

            if (OnPointDeleted != null)
            {
                OnPointDeleted(this, EventArgs.Empty);
            }
        }

        public void RemovePointAt(int index)
        {
            _points.RemoveAt(index);

            if (OnPointDeleted != null)
            {
                OnPointDeleted(this, EventArgs.Empty);
            }
        }

        public void DeleteAllPoints()
        {
            _points.Clear();

            if (OnPointDeleted != null)
            {
                OnPointDeleted(this, EventArgs.Empty);
            }
        }

        private void VerticesCorrection()
        {
            int i1, i2;

            Vertices v = new Vertices(_points);
            v = SimplifyTools.MergeIdenticalPoints(v);
            v = SimplifyTools.CollinearSimplify(v);
            _points.Clear();
            _points.AddRange(v);

            List<int> p = new List<int>();

            // Ensure the polygon is convex and the interior
            // is to the left of each edge.
            for (int i = 0; i < _points.Count; ++i)
            {
                i1 = i;
                i2 = i + 1 < _points.Count ? i + 1 : 0;

                Vector2 edge = _points[i2] - _points[i1];

                for (int j = 0; j < _points.Count; ++j)
                {
                    // Don't check vertices on the current edge.
                    if (j == i1 || j == i2)
                    {
                        continue;
                    }

                    Vector2 r = _points[j] - _points[i1];

                    // Your polygon is non-convex (it has an indentation) or
                    // has colinear edges.
                    float s = edge.X * r.Y - edge.Y * r.X;

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
            if (o is ShapePolygone)
            {
                ShapePolygone p = (ShapePolygone)o;

                if (_isABox != p._isABox)
                {
                    return false;
                }

                if (_points.Count != p._points.Count)
                {
                    return false;
                }

                for (int i = 0; i < _points.Count; i++)
                {
                    if (_points[i] != p._points[i])
                    {
                        return false;
                    }
                }

                return base.CompareTo(o);
            }

            return false;
        }

        public override void Save(XmlElement el, SaveOption option)
        {
            base.Save(el, option);

            XmlElement box = el.OwnerDocument.CreateElementWithText("IsABox", _isABox.ToString());
            el.AppendChild(box);

            XmlElement pointList = el.OwnerDocument.CreateElement("PointList");
            el.AppendChild(pointList);

            foreach (Vector2 p in _points)
            {
                XmlElement point = el.OwnerDocument.CreateElement("Point", p);
                pointList.AppendChild(point);
            }
        }

        public override void Save(BinaryWriter bw, SaveOption option)
        {
            base.Save(bw, option);

            //VerticesCorrection();

            bw.Write(_isABox);
            bw.Write(_points.Count);

            foreach (Vector2 p in _points)
            {
                bw.Write(p);
            }
        }

        public override string ToString()
        {
            return "Polygone";
        }

    }
}
