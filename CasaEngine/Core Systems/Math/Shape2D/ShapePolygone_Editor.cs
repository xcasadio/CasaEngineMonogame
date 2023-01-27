using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using System.Xml;
using CasaEngineCommon.Extension;
using FarseerPhysics.Common.PolygonManipulation;
using FarseerPhysics.Common;
using System.IO;
using CasaEngineCommon.Design;
using System.ComponentModel;

namespace CasaEngine.Math.Shape2D
{
    /// <summary>
    /// 
    /// </summary>
    public partial class ShapePolygone
        : Shape2DObject
    {
        #region Fields

        public event EventHandler OnPointAdded;
        public event EventHandler OnPointDeleted;

        #endregion

        #region Properties

        /// <summary>
        /// Gets
        /// </summary>
#if EDITOR
        [Browsable(false)]
#endif
        public List<Vector2> PointList
        {
            get { return m_Points; }
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
#if EDITOR
        [Browsable(false)]
#endif
        public Vector2[] Points
        {
            get { return m_Points.ToArray(); }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        public ShapePolygone(Vector2 p1_, Vector2 p2_, Vector2 p3_)
            : base(Shape2DType.Polygone)
        {
            m_Points.Add(p1_);
            m_Points.Add(p2_);
            m_Points.Add(p3_);
        }

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="p_"></param>
        public void AddPoint(Vector2 p_)
        {
            m_Points.Add(p_);

            if (OnPointAdded != null)
            {
                OnPointAdded(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index_"></param>
        /// <param name="p_"></param>
        public void AddPoint(int index_, Vector2 p_)
        {
            m_Points.Insert(index_, p_);

            if (OnPointAdded != null)
            {
                OnPointAdded(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index_"></param>
        /// <param name="p_"></param>
        public void ModifyPoint(int index_, Vector2 p_)
        {
            m_Points[index_] = p_;

            /*if (OnPointAdded != null)
            {
                OnPointAdded(this, EventArgs.Empty);
            }*/
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="p_"></param>
        public void RemovePoint(Vector2 p_)
        {
            m_Points.Remove(p_);

            if (OnPointDeleted != null)
            {
                OnPointDeleted(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index_"></param>
        public void RemovePointAt(int index_)
        {
            m_Points.RemoveAt(index_);

            if (OnPointDeleted != null)
            {
                OnPointDeleted(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void DeleteAllPoints()
        {
            m_Points.Clear();

            if (OnPointDeleted != null)
            {
                OnPointDeleted(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void VerticesCorrection()
        {
            int i1, i2;

            Vertices v = new Vertices(m_Points);
            v = SimplifyTools.MergeIdenticalPoints(v);
            v = SimplifyTools.CollinearSimplify(v);
            m_Points.Clear();
            m_Points.AddRange(v);

            List<int> p = new List<int>();

            // Ensure the polygon is convex and the interior
            // is to the left of each edge.
            for (int i = 0; i < m_Points.Count; ++i)
            {
                i1 = i;
                i2 = i + 1 < m_Points.Count ? i + 1 : 0;

                Vector2 edge = m_Points[i2] - m_Points[i1];

                for (int j = 0; j < m_Points.Count; ++j)
                {
                    // Don't check vertices on the current edge.
                    if (j == i1 || j == i2)
                    {
                        continue;
                    }

                    Vector2 r = m_Points[j] - m_Points[i1];

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
            if (p.Count == m_Points.Count)
            {
                m_Points.Reverse();
            }
            else
            {
                /*Vector2 tmp;

                foreach (int i in p)
                {
                    i1 = i == 0 ? m_Points.Count - 1 : i - 1;
                    i2 = i == m_Points.Count - 1 ? 0 : i + 1;

                    tmp = m_Points[i1];
                    m_Points[i1] = m_Points[i2];
                    m_Points[i2] = tmp;
                }*/
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="o_"></param>
        /// <returns></returns>
        public override bool CompareTo(Shape2DObject o_)
        {
            if (o_ is ShapePolygone)
            {
                ShapePolygone p = (ShapePolygone)o_;

                if (m_IsABox != p.m_IsABox)
                {
                    return false;
                }

                if (m_Points.Count != p.m_Points.Count)
                {
                    return false;
                }

                for (int i = 0; i < m_Points.Count; i++)
                {
                    if (m_Points[i] != p.m_Points[i])
                    {
                        return false;
                    }
                }

                return base.CompareTo(o_);
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Save(XmlElement el_, SaveOption option_)
        {
            base.Save(el_, option_);

            XmlElement box = el_.OwnerDocument.CreateElementWithText("IsABox", m_IsABox.ToString());
            el_.AppendChild(box);

            XmlElement pointList = el_.OwnerDocument.CreateElement("PointList");
            el_.AppendChild(pointList);

            foreach (Vector2 p in m_Points)
            {
                XmlElement point = el_.OwnerDocument.CreateElement("Point", p);
                pointList.AppendChild(point);
            }            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Save(BinaryWriter bw_, SaveOption option_)
        {
            base.Save(bw_, option_);

            //VerticesCorrection();

            bw_.Write(m_IsABox);
            bw_.Write(m_Points.Count);

            foreach (Vector2 p in m_Points)
            {
                bw_.Write(p);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "Polygone";
        }

        #endregion
    }
}
