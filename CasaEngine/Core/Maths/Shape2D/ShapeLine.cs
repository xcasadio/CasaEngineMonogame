using System.ComponentModel;
using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Extension;

namespace CasaEngine.Core.Maths.Shape2D
{
    public class ShapeLine : Shape2dObject
    {
        private Point _start, _end;

#if EDITOR
        [Category("Shape Line")]
#endif
        public Point Start
        {
            get { return _start; }
            set
            {
                _start = value;
#if EDITOR
                NotifyPropertyChanged("Start");
#endif
            }
        }

#if EDITOR
        [Category("Shape Line")]
#endif
        public Point End
        {
            get { return _end; }
            set
            {
                _end = value;
#if EDITOR
                NotifyPropertyChanged("End");
#endif
            }
        }

        public ShapeLine() { }

        public ShapeLine(ShapeLine o) : base(o)
        { }

        public override void Load(XmlElement el, SaveOption option)
        {
            base.Load(el, option);
            _start.X = int.Parse(el.Attributes["startX"].Value);
            _start.Y = int.Parse(el.Attributes["startY"].Value);
            _end.X = int.Parse(el.Attributes["endX"].Value);
            _end.Y = int.Parse(el.Attributes["endY"].Value);
        }

        public override Shape2dObject Clone()
        {
            return new ShapeLine(this);
        }

        public override void CopyFrom(Shape2dObject ob)
        {
            if (ob is ShapeLine == false)
            {
                throw new ArgumentException("ShapeLine.CopyFrom() : Shape2DObject is not a ShapeLine");
            }

            base.CopyFrom(ob);
            _start = ((ShapeLine)ob)._start;
            _end = ((ShapeLine)ob)._end;
        }

        public override void FlipHorizontally()
        {
            var x = _end.X - _start.X;
            _start.X += x;
            _end.X -= x;
        }

        public override void FlipVertically()
        {
            var y = _end.Y - _start.Y;
            _start.Y += y;
            _end.Y -= y;
        }
#if EDITOR

        public ShapeLine(Point start, Point end)
            : base(Shape2dType.Line)
        {
            _start = start;
            _end = end;
        }

        public override bool CompareTo(Shape2dObject o)
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
#endif
    }
}
