using BulletSharp;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Game.Components.Physics;

public class PhysicsDebugViewRendererComponent : DrawableGameComponent
{
    private PhysicsDebugDrawComponent _physicsDebugRenderer;

    public bool DisplayPhysics { get; set; } = true;

    public PhysicsDebugViewRendererComponent(Microsoft.Xna.Framework.Game game) : base(game)
    {
        game.Components.Add(this);
        UpdateOrder = (int)ComponentUpdateOrder.DebugPhysics;
        DrawOrder = (int)ComponentDrawOrder.DebugPhysics;
    }

    protected override void LoadContent()
    {
        base.LoadContent();
        var line3dRendererComponent = Game.GetGameComponent<Line3dRendererComponent>();
        _physicsDebugRenderer = new PhysicsDebugDrawComponent(line3dRendererComponent) { DebugMode = DebugDrawModes.MaxDebugDrawMode };
    }

    public void RenderForView(RenderView view)
    {
        if (!DisplayPhysics)
        {
            return;
        }

        var dynamicsWorld = view.World.PhysicsWorldContext.PhysicsEngine.World;
        if (dynamicsWorld == null)
        {
            return;
        }

        if (!ReferenceEquals(dynamicsWorld.DebugDrawer, _physicsDebugRenderer))
        {
            dynamicsWorld.DebugDrawer = _physicsDebugRenderer;
        }

        _physicsDebugRenderer.DrawDebugWorld(dynamicsWorld);
    }

    public override void Draw(GameTime gameTime)
    {
    }
}