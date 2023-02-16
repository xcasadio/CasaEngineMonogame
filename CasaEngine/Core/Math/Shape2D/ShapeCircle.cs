using System.ComponentModel;
using System.Xml;
using CasaEngineCommon.Design;

namespace CasaEngine.Core.Math.Shape2D
{
    public
#if EDITOR
    partial
#endif
    class ShapeCircle
        : Shape2DObject
    {

        int _radius;



#if EDITOR
        [Category("Shape Circle")]
#endif
        public int Radius
        {
            get { return _radius; }
            set
            {
                _radius = value;
#if EDITOR
                NotifyPropertyChanged("Radius");
#endif
            }
        }



        public ShapeCircle() { }

        public ShapeCircle(ShapeCircle o)
            : base(o)
        { }



        public override void Load(XmlElement el, SaveOption option)
        {
            base.Load(el, option);
            _radius = int.Parse(el.Attributes["radius"].Value);
        }

        public override Shape2DObject Clone()
        {
            return new ShapeCircle(this);
        }

        public override void CopyFrom(Shape2DObject ob)
        {
            if (ob is ShapeCircle == false)
            {
                throw new ArgumentException("ShapeCircle.CopyFrom() : Shape2DObject is not a ShapeCircle");
            }

            base.CopyFrom(ob);
            _radius = ((ShapeCircle)ob)._radius;
        }



        // Pool for this type of components.
        /*private static readonly Pool<ShapeCircle> componentPool = new Pool<ShapeCircle>(20);

        internal static Pool<ShapeCircle> ComponentPool { get { return componentPool; } }*/

    }
}
