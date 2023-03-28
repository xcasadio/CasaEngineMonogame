using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Physics 2d")]
public class Physics2dComponent : Component
{
    public static readonly int ComponentId = (int)ComponentIds.Physics2d;

    public Physics2dComponent(Entity entity) : base(entity, ComponentId)
    {
    }

    public override void Initialize()
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
