using System.Linq;
using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.GUI;
using Microsoft.Xna.Framework;

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

    public override string Title => "MGUI UI Overlay Demo (HUD + Modal Pause Menu)";
    public override string Description => "Demonstrates the MGUI per-view UI overlay system: a persistent HUD panel with an elapsed-time counter, and a modal pause menu that blocks input to lower layers.";

    // ---- Demo lifecycle ----

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        var world = game.GameManager.CurrentWorld;

        // Ground plane
        var ground = new Entity { Name = "Ground" };
        var groundMesh = new StaticMeshComponent();
        ground.RootComponent = groundMesh;
        groundMesh.Mesh = StaticMesh.CreateFromGeometricPrimitive(new BoxPrimitive(20, 1, 20));
        groundMesh.Mesh.Initialize(game.AssetContentManager);
        groundMesh.LocalPosition = new Vector3(0, -0.5f, 0);
        world.AddEntity(ground);

        // Three decorative boxes
        for (int i = 0; i < 3; i++)
        {
            var box = new Entity { Name = $"Box {i}" };
            var mesh = new StaticMeshComponent();
            box.RootComponent = mesh;
            mesh.Mesh = StaticMesh.CreateFromGeometricPrimitive(new BoxPrimitive(2, 2, 2));
            mesh.Mesh.Initialize(game.AssetContentManager);
            mesh.LocalPosition = new Vector3((i - 1) * 5f, 1f, 0f);
            world.AddEntity(box);
        }
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        base.InitializeCamera(camera);

        // The UIRoot is created by CasaEngineGame.OnViewAdded after world.LoadContent,
        // so it is guaranteed to be available at this point in the lifecycle.
        var uiRoot = GetUIRoot();
        if (uiRoot == null) return;

        _pauseScreen = new PauseMenuScreen(OnResume);
        _hudScreen   = new HudScreen(OnOpenPauseMenu);

        // Push the HUD as the base layer — it will persist until Clean() is called.
        uiRoot.PushScreen(_hudScreen);
    }

    public override void Update(GameTime gameTime) { }

    public override void OnScreenResized(CasaEngineGame game, int width, int height)
    {
        game.GameManager.ViewManager.ApplyBackBufferLayout(width, height);
    }

    public override void Clean()
    {
        var uiRoot = GetUIRoot();
        uiRoot?.ScreenStack.Clear();

        _hudScreen   = null;
        _pauseScreen = null;
        _game        = null;
    }

    // ---- Button callbacks ----

    private void OnOpenPauseMenu()
    {
        if (_pauseScreen == null) return;
        GetUIRoot()?.PushScreen(_pauseScreen);
    }

    private void OnResume()
    {
        GetUIRoot()?.PopScreen();
    }

    // ---- Helpers ----

    private UIRoot? GetUIRoot()
        => _game?.GameManager.ViewManager.Views
            .FirstOrDefault(v => v.UIRoot != null)?.UIRoot;
}
