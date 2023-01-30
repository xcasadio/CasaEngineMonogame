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
    class ShapeCircle
        : Shape2DObject
    {

        int m_Radius;



#if EDITOR
        [Category("Shape Circle")]
#endif
        public int Radius
        {
            get { return m_Radius; }
            set
            {
                m_Radius = value;
#if EDITOR
                NotifyPropertyChanged("Radius");
#endif
            }
        }



        public ShapeCircle() { }

        public ShapeCircle(ShapeCircle o_)
            : base(o_)
        { }



        public override void Load(XmlElement el_, SaveOption option_)
        {
            base.Load(el_, option_);
            m_Radius = int.Parse(el_.Attributes["radius"].Value);
        }

        public override Shape2DObject Clone()
        {
            return new ShapeCircle(this);
        }

        public override void CopyFrom(Shape2DObject ob_)
        {
            if (ob_ is ShapeCircle == false)
            {
                throw new ArgumentException("ShapeCircle.CopyFrom() : Shape2DObject is not a ShapeCircle");
            }

            base.CopyFrom(ob_);
            m_Radius = ((ShapeCircle)ob_).m_Radius;
        }



        // Pool for this type of components.
        /*private static readonly Pool<ShapeCircle> componentPool = new Pool<ShapeCircle>(20);

        internal static Pool<ShapeCircle> ComponentPool { get { return componentPool; } }*/

    }
}
