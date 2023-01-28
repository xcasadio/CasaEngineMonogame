using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;
using CasaEngineCommon.Pool;

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
    class ShapeCircle
        : Shape2DObject
    {

        int m_Radius;



        /// <summary>
        /// Gets/Sets
        /// </summary>
#if EDITOR
        [Category("Shape Circle")]
#endif
        public int Radius
        {
            get { return m_Radius; }
            set
            {
                m_Radius = value;
#if EDITOR
                NotifyPropertyChanged("Radius");
#endif
            }
        }



        /// <summary>
        /// 
        /// </summary>
        public ShapeCircle() { }

        /// <summary>
        /// 
        /// </summary>
        public ShapeCircle(ShapeCircle o_)
            : base(o_)
        { }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Load(XmlElement el_, SaveOption option_)
        {
            base.Load(el_, option_);
            m_Radius = int.Parse(el_.Attributes["radius"].Value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override Shape2DObject Clone()
        {
            return new ShapeCircle(this);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ob_"></param>
        public override void CopyFrom(Shape2DObject ob_)
        {
            if (ob_ is ShapeCircle == false)
            {
                throw new ArgumentException("ShapeCircle.CopyFrom() : Shape2DObject is not a ShapeCircle");
            }

            base.CopyFrom(ob_);
            m_Radius = ((ShapeCircle)ob_).m_Radius;
        }



        // Pool for this type of components.
        /*private static readonly Pool<ShapeCircle> componentPool = new Pool<ShapeCircle>(20);

        /// <summary>
        /// Pool for this type of components.
        /// </summary>
        internal static Pool<ShapeCircle> ComponentPool { get { return componentPool; } }*/

    }
}
