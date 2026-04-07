
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Rendering.Geometry;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Sprites;

public class Collision2d
{
    public CollisionHitType CollisionHitType;
    public Shape2d Shape;

    public void Load(JObject JObject)
    {
        CollisionHitType = JObject["collision_type"].GetEnum<CollisionHitType>();
        Shape = ShapeLoader.LoadShape2d(JObject);
    }

    public override string ToString()
    {
        return $"{Shape} {CollisionHitType}";
    }
}