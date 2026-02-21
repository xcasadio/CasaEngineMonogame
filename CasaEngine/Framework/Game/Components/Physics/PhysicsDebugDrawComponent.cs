using BulletSharp;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Game.Components.Physics;

public class PhysicsDebugDrawComponent : DebugDraw
{
    private readonly Line3dRendererComponent _line3dRendererComponent;

    public override DebugDrawModes DebugMode { get; set; }

    public PhysicsDebugDrawComponent(Line3dRendererComponent line3dRendererComponent)
    {
        _line3dRendererComponent = line3dRendererComponent;
    }

    public override void Draw3dText(ref Vector3 location, string textString)
    {
        throw new NotImplementedException();
    }

    public override void DrawContactPoint(ref Vector3 pointOnB, ref Vector3 normalOnB, float distance, int lifeTime, Color color)
    {
        _line3dRendererComponent.AddLine(pointOnB, pointOnB + normalOnB, color);
    }

    public override void DrawLine(ref Vector3 from, ref Vector3 to, Color color)
    {
        _line3dRendererComponent.AddLine(from, to, color);
    }

    public void DrawDebugWorld(DynamicsWorld world)
    {
        world.DebugDrawWorld();
    }

    public override void ReportErrorWarning(string warningString)
    {
        throw new NotImplementedException();
    }
}