using System.IO;
using CasaEngine.Engine.Environment;
using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Dialogue.Assets;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.Dialogue.UI;
using CasaEngine.Framework.Dialogue.Yarn;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.UI;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Demonstrates the MGUI per-view UI system integrated in the engine pipeline:
/// <list type="bullet">
///   <item><see cref="HudScreen"/> (UILayer.HUD) — always-on info panel with an
///         elapsed-time counter and an "Open Pause Menu" button.</item>
///   <item><see cref="PauseMenuScreen"/> (UILayer.Menu, IsModal=true) — centred
///         modal window with a "Resume" button that pops itself off the stack.</item>
/// </list>
///
/// How to test:
/// <list type="number">
///   <item>Navigate to this demo via the MGUI demo navigator panel (top-right).</item>
///   <item>Verify the semi-transparent HUD panel appears in the top-left corner.</item>
///   <item>Click "Open Pause Menu" → the modal window should appear in the centre.</item>
///   <item>Click "Resume" → the modal closes; the HUD remains.</item>
///   <item>The time counter in the HUD should keep incrementing while unpaused.</item>
/// </list>
/// </summary>
public class UIOverlayDemo : Demo
{
    private CasaEngineGame? _game;
    private HudScreen?       _hudScreen;
    private PauseMenuScreen? _pauseScreen;
    private DialogueService? _dialogueService;
    private DialogueScreen?  _dialogueScreen;
    private DialogueAsset?       _dialogueAsset;
    private YarnDialogueRunner?  _dialogueRunner;
    private KeyboardState    _previousKeyboard;

    public override string Title => "MGUI UI Overlay Demo (HUD + Modal Screens)";
    public override string Description => "Demonstrates the MGUI per-view UI overlay system: a persistent HUD panel, modal pause menu, and modal dialogue screen. Press D in this demo to toggle the dialogue test line.";

    // ---- Demo lifecycle ----

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        var world = game.GameManager.CurrentWorld;

        // Ground plane
        var ground = new Entity { Name = "Ground" };
        var groundMesh = new StaticModelComponent();
        ground.RootComponent = groundMesh;
        groundMesh.StaticModel = StaticModel.CreateFromPrimitive(new BoxPrimitive(20, 1, 20));
        groundMesh.StaticModel.Meshes[0].Initialize(game.GraphicsDevice);
        groundMesh.StaticModel.Meshes[0].Material = new LitDiffuseMaterial
        {
            DiffuseColor = new Color(190, 180, 150),
            SpecularColor = Vector3.Zero,
        };
        groundMesh.LocalPosition = new Vector3(0, -0.5f, 0);
        world.AddEntity(ground);

        // Three decorative boxes
        var boxColors = new[]
        {
            new Color(200, 95, 95),
            new Color(105, 150, 210),
            new Color(120, 185, 130),
        };

        for (int i = 0; i < 3; i++)
        {
            var box = new Entity { Name = $"Box {i}" };
            var mesh = new StaticModelComponent();
            box.RootComponent = mesh;
            mesh.StaticModel = StaticModel.CreateFromPrimitive(new BoxPrimitive(2, 2, 2));
            mesh.StaticModel.Meshes[0].Initialize(game.GraphicsDevice);
            mesh.StaticModel.Meshes[0].Material = new LitDiffuseMaterial
            {
                DiffuseColor = boxColors[i],
                SpecularColor = Vector3.Zero,
            };
            mesh.LocalPosition = new Vector3((i - 1) * 5f, 1f, 0f);
            world.AddEntity(box);
        }
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        base.InitializeCamera(camera);

        // The per-view UI runtime is created after world.LoadContent,
        // so it is guaranteed to be available at this point in the lifecycle.
        var uiView = GetUIView();
        if (uiView == null) return;

        _dialogueService = new DialogueService();
        _dialogueRunner  = new YarnDialogueRunner(_dialogueService);
        _dialogueAsset   = LoadDialogueAsset();
        _dialogueScreen  = new DialogueScreen(_dialogueService, CloseDialogue);
        _pauseScreen     = new PauseMenuScreen(OnResume);
        _hudScreen       = new HudScreen(OnOpenPauseMenu, OpenDialogue);

        // Push the HUD as the base layer — it will persist until Clean() is called.
        uiView.PushScreen(_hudScreen);
    }

    public override void Update(GameTime gameTime)
    {
        KeyboardState keyboard = _game?.IsActive == true ? Keyboard.GetState() : new KeyboardState();
        if (keyboard.IsKeyDown(Keys.D) && !_previousKeyboard.IsKeyDown(Keys.D))
        {
            ToggleDialogue();
        }

        _previousKeyboard = keyboard;
    }

    public override void OnScreenResized(CasaEngineGame game, int width, int height)
    {
        game.GameManager.ViewManager.ApplyBackBufferLayout(width, height);
    }

    public override void Clean()
    {
        var uiView = GetUIView();
        if (uiView != null)
        {
            if (_pauseScreen != null)
            {
                uiView.RemoveScreen(_pauseScreen);
            }

            if (_dialogueScreen != null)
            {
                uiView.RemoveScreen(_dialogueScreen);
            }

            if (_hudScreen != null)
            {
                uiView.RemoveScreen(_hudScreen);
            }
        }

        _hudScreen   = null;
        _pauseScreen = null;
        _dialogueScreen  = null;
        _dialogueRunner  = null;
        _dialogueAsset   = null;
        _dialogueService = null;
        _game        = null;
    }

    // ---- Button callbacks ----

    private void OnOpenPauseMenu()
    {
        if (_pauseScreen == null) return;
        GetUIView()?.PushScreen(_pauseScreen);
    }

    private void OnResume()
    {
        GetUIView()?.PopScreen();
    }

    private void ToggleDialogue()
    {
        if (_dialogueService?.IsOpen == true)
        {
            ContinueDialogue();
        }
        else
        {
            OpenDialogue();
        }
    }

    private void OpenDialogue()
    {
        if (_dialogueService == null || _dialogueScreen == null || _dialogueRunner == null || _dialogueAsset == null)
        {
            return;
        }

        var uiView = GetUIView();
        if (uiView == null)
        {
            return;
        }

        if (!_dialogueService.IsOpen)
        {
            if (!_dialogueRunner.Start(_dialogueAsset))
            {
                return;
            }
        }

        uiView.RemoveScreen(_dialogueScreen);
        uiView.PushScreen(_dialogueScreen);
    }

    private void ContinueDialogue()
    {
        if (_dialogueRunner?.Continue() != true)
        {
            CloseDialogue();
            return;
        }

        if (_dialogueService?.IsOpen != true && _dialogueScreen != null)
        {
            GetUIView()?.RemoveScreen(_dialogueScreen);
        }
    }

    private void CloseDialogue()
    {
        _dialogueRunner?.Stop();
        _dialogueService?.Close();

        if (_dialogueScreen != null)
        {
            GetUIView()?.RemoveScreen(_dialogueScreen);
        }
    }

    // ---- Helpers ----

    private DialogueAsset? LoadDialogueAsset()
    {
        if (_game == null)
        {
            return null;
        }

        string fileName = Path.Combine(EngineEnvironment.ProjectPath, "Dialogues", "greeting.dialogue");
        var loader = new DialogueAssetLoader();
        return loader.LoadAsset(fileName, _game.AssetContentManager) as DialogueAsset;
    }

    private IUIViewRuntime? GetUIView()
        => _game?.GameManager.ViewManager.GetActiveUIView();
}
