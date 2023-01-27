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
    class ShapeRectangle
        : Shape2DObject
    {
        #region Fields

        int m_Width, m_Height;

        #endregion

        #region Properties

        /// <summary>
        /// Gets/Sets
        /// </summary>
#if EDITOR
        [Category("Shape Rectangle")]
#endif
        public int Width
        {
            get { return m_Width; }
            set 
            { 
                m_Width = value;
#if EDITOR
                NotifyPropertyChanged("Width");
#endif
            }
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
#if EDITOR
        [Category("Shape Rectangle")]
#endif
        public int Height
        {
            get { return m_Height; }
            set
            { 
                m_Height = value;
#if EDITOR
                NotifyPropertyChanged("Height");
#endif
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        public ShapeRectangle() { }

        /// <summary>
        /// 
        /// </summary>
        public ShapeRectangle(ShapeRectangle o_)
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

            m_Width = int.Parse(el_.Attributes["width"].Value);
            m_Height = int.Parse(el_.Attributes["height"].Value); 
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override Shape2DObject Clone()
        {
            return new ShapeRectangle(this);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ob_"></param>
        public override void CopyFrom(Shape2DObject ob_)
        {
            if (ob_ is ShapeRectangle == false)
            {
                throw new ArgumentException("ShapeRectangle.CopyFrom() : Shape2DObject is not a ShapeRectangle");
            }

            base.CopyFrom(ob_);
            m_Width = ((ShapeRectangle)ob_).m_Width;
            m_Height = ((ShapeRectangle)ob_).m_Height;
        }

        #endregion
    }
}
