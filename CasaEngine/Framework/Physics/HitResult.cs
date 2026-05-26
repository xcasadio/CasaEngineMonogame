using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Physics;

public struct HitResult
{
    public Vector3 Normal;

    public Vector3 Point;

    public float HitFraction;

    public bool Succeeded;

    public PhysicsBaseComponent Collider;
}