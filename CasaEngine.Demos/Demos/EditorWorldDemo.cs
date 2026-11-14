using System;
using System.Collections.Generic;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// A world made in the editor, loaded as it is: this demo creates no entity, no mesh, no light and no camera. Every
/// other demo builds its scene in code to show an API; a game is usually made this way instead.
/// <para/>
/// The world is <c>Content/EditorWorld/EditorWorld.world</c>, a copy of the first world of
/// <c>Projects/SampleProject</c>, with the assets it names: two entity assets (a box, the camera), two static models, a
/// material and its texture. Each is listed in <c>Content/AssetInfos.json</c>, which is how the world finds them by id.
/// <para/>
/// To change it: open <c>CasaEngine.Demos/Content/DemosGame.json</c> in the editor, open the world, move or add
/// entities, save, and start this demo again.
/// </summary>
public class EditorWorldDemo : Demo
{
    private const string WorldFileName = @"EditorWorld\EditorWorld.world";

    public override string Title => "World made in the editor";

    public override string Description =>
        "A world authored in the editor and loaded from its file: a ground with a collider, three boxes placed " +
        "from one entity asset, two lights and a camera. The demo creates nothing in code; it asks for the " +
        "world by name and frames its camera. Open Content/DemosGame.json in the editor to change the world, then " +
        "start the demo again.";

    public override IReadOnlyList<string> Highlights { get; } = new[]
    {
        "The three boxes are instances of one entity asset (Entities/Box.entity): edit the asset, all three change.",
        "The lights and the camera are entities saved in the world, like the boxes.",
        "The camera is the one saved in the world; the demo only frames the scene and lets the mouse move it.",
    };

    public override string DocumentationPath => "docs/editor/play-in-editor.md";

    /// <summary>
    /// The world of this demo comes from a file. This is what the engine does itself for the first world of a project
    /// (<c>FirstWorldLoaded</c> in the project file) and for <c>GameManager.SetWorldToLoad(name)</c>: find the world in
    /// the asset catalogue, then load a copy of it.
    /// </summary>
    public override World CreateWorld(CasaEngineGame game)
    {
        AssetInfo worldAsset = AssetCatalog.GetByFileName(WorldFileName)
            ?? throw new InvalidOperationException($"World not found in the asset catalog: '{WorldFileName}'");

        return game.AssetContentManager.LoadCopy<World>(worldAsset.Id);
    }

    public override void Initialize(CasaEngineGame game)
    {
        // Nothing to build: the entities are in the world file.
    }

    public override void ConfigureSceneLighting(World world)
    {
        // The world brings its own lights; the demos' default light rig is not added.
    }

    /// <summary>The world has its own camera entity, so the demo creates none: the engine uses the world's.</summary>
    public override CameraComponent CreateCamera(CasaEngineGame game) => null;

    /// <summary>
    /// Called with the world's camera once the world is loaded. The only code of this demo that touches the scene: the
    /// camera saved in the world stands under the ground, so the demo frames the scene, and it gives the camera the
    /// script the other demos use so that the mouse moves it. A game would save a camera where it wants it, with its
    /// own script.
    /// </summary>
    public override void InitializeCamera(CameraComponent camera)
    {
        if (camera is not ArcBallCameraComponent arcBallCamera)
        {
            return;
        }

        arcBallCamera.SetCamera(new Vector3(0f, 14f, 30f), Vector3.Zero, Vector3.Up);

        var cameraScript = new ScriptArcBallCamera();
        camera.Owner.SetGameplayProxy(cameraScript);
        // SetGameplayProxy initializes the script on an entity that is already initialized, but the world part of the
        // initialization only runs while the world loads: run it here.
        cameraScript.InitializeWithWorld(camera.Owner.World);
    }

    public override void Update(GameTime gameTime)
    {
    }

    public override void Clean()
    {
    }
}
