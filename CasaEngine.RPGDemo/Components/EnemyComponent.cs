using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Framework.Entities;
using CasaEngine.RPGDemo.Controllers;
using Component = CasaEngine.Framework.Entities.Components.Component;

namespace CasaEngine.RPGDemo.Components;

[DisplayName("EnemyComponent")]
public class EnemyComponent : CharacterComponent
{
    public static readonly int ComponentId = (int)RpgDemoComponentIds.EnemyComponent;


    public EnemyComponent(Entity owner) : base(owner, ComponentId)
    {
        Controller = new EnemyController(Character);
    }

    public override Component Clone(Entity owner)
    {
        return new EnemyComponent(owner);
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