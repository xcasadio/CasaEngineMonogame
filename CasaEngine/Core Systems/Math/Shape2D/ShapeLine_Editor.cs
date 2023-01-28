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
    public partial class ShapeLine
        : Shape2DObject
    {





        /// <summary>
        /// 
        /// </summary>
        public ShapeLine(Point start_, Point end_)
            : base(Shape2DType.Line)
        {
            m_Start = start_;
            m_End = end_;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="o_"></param>
        /// <returns></returns>
        public override bool CompareTo(Shape2DObject o_)
        {
            if (o_ is ShapeLine)
            {
                ShapeLine l = (ShapeLine)o_;
                return m_Start == l.Start
                    && m_End == l.End
                    && base.CompareTo(o_);
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
            el_.OwnerDocument.AddAttribute(el_, "startX", m_Start.X.ToString());
            el_.OwnerDocument.AddAttribute(el_, "startY", m_Start.Y.ToString());
            el_.OwnerDocument.AddAttribute(el_, "endX", m_End.X.ToString());
            el_.OwnerDocument.AddAttribute(el_, "endY", m_End.Y.ToString());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Save(BinaryWriter bw_, SaveOption option_)
        {
            base.Save(bw_, option_);

            bw_.Write(m_Start);
            bw_.Write(m_End);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "Line - " + m_Start.ToString() + " - " + m_End.ToString();
        }

    }
}
