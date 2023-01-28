using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using System.Xml;
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
    class ShapeLine
        : Shape2DObject
    {

        Point m_Start, m_End;



        /// <summary>
        /// Gets/Sets
        /// </summary>
#if EDITOR
        [Category("Shape Line")]
#endif
        public Point Start
        {
            get { return m_Start; }
            set
            {
                m_Start = value;
#if EDITOR
                NotifyPropertyChanged("Start");
#endif
            }
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
#if EDITOR
        [Category("Shape Line")]
#endif
        public Point End
        {
            get { return m_End; }
            set
            {
                m_End = value;
#if EDITOR
                NotifyPropertyChanged("End");
#endif
            }
        }



        /// <summary>
        /// 
        /// </summary>
        public ShapeLine() { }

        /// <summary>
        /// 
        /// </summary>
        public ShapeLine(ShapeLine o_)
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
            m_Start.X = int.Parse(el_.Attributes["startX"].Value);
            m_Start.Y = int.Parse(el_.Attributes["startY"].Value);
            m_End.X = int.Parse(el_.Attributes["endX"].Value);
            m_End.Y = int.Parse(el_.Attributes["endY"].Value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override Shape2DObject Clone()
        {
            return new ShapeLine(this);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ob_"></param>
        public override void CopyFrom(Shape2DObject ob_)
        {
            if (ob_ is ShapeLine == false)
            {
                throw new ArgumentException("ShapeLine.CopyFrom() : Shape2DObject is not a ShapeLine");
            }

            base.CopyFrom(ob_);
            m_Start = ((ShapeLine)ob_).m_Start;
            m_End = ((ShapeLine)ob_).m_End;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="x_"></param>
        public override void FlipHorizontally()
        {
            int x = m_End.X - m_Start.X;
            m_Start.X += x;
            m_End.X -= x;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="y_"></param>
        public override void FlipVertically()
        {
            int y = m_End.Y - m_Start.Y;
            m_Start.Y += y;
            m_End.Y -= y;
        }

    }
}
