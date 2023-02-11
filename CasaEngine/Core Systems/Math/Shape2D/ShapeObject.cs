using System.Xml;
using CasaEngineCommon.Extension;
using System.ComponentModel;
using CasaEngineCommon.Design;

namespace CasaEngine.Math.Shape2D
{
    public abstract
#if EDITOR
    partial
#endif
    class Shape2DObject
#if EDITOR
        : INotifyPropertyChanged
#endif
    {

        private Shape2DType _type;
        private Point _location = Point.Zero;
        private float _rotation = 0.0f;
        private int _flag = 0;



#if EDITOR
        [Category("Shape2D"), ReadOnly(true)]
#endif
        public Shape2DType Shape2DType
        {
            get { return _type; }
        }

#if EDITOR
        [Category("Shape2D")]
#endif
        public int Flag
        {
            get { return _flag; }
            set
            {
                _flag = value;
#if EDITOR
                NotifyPropertyChanged("Flag");
#endif
            }
        }

#if EDITOR
        [Category("Shape2D")]
#endif
        public Point Location
        {
            get { return _location; }
            set
            {
                _location = value;
#if EDITOR
                NotifyPropertyChanged("Location");
#endif
            }
        }

#if EDITOR
        [Category("Shape2D")]
#endif
        public float Rotation
        {
            get { return _rotation; }
            set
            {
                _rotation = value;
#if EDITOR
                NotifyPropertyChanged("Rotation");
#endif
            }
        }



        protected Shape2DObject()
        { }

        protected Shape2DObject(Shape2DObject o)
        {
            CopyFrom(o);
        }




        static public Shape2DObject CreateShape2DObject(XmlElement el, SaveOption option)
        {
            Shape2DType type = (Shape2DType)Enum.Parse(typeof(Shape2DType), el.Attributes["type"].Value);
            Shape2DObject res = null;

            switch (type)
            {
                case Shape2DType.Circle:
                    res = new ShapeCircle();
                    break;

                case Shape2DType.Line:
                    res = new ShapeLine();
                    break;

                case Shape2DType.Polygone:
                    res = new ShapePolygone();
                    break;

                case Shape2DType.Rectangle:
                    res = new ShapeRectangle();
                    break;

                default:
                    throw new InvalidOperationException("Shape2DObject.CreateShape2DObject() : the type " + Enum.GetName(typeof(Shape2DType), type) + " is not supported");
            }

            res.Load(el, option);

            return res;
        }

        public virtual void Load(XmlElement el, SaveOption option)
        {
            int version = int.Parse(el.Attributes["version"].Value);

            _type = (Shape2DType)Enum.Parse(typeof(Shape2DType), el.Attributes["type"].Value);
            Point p = new Point();
            ((XmlElement)el.SelectSingleNode("Location")).Read(ref p);
            Location = p;
            _rotation = float.Parse(el.Attributes["rotation"].Value);
            _flag = int.Parse(el.Attributes["flag"].Value);
        }


        public abstract Shape2DObject Clone();

        public virtual void CopyFrom(Shape2DObject ob)
        {
            _location = ob._location;
            _rotation = ob._rotation;
            _type = ob._type;
            _flag = ob._flag;
        }

        public virtual void FlipHorizontally()
        {

        }

        public virtual void FlipVertically()
        {

        }

    }
}
