﻿using BulletSharp;
using CasaEngine.Engine.Physics;
using CasaEngine.Engine.Renderer;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Game.Components.Physics;

public class PhysicsDebugViewRendererComponent : DrawableGameComponent
{
    public static bool DisplayPhysics;
    private PhysicsDebugDraw _physicsDebugRenderer;
    private readonly CasaEngineGame _game;

    public PhysicsDebugViewRendererComponent(Microsoft.Xna.Framework.Game game) : base(game)
    {
        _game = (game as CasaEngineGame)!;
        game.Components.Add(this);
        UpdateOrder = (int)ComponentUpdateOrder.DebugPhysics;
        DrawOrder = (int)ComponentDrawOrder.DebugPhysics;
    }

    protected override void LoadContent()
    {
        base.LoadContent();
        var line3dRendererComponent = Game.GetGameComponent<Line3dRendererComponent>();
        var physicsEngineComponent = Game.GetGameComponent<PhysicsEngineComponent>();
        _physicsDebugRenderer = new PhysicsDebugDraw(line3dRendererComponent) { DebugMode = DebugDrawModes.MaxDebugDrawMode };
        physicsEngineComponent.PhysicsEngine.World.DebugDrawer = _physicsDebugRenderer;
    }

    public override void Draw(GameTime gameTime)
    {
        if (DisplayPhysics)
        {
            _physicsDebugRenderer.DrawDebugWorld(_game.GameManager.PhysicsEngineComponent.PhysicsEngine.World);
        }
    }
}