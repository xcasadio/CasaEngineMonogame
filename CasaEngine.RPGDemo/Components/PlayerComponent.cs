using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.RPGDemo.Controllers;
using Microsoft.Xna.Framework;
using Component = CasaEngine.Framework.Entities.Components.Component;

namespace CasaEngine.RPGDemo.Components;

[DisplayName("PlayerComponent")]
public class PlayerComponent : CharacterComponent
{
    public static readonly int ComponentId = (int)RpgDemoComponentIds.PlayerComponent;

    private readonly HumanPlayerController _playerController;

    public PlayerComponent(Entity owner) : base(owner, ComponentId)
    {
        _playerController = new HumanPlayerController(Character, PlayerIndex.One);
        Controller = _playerController; ;
    }

    public override Component Clone(Entity owner)
    {
        return new PlayerComponent(owner);
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