using CasaEngine.Framework.Game;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.SceneManagement.Components;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos;

public abstract class Demo
{
    public abstract string Title { get; }

    public abstract void Initialize(CasaEngineGame game);

    public virtual CameraComponent CreateCamera(CasaEngineGame game)
    {
        var entity = new Entity();
        var camera = new ArcBallCameraComponent();
        entity.RootComponent = camera;
        entity.GameplayProxyClassName = nameof(ScriptArcBallCamera);
        entity.Initialize();
        //entity.InitializeWithWorld(game.GameManager.CurrentWorld);
        game.GameManager.CurrentWorld.AddEntity(entity);

        return camera;
    }

    public virtual void InitializeCamera(CameraComponent camera)
    {
        ((ArcBallCameraComponent)camera).SetCamera(Vector3.Backward * 15 + Vector3.Up * 12, Vector3.Zero, Vector3.Up);
    }

    public abstract void Update(GameTime gameTime);

    public abstract void Clean();
}