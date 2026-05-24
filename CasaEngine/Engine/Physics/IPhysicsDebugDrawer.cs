using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Physics;

public interface IPhysicsDebugDrawer
{
    PhysicsDebugDrawModes DebugMode { get; set; }

    void Draw3dText(ref Vector3 location, string textString);

    void DrawContactPoint(ref Vector3 pointOnB, ref Vector3 normalOnB, float distance, int lifeTime, Color color);

    void DrawLine(ref Vector3 from, ref Vector3 to, Color color);

    void ReportErrorWarning(string warningString);
}