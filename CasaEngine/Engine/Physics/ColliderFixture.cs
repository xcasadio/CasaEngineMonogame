using CasaEngine.Core.Serialization;
using CasaEngine.Engine.Geometry;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Engine.Physics;

/// <summary>
/// One collision volume of a body: a pure geometric shape placed in the local space of the
/// component owning it, with its own collision profile and an identification tag.
/// </summary>
public sealed class ColliderFixture
{
    public ColliderFixture()
    {
    }

    public ColliderFixture(Shape3d shape)
    {
        Shape = shape;
    }

    public ColliderFixture(ColliderFixture other)
    {
        Shape = other.Shape;
        LocalPosition = other.LocalPosition;
        LocalRotation = other.LocalRotation;
        ProfileName = other.ProfileName;
        Tag = other.Tag;
    }

    public Shape3d Shape { get; set; }

    public Vector3 LocalPosition { get; set; }

    public Quaternion LocalRotation { get; set; } = Quaternion.Identity;

    /// <summary>
    /// Collision profile of this fixture. When empty the fixture inherits the profile of the
    /// <see cref="PhysicsDefinition"/> of the component owning it.
    /// </summary>
    public string ProfileName { get; set; }

    /// <summary>
    /// Free identifier reported by contact and query results to tell fixtures of a body apart.
    /// </summary>
    public string Tag { get; set; }

    public bool HasIdentityPose => LocalPosition == Vector3.Zero && LocalRotation == Quaternion.Identity;

    public Matrix GetLocalMatrix()
    {
        var matrix = Matrix.CreateFromQuaternion(LocalRotation);
        matrix.Translation = LocalPosition;
        return matrix;
    }

    public void Load(JObject element)
    {
        Shape = ShapeLoader.LoadShape3d((JObject)element["shape"]);

        var localPositionElement = element["local_position"];
        LocalPosition = localPositionElement == null ? Vector3.Zero : localPositionElement.GetVector3();

        var localRotationElement = element["local_rotation"];
        LocalRotation = localRotationElement == null ? Quaternion.Identity : localRotationElement.GetQuaternion();

        ProfileName = element["collision_profile"]?.Value<string>();
        Tag = element["tag"]?.Value<string>();
    }
}
