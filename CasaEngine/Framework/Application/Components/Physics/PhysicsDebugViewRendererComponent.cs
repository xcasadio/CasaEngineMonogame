using BulletSharp;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Application.Components.Physics;

public class PhysicsDebugViewRendererComponent : DrawableGameComponent
{
    private PhysicsDebugDrawComponent _physicsDebugRenderer;
    private readonly Dictionary<ViewId, string> _lastPhysicsWorldNamesByViewId = [];
    private readonly Dictionary<ViewId, int> _lastPhysicsObjectCountsByViewId = [];

    public bool DisplayPhysics { get; set; } = true;

    public PhysicsDebugViewRendererComponent(Game game) : base(game)
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

        var dynamicsWorld = view.World.PhysicsWorld.BulletPhysicsEngine.World;
        if (dynamicsWorld == null)
        {
            return;
        }

        if (!ReferenceEquals(dynamicsWorld.DebugDrawer, _physicsDebugRenderer))
        {
            dynamicsWorld.DebugDrawer = _physicsDebugRenderer;
        }

        _physicsDebugRenderer.DrawDebugWorld(dynamicsWorld);
        _lastPhysicsWorldNamesByViewId[view.Id] = view.World.Name;
        _lastPhysicsObjectCountsByViewId[view.Id] = dynamicsWorld.CollisionObjectArray.Count;
    }

    public bool TryGetLastRenderedPhysicsWorldName(ViewId viewId, out string worldName)
    {
        return _lastPhysicsWorldNamesByViewId.TryGetValue(viewId, out worldName);
    }

    public int GetLastRenderedPhysicsObjectCount(ViewId viewId)
    {
        if (_lastPhysicsObjectCountsByViewId.TryGetValue(viewId, out int count))
        {
            return count;
        }

        return -1;
    }

    public override void Draw(GameTime gameTime)
    {
    }
}