using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;


namespace CasaEngine.Math.Shape2D
{
    public partial class ShapeCircle
    {





        public ShapeCircle(Point center_, int radius_)
            : base(Shape2DType.Circle)
        {
            Location = center_;
            m_Radius = radius_;
        }



        public override bool CompareTo(Shape2DObject o_)
        {
            if (o_ is ShapeCircle)
            {
                ShapeCircle c = (ShapeCircle)o_;
                return m_Radius == c.Radius && base.CompareTo(o_);
            }

            return false;
        }

        public override void Save(XmlElement el_, SaveOption option_)
        {
            base.Save(el_, option_);
            el_.OwnerDocument.AddAttribute(el_, "radius", m_Radius.ToString());
        }

        public override void Save(BinaryWriter bw_, SaveOption option_)
        {
            base.Save(bw_, option_);

            bw_.Write(m_Radius);
        }

        public override string ToString()
        {
            return "Circle - " + Location.ToString() + " - " + m_Radius;
        }

    }
}
