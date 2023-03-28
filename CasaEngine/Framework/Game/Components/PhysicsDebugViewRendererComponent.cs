using Microsoft.Xna.Framework;
using PhysicsDebug;

namespace CasaEngine.Framework.Game.Components;

public class PhysicsDebugViewRendererComponent : DrawableGameComponent, IPhysicsDebugRenderer
{
    public static bool DisplayPhysics = true;
    private PhysicsDebugRenderer _physicsDebugRenderer;

    public PhysicsDebugViewRendererComponent(Microsoft.Xna.Framework.Game game) : base(game)
    {
        game.Components.Add(this);
        UpdateOrder = (int)ComponentUpdateOrder.DebugPhysics;
        DrawOrder = (int)ComponentDrawOrder.DebugPhysics;
    }

    protected override void LoadContent()
    {
        base.LoadContent();
        var physicsEngineComponent = Game.GetGameComponent<PhysicsEngineComponent>();
        _physicsDebugRenderer = new PhysicsDebugRenderer(physicsEngineComponent.PhysicsEngine.Simulation, this);
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        _physicsDebugRenderer.Update();
    }

    public override void Draw(GameTime gameTime)
    {
        if (DisplayPhysics)
        {
            _physicsDebugRenderer.Render();
        }
    }

    public void DrawBox(float halfWidth, float halfHeight, float halfLength, Vector3 boxPosition, Quaternion boxOrientation, Vector3 boxColor)
    {

    }

    public void DrawSphere(float radius, Vector3 position, Quaternion orientation, Vector3 color)
    {

    }

    public void DrawCapsule(float radius, float halfLength, Vector3 position, Quaternion orientation, Vector3 color)
    {

    }

    public void DrawTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 position, Quaternion orientation, Vector3 color)
    {

    }
}