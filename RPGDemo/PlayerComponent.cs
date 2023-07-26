using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using RPGDemo.Controllers;
using Component = CasaEngine.Framework.Entities.Component;

namespace RPGDemo;

[DisplayName("PlayerComponent")]
public class PlayerComponent : Component
{
    public static readonly int ComponentId = (int)ComponentIds.Custom + 1;

    private PlayerController _playerController;

    public PlayerComponent(Entity owner) : base(owner, ComponentId)
    {
        _playerController = new HumanPlayerController(new Character(owner), PlayerIndex.One);
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

    public override void Load(JsonElement element)
    {

    }
}