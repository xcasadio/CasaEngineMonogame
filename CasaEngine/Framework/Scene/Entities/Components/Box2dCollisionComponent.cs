using System.ComponentModel;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Rendering.Geometry;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

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

    protected override PhysicsShape ConvertToCollisionShape()
    {
        var rectangle = Rectangle;
        return PhysicsShape.CreateBox(rectangle.Width / 2f, rectangle.Height / 2f, 0.5f);
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

}