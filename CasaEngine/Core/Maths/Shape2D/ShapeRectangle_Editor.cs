using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Extension;

namespace CasaEngine.Core.Maths.Shape2D
{
    public partial class ShapeRectangle
    {





        public ShapeRectangle(int x, int y, int w, int h)
            : base(Shape2DType.Rectangle)
        {
            Location = new Point(x, y);
            _width = w;
            _height = h;
        }



        public override bool CompareTo(Shape2DObject o)
        {
            if (o is ShapeRectangle)
            {
                var l = (ShapeRectangle)o;
                return _height == l._height
                    && _width == l._width
                    && base.CompareTo(o);
            }

            return false;
        }

        public override void Save(XmlElement el, SaveOption option)
        {
            base.Save(el, option);
            el.OwnerDocument.AddAttribute(el, "width", _width.ToString());
            el.OwnerDocument.AddAttribute(el, "height", _height.ToString());
        }

        public override void Save(BinaryWriter bw, SaveOption option)
        {
            base.Save(bw, option);

            bw.Write(_width);
            bw.Write(_height);
        }

        public override string ToString()
        {
            return "Rectangle " + _width.ToString() + " - " + _height.ToString();
        }

    }
}
