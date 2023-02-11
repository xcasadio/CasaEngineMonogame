using Microsoft.Xna.Framework;
using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;

#if EDITOR
using System.ComponentModel;
#endif

namespace CasaEngine.Math.Shape2D
{
    public
#if EDITOR
 partial
#endif    
    class ShapePolygone
        : Shape2DObject
    {

#if EDITOR
        readonly List<Vector2> _points = new List<Vector2>();
#else
        Vector2[] _Points;
#endif

        bool _isABox = false;



#if !EDITOR
        public Vector2[] Points
        {
            get { return _Points; }
        }
#endif

#if EDITOR
        [Browsable(false)]
#endif
        public bool IsABox
        {
            get { return _isABox; }
        }



        public ShapePolygone() { }

        public ShapePolygone(ShapePolygone o)
            : base(o)
        { }



        public override void Load(XmlElement el, SaveOption option)
        {
            base.Load(el, option);

            int version = int.Parse(el.Attributes["version"].Value);

            if (version > 2)
            {
                _isABox = bool.Parse(el.Attributes["isABox"].Value);
            }

            XmlElement pointList = (XmlElement)el.SelectSingleNode("PointList");

#if EDITOR
            _points.Clear();
#else
            _Points = new Vector2[pointList.ChildNodes.Count];
            int i = 0;
#endif

            foreach (XmlNode node in pointList.ChildNodes)
            {
                Vector2 p = new Vector2();
                ((XmlElement)node).Read(ref p);
#if EDITOR
                _points.Add(p);
#else
                _Points[i++] = p;
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

            ShapePolygone s = (ShapePolygone)ob;

#if EDITOR
            _points.AddRange(s._points);
#else
            _Points = new Vector2[s._Points.Length];
            s._Points.CopyTo(_Points, 0);
#endif
        }

        public override void FlipHorizontally()
        {
#if EDITOR
            int c = _points.Count;
#else
            int c = _Points.Length;
#endif

            for (int i = 0; i < c; i++)
            {
#if EDITOR
                _points[i] = new Vector2(-_points[i].X, _points[i].Y);
#else
                _Points[i].X = -_Points[i].X;
#endif
            }

            _points.Reverse();
        }

        public override void FlipVertically()
        {
#if EDITOR
            int c = _points.Count;
#else
            int c = _Points.Length;
#endif

            for (int i = 0; i < c; i++)
            {
#if EDITOR
                _points[i] = new Vector2(_points[i].X, -_points[i].Y);
#else
                _Points[i].Y = -_Points[i].Y;
#endif
            }
        }

    }
}
