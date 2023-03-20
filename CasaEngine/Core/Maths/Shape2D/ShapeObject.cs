using System.ComponentModel;
using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Extension;

namespace CasaEngine.Core.Maths.Shape2D
{
    public abstract class Shape2dObject
    {
        private Shape2dType _type;
        private Point _location = Point.Zero;
        private float _rotation;
        private int _flag;

#if EDITOR
        [Category("Shape2D"), ReadOnly(true)]
#endif
        public Shape2dType Shape2dType
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

        protected Shape2dObject()
        { }

        protected Shape2dObject(Shape2dObject o)
        {
            CopyFrom(o);
        }

        public static Shape2dObject CreateShape2DObject(XmlElement el, SaveOption option)
        {
            var type = (Shape2dType)Enum.Parse(typeof(Shape2dType), el.Attributes["type"].Value);
            Shape2dObject res = null;

            switch (type)
            {
                case Shape2dType.Circle:
                    res = new ShapeCircle();
                    break;

                case Shape2dType.Line:
                    res = new ShapeLine();
                    break;

                case Shape2dType.Polygone:
                    res = new ShapePolygone();
                    break;

                case Shape2dType.Rectangle:
                    res = new ShapeRectangle();
                    break;

                default:
                    throw new InvalidOperationException("Shape2DObject.CreateShape2DObject() : the type " + Enum.GetName(typeof(Shape2dType), type) + " is not supported");
            }

            res.Load(el, option);

            return res;
        }

        public virtual void Load(XmlElement el, SaveOption option)
        {
            var version = int.Parse(el.Attributes["version"].Value);

            _type = (Shape2dType)Enum.Parse(typeof(Shape2dType), el.Attributes["type"].Value);
            var p = new Point();
            ((XmlElement)el.SelectSingleNode("Location")).Read(ref p);
            Location = p;
            _rotation = float.Parse(el.Attributes["rotation"].Value);
            _flag = int.Parse(el.Attributes["flag"].Value);
        }

        public abstract Shape2dObject Clone();

        public virtual void CopyFrom(Shape2dObject ob)
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

#if EDITOR
        private static readonly int Version = 1;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool PropertyChangedActivated = true;
        private object _tag;



        [Browsable(false)]
        public object Tag
        {
            get => _tag;
            set => _tag = value;
        }



        public Shape2dObject(Shape2dType type)
        {
            _type = type;
        }



        public virtual bool CompareTo(Shape2dObject o)
        {
            return _flag == o._flag
                && _location == o._location
                && _rotation == o._rotation
                && _type == o._type;
        }

        public virtual void Save(XmlElement el, SaveOption option)
        {
            el.OwnerDocument.AddAttribute(el, "version", Version.ToString());
            el.OwnerDocument.AddAttribute(el, "type", Enum.GetName(typeof(Shape2dType), _type));

            var location = el.OwnerDocument.CreateElement("Location", Location);
            el.AppendChild(location);

            el.OwnerDocument.AddAttribute(el, "rotation", _rotation.ToString());
            el.OwnerDocument.AddAttribute(el, "flag", _flag.ToString());
        }

        public virtual void Save(BinaryWriter bw, SaveOption option)
        {
            bw.Write(Version);
            bw.Write(Enum.GetName(typeof(Shape2dType), _type));
            bw.Write(_rotation);
            bw.Write(_flag);
        }

        public void NotifyPropertyChanged(string info)
        {
            if (PropertyChanged != null
                && PropertyChangedActivated)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is Shape2dObject)
            {
                return CompareTo((Shape2dObject)obj);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return base.ToString();
        }
#endif
    }
}
