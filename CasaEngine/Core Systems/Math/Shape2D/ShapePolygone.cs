using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;

#if EDITOR
using System.ComponentModel;
#endif

namespace CasaEngine.Math.Shape2D
{
    /// <summary>
    /// 
    /// </summary>
    public
#if EDITOR
 partial
#endif    
    class ShapePolygone
        : Shape2DObject
    {
        #region Fields

#if EDITOR
        List<Vector2> m_Points = new List<Vector2>();
#else
        Vector2[] m_Points;
#endif

        bool m_IsABox = false;

        #endregion

        #region Properties

#if !EDITOR
        /// <summary>
        /// Gets/Sets
        /// </summary>
        public Vector2[] Points
        {
            get { return m_Points; }
        }
#endif

        /// <summary>
        /// Gets
        /// </summary>
#if EDITOR
        [Browsable(false)]
#endif
        public bool IsABox
        {
            get { return m_IsABox; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        public ShapePolygone() { }

        /// <summary>
        /// 
        /// </summary>
        public ShapePolygone(ShapePolygone o_)
            : base(o_)
        {}

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Load(XmlElement el_, SaveOption option_)
        {
            base.Load(el_, option_);

            int version = int.Parse(el_.Attributes["version"].Value);

            if (version > 2)
            {
                m_IsABox = bool.Parse(el_.Attributes["isABox"].Value);
            }            

            XmlElement pointList = (XmlElement)el_.SelectSingleNode("PointList");

#if EDITOR
            m_Points.Clear();
#else
            m_Points = new Vector2[pointList.ChildNodes.Count];
            int i = 0;
#endif

            foreach (XmlNode node in pointList.ChildNodes)
            {
                Vector2 p = new Vector2();
                ((XmlElement)node).Read(ref p);
#if EDITOR
                m_Points.Add(p);
#else
                m_Points[i++] = p;
#endif
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override Shape2DObject Clone()
        {
            return new ShapePolygone(this);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ob_"></param>
        public override void CopyFrom(Shape2DObject ob_)
        {
            if (ob_ is ShapePolygone == false)
            {
                throw new ArgumentException("ShapePolygone.CopyFrom() : Shape2DObject is not a ShapePolygone");
            }

            base.CopyFrom(ob_);

            ShapePolygone s = (ShapePolygone)ob_;

#if EDITOR
            m_Points.AddRange(s.m_Points);
#else
            m_Points = new Vector2[s.m_Points.Length];
            s.m_Points.CopyTo(m_Points, 0);
#endif
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="x_"></param>
        public override void FlipHorizontally()
        {
#if EDITOR
            int c = m_Points.Count;
#else
            int c = m_Points.Length;
#endif

            for (int i = 0; i < c; i++)
            {
#if EDITOR
                m_Points[i] = new Vector2(-m_Points[i].X, m_Points[i].Y);
#else
                m_Points[i].X = -m_Points[i].X;
#endif
            }

            m_Points.Reverse();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="y_"></param>
        public override void FlipVertically()
        {
#if EDITOR
            int c = m_Points.Count;
#else
            int c = m_Points.Length;
#endif

            for (int i = 0; i < c; i++)
            {
#if EDITOR
                m_Points[i] = new Vector2(m_Points[i].X, -m_Points[i].Y);
#else
                m_Points[i].Y = -m_Points[i].Y;
#endif
            }
        }

        #endregion
    }
}
