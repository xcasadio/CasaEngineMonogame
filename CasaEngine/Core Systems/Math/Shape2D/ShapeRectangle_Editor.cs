using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;

namespace CasaEngine.Math.Shape2D
{
    public partial class ShapeRectangle
    {





        public ShapeRectangle(int x_, int y_, int w_, int h_)
            : base(Shape2DType.Rectangle)
        {
            Location = new Point(x_, y_);
            m_Width = w_;
            m_Height = h_;
        }



        public override bool CompareTo(Shape2DObject o_)
        {
            if (o_ is ShapeRectangle)
            {
                ShapeRectangle l = (ShapeRectangle)o_;
                return m_Height == l.m_Height
                    && m_Width == l.m_Width
                    && base.CompareTo(o_);
            }

            return false;
        }

        public override void Save(XmlElement el_, SaveOption option_)
        {
            base.Save(el_, option_);
            el_.OwnerDocument.AddAttribute(el_, "width", m_Width.ToString());
            el_.OwnerDocument.AddAttribute(el_, "height", m_Height.ToString());
        }

        public override void Save(BinaryWriter bw_, SaveOption option_)
        {
            base.Save(bw_, option_);

            bw_.Write(m_Width);
            bw_.Write(m_Height);
        }

        public override string ToString()
        {
            return "Rectangle " + m_Width.ToString() + " - " + m_Height.ToString();
        }

    }
}
