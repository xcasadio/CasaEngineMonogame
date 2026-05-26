using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.UI.Backend.MonoGame;
using MGUI.Backend.MonoGame;
using MGUI.FontStashSharp;
using MGUI.Core.UI;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.UI;

/// <summary>
/// Per-view UI root that owns a <see cref="MGDesktop"/> (MGUI surface) and a
/// <see cref="ScreenStack"/> (layered screen management).
///
/// One <see cref="UIRoot"/> is automatically created for each <see cref="RenderView"/>
/// by <see cref="CasaEngineGame"/> when a view is registered with <see cref="ViewManager"/>.
///
/// <b>Render order:</b> call <see cref="Draw"/> AFTER all world renderers have flushed
/// so the UI is composited on top of the 3D scene (handled by <c>DefaultViewPipeline</c>).
///
/// <b>Update order:</b> call <see cref="Update"/> BEFORE gameplay logic each frame
/// so the UI has first-chance to consume input events (handled by <see cref="CasaEngineGame"/>).
/// </summary>
public sealed class UIRoot : IUIViewRuntime
{
    private bool _disposed;

    /// <summary>The backend runtime that powers this MGUI desktop.</summary>
    public IUIDesktopRuntime Runtime { get; }

    /// <summary>The MGUI desktop surface for this view.</summary>
    public MGDesktop Desktop { get; }

    /// <summary>Layered screen stack (HUD / Menu / Modal / Tooltip / Debug).</summary>
    public ScreenStack ScreenStack { get; }

    /// <summary>
    /// Uniform UI scale factor for this view.
    /// 1.0 = no scaling; updated from per-view UI metrics.
    /// </summary>
    public float UIScale { get; set; } = 1.0f;

    public UIViewMetrics Metrics { get; private set; } = new(new Point(1, 1), new Point(1920, 1080), 1.0f, new Rectangle(0, 0, 1, 1));

    public bool IsPointerOverUI => Desktop.Windows.Any(w => w.HoveredElement != null);

    public bool IsPointerCaptured => Desktop.Windows.Any(w => w.PressedElement != null)
        || Desktop.ActiveContextMenu?.PressedElement != null;

    public bool IsKeyboardCaptured => Desktop.FocusedKeyboardHandler != null;

    public UIViewInputState InputState => new(IsPointerOverUI, IsPointerCaptured, IsKeyboardCaptured, HasModalInput);

    public bool HasModalInput => ScreenStack.HasModalInput;

    public void UpdateMetrics(UIViewMetrics metrics)
    {
        bool metricsChanged = Metrics != metrics;
        Metrics = metrics;
        UIScale = metrics.Scale;

        if (metricsChanged)
        {
            Desktop.InvalidateAllLayouts();
        }
    }

    /// <summary>
    /// Initializes the UIRoot for the given view.
    /// Creates a <see cref="ViewRenderHost"/> from the view's surface so that
    /// MGUI bounds and mouse input are viewport-local.
    /// </summary>
    public UIRoot(CasaEngineGame game, IRenderSurface surface, EngineRuntimeContext runtimeContext = null)
    {
        var host    = new ViewRenderHost(game, surface, runtimeContext?.WindowInputSource);
        var backend = CasaMonoGameBackendBootstrap.Create(host, surface: new CasaRenderSurfaceAdapter(surface));
        Runtime     = backend.Runtime;
        Desktop     = new MGDesktop(Runtime);
        Desktop.TextEngine = CreateFontStashSharpTextEngine(game);
        ScreenStack = new ScreenStack(this);
    }

    private FontStashSharpTextEngine CreateFontStashSharpTextEngine(CasaEngineGame game)
    {
        if (Runtime is not IMonoGameDesktopBackend monoGameBackend)
        {
            throw new InvalidOperationException("UIRoot requires a MonoGame-backed MGUI runtime.");
        }

        var textEngine = new FontStashSharpTextEngine();
        string familyName = monoGameBackend.DefaultFontFamily;

        if (game.DefaultFontSystemTtfData.Length > 0)
        {
            textEngine.AddFontSystem(familyName, CustomFontStyles.Normal, game.FontSystem, game.DefaultFontSystemTtfData);
        }
        else
        {
            textEngine.AddFontSystem(familyName, CustomFontStyles.Normal, game.FontSystem);
        }

        textEngine.MatchSpriteFontSizing(monoGameBackend.FontManager);
        return textEngine;
    }

    // ---- Frame lifecycle ----

    /// <summary>
    /// Updates the MGUI desktop input state and all active screens.
    /// Must be called every frame before gameplay logic.
    /// </summary>
    public void Update(GameTime gameTime)
    {
        if (_disposed)
        {
            return;
        }

        // Desktop.Update() reads the current input snapshot that was refreshed
        // when PreviewUpdate fired on the UI host/runtime bridge.
        Desktop.Update();
        ScreenStack.Update(gameTime);
    }

    /// <summary>
    /// Draws the UI overlay onto the current render target.
    /// Must be called while the view's render target and viewport are active,
    /// i.e., from inside <c>IViewRenderPipeline.RenderView</c> after renderer flushes.
    /// </summary>
    public void Draw()
    {
        if (_disposed)
        {
            return;
        }

        Desktop.Draw();
    }

    // ---- Screen convenience helpers ----

    /// <summary>
    /// Pushes <paramref name="screen"/> onto the <see cref="ScreenStack"/> at its declared layer.
    /// </summary>
    public void PushScreen(IUIScreen screen) => ScreenStack.Push(screen);

    /// <summary>Pops the topmost screen from the <see cref="ScreenStack"/>.</summary>
    public IUIScreen PopScreen() => ScreenStack.Pop();

    /// <summary>Removes a specific screen from the <see cref="ScreenStack"/>.</summary>
    public void RemoveScreen(IUIScreen screen) => ScreenStack.Remove(screen);

    // ---- IDisposable ----

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ScreenStack.Clear();
    }
}
