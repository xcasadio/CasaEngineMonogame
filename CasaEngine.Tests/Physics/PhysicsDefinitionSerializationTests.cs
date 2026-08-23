using CasaEngine.EditorServices;
using CasaEngine.Engine.Physics;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Physics;

/// <summary>
/// Bullet-specific fields (AdditionalDamping*, RollingFriction, LocalInertia,
/// Linear/AngularSleepingThreshold) were removed from <see cref="PhysicsDefinition"/> when the
/// engine moved to a Bepu backend (see ai-agent/audits/analysis-bepuphysics2-migration.md §4.7).
/// <see cref="PhysicsDefinition.Load"/> must still accept assets carrying those dead keys.
/// </summary>
public class PhysicsDefinitionSerializationTests
{
    [Fact]
    public void Load_ToleratesDeadBulletKeys_AndMissingSleepThreshold()
    {
        var node = JObject.Parse("""
        {
          "physics_type": "Dynamic",
          "additional_angular_damping_factor": 0.01,
          "additional_angular_damping_threshold_sqr": 0.01,
          "additional_damping": false,
          "additional_damping_factor": 0.005,
          "additional_linear_damping_threshold_sqr": 0.01,
          "angular_damping": 0.1,
          "angular_factor": { "x": 1.0, "y": 1.0, "z": 0.0 },
          "angular_sleeping_threshold": 1.0,
          "friction": 0.6,
          "linear_damping": 0.2,
          "linear_factor": { "x": 1.0, "y": 1.0, "z": 0.0 },
          "linear_sleeping_threshold": 0.8,
          "local_inertia": { "x": 0.0, "y": 0.0, "z": 0.0 },
          "mass": 5.0,
          "restitution": 0.3,
          "rolling_friction": 0.0,
          "apply_gravity": true,
          "debug_color": "null"
        }
        """);

        var definition = new PhysicsDefinition();
        definition.Load(node);

        Assert.Equal(PhysicsType.Dynamic, definition.PhysicsType);
        Assert.Equal(0.1f, definition.AngularDamping);
        Assert.Equal(new Vector3(1f, 1f, 0f), definition.AngularFactor);
        Assert.Equal(0.6f, definition.Friction);
        Assert.Equal(0.2f, definition.LinearDamping);
        Assert.Equal(new Vector3(1f, 1f, 0f), definition.LinearFactor);
        Assert.Equal(5.0f, definition.Mass);
        Assert.Equal(0.3f, definition.Restitution);
        Assert.True(definition.ApplyGravity);
        Assert.Null(definition.DebugColor);

        // sleep_threshold is absent from this legacy node: falls back to the default.
        Assert.Equal(new PhysicsDefinition().SleepThreshold, definition.SleepThreshold);
    }

    [Fact]
    public void Load_ToleratesAnEmptyNode_AndKeepsTheCurrentPhysicsType()
    {
        var emptyNode = JObject.Parse("{}");

        var fresh = new PhysicsDefinition();
        fresh.Load(emptyNode);
        Assert.Equal(PhysicsType.Static, fresh.PhysicsType);

        var dynamic = new PhysicsDefinition { PhysicsType = PhysicsType.Dynamic, Mass = 2f };
        dynamic.Load(emptyNode);
        Assert.Equal(PhysicsType.Dynamic, dynamic.PhysicsType);
        Assert.Equal(2f, dynamic.Mass);

        var nullNode = JObject.Parse("""{ "physics_type": null }""");
        dynamic.Load(nullNode);
        Assert.Equal(PhysicsType.Dynamic, dynamic.PhysicsType);
    }

    [Fact]
    public void Load_ToleratesMinimalNode_WithOnlyPhysicsType()
    {
        var node = JObject.Parse("""{ "physics_type": "Static" }""");

        var definition = new PhysicsDefinition();
        definition.Load(node);

        Assert.Equal(PhysicsType.Static, definition.PhysicsType);
        Assert.Equal(0f, definition.AngularDamping);
        Assert.Equal(Vector3.One, definition.AngularFactor);
        Assert.Equal(0.5f, definition.Friction);
        Assert.Equal(0f, definition.LinearDamping);
        Assert.Equal(Vector3.One, definition.LinearFactor);
        Assert.Equal(new PhysicsDefinition().SleepThreshold, definition.SleepThreshold);
        Assert.Equal(0f, definition.Mass);
        Assert.Equal(0f, definition.Restitution);
        Assert.True(definition.ApplyGravity);
        Assert.Null(definition.DebugColor);
        Assert.Null(definition.ProfileName);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsTheRemainingFields()
    {
        var original = new PhysicsDefinition
        {
            PhysicsType = PhysicsType.Dynamic,
            ProfileName = "Default",
            AngularDamping = 0.15f,
            AngularFactor = new Vector3(0f, 1f, 0f),
            Friction = 0.42f,
            LinearDamping = 0.05f,
            LinearFactor = new Vector3(1f, 1f, 0f),
            SleepThreshold = 0.25f,
            Mass = 12.5f,
            Restitution = 0.1f,
            ApplyGravity = false,
            DebugColor = Color.CornflowerBlue,
        };

        var node = new JObject();
        EditorEntityJsonSerializer.SavePhysicsDefinition(original, node);

        var roundTripped = new PhysicsDefinition();
        roundTripped.Load(node);

        Assert.Equal(original.PhysicsType, roundTripped.PhysicsType);
        Assert.Equal(original.ProfileName, roundTripped.ProfileName);
        Assert.Equal(original.AngularDamping, roundTripped.AngularDamping);
        Assert.Equal(original.AngularFactor, roundTripped.AngularFactor);
        Assert.Equal(original.Friction, roundTripped.Friction);
        Assert.Equal(original.LinearDamping, roundTripped.LinearDamping);
        Assert.Equal(original.LinearFactor, roundTripped.LinearFactor);
        Assert.Equal(original.SleepThreshold, roundTripped.SleepThreshold);
        Assert.Equal(original.Mass, roundTripped.Mass);
        Assert.Equal(original.Restitution, roundTripped.Restitution);
        Assert.Equal(original.ApplyGravity, roundTripped.ApplyGravity);
        Assert.Equal(original.DebugColor, roundTripped.DebugColor);
    }
}
