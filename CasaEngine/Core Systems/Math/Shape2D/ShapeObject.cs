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

        private Shape2DType m_Type;
        private Point m_Location = Point.Zero;
        private float m_Rotation = 0.0f;
        private int m_Flag = 0;



#if EDITOR
        [Category("Shape2D"), ReadOnly(true)]
#endif
        public Shape2DType Shape2DType
        {
            get { return m_Type; }
        }

#if EDITOR
        [Category("Shape2D")]
#endif
        public int Flag
        {
            get { return m_Flag; }
            set
            {
                m_Flag = value;
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
            get { return m_Location; }
            set
            {
                m_Location = value;
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
            get { return m_Rotation; }
            set
            {
                m_Rotation = value;
#if EDITOR
                NotifyPropertyChanged("Rotation");
#endif
            }
        }



        protected Shape2DObject()
        { }

        protected Shape2DObject(Shape2DObject o_)
        {
            CopyFrom(o_);
        }




        static public Shape2DObject CreateShape2DObject(XmlElement el_, SaveOption option_)
        {
            Shape2DType type = (Shape2DType)Enum.Parse(typeof(Shape2DType), el_.Attributes["type"].Value);
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

            res.Load(el_, option_);

            return res;
        }

        public virtual void Load(XmlElement el_, SaveOption option_)
        {
            int version = int.Parse(el_.Attributes["version"].Value);

            m_Type = (Shape2DType)Enum.Parse(typeof(Shape2DType), el_.Attributes["type"].Value);
            Point p = new Point();
            ((XmlElement)el_.SelectSingleNode("Location")).Read(ref p);
            Location = p;
            m_Rotation = float.Parse(el_.Attributes["rotation"].Value);
            m_Flag = int.Parse(el_.Attributes["flag"].Value);
        }


        public abstract Shape2DObject Clone();

        public virtual void CopyFrom(Shape2DObject ob_)
        {
            m_Location = ob_.m_Location;
            m_Rotation = ob_.m_Rotation;
            m_Type = ob_.m_Type;
            m_Flag = ob_.m_Flag;
        }

        public virtual void FlipHorizontally()
        {

        }

        public virtual void FlipVertically()
        {

        }

    }
}
