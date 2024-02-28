using System.ComponentModel;
using BulletSharp;
using CasaEngine.Core.Shapes;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Capsule collision")]
public class CapsuleCollisionComponent : PhysicsBaseComponent
{
    public Capsule Capsule { get; }

    public CapsuleCollisionComponent() : base()
    {
        Capsule = new Capsule();
    }

    public CapsuleCollisionComponent(CapsuleCollisionComponent other) : base(other)
    {
    }

    public override CapsuleCollisionComponent Clone()
    {
        return new CapsuleCollisionComponent(this);
    }

    protected override CollisionShape ConvertToCollisionShape()
    {
        var capsule = Capsule;
        return new CapsuleShape(capsule.Radius, capsule.Length);
    }

    protected override BoundingBox ComputeBoundingBox()
    {
        return Capsule.BoundingBox;
    }

    public override void Load(JObject element)
    {
        base.Load(element);
        Capsule.Load((JObject)element["capsule"]);
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        JObject newJObject = new();
        Capsule.Save(newJObject);
        jObject.Add("capsule", newJObject);
    }

#endif
}