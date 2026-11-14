using System.Collections.Generic;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Scene.World;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Rigid bodies in 2D, with the same components as the 3D demo. Three things to read here:
/// <list type="bullet">
///   <item>a 2D body is a 3D body that may only move in the plane: see <see cref="KeepInPlane"/>;</item>
///   <item>throwing a body: the same three lines as in the 3D demo (<see cref="ThrowBall"/>);</item>
///   <item>a trigger volume: a body that collides with nothing and only reports what overlaps it
///   (<see cref="CreateTriggerZone"/>), read each frame to switch a lamp.</item>
/// </list>
/// </summary>
public class Collision2dBasicDemo : Demo
{
    private const int Columns = 5;
    private const int Rows = 5;
    private const int BodySize = 2;

    // The balls the player throws: made with the scene and reused in turn.
    private const int BallCount = 8;
    private static readonly Vector3 ThrowPosition = new(-14f, 5f, 0f);
    private static readonly Vector3 ThrowVelocity = new(24f, 3f, 0f);

    private static readonly Color LampOff = new(70, 70, 70);
    private static readonly Color LampOn = new(60, 220, 90);

    private readonly StaticModelComponent[] _balls = new StaticModelComponent[BallCount];
    private readonly CollisionComponent[] _ballBodies = new CollisionComponent[BallCount];
    private int _nextBall;

    private CasaEngineGame? _game;
    private CollisionComponent? _triggerZone;
    private LitDiffuseMaterial? _lampMaterial;
    private KeyboardState _previousKeyboard;

    public override string Title => "Collision 2d basic demo";
    public override string Description =>
        "Rigid bodies in 2D: boxes and discs kept in a plane fall on a static ground and pile up. Throw balls " +
        "at the pile to push bodies into the trigger zone on the right: the lamp above it is lit while " +
        "something is inside.";

    public override IReadOnlyList<DemoControl> Controls { get; } = new DemoControl[]
    {
        new("Space", "throw a ball at the pile, from the left"),
    };

    public override IReadOnlyList<string> Highlights { get; } = new[]
    {
        "The same CollisionComponent as in 3D: LinearFactor and AngularFactor keep each body in the plane.",
        "The trigger zone stops nothing: bodies go through it, and it only reports that they are inside.",
        "The wireframes are the physics debug view: the trigger zone is drawn there too.",
    };

    public override string DocumentationPath => "docs/engine/collision-2d-3d-architecture.md";

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        World world = game.GameManager.CurrentWorld;

        var checkMaterial = new LitDiffuseMaterial
        {
            BasColor = game.AssetContentManager.LoadFromFile<Texture2D>("checkboard.png"),
            DiffuseColor = Color.White,
        };

