using System.ComponentModel;
using BulletSharp;
using CasaEngine.Core.Shapes;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Box collision")]
public class BoxCollisionComponent : PhysicsBaseComponent
{
    public Box Box { get; } = new();

    public BoxCollisionComponent() : base()
    {
    }

    public BoxCollisionComponent(BoxCollisionComponent other) : base(other)
    {
        Box = other.Box;
    }

    public override BoxCollisionComponent Clone()
    {
        return new BoxCollisionComponent(this);
    }

    protected override CollisionShape ConvertToCollisionShape()
    {
        return new BoxShape(Box.Size.X / 2.0f, Box.Size.Y / 2.0f, Box.Size.Z / 2.0f);
    }

    protected override BoundingBox ComputeBoundingBox()
    {
        return Box.BoundingBox;
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        if (element.TryGetValue("box", out var boxNode))
        {
            Box.Load((JObject)boxNode);
        }
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        JObject newJObject = new();
        Box.Save(newJObject);
        jObject.Add("box", newJObject);
    }

#endif
}