// -- XNA 3D Gizmo (Component) -- originally by Tom Looman, licensed under Ms-PL
// -- Adapted for MonoGame by CasaEngine project.

using Microsoft.Xna.Framework;

namespace GizmoTools;

public interface ITransformable
{
    Vector3 Position { get; set; }
    Vector3 Scale { get; set; }
    Quaternion Orientation { get; set; }
    Vector3 Forward { get; }
    Vector3 Up { get; }
    BoundingBox BoundingBox { get; }
}