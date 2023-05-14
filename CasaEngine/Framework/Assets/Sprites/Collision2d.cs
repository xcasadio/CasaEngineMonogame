using System.Text.Json;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Shapes;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Sprites;

public class Collision2d
{
    public CollisionHitType CollisionHitType;
    public Shape2d Shape;

    public void Load(JsonElement jsonElement)
    {
        CollisionHitType = jsonElement.GetJsonPropertyByName("collision_type").Value.GetEnum<CollisionHitType>();
        Shape = ShapeLoader.LoadShape2d(jsonElement);
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