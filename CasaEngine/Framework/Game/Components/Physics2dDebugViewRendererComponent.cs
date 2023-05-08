using Genbox.VelcroPhysics.MonoGame.DebugView;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Game.Components;

public class Physics2dDebugViewRendererComponent : DrawableGameComponent
{
    public static bool DisplayPhysics = false;
    private readonly CasaEngineGame _game;
    private DebugView _shapeRenderer;

    public Physics2dDebugViewRendererComponent(Microsoft.Xna.Framework.Game game) : base(game)
    {
        _game = (CasaEngineGame)game;
        game.Components.Add(this);
        UpdateOrder = (int)ComponentUpdateOrder.DebugPhysics;
        DrawOrder = (int)ComponentDrawOrder.DebugPhysics;
    }

    public void SetCurrentPhysicsWorld(Genbox.VelcroPhysics.Dynamics.World world)
    {
        _shapeRenderer = new DebugView(world);
    }

    public override void Draw(GameTime gameTime)
    {
        if (DisplayPhysics)
        {
            var camera = _game.GameManager.ActiveCamera;
            _shapeRenderer.RenderDebugData(camera.ProjectionMatrix, camera.ViewMatrix);
        }
    }
}