﻿using CasaEngine.Framework.Game;

namespace CasaEngine.RPGDemo.Controllers;

public class PlayerController : Controller
{
    protected PlayerController(Character character)
        : base(character)
    {
    }

    public override void Initialize(CasaEngineGame game)
    {
        base.Initialize(game);
    }
}