using System.Text.Json;
using CasaEngine.Core.Design;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

public class PlayerStartComponent : Component
{
    public override int ComponentId => (int)ComponentIds.PlayerStart;

    public PlayerStartComponent() : base()
    {
    }

    public override void Update(float elapsedTime)
    {
    }

    public override Component Clone()
    {
        var component = new PlayerStartComponent();
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