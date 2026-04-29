using CasaEngine.Framework.Application;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos;

public abstract class Demo
{
    public abstract string Title { get; }

    /// <summary>Short English description of what this demo demonstrates.</summary>
    public virtual string Description => string.Empty;

    public abstract void Initialize(CasaEngineGame game);

    public virtual void ConfigureSceneLighting(World world)
    {
        DemoSceneLightRig.AddLegacyDirectionalRig(world);
    }

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

    /// <summary>
    /// Called after the render pipeline has run, before the frame is presented.
    /// Override to draw 2D overlays (SpriteBatch, render-to-texture thumbnails, etc.).
    /// </summary>
    public virtual void PostDraw(CasaEngineGame game, GameTime gameTime)
    {
    }

    /// <summary>
    /// Called when the game window is resized. Override to recompute multi-view
    /// split-screen layouts (camera projection + BackBufferSurface rects).
    /// The default implementation is a no-op.
    /// </summary>
    public virtual void OnScreenResized(CasaEngineGame game, int width, int height) { }

    public abstract void Clean();
}