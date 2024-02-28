using System.ComponentModel;
using BulletSharp;
using CasaEngine.Core.Shapes;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Cylinder collision")]
public class CylinderCollisionComponent : PhysicsBaseComponent
{
    public Cylinder Cylinder { get; }

    public CylinderCollisionComponent() : base()
    {
        Cylinder = new Cylinder();
    }

    public CylinderCollisionComponent(CylinderCollisionComponent other) : base(other)
    {
    }

    public override CylinderCollisionComponent Clone()
    {
        return new CylinderCollisionComponent(this);
    }

    protected override CollisionShape ConvertToCollisionShape()
    {
        var cylinder = Cylinder;
        return new CylinderShape(cylinder.Radius);
    }

    protected override BoundingBox ComputeBoundingBox()
    {
        return Cylinder.BoundingBox;
    }

    public override void Load(JObject element)
    {
        base.Load(element);
        Cylinder.Load((JObject)element["cylinder"]);
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        JObject newJObject = new();
        Cylinder.Save(newJObject);
        jObject.Add("cylinder", newJObject);
    }

#endif
}