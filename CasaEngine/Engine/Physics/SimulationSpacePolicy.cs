using CasaEngine.Engine.Geometry;
using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Physics;

/// <summary>
/// Simulation space of a world. It owns the lowering of the 2d authoring shapes to the 3d shapes
/// the physics backend consumes; the pose of a shape belongs to its authoring attachment, not to the shape.
/// </summary>
public class SimulationSpacePolicy
{
    /// <summary>Depth given to a lowered 2d shape along the axis the simulation does not author.</summary>
    public float ExtrusionDepth { get; set; } = 1f;

    public virtual Shape3d Lower(Shape2d shape)
    {
        ArgumentNullException.ThrowIfNull(shape);

        switch (shape)
        {
            case ShapeRectangle rectangle:
                return new Box { Size = new Vector3(rectangle.Width, rectangle.Height, ExtrusionDepth) };

            case ShapeCircle circle:
                return new Sphere { Radius = circle.Radius };

            default:
                throw new NotSupportedException($"The simulation space policy cannot lower a 2d shape of type '{shape.Type}'.");
        }
    }
}

/// <summary>
/// Default policy: the simulation space is the render space, nothing is constrained.
/// </summary>
public sealed class Identity3dSimulationSpacePolicy : SimulationSpacePolicy
{
}

/// <summary>
/// Policy of a world authored in a plane. It only lowers shapes for now; the body constraints it
/// will provide are the subject of the simulation space phase.
/// </summary>
public sealed class Planar2dSimulationSpacePolicy : SimulationSpacePolicy
{
}
