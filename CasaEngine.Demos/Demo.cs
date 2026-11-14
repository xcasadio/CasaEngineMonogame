using CasaEngine.Framework.Application;
using System;
using System.Collections.Generic;
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

    /// <summary>
    /// The inputs this demo reads itself, in the order the player should try them. Leave out "back to the menu" and the
    /// default camera: the demos app adds them. The list is static data, built once.
    /// </summary>
    public virtual IReadOnlyList<DemoControl> Controls => Array.Empty<DemoControl>();

    /// <summary>What to look at while the demo runs, one short sentence per entry.</summary>
    public virtual IReadOnlyList<string> Highlights => Array.Empty<string>();

    /// <summary>Repository-relative path of the page that documents the feature ("docs/engine/audio-system.md"), or empty.</summary>
    public virtual string DocumentationPath => string.Empty;

    /// <summary>
    /// True for a demo that reads its own back buffer to check itself. The app then draws neither the reminder nor the
    /// help panel over it: they would be part of the image the demo reads. Esc and Select still return to the menu.
    /// </summary>
    public virtual bool ReadsItsOwnImage => false;

    /// <summary>
    /// Creates the world of this demo, before <see cref="Initialize"/>. The default is a new, empty world that the demo
    /// fills in code; a demo overrides this to load a world made in the editor.
    /// </summary>
    public virtual World CreateWorld(CasaEngineGame game) => new World();

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
        entity.SetGameplayProxy(new ScriptArcBallCamera());
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
    /// The graphics device viewport is the scene area (the layout area of the views, ADR-0070), so an overlay placed
    /// from the viewport stays inside the scene; it is restored after the call.
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