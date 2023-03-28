using System.ComponentModel;
using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Extension;

namespace CasaEngine.Core.Maths.Shape2D;

public class ShapeCircle : Shape2dObject
{
    private int _radius;

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

    public override Shape2dObject Clone()
    {
        return new ShapeCircle(this);
    }

    public override void CopyFrom(Shape2dObject ob)
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

#if EDITOR
    public ShapeCircle(Point center, int radius)
        : base(Shape2dType.Circle)
    {
        Location = center;
        _radius = radius;
    }

    public override bool CompareTo(Shape2dObject o)
    {
        if (o is ShapeCircle)
        {
            var c = (ShapeCircle)o;
            return _radius == c.Radius && base.CompareTo(o);
        }

        return false;
    }

    public override void Save(XmlElement el, SaveOption option)
    {
        base.Save(el, option);
        el.OwnerDocument.AddAttribute(el, "radius", _radius.ToString());
    }

    public override void Save(BinaryWriter bw, SaveOption option)
    {
        base.Save(bw, option);

        bw.Write(_radius);
    }

    public override string ToString()
    {
        return "Circle - " + Location.ToString() + " - " + _radius;
    }
#endif
}