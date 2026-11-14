using System.Collections.Generic;
using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// The smallest scene worth looking at: one cube, one light, one camera. Read this file first; every other demo is this
/// one with more components.
/// <para/>
/// The four ideas it shows:
/// <list type="bullet">
///   <item>A <see cref="World"/> holds entities. The demos app gives each demo a new, empty world.</item>
///   <item>An <see cref="Entity"/> is a name and a tree of components; its root component carries its transform.</item>
///   <item>Something is visible because a component draws it (here a <see cref="StaticModelComponent"/>) and a material
///   says how.</item>
///   <item>A lit material needs a light, and a world needs a camera: both are entities too.</item>
/// </list>
/// </summary>
public class HelloWorldDemo : Demo
{
    // Radians per second.
    private const float SpinSpeed = 0.8f;

    private StaticModelComponent? _cube;
    private float _angle;

    public override string Title => "Hello world";

    public override string Description =>
        "The smallest scene: one entity with a cube mesh and a material, one directional light, one camera. " +
        "The cube spins because the demo writes its orientation every frame. Read HelloWorldDemo.cs first: " +
        "every other demo is this one with more components.";

    public override IReadOnlyList<string> Highlights { get; } = new[]
    {
        "The cube is one entity; its root component is the mesh, and the mesh carries the transform.",
        "The faces are shaded: the material is lit, and the world holds one directional light.",
        "The camera is an entity too: move it with the mouse, the scene does not change.",
    };

    public override string DocumentationPath => "docs/engine/demos-main-menu.md";

    public override void Initialize(CasaEngineGame game)
    {
        // The world this demo owns. It is empty and not loaded yet: entities added here are created with it.
        World world = game.GameManager.CurrentWorld;

        // 1. The mesh: a unit cube built in code. CreateFromPrimitive wraps it in a model with one mesh, and
        //    Initialize uploads its vertices to the graphics card.
        StaticModel cubeModel = StaticModel.CreateFromPrimitive(new BoxPrimitive());
        cubeModel.Meshes[0].Initialize(game.GraphicsDevice);

        // 2. The material: how the mesh reacts to light. No texture here, only a colour.
        cubeModel.Meshes[0].Material = new LitDiffuseMaterial { DiffuseColor = new Color(230, 140, 60) };

        // 3. The entity. Its root component is the one that places it in the world.
        var cubeEntity = new Entity { Name = "cube" };
        _cube = new StaticModelComponent { StaticModel = cubeModel };
        cubeEntity.RootComponent = _cube;
        _cube.Position = new Vector3(0f, 1f, 0f);
        _cube.Scale = new Vector3(2f);

        world.AddEntity(cubeEntity);
    }

    /// <summary>
    /// The base class adds three lights so that any demo is readable. This demo adds its single light itself, to show
    /// that a light is an entity with a <see cref="LightComponent"/>.
    /// </summary>
    public override void ConfigureSceneLighting(World world)
    {
        var lightEntity = new Entity { Name = "sun" };
        lightEntity.RootComponent = new LightComponent
        {
            Type = LightType.Directional,
            // A directional light shines along its forward axis (-Z when not rotated). Turned this way it comes from
            // behind the camera, above and to the right, so the faces the camera sees are the lit ones. A face the
            // light does not reach is black: this scene has no other light.
            LocalOrientation = Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(25f), MathHelper.ToRadians(-40f), 0f),
            Color = Color.White,
            SpecularColor = Color.White,
            Intensity = 1f,
        };

        world.AddEntity(lightEntity);

    }

    /// <summary>
    /// Same camera as the base class, written out: an entity whose root component is the camera, moved by a script
    /// that reads the mouse and the keyboard (the "Camera" lines of the help panel).
    /// </summary>
    public override CameraComponent CreateCamera(CasaEngineGame game)
    {
        var cameraEntity = new Entity { Name = "camera" };
        var camera = new ArcBallCameraComponent();
        cameraEntity.RootComponent = camera;
        cameraEntity.SetGameplayProxy(new ScriptArcBallCamera());
        cameraEntity.Initialize();
        game.GameManager.CurrentWorld.AddEntity(cameraEntity);

        return camera;
    }

    /// <summary>Called once the world is loaded: where the camera stands and what it looks at.</summary>
    public override void InitializeCamera(CameraComponent camera)
    {
        ((ArcBallCameraComponent)camera).SetCamera(new Vector3(5f, 4f, 7f), new Vector3(0f, 1f, 0f), Vector3.Up);
    }

    public override void Update(GameTime gameTime)
    {
        if (_cube == null)
        {
            return;
        }

        // Gameplay code changes the transform of a component; the engine draws the result.
        _angle += SpinSpeed * (float)gameTime.ElapsedGameTime.TotalSeconds;
        _cube.Orientation = Quaternion.CreateFromAxisAngle(Vector3.Up, _angle);
    }

    public override void Clean()
    {
        // The world is already cleared when this runs: only drop what the demo itself holds.
        _cube = null;
        _angle = 0f;
    }
}
