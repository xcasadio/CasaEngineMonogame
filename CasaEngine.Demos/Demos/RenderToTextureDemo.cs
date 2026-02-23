using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Demonstrates rendering a scene into a <see cref="RenderTargetSurface"/>
/// and displaying the resulting texture as an overlay on the main view.
///
/// Layout:
///   - View 1: full-screen BackBuffer (camera 1, front angle)
///   - View 2: 256x256 RenderTargetSurface (camera 2, top-down angle)
///   - PostDraw: RT texture drawn in the bottom-right corner using SpriteBatch
/// </summary>
public class RenderToTextureDemo : Demo
{
    private const int RtSize = 256;

    private ArcBallCameraComponent? _camera2;
    private RenderTargetSurface? _rtSurface;
    private CasaEngineGame? _game;

    public override string Title => "Render-to-texture demo";
    public override string Description => "Renders a 3D scene into a RenderTarget2D and then displays that texture on a quad in the main scene.";

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        var world = game.GameManager.CurrentWorld;

        // Ground
        var groundEntity = new Entity { Name = "Ground" };
        var groundMesh = new StaticMeshComponent();
        groundEntity.RootComponent = groundMesh;
        groundMesh.Mesh = StaticMesh.CreateFromGeometricPrimitive(new BoxPrimitive(20, 1, 20));
        groundMesh.Mesh.Initialize(game.AssetContentManager);
        groundMesh.LocalPosition = new Vector3(0, -0.5f, 0);
        world.AddEntity(groundEntity);

        // A few boxes
        for (int i = 0; i < 3; i++)
        {
            var boxEntity = new Entity { Name = $"Box {i}" };
            var boxMesh = new StaticMeshComponent();
            boxEntity.RootComponent = boxMesh;
            boxMesh.Mesh = StaticMesh.CreateFromGeometricPrimitive(new BoxPrimitive(2, 2, 2));
            boxMesh.Mesh.Initialize(game.AssetContentManager);
            boxMesh.LocalPosition = new Vector3((i - 1) * 4, 1, 0);
            world.AddEntity(boxEntity);
        }
    }

    public override CameraComponent CreateCamera(CasaEngineGame game)
    {
        // Camera 1: main view (inherited from Demo base)
        var camera1 = (ArcBallCameraComponent)base.CreateCamera(game);

        // Camera 2: top-down view used for render-to-texture
        var entity2 = new Entity { Name = "Camera 2 (top)" };
        _camera2 = new ArcBallCameraComponent();
        entity2.RootComponent = _camera2;
        entity2.Initialize();
        game.GameManager.CurrentWorld.AddEntity(entity2);

        return camera1;
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        var game = _game!;
        var pp = game.GraphicsDevice.PresentationParameters;

        // ---- Camera 1: front view (full-screen backbuffer) ----
        var cam1 = (ArcBallCameraComponent)camera;
        cam1.SetCamera(Vector3.Backward * 18 + Vector3.Up * 10, Vector3.Zero, Vector3.Up);
        cam1.OnScreenResized(pp.BackBufferWidth, pp.BackBufferHeight);

        // ---- Camera 2: top-down view (render-to-texture) ----
        _camera2!.SetCamera(Vector3.Up * 25, Vector3.Zero, Vector3.Forward);
        _camera2.OnScreenResized(RtSize, RtSize);

        // ---- Create / (re)create render target ----
        _rtSurface?.Dispose();
        _rtSurface = new RenderTargetSurface(game.GraphicsDevice, RtSize, RtSize);

        // ---- Register views ----
        var world = game.GameManager.CurrentWorld;
        var viewManager = game.GameManager.ViewManager;

        viewManager.Clear();

        // View 1: main backbuffer
        var fullScreen = new Rectangle(0, 0, pp.BackBufferWidth, pp.BackBufferHeight);
        viewManager.Add(new RenderView(world, cam1, new BackBufferSurface(fullScreen))
        {
            Name = "Main view",
            ClearColor = Color.CornflowerBlue,
        });

        // View 2: render-to-texture (top-down)
        viewManager.Add(new RenderView(world, _camera2, _rtSurface)
        {
            Name = "Top-down RT",
            ClearColor = new Color(0.05f, 0.05f, 0.15f),
        });
    }

    /// <summary>
    /// Draws the render-target texture as a thumbnail in the bottom-right corner.
    /// Called after the pipeline has flushed all views.
    /// </summary>
    public override void PostDraw(CasaEngineGame game, GameTime gameTime)
    {
        if (_rtSurface?.Texture == null)
        {
            return;
        }

        var pp = game.GraphicsDevice.PresentationParameters;
        const int padding = 8;
        const int thumbSize = RtSize / 2; // 128x128

        var dest = new Rectangle(
            pp.BackBufferWidth  - thumbSize - padding,
            pp.BackBufferHeight - thumbSize - padding,
            thumbSize,
            thumbSize);

        // Draw a dark border
        var border = new Rectangle(dest.X - 2, dest.Y - 2, dest.Width + 4, dest.Height + 4);

        var sb = game.SpriteBatch!;
        // Use BlendState.Opaque so the thumbnail is always fully opaque regardless of
        // the alpha channel content of the RT (3D effects may write alpha != 1).
        sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone);
        sb.Draw(_rtSurface.Texture, dest, Color.White);
        sb.End();
    }

    public override void Update(GameTime gameTime)
    {
    }

    public override void Clean()
    {
        if (_game != null)
        {
            _game.GameManager.ViewManager.Clear();
        }

        _rtSurface?.Dispose();
        _rtSurface = null;
        _camera2 = null;
        _game = null;
    }
}
