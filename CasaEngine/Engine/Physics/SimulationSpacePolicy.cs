using CasaEngine.Engine.Geometry;
using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Physics;

/// <summary>Names under which a simulation space policy is serialized on a world.</summary>
public static class SimulationSpacePolicyNames
{
    public const string Identity3d = "Identity3d";
    public const string Planar2d = "Planar2d";
    public const string TopDownElevation = "TopDownElevation";
}

/// <summary>
/// Simulation space of a world. It owns the lowering of the 2d authoring shapes to the 3d shapes
/// the physics backend consumes; the pose of a shape belongs to its authoring attachment, not to the shape.
/// It also owns the two other ends of the space rule: the body constraints a world gives its bodies by
/// default, and the mapping from the logical pose a world simulates to the pose it renders.
/// </summary>
public class SimulationSpacePolicy
{
    /// <summary>Name this policy is serialized under; see <see cref="CreateByName"/>.</summary>
    public virtual string Name => SimulationSpacePolicyNames.Identity3d;

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

    /// <summary>
    /// Constraints a world gives a body by default. The base policy constrains nothing.
    /// </summary>
    public virtual void ApplyDefaultConstraints(PhysicsDefinition definition)
    {
        //Nothing to constrain: the simulation space is the whole 3d space.
    }

    /// <summary>
    /// Render position derived from a logical position. The base policy renders where it simulates.
    /// </summary>
    public virtual Vector3 DeriveRenderPosition(Vector3 logicalPosition)
    {
        return logicalPosition;
    }

    /// <summary>Creates the policy a world names. An unknown name is a project data error.</summary>
    public static SimulationSpacePolicy CreateByName(string name)
    {
        switch (name)
        {
            case SimulationSpacePolicyNames.Identity3d:
                return new Identity3dSimulationSpacePolicy();

            case SimulationSpacePolicyNames.Planar2d:
                return new Planar2dSimulationSpacePolicy();

            case SimulationSpacePolicyNames.TopDownElevation:
                return new TopDownElevationSimulationSpacePolicy();

            default:
                throw new ArgumentException(
                    $"Unknown simulation space policy '{name}'. Known policies: " +
                    $"'{SimulationSpacePolicyNames.Identity3d}', '{SimulationSpacePolicyNames.Planar2d}', " +
                    $"'{SimulationSpacePolicyNames.TopDownElevation}'.",
                    nameof(name));
        }
    }
}

/// <summary>
/// Default policy: the simulation space is the render space, nothing is constrained.
/// </summary>
public sealed class Identity3dSimulationSpacePolicy : SimulationSpacePolicy
{
    public override string Name => SimulationSpacePolicyNames.Identity3d;
}

/// <summary>
/// Policy of a world authored in the XY plane: bodies are locked to that plane and only spin around Z.
/// The simulation space is still the render space.
/// </summary>
public sealed class Planar2dSimulationSpacePolicy : SimulationSpacePolicy
{
    public override string Name => SimulationSpacePolicyNames.Planar2d;

    /// <summary>
    /// Heuristic: a factor left at <see cref="Vector3.One"/> is an unexpressed factor, so the world
    /// fills it in; any other value is an authored override and is kept as is.
    /// Known limitation: a body meant to stay unconstrained in a planar world is not expressible,
    /// since its unconstrained factors are exactly the ones the policy reads as unexpressed.
    /// </summary>
    public override void ApplyDefaultConstraints(PhysicsDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.LinearFactor == Vector3.One)
        {
            definition.LinearFactor = new Vector3(1f, 1f, 0f);
        }

        if (definition.AngularFactor == Vector3.One)
        {
            definition.AngularFactor = new Vector3(0f, 0f, 1f);
        }
    }
}

/// <summary>
/// Policy of a top-down world with elevation: X goes east, Y is the depth on the ground and Z is the
/// elevation above it. The simulation runs in that logical space; the render space folds the two vertical
/// axes into a single screen axis, so two entities at different elevations may overlap on screen while
/// staying far apart in the simulation.
/// </summary>
public sealed class TopDownElevationSimulationSpacePolicy : SimulationSpacePolicy
{
    public override string Name => SimulationSpacePolicyNames.TopDownElevation;

    /// <summary>Screen position of a logical position: (X, -(Y - Z), 0).</summary>
    public override Vector3 DeriveRenderPosition(Vector3 logicalPosition)
    {
        return new Vector3(logicalPosition.X, -(logicalPosition.Y - logicalPosition.Z), 0f);
    }
}
