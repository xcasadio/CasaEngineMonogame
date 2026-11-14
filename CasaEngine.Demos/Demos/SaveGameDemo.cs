using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.SaveGames;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Saving the player's game. Three things to read here:
/// <list type="bullet">
///   <item>the save object: a class that implements <see cref="ISaveGameData"/>, whose one
///   <c>Serialize</c> method both writes and reads (<see cref="CubeSave"/>);</item>
///   <item>the service: <see cref="SaveGameService.Save"/>, <see cref="SaveGameService.TryLoad{T}"/>,
///   <see cref="SaveGameService.Exists"/> and <see cref="SaveGameService.Delete"/> work on a named slot and answer
///   with a result, never an exception for a file problem;</item>
///   <item>the contract of a load: the file is not trusted, so the game checks what the values mean before it applies
///   them (<see cref="LoadGame"/>).</item>
/// </list>
/// A game normally uses <c>GameSettings.SaveGames</c>, which writes in the per-user folder. This demo builds its own
/// service on a folder beside the executable, so that the file is easy to find, open and delete.
/// </summary>
public class SaveGameDemo : Demo
{
    private const string SlotName = "demo";
    private const float MoveSpeed = 5f;
    private const float GroundHalfSize = 8f;

    private static readonly Vector3 StartPosition = new(0f, 0.5f, 0f);
    private static readonly Color LampNoSave = new(70, 70, 70);
    private static readonly Color LampHasSave = new(60, 220, 90);

    // The folder of the slots, beside the executable (bin/...): not the place a shipped game would use.
    private static readonly string SaveFolder = Path.Combine(AppContext.BaseDirectory, "SaveGames");

    private CasaEngineGame? _game;
    private SaveGameService? _saves;
    private StaticModelComponent? _cube;
    private StaticModelComponent? _savedMarker;
    private LitDiffuseMaterial? _lampMaterial;

    /// <summary>
    /// What this game saves: where the cube is. The same calls, in the same order, write the fields when saving and
    /// overwrite them when loading.
    /// </summary>
    private sealed class CubeSave : ISaveGameData
    {
        public float X;
        public float Z;

        // Increase it when the fields or their order change; Serialize then branches on archive.DataVersion to read
        // an older file.
        public int LatestDataVersion => 1;

        public void Serialize(SaveGameArchive archive)
        {
            archive.Value("x", ref X);
            archive.Value("z", ref Z);
        }
    }

    public override string Title => "Save game demo";

    public override string Description =>
        "Move the cube, save, move it again, then load: the cube goes back to the saved place, shown by the flat " +
        "yellow marker. The save is a file on disk: quit the demo, or the program, and the cube is back where it " +
        "was saved when the demo starts again.";

    public override IReadOnlyList<DemoControl> Controls { get; } = new DemoControl[]
    {
        DemoControl.ForActions("move the cube on the ground", DemoInput.MoveLeft, DemoInput.MoveRight, DemoInput.MoveUp, DemoInput.MoveDown),
        DemoControl.ForActions("save the position of the cube to the slot", "SaveGame.Save"),
        DemoControl.ForActions("load the slot: the cube goes back to the saved position", "SaveGame.Load"),
        DemoControl.ForActions("delete the slot file", "SaveGame.Delete"),
    };

    public override IReadOnlyList<string> Highlights { get; } = new[]
    {
        "The lamp at the back is green while the slot file exists, grey when there is no save.",
        "The yellow marker is the position stored in the file; it disappears when the slot is deleted.",
        "The file is readable JSON: " + Path.Combine(SaveFolder, SlotName + ".sav"),
    };

    public override string DocumentationPath => "docs/engine/save-games.md";

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        World world = game.GameManager.CurrentWorld;

        // The service on an explicit folder. Nothing is created on disk until the first save.
        _saves = new SaveGameService(SaveFolder);

        StaticModel groundModel = StaticModel.CreateFromPrimitive(new BoxPrimitive(GroundHalfSize * 2f, 1f, GroundHalfSize * 2f));
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

        // The cube the player moves: its position is what is saved.
        StaticModel cubeModel = StaticModel.CreateFromPrimitive(new BoxPrimitive());
        cubeModel.Meshes[0].Initialize(game.GraphicsDevice);
        cubeModel.Meshes[0].Material = new LitDiffuseMaterial { DiffuseColor = new Color(220, 70, 60) };

        var cubeEntity = new Entity { Name = "player cube" };
        _cube = new StaticModelComponent { StaticModel = cubeModel };
        cubeEntity.RootComponent = _cube;
        _cube.Position = StartPosition;
        world.AddEntity(cubeEntity);

        // A flat marker on the ground, at the position stored in the slot.
        StaticModel markerModel = StaticModel.CreateFromPrimitive(new BoxPrimitive(1.4f, 0.05f, 1.4f));
        markerModel.Meshes[0].Initialize(game.GraphicsDevice);
        markerModel.Meshes[0].Material = new LitDiffuseMaterial { DiffuseColor = new Color(240, 210, 50) };

