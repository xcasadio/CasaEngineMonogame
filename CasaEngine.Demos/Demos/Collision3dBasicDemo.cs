using CasaEngine.Engine.Geometry;
using System.Collections.Generic;
using CasaEngine.Engine.Physics;
using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Scene.World;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Rigid bodies in 3D. Three things to read here:
/// <list type="bullet">
///   <item>how an entity becomes a physics body: a <see cref="CollisionComponent"/> with a shape and a
///   <see cref="PhysicsDefinition"/> (static or dynamic, mass);</item>
///   <item>how gameplay acts on a dynamic body: it sets a velocity and lets the physics world move the entity
///   (<see cref="ThrowBall"/>);</item>
///   <item>how gameplay asks the physics world a question: a sweep from the camera, answered with the body hit, the
///   point and the normal (<see cref="ProbeFromCamera"/>).</item>
/// </list>
/// </summary>
public class Collision3dBasicDemo : Demo
{
    // The balls the player throws: a fixed set made with the scene and reused in turn, so that nothing is created
    // while the demo runs.
    private const int BallCount = 12;
    private const float BallSpeed = 35f;
    private const float ProbeLength = 200f;

    private static readonly Color ProbeColor = Color.Yellow;

    private readonly StaticModelComponent[] _balls = new StaticModelComponent[BallCount];
    private readonly CollisionComponent[] _ballBodies = new CollisionComponent[BallCount];
    private int _nextBall;

    private CasaEngineGame? _game;
    private ArcBallCameraComponent? _camera;
    private PhysicsQueryShape? _probeShape;
    private KeyboardState _previousKeyboard;

    public override string Title => "Collision 3d basic demo";
    public override string Description =>
        "Rigid bodies in 3D: a stack of dynamic boxes falls on a static ground. Each box is an entity with a " +
        "mesh and a CollisionComponent; the physics world moves the entity. Throw balls at the stack, and watch " +
        "the yellow marker: it is where a sweep from the camera hits the scene.";

    public override IReadOnlyList<DemoControl> Controls { get; } = new DemoControl[]
    {
        new("Space", "throw a ball from the camera, where it looks"),
    };

    public override IReadOnlyList<string> Highlights { get; } = new[]
    {
        "The green wireframes are the physics debug view: the collider of each body, as the physics world sees it.",
        "The ground is a static body (mass 0); the boxes and the balls are dynamic bodies.",
        "The yellow marker is the answer to a physics query: the point hit in front of the camera, and its normal.",
    };

    public override string DocumentationPath => "docs/engine/collision-2d-3d-architecture.md";

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        World world = game.GameManager.CurrentWorld;

