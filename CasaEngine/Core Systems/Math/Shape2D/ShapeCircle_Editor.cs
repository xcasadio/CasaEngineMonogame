using System;




using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using System.Xml;
using CasaEngineCommon.Extension;
using System.IO;
using CasaEngineCommon.Design;


namespace CasaEngine.Math.Shape2D
{
    /// <summary>
    /// 
    /// </summary>
    public partial class ShapeCircle
    {





        /// <summary>
        /// 
        /// </summary>
        /// <param name="center_"></param>
        /// <param name="radius_"></param>
        public ShapeCircle(Point center_, int radius_)
            : base(Shape2DType.Circle)
        {
            Location = center_;
            m_Radius = radius_;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="o_"></param>
        /// <returns></returns>
        public override bool CompareTo(Shape2DObject o_)
        {
            if (o_ is ShapeCircle)
            {
                ShapeCircle c = (ShapeCircle)o_;
                return m_Radius == c.Radius && base.CompareTo(o_);
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
            el_.OwnerDocument.AddAttribute(el_, "radius", m_Radius.ToString());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Save(BinaryWriter bw_, SaveOption option_)
        {
            base.Save(bw_, option_);

            bw_.Write(m_Radius);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "Circle - " + Location.ToString() + " - " + m_Radius;
        }

    }
}
