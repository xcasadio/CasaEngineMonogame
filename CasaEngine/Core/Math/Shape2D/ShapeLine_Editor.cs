using System.Xml;
using CasaEngineCommon.Design;
using CasaEngineCommon.Extension;

namespace CasaEngine.Core.Math.Shape2D
{
    public partial class ShapeLine
        : Shape2DObject
    {





        public ShapeLine(Point start, Point end)
            : base(Shape2DType.Line)
        {
            _start = start;
            _end = end;
        }



        public override bool CompareTo(Shape2DObject o)
        {
            if (o is ShapeLine)
            {
                var l = (ShapeLine)o;
                return _start == l.Start
                    && _end == l.End
                    && base.CompareTo(o);
            }

            return false;
        }

        public override void Save(XmlElement el, SaveOption option)
        {
            base.Save(el, option);
            el.OwnerDocument.AddAttribute(el, "startX", _start.X.ToString());
            el.OwnerDocument.AddAttribute(el, "startY", _start.Y.ToString());
            el.OwnerDocument.AddAttribute(el, "endX", _end.X.ToString());
            el.OwnerDocument.AddAttribute(el, "endY", _end.Y.ToString());
        }

        public override void Save(BinaryWriter bw, SaveOption option)
        {
            base.Save(bw, option);

            bw.Write(_start);
            bw.Write(_end);
        }

        public override string ToString()
        {
            return "Line - " + _start.ToString() + " - " + _end.ToString();
        }

    }
}
