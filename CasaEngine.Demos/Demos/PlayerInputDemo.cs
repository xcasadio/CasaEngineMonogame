using System.Collections.Generic;
using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Who plays, and what is played. Three objects to tell apart:
/// <list type="bullet">
///   <item>the pawn: an ordinary <see cref="Entity"/> with a <see cref="CharacterControllerComponent"/>, the component
///   that moves a body from a move intent;</item>
///   <item>the <see cref="PlayerController"/>: the player. It possesses one pawn at a time
///   (<see cref="Controller.Possess"/>) and can change pawn while the game runs;</item>
///   <item>the <see cref="PlayerInput"/>: the keyboard and the game pad of that player, cut off as a whole when
///   <see cref="PlayerController.IsInputEnable"/> is false or when a UI has taken the keyboard.</item>
/// </list>
/// A game world usually gets its player controller from the engine (a default pawn asset and a player start in the
/// world, see docs/engine/gameplay-possession.md). This demo creates one by hand so that every step is visible.
/// Read <see cref="Update"/> first.
/// </summary>
public class PlayerInputDemo : Demo
{
    private const int PawnCount = 3;

    private static readonly Color[] PawnColors =
    {
        new(220, 70, 60),
        new(70, 130, 230),
        new(240, 200, 60),
    };

    private static readonly Color NotPossessedColor = new(90, 90, 90);
    private static readonly Color LampInputOn = new(60, 220, 90);
    private static readonly Color LampInputOff = new(200, 60, 50);

    private readonly Entity[] _pawns = new Entity[PawnCount];
    private readonly CharacterControllerComponent[] _characters = new CharacterControllerComponent[PawnCount];
    private readonly LitDiffuseMaterial[] _pawnMaterials = new LitDiffuseMaterial[PawnCount];

    private CasaEngineGame? _game;
    private PlayerController? _player;
    private PlayerInput? _input;
    private LitDiffuseMaterial? _lampMaterial;
    private int _possessedIndex;

    public override string Title => "Player input and possession demo";

    public override string Description =>
        "One player, three pawns. The player controller possesses one pawn at a time: only that pawn listens to the " +
        "keyboard or the game pad, the others are switched off and grey. Change pawn while the game runs, and cut " +
        "the player's input as a pause menu or a cutscene would.";

    public override IReadOnlyList<DemoControl> Controls { get; } = new DemoControl[]
    {
        DemoControl.ForActions("move the possessed pawn", DemoInput.MoveLeft, DemoInput.MoveRight, DemoInput.MoveUp, DemoInput.MoveDown),
        DemoControl.ForActions("possess the next pawn", "PlayerInput.NextPawn"),
        DemoControl.ForActions("enable or disable the input of the player", "PlayerInput.ToggleInput"),
    };

    public override IReadOnlyList<string> Highlights { get; } = new[]
    {
        "The coloured pawn is the possessed one: possession sets its control mode to Player.",
        "A pawn that is released goes back to the mode it had before, Disabled here: it stops and turns grey.",
        "The lamp is green while the player's input is enabled, red when it is cut: no key moves the pawn then.",
    };

    public override string DocumentationPath => "docs/engine/gameplay-possession.md";

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        World world = game.GameManager.CurrentWorld;

        // A ground to look at. It has no collider: the pawns below move on a plane, without gravity.
        StaticModel groundModel = StaticModel.CreateFromPrimitive(new BoxPrimitive(18f, 1f, 12f));
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

        for (int i = 0; i < PawnCount; i++)
        {
            _pawns[i] = CreatePawn(game, i);
            _characters[i] = _pawns[i].GetRequiredComponent<CharacterControllerComponent>();
            world.AddEntity(_pawns[i]);
        }

        // The lamp that shows whether the player's input is enabled.
        _lampMaterial = new LitDiffuseMaterial { DiffuseColor = LampInputOn };
        StaticModel lampModel = StaticModel.CreateFromPrimitive(new BoxPrimitive(4f, 1f, 1f));
        lampModel.Meshes[0].Initialize(game.GraphicsDevice);
        lampModel.Meshes[0].Material = _lampMaterial;

        var lampEntity = new Entity { Name = "input lamp" };
        var lamp = new StaticModelComponent { StaticModel = lampModel };
        lampEntity.RootComponent = lamp;
        lamp.Position = new Vector3(0f, 2.5f, -6f);
        world.AddEntity(lampEntity);

        // The player. LocalPlayer.ControllerId says which game pad is this player's.
        _player = new PlayerController { Player = new LocalPlayer { ControllerId = PlayerIndex.One } };

        // The player's input. The engine builds it for the controllers it creates itself; for a controller made by
        // hand the game builds it, from the input component of the game.
        _input = new PlayerInput(_player, game.InputComponent);

