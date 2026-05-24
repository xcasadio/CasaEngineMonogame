using System.ComponentModel;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Rendering.Geometry;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

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

    protected override PhysicsShape ConvertToCollisionShape()
    {
        var capsule = Capsule;
        return PhysicsShape.CreateCapsule(capsule.Radius, capsule.Length);
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

}