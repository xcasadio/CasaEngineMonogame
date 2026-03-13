using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Transform;

/// <summary>
/// Runtime-owned transform contract that editor tooling can adapt without taking a direct dependency on GizmoTools.
/// </summary>
public interface ITransformableObject
{
    Vector3 Position { get; set; }
    Vector3 Scale { get; set; }
    Quaternion Orientation { get; set; }
    Vector3 Forward { get; }
    Vector3 Up { get; }
    BoundingBox BoundingBox { get; }
}