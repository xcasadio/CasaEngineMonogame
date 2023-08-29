using System.Text.Json;
using CasaEngine.Core.Design;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

public class PlayerStartComponent : Component
{
    public static readonly int ComponentId = (int)ComponentIds.PlayerStart;

    public PlayerStartComponent(Entity entity) : base(entity, ComponentId)
    {
    }

    public override void Update(float elapsedTime)
    {
    }

    public override Component Clone(Entity owner)
    {
        var component = new PlayerStartComponent(owner);
        return component;
    }

    public override void Load(JsonElement element, SaveOption option)
    {
    }


#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);
    }

#endif
}