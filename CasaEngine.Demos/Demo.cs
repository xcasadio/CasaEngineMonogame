using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos;

public abstract class Demo
{
    public abstract string Title { get; }

    public abstract void Initialize(CasaEngineGame game);

    public virtual CameraComponent CreateCamera(CasaEngineGame game)
    {
        var entity = new Entity();
        var camera = new ArcBallCameraComponent(entity);
        entity.ComponentManager.Components.Add(camera);
        camera.SetCamera(Vector3.Backward * 15 + Vector3.Up * 12, Vector3.Zero, Vector3.Up);
        var gamePlayComponent = new GamePlayComponent(entity);
        entity.ComponentManager.Components.Add(gamePlayComponent);
        gamePlayComponent.ExternalComponent = new ScriptArcBallCamera();
        game.GameManager.CurrentWorld.AddEntityImmediately(entity);

        return camera;
    }

    public abstract void Update(GameTime gameTime);

    public void Clean(World world)
    {
        world.ClearEntities();

        Clean();
    }

    protected abstract void Clean();
}