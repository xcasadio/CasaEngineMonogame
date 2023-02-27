﻿using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Extension;

namespace CasaEngine.Core.Maths.Shape2D
{
    public partial class ShapeCircle
    {





        public ShapeCircle(Point center, int radius)
            : base(Shape2DType.Circle)
        {
            Location = center;
            _radius = radius;
        }



        public override bool CompareTo(Shape2DObject o)
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

    }
}