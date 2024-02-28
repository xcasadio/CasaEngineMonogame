using System.ComponentModel;
using BulletSharp;
using CasaEngine.Core.Shapes;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Box 2d collision")]
public class Box2dCollisionComponent : Physics2dComponent
{
    public ShapeRectangle Rectangle { get; }

    public Box2dCollisionComponent() : base()
    {
        Rectangle = new ShapeRectangle();
    }

    public Box2dCollisionComponent(Box2dCollisionComponent other) : base(other)
    {
        Rectangle = other.Rectangle;
    }

    public override Box2dCollisionComponent Clone()
    {
        return new Box2dCollisionComponent(this);
    }

    protected override CollisionShape ConvertToCollisionShape()
    {
        var rectangle = Rectangle;
        return new BoxShape(rectangle.Width / 2f, rectangle.Height / 2f, 0.5f);
    }

    protected override BoundingBox ComputeBoundingBox()
    {
        return Rectangle.BoundingBox;
    }

    public override void Load(JObject element)
    {
        base.Load(element);
        Rectangle.Load((JObject)element["rectangle"]);
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        JObject newJObject = new();
        Rectangle.Save(newJObject);
        jObject.Add("rectangle", newJObject);
    }

#endif
}