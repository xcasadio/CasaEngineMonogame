using System.ComponentModel;
using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Extension;

namespace CasaEngine.Core.Maths.Shape2D;

public class ShapeRectangle : Shape2dObject
{
    private int _width, _height;

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

    public override Shape2dObject Clone()
    {
        return new ShapeRectangle(this);
    }

    public override void CopyFrom(Shape2dObject ob)
    {
        if (ob is ShapeRectangle == false)
        {
            throw new ArgumentException("ShapeRectangle.CopyFrom() : Shape2DObject is not a ShapeRectangle");
        }

        base.CopyFrom(ob);
        _width = ((ShapeRectangle)ob)._width;
        _height = ((ShapeRectangle)ob)._height;
    }


    public ShapeRectangle(int x, int y, int w, int h)
        : base(Shape2dType.Rectangle)
    {
        Location = new Point(x, y);
        _width = w;
        _height = h;
    }

#if EDITOR
    public override bool CompareTo(Shape2dObject o)
    {
        if (o is ShapeRectangle)
        {
            var l = (ShapeRectangle)o;
            return _height == l._height
                   && _width == l._width
                   && base.CompareTo(o);
        }

        return false;
    }

    public override void Save(XmlElement el, SaveOption option)
    {
        base.Save(el, option);
        el.OwnerDocument.AddAttribute(el, "width", _width.ToString());
        el.OwnerDocument.AddAttribute(el, "height", _height.ToString());
    }

    public override void Save(BinaryWriter bw, SaveOption option)
    {
        base.Save(bw, option);

        bw.Write(_width);
        bw.Write(_height);
    }

    public override string ToString()
    {
        return "Rectangle " + _width.ToString() + " - " + _height.ToString();
    }
#endif
}