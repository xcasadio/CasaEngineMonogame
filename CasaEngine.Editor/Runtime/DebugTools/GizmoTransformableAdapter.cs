using CasaEngine.Framework.Scene.Transform;
using GizmoTools;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Application.Components.DebugTools;

internal sealed class GizmoTransformableAdapter : ITransformable
{
    public ITransformableObject Transformable { get; }

    public GizmoTransformableAdapter(ITransformableObject transformable)
    {
        Transformable = transformable;
    }

    public Vector3 Position
    {
        get => Transformable.Position;
        set => Transformable.Position = value;
    }

    public Vector3 Scale
    {
        get => Transformable.Scale;
        set => Transformable.Scale = value;
    }

    public Quaternion Orientation
    {
        get => Transformable.Orientation;
        set => Transformable.Orientation = value;
    }

    public Vector3 Forward => Transformable.Forward;
    public Vector3 Up => Transformable.Up;
    public BoundingBox BoundingBox => Transformable.BoundingBox;
}