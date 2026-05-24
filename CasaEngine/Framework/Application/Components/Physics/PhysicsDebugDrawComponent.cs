using CasaEngine.Engine.Physics;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Application.Components.Physics;

public class PhysicsDebugDrawComponent : IPhysicsDebugDrawer
{
    private readonly Line3dRendererComponent _line3dRendererComponent;

    public PhysicsDebugDrawModes DebugMode { get; set; }

    public PhysicsDebugDrawComponent(Line3dRendererComponent line3dRendererComponent)
    {
        _line3dRendererComponent = line3dRendererComponent;
    }

    public void Draw3dText(ref Vector3 location, string textString)
    {
        throw new NotImplementedException();
    }

    public void DrawContactPoint(ref Vector3 pointOnB, ref Vector3 normalOnB, float distance, int lifeTime, Color color)
    {
        _line3dRendererComponent.AddLine(pointOnB, pointOnB + normalOnB, color);
    }

    public void DrawLine(ref Vector3 from, ref Vector3 to, Color color)
    {
        _line3dRendererComponent.AddLine(from, to, color);
    }

    public void DrawDebugWorld(IPhysicsWorld physicsWorld)
    {
        physicsWorld.DrawDebugWorld(this);
    }

    public void ReportErrorWarning(string warningString)
    {
        throw new NotImplementedException();
    }
}