        CreateGround(game, world, checkMaterial);
        CreatePile(game, world, checkMaterial);
        CreateBalls(game, world);
        CreateTriggerZone(game, world);
    }

    /// <summary>
    /// What makes a body 2D: it may move along X and Y but not Z, and turn around Z only. The factors multiply the
    /// motion the solver computes, so a 0 freezes that axis.
    /// </summary>
    private static void KeepInPlane(CollisionComponent body)
    {
        body.PhysicsDefinition.LinearFactor = new Vector3(1, 1, 0);
        body.PhysicsDefinition.AngularFactor = new Vector3(0, 0, 1);
    }

    private static void CreateGround(CasaEngineGame game, World world, LitDiffuseMaterial material)
    {
        var size = new Vector3(150, 1, 1);

        var entity = new Entity { Name = "ground" };
        var mesh = new StaticModelComponent();
        entity.RootComponent = mesh;
        mesh.StaticModel = StaticModel.CreateFromPrimitive(new BoxPrimitive(size.X, size.Y, size.Z));
        mesh.StaticModel.Meshes[0].Initialize(game.GraphicsDevice);
        mesh.StaticModel.Meshes[0].Material = material;

        // A unit box scaled to the size of the mesh, static: the solver never moves it.
        var body = new CollisionComponent();
        body.Fixtures.Add(new ColliderFixture(new Box { Size = Vector3.One }));
        mesh.AddChildComponent(body);
        body.PhysicsDefinition.PhysicsType = PhysicsType.Static;
        body.PhysicsDefinition.Mass = 0.0f;
        body.Scale = size;
        KeepInPlane(body);

        world.AddEntity(entity);
    }

    /// <summary>A staircase of columns, boxes and discs alternating, dropped from above the ground.</summary>
    private static void CreatePile(CasaEngineGame game, World world, LitDiffuseMaterial material)
    {
        for (int column = 0; column < Columns; ++column)
        {
            for (int row = column; row < Rows; ++row)
            {
                var entity = new Entity { Name = $"shape 2d {row}-{column}" };
                var mesh = new StaticModelComponent();
                entity.RootComponent = mesh;
                // Columns one body apart: bodies must not overlap when they appear, the solver would fling them apart.
                mesh.Position = new Vector3(column * BodySize + 3, 8 + row * BodySize, 0);

                var body = new CollisionComponent();
                if (row % 2 == 0)
                {
                    body.Fixtures.Add(new ColliderFixture(new Box { Size = Vector3.One }));
                    body.Scale = new Vector3(BodySize, BodySize, 1f);
                    mesh.StaticModel = StaticModel.CreateFromPrimitive(new BoxPrimitive(BodySize, BodySize, 1.0f));
                }
                else
                {
                    body.Fixtures.Add(new ColliderFixture(new Sphere()));
                    body.Scale = new Vector3(BodySize / 2f);
                    mesh.StaticModel = StaticModel.CreateFromPrimitive(new SpherePrimitive(BodySize));
                    // A rolling sphere meets no rolling resistance: a light angular damping makes it settle.
                    body.PhysicsDefinition.AngularDamping = 0.3f;
                }

                mesh.AddChildComponent(body);
                body.PhysicsDefinition.PhysicsType = PhysicsType.Dynamic;
                body.PhysicsDefinition.Mass = 1.0f;
                KeepInPlane(body);

                mesh.StaticModel.Meshes[0].Initialize(game.GraphicsDevice);
                mesh.StaticModel.Meshes[0].Material = material;

                world.AddEntity(entity);
            }
        }
    }

    /// <summary>The balls wait on the ground, on the left, until they are thrown.</summary>
    private void CreateBalls(CasaEngineGame game, World world)
    {
        StaticModel ballModel = StaticModel.CreateFromPrimitive(new SpherePrimitive(1.5f));
        ballModel.Meshes[0].Initialize(game.GraphicsDevice);
        ballModel.Meshes[0].Material = new LitDiffuseMaterial { DiffuseColor = new Color(220, 60, 50) };

        for (int i = 0; i < BallCount; i++)
        {
            var entity = new Entity { Name = $"ball {i}" };
            var mesh = new StaticModelComponent();
            entity.RootComponent = mesh;
            mesh.Position = new Vector3(-40f + i * 2.5f, 1.5f, 0f);
            mesh.StaticModel = ballModel;

            var body = new CollisionComponent();
            body.Fixtures.Add(new ColliderFixture(new Sphere { Radius = 0.75f }));
            mesh.AddChildComponent(body);
            body.PhysicsDefinition.PhysicsType = PhysicsType.Dynamic;
            body.PhysicsDefinition.Mass = 3.0f;
            body.PhysicsDefinition.AngularDamping = 0.3f;
            KeepInPlane(body);

            _balls[i] = mesh;
            _ballBodies[i] = body;
            world.AddEntity(entity);
        }
    }

    /// <summary>
    /// A trigger volume and the lamp that shows its state. The zone uses the collision profile "Trigger", which answers
    /// every other body with an overlap instead of a block: nothing bounces on it, and the bodies inside it are listed
    /// in <see cref="PhysicsBaseComponent.Collisions"/>.
    /// </summary>
    private void CreateTriggerZone(CasaEngineGame game, World world)
    {
        var zoneCentre = new Vector3(21f, 3.5f, 0f);
        var zoneSize = new Vector3(8f, 6f, 1f);

        var zoneEntity = new Entity { Name = "trigger zone" };
        _triggerZone = new CollisionComponent();
        zoneEntity.RootComponent = _triggerZone;
        _triggerZone.Position = zoneCentre;
        _triggerZone.Fixtures.Add(new ColliderFixture(new Box { Size = zoneSize }));
        // Kinetic: moved by gameplay only (here it never moves). The profile is what makes it a sensor.
        _triggerZone.PhysicsDefinition.PhysicsType = PhysicsType.Kinetic;
        _triggerZone.PhysicsDefinition.ProfileName = CollisionProfileNames.Trigger;
        world.AddEntity(zoneEntity);

        // The lamp: a plain mesh above the zone. It has no collider; the demo changes its colour.
        _lampMaterial = new LitDiffuseMaterial { DiffuseColor = LampOff };
        var lampEntity = new Entity { Name = "lamp" };
        var lamp = new StaticModelComponent();
        lampEntity.RootComponent = lamp;
        lamp.StaticModel = StaticModel.CreateFromPrimitive(new BoxPrimitive(zoneSize.X, 1f, 1f));
        lamp.StaticModel.Meshes[0].Initialize(game.GraphicsDevice);
        lamp.StaticModel.Meshes[0].Material = _lampMaterial;
        lamp.Position = zoneCentre + new Vector3(0f, zoneSize.Y / 2f + 1.5f, 0f);
        world.AddEntity(lampEntity);
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        // Face the plane of the simulation, far enough to see the pile and the trigger zone.
        ((ArcBallCameraComponent)camera).SetCamera(new Vector3(8f, 9f, 42f), new Vector3(8f, 6f, 0f), Vector3.Up);
    }

    public override void Update(GameTime gameTime)
    {
        if (_game == null)
        {
            return;
        }

        KeyboardState keyboard = DemoKeyboard.Read(_game);
        if (keyboard.IsKeyDown(Keys.Space) && !_previousKeyboard.IsKeyDown(Keys.Space))
        {
            ThrowBall();
        }

        _previousKeyboard = keyboard;

        // Reading the trigger: the physics world keeps the list of what overlaps the zone up to date.
        if (_triggerZone != null && _lampMaterial != null)
        {
            _lampMaterial.DiffuseColor = _triggerZone.Collisions.Count > 0 ? LampOn : LampOff;
        }
    }

    /// <summary>Same as in the 3D demo: write the transform, push it to the body, then set the velocity.</summary>
    private void ThrowBall()
    {
        StaticModelComponent ball = _balls[_nextBall];
        CollisionComponent body = _ballBodies[_nextBall];
        _nextBall = (_nextBall + 1) % BallCount;

        ball.Position = ThrowPosition;
        body.SyncTransformFromScene();
        body.Velocity = ThrowVelocity;
    }

    public override void Clean()
    {
        _triggerZone = null;
        _lampMaterial = null;
        _game = null;
        _nextBall = 0;
        _previousKeyboard = default;
    }
}
