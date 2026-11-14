using System;
using System.Collections;
using System.Collections.Generic;
using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting.Coroutines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Coroutines: a sequence spread over many frames, written as one method that reads from top to bottom. Without them
/// the same patrol needs a state variable, a timer and a switch in <c>Update</c>.
/// <para/>
/// A coroutine is a C# iterator (<see cref="IEnumerator"/>). Each <c>yield return</c> hands control back to the engine
/// and says when to come back:
/// <list type="bullet">
///   <item><c>yield return null</c>: at the next frame;</item>
///   <item><c>yield return new WaitForSeconds(1f)</c>: after one second of game time;</item>
///   <item><c>yield return new WaitUntil(condition)</c>: when the condition becomes true;</item>
///   <item><c>yield return AnotherCoroutine()</c>: when that nested coroutine has finished.</item>
/// </list>
/// The world owns a <see cref="CoroutineManager"/> that advances every running coroutine once per frame; the handle it
/// returns stops one. Read <see cref="Patrol"/> first.
/// </summary>
public class CoroutineDemo : Demo
{
    private const float MoveSeconds = 1.5f;
    private const float PauseSeconds = 1f;

    // The colour of the cube tells which line of the coroutine is running.
    private static readonly Color MovingColor = new(70, 130, 230);
    private static readonly Color TimedWaitColor = new(235, 200, 60);
    private static readonly Color KeyWaitColor = new(70, 200, 100);
    private static readonly Color StoppedColor = new(120, 120, 120);

    private static readonly Vector3[] Waypoints =
    {
        new(-6f, 0.5f, -4f),
        new(6f, 0.5f, -4f),
        new(6f, 0.5f, 4f),
        new(-6f, 0.5f, 4f),
    };

    private CasaEngineGame? _game;
    private StaticModelComponent? _cube;
    private LitDiffuseMaterial? _cubeMaterial;
    private CoroutineHandle _patrol = CoroutineHandle.Invalid;
    private KeyboardState _previousKeyboard;
    private bool _continueRequested;
    private float _frameSeconds;

    // WaitUntil takes a delegate: it is created once, not at each wait.
    private readonly Func<bool> _continueWasRequested;

    public CoroutineDemo()
    {
        _continueWasRequested = () => _continueRequested;
    }

    public override string Title => "Coroutine demo";

    public override string Description =>
        "A cube patrols four corners, pauses one second at each and waits for a key at the last one: the whole " +
        "sequence is one method, a coroutine, that the engine resumes frame after frame. The colour of the cube " +
        "shows which line of that method is running.";

    public override IReadOnlyList<DemoControl> Controls { get; } = new DemoControl[]
    {
        new("Space", "let the patrol go on when the cube is green"),
        new("S", "stop the coroutine where it is, or start it again"),
    };

    public override IReadOnlyList<string> Highlights { get; } = new[]
    {
        "Blue: the cube moves (a nested coroutine that yields every frame).",
        "Yellow: WaitForSeconds. Green: WaitUntil, the coroutine waits for Space.",
        "Grey: the coroutine was stopped by its handle; the cube stays where it was.",
    };

    public override string DocumentationPath => "docs/engine/coroutines_specifications.md";

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        World world = game.GameManager.CurrentWorld;

        // A ground, so that the patrol is readable.
        StaticModel groundModel = StaticModel.CreateFromPrimitive(new BoxPrimitive(20f, 1f, 16f));
        groundModel.Meshes[0].Initialize(game.GraphicsDevice);
        groundModel.Meshes[0].Material = new LitDiffuseMaterial
        {
            BasColor = game.AssetContentManager.LoadFromFile<Texture2D>("checkboard.png"),
            DiffuseColor = Color.White,
        };

        var groundEntity = new Entity { Name = "ground" };
        var ground = new StaticModelComponent { StaticModel = groundModel };
        groundEntity.RootComponent = ground;
        ground.Position = new Vector3(0f, -0.5f, 0f);
        world.AddEntity(groundEntity);

