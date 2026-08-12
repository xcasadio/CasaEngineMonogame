
using CasaEngine.Core.Serialization;
using CasaEngine.Engine.Geometry;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Sprites;

/// <summary>
/// An authoring collision volume attached to a sprite or a tile: a local pose, a shape and the
/// collision profile the lowered body must use.
/// </summary>
public class Collision2d
{
    public Vector2 LocalPosition;
    public float Rotation;

    /// Name of the collision profile of the body. Empty means the reserved Trigger profile.
    public string ProfileName = string.Empty;

    /// Free gameplay tag carried by the volume.
    public string Tag = string.Empty;

    public Shape2d Shape;

    public void Load(JObject JObject)
    {
        LocalPosition = JObject["location"].GetVector2();
        Rotation = JObject["orientation"].GetSingle();
        ProfileName = JObject["collision_profile"]?.ToString() ?? string.Empty;
        Tag = JObject["tag"]?.ToString() ?? string.Empty;
        Shape = ShapeLoader.LoadShape2d(JObject);
    }

    public override string ToString()
    {
        return $"{Shape} {ProfileName}";
    }
}
