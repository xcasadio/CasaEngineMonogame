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
    class ShapeLine
        : Shape2DObject
    {

        Point _start, _end;



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

        public ShapeLine(ShapeLine o)
            : base(o)
        { }



        public override void Load(XmlElement el, SaveOption option)
        {
            base.Load(el, option);
            _start.X = int.Parse(el.Attributes["startX"].Value);
            _start.Y = int.Parse(el.Attributes["startY"].Value);
            _end.X = int.Parse(el.Attributes["endX"].Value);
            _end.Y = int.Parse(el.Attributes["endY"].Value);
        }

        public override Shape2DObject Clone()
        {
            return new ShapeLine(this);
        }

        public override void CopyFrom(Shape2DObject ob)
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
            int x = _end.X - _start.X;
            _start.X += x;
            _end.X -= x;
        }

        public override void FlipVertically()
        {
            int y = _end.Y - _start.Y;
            _start.Y += y;
            _end.Y -= y;
        }

    }
}
