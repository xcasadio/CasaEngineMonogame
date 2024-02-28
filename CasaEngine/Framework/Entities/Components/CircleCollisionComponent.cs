using System.ComponentModel;
using BulletSharp;
using CasaEngine.Core.Shapes;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Circle collision")]
public class CircleCollisionComponent : Physics2dComponent
{
    public ShapeCircle Circle { get; } = new();

    public CircleCollisionComponent() : base()
    {
    }

    public CircleCollisionComponent(CircleCollisionComponent other) : base(other)
    {
        Circle = other.Circle;
    }

    public override CircleCollisionComponent Clone()
    {
        return new CircleCollisionComponent(this);
    }

    protected override CollisionShape ConvertToCollisionShape()
    {
        return new SphereShape(Circle.Radius);
    }

    protected override BoundingBox ComputeBoundingBox()
    {
        return Circle.BoundingBox;
    }

    public override void Load(JObject element)
    {
        base.Load(element);
        if (element.TryGetValue("circle", out var circleNode))
        {
            Circle.Load((JObject)circleNode);
        }
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        JObject newJObject = new();
        Circle.Save(newJObject);
        jObject.Add("circle", newJObject);
    }

#endif
}