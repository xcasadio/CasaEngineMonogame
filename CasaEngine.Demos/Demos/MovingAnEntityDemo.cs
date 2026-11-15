using System.Collections.Generic;
using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// The second demo to read: the player moves an entity. It shows the frame loop of a game: read the input, compute a
/// velocity, move by <c>velocity * elapsed time</c>, write the transform.
/// <para/>
/// There is no physics here: the demo writes the position itself, so the cube goes through anything. The physics demos
/// show the other way, where a body is moved by the physics world.
/// </summary>
public class MovingAnEntityDemo : Demo
{
    // World units per second.
    private const float WalkSpeed = 4f;
    private const float RunSpeed = 10f;

    // Half the side of the ground: the cube stays on it.
    private const float GroundHalfSize = 8f;

    private CasaEngineGame? _game;
    private StaticModelComponent? _cube;

    public override string Title => "Moving an entity";

    public override string Description =>
        "The player moves a cube on a ground with the arrow keys. Each frame the demo reads the keyboard, turns " +
        "it into a velocity and moves the cube by velocity x elapsed time, so the speed is the same at any frame " +
        "rate. No physics: the demo writes the position itself.";

    public override IReadOnlyList<DemoControl> Controls { get; } = new DemoControl[]
    {
        DemoControl.ForActions("move the cube on the ground", DemoInput.MoveLeft, DemoInput.MoveRight, DemoInput.MoveUp, DemoInput.MoveDown),
        DemoControl.ForActions("run while held", "MovingAnEntity.Run"),
        DemoControl.ForActions("put the cube back at the centre", "MovingAnEntity.Recentre"),
    };

    public override IReadOnlyList<string> Highlights { get; } = new[]
    {
        "The cube turns to face the direction it moves in.",
        "Moving diagonally is not faster: the direction is normalized before it is scaled by the speed.",
        "The cube stops at the edge of the ground because the demo clamps its position.",
    };

    public override string DocumentationPath => "docs/engine/demos-main-menu.md";

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        World world = game.GameManager.CurrentWorld;

        // The ground: a flat box with a checker texture, so that the movement is readable.
        StaticModel groundModel = StaticModel.CreateFromPrimitive(new BoxPrimitive(GroundHalfSize * 2f, 1f, GroundHalfSize * 2f));
        groundModel.Meshes[0].Initialize(game.GraphicsDevice);
        groundModel.Meshes[0].Material = new LitDiffuseMaterial
        {
            // LoadFromFile reads a file of the demos' Content folder.
            BasColor = game.AssetContentManager.LoadFromFile<Texture2D>("checkboard.png"),
            DiffuseColor = Color.White,
        };

        var groundEntity = new Entity { Name = "ground" };
        var ground = new StaticModelComponent { StaticModel = groundModel };
        groundEntity.RootComponent = ground;
        // The box is one unit thick: lower it so that its top face is at height 0.
        ground.Position = new Vector3(0f, -0.5f, 0f);
        world.AddEntity(groundEntity);

        // The cube the player moves.
        StaticModel cubeModel = StaticModel.CreateFromPrimitive(new BoxPrimitive());
        cubeModel.Meshes[0].Initialize(game.GraphicsDevice);
        cubeModel.Meshes[0].Material = new LitDiffuseMaterial { DiffuseColor = new Color(220, 70, 60) };

        var cubeEntity = new Entity { Name = "player cube" };
        _cube = new StaticModelComponent { StaticModel = cubeModel };
        cubeEntity.RootComponent = _cube;
        _cube.Position = new Vector3(0f, 0.5f, 0f);
        world.AddEntity(cubeEntity);
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        ((ArcBallCameraComponent)camera).SetCamera(new Vector3(0f, 9f, 11f), Vector3.Zero, Vector3.Up);
    }

    public override void Update(GameTime gameTime)
    {
        if (_game == null || _cube == null)
        {
            return;
        }

        // 1. Read the input. A demo reads named actions (DemoInput), not keys: the input configuration of the demos
        //    binds each action to a key and to a gamepad button. Nothing is read while the window is not the active one.
        if (DemoInput.IsDown(_game, "MovingAnEntity.Recentre"))
        {
            _cube.Position = new Vector3(0f, 0.5f, 0f);
            return;
        }

        // 2. Turn the keys into a direction on the ground plane (X to the right, -Z away from the camera).
        Vector3 direction = Vector3.Zero;
        if (DemoInput.IsDown(_game, DemoInput.MoveLeft))
        {
            direction.X -= 1f;
        }

        if (DemoInput.IsDown(_game, DemoInput.MoveRight))
        {
            direction.X += 1f;
        }

        if (DemoInput.IsDown(_game, DemoInput.MoveUp))
        {
            direction.Z -= 1f;
        }

        if (DemoInput.IsDown(_game, DemoInput.MoveDown))
        {
            direction.Z += 1f;
        }

        if (direction == Vector3.Zero)
        {
            return;
        }

        // Two keys give a vector of length 1.41: normalize it, or diagonals would be faster.
        direction.Normalize();

        // 3. Move by velocity x elapsed time. The elapsed time is the duration of the last frame, in seconds.
        float speed = DemoInput.IsDown(_game, "MovingAnEntity.Run") ? RunSpeed : WalkSpeed;
        float elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        Vector3 position = _cube.Position + direction * speed * elapsedSeconds;

        // 4. Keep the cube on the ground, then write the transform. The engine draws the cube where it now is.
        float limit = GroundHalfSize - 0.5f;
        position.X = MathHelper.Clamp(position.X, -limit, limit);
        position.Z = MathHelper.Clamp(position.Z, -limit, limit);
        _cube.Position = position;

        // Face the direction of the movement: a rotation around the vertical axis.
        _cube.Orientation = Quaternion.CreateFromAxisAngle(Vector3.Up, System.MathF.Atan2(-direction.X, -direction.Z));
    }

    public override void Clean()
    {
        _cube = null;
        _game = null;
    }
}
