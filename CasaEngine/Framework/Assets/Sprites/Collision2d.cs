
using CasaEngine.Core.Serialization;
using CasaEngine.Core.Shapes;
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

#if EDITOR
    public void Save(JObject jObject)
    {
        jObject.Add("collision_type", CollisionHitType.ConvertToString());
        Shape.Save(jObject);
    }

    public override string ToString()
    {
        return $"{Shape} {CollisionHitType}";
    }
#endif
}