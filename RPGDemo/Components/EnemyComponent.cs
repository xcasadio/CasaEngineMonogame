﻿using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using RPGDemo.Controllers;
using Component = CasaEngine.Framework.Entities.Component;

namespace RPGDemo.Components;

[DisplayName("EnemyComponent")]
public class EnemyComponent : Component
{
    public static readonly int ComponentId = (int)RpgDemoComponentIds.EnemyComponent;

    public EnemyController Controller { get; }

    public Character Character { get; }

    public EnemyComponent(Entity owner) : base(owner, ComponentId)
    {
        Character = new Character(owner);
        Controller = new EnemyController(Character);
    }

    public override void Initialize(CasaEngineGame game)
    {
        Controller.Initialize(game);
    }

    public override void Update(float elapsedTime)
    {
        Controller.Update(elapsedTime);
    }

    public override void Draw()
    {

    }

    public override void Load(JsonElement element)
    {

    }

    public override Component Clone(Entity owner)
    {
        return new EnemyComponent(owner);
    }
}