using CasaEngine.Framework.Application;
using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.UI;
using MGUI.Core.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>
/// <c>FirstScreenName</c> opens a screen once, in the runtime only (ADR-0078). <c>UpdateWorld</c> needs a game, so
/// these tests drive <see cref="GameManager.OpenFirstScreen"/> directly with a stand-in view and factory.
/// </summary>
public class FirstScreenTests
{
    private readonly GameManager _gameManager = new(null);
    private readonly List<string> _created = new();

    [Fact]
    public void TheDesignResolution_ExistsOnlyWhenTheProjectSetsScaleUI()
    {
        Assert.Null(GameManager.DesignResolutionOf(null));
        Assert.Null(GameManager.DesignResolutionOf(new VirtualResolutionSettings { Width = 320, Height = 180 }));
        Assert.Equal(
            new Point(320, 180),
            GameManager.DesignResolutionOf(new VirtualResolutionSettings { Width = 320, Height = 180, ScaleUI = true }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AnEmptyName_OpensNothing_EvenWithoutAView(string? name)
    {
        _gameManager.OpenFirstScreen(GameplayExecutionPolicies.Runtime, name!, Create);

        Assert.Empty(_created);
    }

    [Fact]
    public void InTheRuntime_TheNamedScreenIsPushedOnTheActiveView()
    {
        var view = AddView();

        _gameManager.OpenFirstScreen(GameplayExecutionPolicies.Runtime, "Main Menu", Create);

        Assert.Equal(new[] { "Main Menu" }, _created);
        Assert.Single(view.Pushed);
    }

    [Fact]
    public void TheFirstScreen_OpensOnce()
    {
        var view = AddView();

        _gameManager.OpenFirstScreen(GameplayExecutionPolicies.Runtime, "Main Menu", Create);
        _gameManager.OpenFirstScreen(GameplayExecutionPolicies.Runtime, "Main Menu", Create);

        Assert.Single(view.Pushed);
    }

    [Fact]
    public void InTheRuntime_WithoutAnActiveView_ItThrowsAndNamesTheScreen()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => _gameManager.OpenFirstScreen(GameplayExecutionPolicies.Runtime, "Main Menu", Create));

        Assert.Contains("Main Menu", error.Message);
    }

    [Fact]
    public void InTheRuntime_WithAViewThatHostsNoUI_ItThrows()
    {
        AddViewWithRuntime(null);

        Assert.Throws<InvalidOperationException>(
            () => _gameManager.OpenFirstScreen(GameplayExecutionPolicies.Runtime, "Main Menu", Create));
    }

    public static TheoryData<string> EditorPolicies() => new() { "preview", "simulation" };

    private static GameplayExecutionPolicy EditorPolicy(string name)
        => name == "preview" ? GameplayExecutionPolicies.EditorPreview : GameplayExecutionPolicies.EditorSimulation;

    [Theory]
    [MemberData(nameof(EditorPolicies))]
    public void InTheEditor_NoScreenIsPushed_DespiteAnActiveViewAndAName(string policy)
    {
        var view = AddView();

        _gameManager.OpenFirstScreen(EditorPolicy(policy), "Main Menu", Create);

        Assert.Empty(_created);
        Assert.Empty(view.Pushed);
    }

    [Theory]
    [MemberData(nameof(EditorPolicies))]
    public void InTheEditor_NoActiveView_IsNotAnError(string policy)
    {
        _gameManager.OpenFirstScreen(EditorPolicy(policy), "Main Menu", Create);

        Assert.Empty(_created);
    }

    [Fact]
    public void AScreenThatFailsToPush_IsDisposed_AndTheFailureGoesThrough()
    {
        var view = AddView();
        view.PushFailure = new InvalidOperationException("Bad envelope.");
        var screen = new StubScreen();

        var error = Assert.Throws<InvalidOperationException>(
            () => _gameManager.OpenFirstScreen(GameplayExecutionPolicies.Runtime, "Main Menu", (_, _) => screen));

        Assert.Same(view.PushFailure, error);
        Assert.True(screen.IsDisposed);
    }

    private IUIScreen Create(string name, IUIViewRuntime view)
    {
        _created.Add(name);
        return new StubScreen();
    }

    private StubUIViewRuntime AddView()
    {
        var runtime = new StubUIViewRuntime();
        AddViewWithRuntime(runtime);
        return runtime;
    }

    private void AddViewWithRuntime(IUIViewRuntime? runtime)
    {
        var bounds = new Rectangle(0, 0, 320, 200);
        _gameManager.ViewManager.Add(new RenderView(new CasaEngine.Framework.Scene.World.World(), new ArcBallCameraComponent(), new StubRenderSurface(bounds))
        {
            Host = new StubViewHost(bounds),
            UIView = runtime!,
        });
    }

    private sealed class StubScreen : IUIScreen, IDisposable
    {
        public bool IsDisposed { get; private set; }

        public UILayer Layer => UILayer.Menu;

        public bool IsModal => false;

        public bool BlocksViewsBelow => false;

        public void Initialize(UIRoot root)
        {
        }

        public void Show()
        {
        }

        public void Hide()
        {
        }

        public void Update(GameTime gameTime)
        {
        }

        public IEnumerable<MGWindow> GetWindows() => Enumerable.Empty<MGWindow>();

        public void Dispose() => IsDisposed = true;
    }

    private sealed class StubRenderSurface : IRenderSurface
    {
        public StubRenderSurface(Rectangle viewportRect)
        {
            ViewportRect = viewportRect;
        }

        public bool IsBackBuffer => false;

        public Rectangle ViewportRect { get; }

        public RenderTarget2D? RenderTarget => null;

        public void Apply(GraphicsDevice graphicsDevice)
        {
        }

        public void Restore(GraphicsDevice graphicsDevice)
        {
        }
    }

    private sealed class StubViewHost : IViewHost, IViewScreenBoundsHost
    {
        public StubViewHost(Rectangle screenBounds)
        {
            ScreenBounds = screenBounds;
        }

        public ViewId ViewId => ViewId.Empty;

        public int Width => ScreenBounds.Width;

        public int Height => ScreenBounds.Height;

        public bool IsVisible => true;

        public Rectangle ScreenBounds { get; }

        public event Action<IViewHost, int, int>? Resized;

        public event Action<IViewHost>? Closed;

        public void NotifyResized(int newWidth, int newHeight)
        {
            Resized?.Invoke(this, newWidth, newHeight);
        }

        public void Dispose()
        {
            Closed?.Invoke(this);
        }
    }

    private sealed class StubUIViewRuntime : IUIViewRuntime
    {
        public List<IUIScreen> Pushed { get; } = new();

        public Exception? PushFailure { get; set; }

        public bool IsPointerOverUI => false;

        public bool IsPointerCaptured => false;

        public bool IsKeyboardCaptured => false;

        public UIViewInputState InputState => new(false, false, false, false);

        public bool HasModalInput => false;

        public UIViewMetrics Metrics { get; private set; } = new(new Point(1, 1), new Point(1, 1), 1.0f, Rectangle.Empty);

        public void Update(GameTime gameTime)
        {
        }

        public void Draw()
        {
        }

        public void UpdateMetrics(UIViewMetrics metrics)
        {
            Metrics = metrics;
        }

        public void PushScreen(IUIScreen screen)
        {
            if (PushFailure != null)
            {
                throw PushFailure;
            }

            Pushed.Add(screen);
        }

        public IUIScreen? PopScreen() => null;

        public void RemoveScreen(IUIScreen screen)
        {
        }

        public void Dispose()
        {
        }
    }
}