        CreateGround(game, world);
        CreateBoxStack(game, world);
        CreateBalls(game, world);
    }

    private static void CreateGround(CasaEngineGame game, World world)
    {
        var entity = new Entity { Name = "ground" };

        // What is drawn: a flat box, 50 x 1 x 50.
        var mesh = new StaticModelComponent();
        entity.RootComponent = mesh;
        mesh.StaticModel = StaticModel.CreateFromPrimitive(new BoxPrimitive(50, 1, 50));
        mesh.StaticModel.Meshes[0].Initialize(game.GraphicsDevice);
        mesh.StaticModel.Meshes[0].Material = new LitDiffuseMaterial
        {
            BasColor = game.AssetContentManager.LoadFromFile<Texture2D>("checkboard.png"),
            DiffuseColor = Color.White,
        };

        // What collides: a unit box shape scaled to the size of the mesh. A component added to the entity itself
        // (AddComponent) follows the entity. Static: the physics world never moves it, and its mass is 0.
        var body = new CollisionComponent();
        body.Fixtures.Add(new ColliderFixture(new Box()));
        entity.AddComponent(body);
        body.PhysicsDefinition.PhysicsType = PhysicsType.Static;
        body.LocalScale = new Vector3(50, 1, 50);
        body.PhysicsDefinition.Mass = 0.0f;

        world.AddEntity(entity);
    }

    private static void CreateBoxStack(CasaEngineGame game, World world)
    {
        const float mass = 1.0f;
        const int sizeX = 5, sizeY = 5, sizeZ = 5;
        var start = new Vector3(-sizeX / 2f, sizeY + 10, -sizeZ / 2f);

        // One model shared by every box: the mesh and its material exist once.
        StaticModel boxModel = StaticModel.CreateFromPrimitive(new BoxPrimitive());
        boxModel.Meshes[0].Initialize(game.GraphicsDevice);
        boxModel.Meshes[0].Material = new LitDiffuseMaterial
        {
            BasColor = game.AssetContentManager.LoadFromFile<Texture2D>("paper_box_texture.jpg"),
            DiffuseColor = Color.White,
        };

        for (int y = 0; y < sizeY; y++)
        {
            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    var entity = new Entity { Name = $"box {x}-{y}-{z}" };

                    var mesh = new StaticModelComponent();
                    entity.RootComponent = mesh;
                    mesh.Position = start + new Vector3(x, y, z);
                    mesh.StaticModel = boxModel;

                    // Dynamic: the physics world moves this body, and the body moves the mesh it is a child of.
                    var body = new CollisionComponent();
                    body.Fixtures.Add(new ColliderFixture(new Box()));
                    mesh.AddChildComponent(body);
                    body.PhysicsDefinition.PhysicsType = PhysicsType.Dynamic;
                    body.PhysicsDefinition.Mass = mass;

                    world.AddEntity(entity);
                }
            }
        }
    }

    /// <summary>The balls wait in a row at the edge of the ground until they are thrown.</summary>
    private void CreateBalls(CasaEngineGame game, World world)
    {
        StaticModel ballModel = StaticModel.CreateFromPrimitive(new SpherePrimitive(1f, 16));
        ballModel.Meshes[0].Initialize(game.GraphicsDevice);
        ballModel.Meshes[0].Material = new LitDiffuseMaterial { DiffuseColor = new Color(220, 60, 50) };

        for (int i = 0; i < BallCount; i++)
        {
            var entity = new Entity { Name = $"ball {i}" };

            var mesh = new StaticModelComponent();
            entity.RootComponent = mesh;
            mesh.Position = new Vector3(-16.5f + i * 3f, 1f, 22f);
            mesh.StaticModel = ballModel;

            var body = new CollisionComponent();
            body.Fixtures.Add(new ColliderFixture(new Sphere { Radius = 0.5f }));
            mesh.AddChildComponent(body);
            body.PhysicsDefinition.PhysicsType = PhysicsType.Dynamic;
            // Heavier than a box: a ball goes through the stack instead of bouncing off it.
            body.PhysicsDefinition.Mass = 4.0f;

            _balls[i] = mesh;
            _ballBodies[i] = body;
            world.AddEntity(entity);
        }
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        base.InitializeCamera(camera);
        _camera = camera as ArcBallCameraComponent;
    }

    public override void Update(GameTime gameTime)
    {
        if (_game == null || _camera == null)
        {
            return;
        }

        KeyboardState keyboard = DemoKeyboard.Read(_game);
        if (keyboard.IsKeyDown(Keys.Space) && !_previousKeyboard.IsKeyDown(Keys.Space))
        {
            ThrowBall();
        }

        _previousKeyboard = keyboard;

        ProbeFromCamera();
    }

    /// <summary>
    /// Acting on a dynamic body. The ball is placed in front of the camera, the physics body is told about the new
    /// place, then it is given a velocity: from there the physics world moves it.
    /// </summary>
    private void ThrowBall()
    {
        StaticModelComponent ball = _balls[_nextBall];
        CollisionComponent body = _ballBodies[_nextBall];
        _nextBall = (_nextBall + 1) % BallCount;

        Vector3 direction = _camera!.Direction;

        // A dynamic body normally writes the transform of the mesh. To move it by hand, write the transform and push
        // it to the body.
        ball.Position = _camera.Position + direction * 2f;
        body.SyncTransformFromScene();
        body.Velocity = direction * BallSpeed;
    }

    /// <summary>
    /// Asking the physics world. A small sphere is swept from the camera along its view direction; the answer tells
    /// whether it hit something, where, and the normal of the surface there. Drawn with the debug line renderer.
    /// </summary>
    private void ProbeFromCamera()
    {
        World world = _game!.GameManager.CurrentWorld;
        if (world?.PhysicsWorld == null)
        {
            return;
        }

        // The query shape is created once and reused: creating one per frame would allocate.
        _probeShape ??= world.PhysicsWorld.CreateQueryShape(new Sphere { Radius = 0.05f }, Vector3.One);

        Vector3 direction = _camera!.Direction;
        // Start a little ahead, so that a ball just thrown is not the first thing hit.
        Vector3 from = _camera.Position + direction * 4f;
        Vector3 to = from + direction * ProbeLength;

        if (!world.PhysicsWorld.ShapeSweep(_probeShape, Matrix.CreateTranslation(from), Matrix.CreateTranslation(to), out HitResult hit))
        {
            return;
        }

        var lines = _game.Line3dRendererComponent;
        const float size = 0.4f;
        lines.AddLine(hit.Point - Vector3.UnitX * size, hit.Point + Vector3.UnitX * size, ProbeColor);
        lines.AddLine(hit.Point - Vector3.UnitY * size, hit.Point + Vector3.UnitY * size, ProbeColor);
        lines.AddLine(hit.Point - Vector3.UnitZ * size, hit.Point + Vector3.UnitZ * size, ProbeColor);
        lines.AddLine(hit.Point, hit.Point + hit.Normal * 2f, ProbeColor);
    }

    public override void Clean()
    {
        _probeShape?.Dispose();
        _probeShape = null;
        _camera = null;
        _game = null;
        _nextBall = 0;
        _previousKeyboard = default;
    }
}