        _possessedIndex = 0;
        _player.Possess(_pawns[_possessedIndex]);
    }

    /// <summary>
    /// A pawn is an entity with a character controller: nothing in it says that a player drives it.
    /// </summary>
    private Entity CreatePawn(CasaEngineGame game, int index)
    {
        _pawnMaterials[index] = new LitDiffuseMaterial { DiffuseColor = NotPossessedColor };

        StaticModel model = StaticModel.CreateFromPrimitive(new BoxPrimitive(1f, 1.4f, 1f));
        model.Meshes[0].Initialize(game.GraphicsDevice);
        model.Meshes[0].Material = _pawnMaterials[index];

        var entity = new Entity { Name = $"pawn {index}" };
        var mesh = new StaticModelComponent { StaticModel = model };
        entity.RootComponent = mesh;
        // The character controller moves the root component of its entity.
        mesh.Position = new Vector3(-5f + index * 5f, 0.7f, 0f);

        var character = new CharacterControllerComponent
        {
            Settings = new CharacterControllerSettings
            {
                MaxHorizontalSpeed = 5f,
                Acceleration = 30f,
                Deceleration = 35f,
                // No ground collider in this scene: no gravity and no ground snapping, the pawn slides on its plane.
                Gravity = 0f,
                GroundSnapDistance = 0f,
            },
        };
        entity.AddComponent(character);

        // Nobody drives this pawn yet. Possession replaces this mode by Player and puts it back on release.
        character.SetControlMode(CharacterControlMode.Disabled);

        return entity;
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        ((ArcBallCameraComponent)camera).SetCamera(new Vector3(0f, 10f, 13f), Vector3.Zero, Vector3.Up);
    }

    public override void Update(GameTime gameTime)
    {
        if (_player == null || _input == null)
        {
            return;
        }

        // 1. Demo commands. They do not go through the gate of the player on purpose: they must still work when the
        //    input of the player is cut, as a pause menu does.
        ReadDemoCommands();

        // 2. Gameplay input. The player's facade is the gate: it says no while the input is disabled, or while a UI
        //    holds the keyboard and no gamepad is connected. PlayerInput.GetButtonState(name) applies the same gate to
        //    one action, but returns a new object at each call: fine for a press, avoided here where four directions
        //    are read every frame.
        Vector2 intent = Vector2.Zero;
        if (_input.IsInputEnabled && (_input.IsKeyboardAvailable || _input.IsGamePadConnected))
        {
            if (DemoInput.IsDown(_game!, DemoInput.MoveLeft))
            {
                intent.X -= 1f;
            }

            if (DemoInput.IsDown(_game!, DemoInput.MoveRight))
            {
                intent.X += 1f;
            }

            if (DemoInput.IsDown(_game!, DemoInput.MoveUp))
            {
                intent.Y += 1f;
            }

            if (DemoInput.IsDown(_game!, DemoInput.MoveDown))
            {
                intent.Y -= 1f;
            }
        }

        // 3. The player drives whatever it possesses. X of the intent is world X, Y is "away from the camera" (-Z);
        //    the character controller turns the intent into a velocity with its own acceleration.
        Entity? pawn = _player.Pawn;
        pawn?.GetComponent<CharacterControllerComponent>()?.SetMoveIntent(intent);

        ShowState();
    }

    private void ReadDemoCommands()
    {
        if (DemoInput.WasPressed(_game!, "PlayerInput.NextPawn"))
        {
            // Possessing another entity releases the current one first: its control mode is restored (Disabled), and
            // the new pawn gets the mode of a player controller (Player).
            _possessedIndex = (_possessedIndex + 1) % PawnCount;
            _player!.Possess(_pawns[_possessedIndex]);
        }

        if (DemoInput.WasPressed(_game!, "PlayerInput.ToggleInput"))
        {
            _player!.IsInputEnable = !_player.IsInputEnable;
        }
    }

    /// <summary>Colours only: each pawn shows its control mode, the lamp shows the input gate.</summary>
    private void ShowState()
    {
        for (int i = 0; i < PawnCount; i++)
        {
            bool drivenByPlayer = _characters[i].ControlMode == CharacterControlMode.Player;
            _pawnMaterials[i].DiffuseColor = drivenByPlayer ? PawnColors[i] : NotPossessedColor;
        }

        _lampMaterial!.DiffuseColor = _player!.IsInputEnable ? LampInputOn : LampInputOff;
    }

    public override void Clean()
    {
        _player?.UnPossess();
        _player = null;
        _input = null;
        _game = null;
        _lampMaterial = null;
        _possessedIndex = 0;

        for (int i = 0; i < PawnCount; i++)
        {
            _pawns[i] = null!;
            _characters[i] = null!;
            _pawnMaterials[i] = null!;
        }
    }
}
