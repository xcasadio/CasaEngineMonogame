using System.Xml;
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
    class ShapeRectangle
        : Shape2DObject
    {

        int m_Width, m_Height;



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



        public ShapeRectangle() { }

        public ShapeRectangle(ShapeRectangle o_)
            : base(o_)
        { }



        public override void Load(XmlElement el_, SaveOption option_)
        {
            base.Load(el_, option_);

            m_Width = int.Parse(el_.Attributes["width"].Value);
            m_Height = int.Parse(el_.Attributes["height"].Value);
        }

        public override Shape2DObject Clone()
        {
            return new ShapeRectangle(this);
        }

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

    }
}