        // The cube that patrols. Its material is kept: the coroutine changes its colour.
        _cubeMaterial = new LitDiffuseMaterial { DiffuseColor = StoppedColor };
        StaticModel cubeModel = StaticModel.CreateFromPrimitive(new BoxPrimitive());
        cubeModel.Meshes[0].Initialize(game.GraphicsDevice);
        cubeModel.Meshes[0].Material = _cubeMaterial;

        var cubeEntity = new Entity { Name = "patrolling cube" };
        _cube = new StaticModelComponent { StaticModel = cubeModel };
        cubeEntity.RootComponent = _cube;
        _cube.Position = Waypoints[0];
        world.AddEntity(cubeEntity);
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        ((ArcBallCameraComponent)camera).SetCamera(new Vector3(0f, 12f, 16f), Vector3.Zero, Vector3.Up);

        // Called once the world is loaded: start the coroutine here.
        StartPatrol();
    }

    private void StartPatrol()
    {
        World world = _game!.GameManager.CurrentWorld;

        // The second argument is the owner: StopAllCoroutines(owner) stops everything an object started. The name
        // shows in the coroutine debug listing.
        _patrol = world.CoroutineManager.StartCoroutine(Patrol(), this, "CoroutineDemo.Patrol");
    }

    /// <summary>
    /// The whole behaviour, in the order it happens. It never returns by itself: the loop runs until the coroutine is
    /// stopped or the world is cleared.
    /// </summary>
    private IEnumerator Patrol()
    {
        while (true)
        {
            for (int i = 0; i < Waypoints.Length; i++)
            {
                Vector3 target = Waypoints[(i + 1) % Waypoints.Length];

                // A nested coroutine: Patrol resumes after MoveTo has finished.
                _cubeMaterial!.DiffuseColor = MovingColor;
                yield return MoveTo(target, MoveSeconds);

                bool backAtStart = i == Waypoints.Length - 1;
                if (!backAtStart)
                {
                    // Wait in game time: the wait stretches if the game is slowed down or paused.
                    _cubeMaterial.DiffuseColor = TimedWaitColor;
                    yield return new WaitForSeconds(PauseSeconds);
                }
                else
                {
                    // Wait for something to happen. The condition is checked once per frame.
                    _cubeMaterial.DiffuseColor = KeyWaitColor;
                    _continueRequested = false;
                    yield return new WaitUntil(_continueWasRequested);
                }
            }
        }
    }

    /// <summary>Moves the cube in a straight line, one step per frame.</summary>
    private IEnumerator MoveTo(Vector3 target, float seconds)
    {
        Vector3 start = _cube!.Position;
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            // An iterator has no elapsed-time argument: it reads the duration of the frame the demo keeps in Update.
            elapsed += _frameSeconds;
            _cube.Position = Vector3.Lerp(start, target, MathHelper.Clamp(elapsed / seconds, 0f, 1f));

            // Come back at the next frame.
            yield return null;
        }

        _cube.Position = target;
    }

    public override void Update(GameTime gameTime)
    {
        if (_game == null)
        {
            return;
        }

        _frameSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;

        KeyboardState keyboard = DemoKeyboard.Read(_game);

        // The demo only records the key: the coroutine reads the flag at its own pace.
        if (keyboard.IsKeyDown(Keys.Space) && !_previousKeyboard.IsKeyDown(Keys.Space))
        {
            _continueRequested = true;
        }

        if (keyboard.IsKeyDown(Keys.S) && !_previousKeyboard.IsKeyDown(Keys.S))
        {
            CoroutineManager coroutines = _game.GameManager.CurrentWorld.CoroutineManager;
            if (coroutines.IsRunning(_patrol))
            {
                // Stopping by handle: the method simply never resumes after its current yield.
                coroutines.StopCoroutine(_patrol);
                _cubeMaterial!.DiffuseColor = StoppedColor;
            }
            else
            {
                StartPatrol();
            }
        }

        _previousKeyboard = keyboard;
    }

    public override void Clean()
    {
        // The world stopped its coroutines when it was cleared.
        _patrol = CoroutineHandle.Invalid;
        _cube = null;
        _cubeMaterial = null;
        _game = null;
        _continueRequested = false;
        _previousKeyboard = default;
    }
}
