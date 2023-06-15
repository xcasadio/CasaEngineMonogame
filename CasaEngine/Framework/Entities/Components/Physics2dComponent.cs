using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Framework.Game;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Physics 2d")]
public class Physics2dComponent : Component
{
    public static readonly int ComponentId = (int)ComponentIds.Physics2d;

    public Physics2dComponent(Entity entity) : base(entity, ComponentId)
    {
    }

    public override void Initialize(CasaEngineGame game)
    {

    }

    public override void Update(float elapsedTime)
    {

    }

    public override void Load(JsonElement element)
    {

    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        throw new NotImplementedException();
    }
#endif
}
