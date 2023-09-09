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
public class PlayerComponent : CasaEngine.Framework.Entities.Components.Component
{
    public static readonly int ComponentId = (int)RpgDemoComponentIds.PlayerComponent;

    private readonly HumanPlayerController _playerController;
    public Character Character { get; }

    public PlayerComponent(Entity owner) : base(owner, ComponentId)
    {
        Character = new Character(owner);
        _playerController = new HumanPlayerController(Character, PlayerIndex.One);
    }

    public override void Initialize(CasaEngineGame game)
    {
        _playerController.Initialize(game);
    }

    public override void Update(float elapsedTime)
    {
        _playerController.Update(elapsedTime);
    }

    public override void Draw()
    {

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