        var markerEntity = new Entity { Name = "saved position marker" };
        _savedMarker = new StaticModelComponent { StaticModel = markerModel };
        markerEntity.RootComponent = _savedMarker;
        world.AddEntity(markerEntity);

        // The lamp: green while the slot exists.
        _lampMaterial = new LitDiffuseMaterial { DiffuseColor = LampNoSave };
        StaticModel lampModel = StaticModel.CreateFromPrimitive(new BoxPrimitive(4f, 1f, 1f));
        lampModel.Meshes[0].Initialize(game.GraphicsDevice);
        lampModel.Meshes[0].Material = _lampMaterial;

        var lampEntity = new Entity { Name = "lamp" };
        var lamp = new StaticModelComponent { StaticModel = lampModel };
        lampEntity.RootComponent = lamp;
        lamp.Position = new Vector3(0f, 2.5f, -GroundHalfSize);
        world.AddEntity(lampEntity);

        // A save left by an earlier run is loaded at once: this is what makes it a save.
        HideMarker();
        if (_saves.Exists(SlotName))
        {
            LoadGame();
        }
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        ((ArcBallCameraComponent)camera).SetCamera(new Vector3(0f, 10f, 13f), Vector3.Zero, Vector3.Up);
    }

    public override void Update(GameTime gameTime)
    {
        if (_game == null || _cube == null)
        {
            return;
        }

        MoveCube((float)gameTime.ElapsedGameTime.TotalSeconds);

        // Saving and loading read and write a file: they run on a key press, never every frame.
        if (DemoInput.WasPressed(_game, "SaveGame.Save"))
        {
            SaveGame();
        }

        if (DemoInput.WasPressed(_game, "SaveGame.Load"))
        {
            LoadGame();
        }

        if (DemoInput.WasPressed(_game, "SaveGame.Delete"))
        {
            DeleteSave();
        }
    }

    private void MoveCube(float elapsedSeconds)
    {
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

        direction.Normalize();
        Vector3 position = _cube!.Position + direction * MoveSpeed * elapsedSeconds;
        float limit = GroundHalfSize - 0.5f;
        position.X = MathHelper.Clamp(position.X, -limit, limit);
        position.Z = MathHelper.Clamp(position.Z, -limit, limit);
        _cube.Position = position;
    }

    /// <summary>
    /// Saving: fill a save object from the live state, hand it to the service. JSON here so that the file can be read
    /// in a text editor; <see cref="SaveGameFormat.Binary"/> is the compact format for a shipped game.
    /// </summary>
    private void SaveGame()
    {
        var save = new CubeSave { X = _cube!.Position.X, Z = _cube.Position.Z };

        SaveGameSaveResult result = _saves!.Save(SlotName, save, SaveGameFormat.Json);
        if (result.Status != SaveGameSaveStatus.Saved)
        {
            // The service has logged the cause with the file path. Nothing was written: the old save, if any, stays.
            return;
        }

        ShowMarker(save.X, save.Z);
    }

    /// <summary>
    /// Loading: the service returns a new object, or a status that says why not (no file, corrupted, written by a
    /// newer version...). A loaded value has the right type and nothing more: anyone can edit the file, so the game
    /// checks what the values mean before it touches its live state, and applies all of them or none.
    /// </summary>
    private void LoadGame()
    {
        SaveGameLoadResult result = _saves!.TryLoad(SlotName, out CubeSave save);
        if (result.Status != SaveGameLoadStatus.Loaded)
        {
            // Already logged by the service. The cube stays where it is.
            return;
        }

        float limit = GroundHalfSize - 0.5f;
        if (MathF.Abs(save.X) > limit || MathF.Abs(save.Z) > limit)
        {
            // A position outside the ground: the file was edited by hand. Refuse the whole save.
            return;
        }

        _cube!.Position = new Vector3(save.X, StartPosition.Y, save.Z);
        ShowMarker(save.X, save.Z);
    }

    private void DeleteSave()
    {
        SaveGameSaveResult result = _saves!.Delete(SlotName);
        if (result.Status == SaveGameSaveStatus.Deleted || result.Status == SaveGameSaveStatus.NotFound)
        {
            HideMarker();
        }
    }

    private void ShowMarker(float x, float z)
    {
        _savedMarker!.Position = new Vector3(x, 0.03f, z);
        _lampMaterial!.DiffuseColor = LampHasSave;
    }

    private void HideMarker()
    {
        // Under the ground: out of sight without removing the entity.
        _savedMarker!.Position = new Vector3(0f, -5f, 0f);
        _lampMaterial!.DiffuseColor = LampNoSave;
    }

    public override void Clean()
    {
        // The save file is kept on purpose: it is still there the next time the demo starts.
        _saves = null;
        _cube = null;
        _savedMarker = null;
        _lampMaterial = null;
        _game = null;
    }
}
