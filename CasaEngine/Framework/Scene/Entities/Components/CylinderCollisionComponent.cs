using System.ComponentModel;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Rendering.Geometry;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

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

    protected override PhysicsShape ConvertToCollisionShape()
    {
        var cylinder = Cylinder;
        return PhysicsShape.CreateCylinder(cylinder.Radius);
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

}