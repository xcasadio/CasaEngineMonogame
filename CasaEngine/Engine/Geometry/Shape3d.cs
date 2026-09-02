using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Geometry;

public abstract class Shape3d : ObjectBase
{
    public Shape3dType Type { get; }

    public abstract BoundingBox BoundingBox { get; }

    protected Shape3d(Shape3dType type)
    {
        Type = type;
    }

    /// <summary>Additive constructor for callers assigning a deterministic id (see <see cref="ObjectBase(Guid)"/>).</summary>
    protected Shape3d(Guid id, Shape3dType type) : base(id)
    {
        Type = type;
    }
}