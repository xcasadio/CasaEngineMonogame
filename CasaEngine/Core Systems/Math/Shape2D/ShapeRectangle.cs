using System.ComponentModel;
using System.Xml;
using CasaEngineCommon.Design;

namespace CasaEngine.Core_Systems.Math.Shape2D
{
    public
#if EDITOR
    partial
#endif     
    class ShapeRectangle
        : Shape2DObject
    {
        int _width, _height;

#if EDITOR
        [Category("Shape Rectangle")]
#endif
        public int Width
        {
            get { return _width; }
            set
            {
                _width = value;
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
            get { return _height; }
            set
            {
                _height = value;
#if EDITOR
                NotifyPropertyChanged("Height");
#endif
            }
        }

        public ShapeRectangle() { }

        public ShapeRectangle(ShapeRectangle o)
            : base(o)
        { }

        public override void Load(XmlElement el, SaveOption option)
        {
            base.Load(el, option);

            _width = int.Parse(el.Attributes["width"].Value);
            _height = int.Parse(el.Attributes["height"].Value);
        }

        public override Shape2DObject Clone()
        {
            return new ShapeRectangle(this);
        }

        public override void CopyFrom(Shape2DObject ob)
        {
            if (ob is ShapeRectangle == false)
            {
                throw new ArgumentException("ShapeRectangle.CopyFrom() : Shape2DObject is not a ShapeRectangle");
            }

            base.CopyFrom(ob);
            _width = ((ShapeRectangle)ob)._width;
            _height = ((ShapeRectangle)ob)._height;
        }
    }
}
