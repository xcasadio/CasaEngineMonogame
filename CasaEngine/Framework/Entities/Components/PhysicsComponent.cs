using System.ComponentModel;
using System.Text.Json;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Physics")]
public class PhysicsComponent : Component
{
    public static readonly int ComponentId = (int)ComponentIds.Physics;

    public PhysicsComponent(Entity entity) : base(entity, ComponentId)
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
}