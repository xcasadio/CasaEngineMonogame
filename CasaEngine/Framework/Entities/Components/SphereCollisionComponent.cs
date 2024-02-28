using System.ComponentModel;
using BulletSharp;
using CasaEngine.Core.Shapes;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Sphere collision")]
public class SphereCollisionComponent : PhysicsBaseComponent
{
    public Sphere Sphere { get; } = new();

    public SphereCollisionComponent() : base()
    {
    }

    public SphereCollisionComponent(SphereCollisionComponent other) : base(other)
    {
        Sphere = other.Sphere;
    }

    public override SphereCollisionComponent Clone()
    {
        return new SphereCollisionComponent(this);
    }

    protected override CollisionShape ConvertToCollisionShape()
    {
        return new SphereShape(Sphere.Radius);
    }

    protected override BoundingBox ComputeBoundingBox()
    {
        return Sphere.BoundingBox;
    }

    public override void Load(JObject element)
    {
        base.Load(element);
        if (element.TryGetValue("sphere", out var sphereNode))
        {
            Sphere.Load((JObject)sphereNode);
        }
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        JObject newJObject = new();
        Sphere.Save(newJObject);
        jObject.Add("sphere", newJObject);
    }

#endif
}