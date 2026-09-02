using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.Dialogue.UI;
using MGUI.Core.UI;
using MGUI.Shared.Assets;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace CasaEngine.Tests.Dialogue;

/// <summary>
/// Layout tests for <see cref="DialogueScreen"/>, driven on a headless MGUI desktop (the
/// <c>ContentBrowserViewTestHarness</c> montage: measurement-only text engine, never draws).
///
/// User-reported defect these exist for: with a wrapped multi-line line plus the OUI/NON choice, the
/// second choice button was clipped OUT of the window - the window was a fixed 150 px and its inner
/// stack carried a fixed PreferredHeight, so the content could never grow the box. The screen now
/// resizes to fit its content after every presentation refresh; these tests pin "every choice button
/// lies inside the window" on the REAL build/refresh/resize path (BuildWindow is the exact code
/// OnInitialize runs, minus the graphics-backed UIRoot the screen never actually needs).
/// </summary>
public class DialogueScreenLayoutTests
{
    private const int SurfaceWidth = 640;
    private const int SurfaceHeight = 480;

    private static (MGDesktop Desktop, TestRuntime Runtime) NewHeadlessDesktop()
    {
        TestRuntime runtime = new(new Rectangle(0, 0, SurfaceWidth, SurfaceHeight));
        MGDesktop desktop = new(runtime);
        desktop.LoadDefaultResources();
        return (desktop, runtime);
    }

    private static void AdvanceFrame(TestRuntime runtime, MGDesktop desktop, int totalElapsedMs)
    {
        runtime.ApplyFrame(new UpdateBaseArgs(
            TimeSpan.FromMilliseconds(totalElapsedMs),
            TimeSpan.FromMilliseconds(16),
            new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released),
            new KeyboardState()));
        desktop.Update();
    }

    [Fact]
    public void MultiLineTextWithTwoChoices_EveryChoiceButtonLiesInsideTheWindow()
    {
        var (desktop, runtime) = NewHeadlessDesktop();
        var service = new DialogueService();
        var screen = new DialogueScreen(service, static () => { });

        screen.BuildWindow(desktop);
        desktop.Windows.Add(screen.WindowForTests);
        screen.Show();

        // The sailor-12 shape: a line that wraps to several rows, then the OUI/NON choice.
        service.ShowLine(new DialogueLine("Qu'est-ce que tu veux, petit ? As-tu encore oublié où se trouve ta cabine ?"));
        service.ShowChoices(new[] { "OUI", "NON" });
        AdvanceFrame(runtime, desktop, 16);

        var window = screen.WindowForTests;
        var buttons = screen.ChoiceButtonsForTests;

        Assert.Equal(2, buttons.Count);
        Assert.True(window.WindowHeight > DialogueScreen.MinWindowHeight,
            $"the window must have GROWN past its {DialogueScreen.MinWindowHeight}px minimum to fit "
            + $"the wrapped text plus two choice buttons (actual: {window.WindowHeight}).");

        Rectangle windowBounds = new(window.Left, window.Top, window.WindowWidth, window.WindowHeight);
        foreach (var button in buttons)
        {
            Rectangle b = button.LayoutBounds;
            Assert.True(b.Height > 0 && b.Width > 0,
                "each choice button must have been laid out with a real size.");
            Assert.True(windowBounds.Contains(b),
                $"choice button at {b} must lie inside the window {windowBounds} - the user-reported "
                + "defect was the second button clipped below the fixed-height window.");
        }

        // The window must stay bottom-anchored on screen, whole.
        Assert.True(window.Top >= 0 && window.Top + window.WindowHeight <= SurfaceHeight,
            $"the grown window must remain fully on screen (Top={window.Top}, Height={window.WindowHeight}).");
    }

    [Fact]
    public void ShortLineWithoutChoices_StaysAtTheMinimumHeight()
    {
        var (desktop, runtime) = NewHeadlessDesktop();
        var service = new DialogueService();
        var screen = new DialogueScreen(service, static () => { });

        screen.BuildWindow(desktop);
        desktop.Windows.Add(screen.WindowForTests);
        screen.Show();

        service.ShowLine(new DialogueLine("Bonjour."));
        AdvanceFrame(runtime, desktop, 16);

        Assert.Equal(DialogueScreen.MinWindowHeight, screen.WindowForTests.WindowHeight);
    }

    [Fact]
    public void ChoicesClosing_ShrinksTheWindowBackToTheMinimum()
    {
        var (desktop, runtime) = NewHeadlessDesktop();
        var service = new DialogueService();
        var screen = new DialogueScreen(service, static () => { });

        screen.BuildWindow(desktop);
        desktop.Windows.Add(screen.WindowForTests);
        screen.Show();

        service.ShowLine(new DialogueLine("Qu'est-ce que tu veux, petit ? As-tu encore oublié où se trouve ta cabine ?"));
        service.ShowChoices(new[] { "OUI", "NON" });
        AdvanceFrame(runtime, desktop, 16);
        int grownHeight = screen.WindowForTests.WindowHeight;

        service.SelectChoice(0);
        service.ShowLine(new DialogueLine("Bien."));
        AdvanceFrame(runtime, desktop, 32);

        Assert.True(screen.WindowForTests.WindowHeight < grownHeight,
            "after the choices are consumed and a short line follows, the window must shrink back.");
        Assert.Equal(DialogueScreen.MinWindowHeight, screen.WindowForTests.WindowHeight);
    }

    // ------------------------------------------------------------------------------------------
    // Headless MGUI runtime stubs - the ContentBrowserViewTestHarness montage (measurement-only
    // text engine with deterministic metrics: line height = font size, char width = size / 2).
    // ------------------------------------------------------------------------------------------

    internal sealed class TestRuntime : IUIDesktopRuntime
    {
        private ITextMeasurementEngine _textEngine;

        public InputTracker Input { get; } = new();
        public string DefaultFontFamily { get; } = "TestSans";
        public IUISurface Surface { get; }
        public IUIAssetProvider AssetProvider { get; }
        public UpdateBaseArgs UpdateArgs { get; private set; } = new(TimeSpan.Zero, TimeSpan.Zero, default, default);

        public event EventHandler<EventArgs<ITextMeasurementEngine>>? TextEngineChanged;
        public event EventHandler<EventArgs>? EndUpdate
        {
            add { }
            remove { }
        }

        public ITextMeasurementEngine TextEngine
        {
            get => _textEngine;
            set
            {
                ITextMeasurementEngine previous = _textEngine;
                _textEngine = value ?? throw new ArgumentNullException(nameof(value));
                TextEngineChanged?.Invoke(this, new(previous, _textEngine));
            }
        }

        public TestRuntime(Rectangle surfaceBounds)
        {
            Surface = new TestSurface(surfaceBounds, new TestRenderTarget(surfaceBounds.Width, surfaceBounds.Height));
            AssetProvider = new TestAssetProvider();
            _textEngine = new TestTextEngine(DefaultFontFamily);
        }

        public IUIDrawTransaction CreateDrawTransaction(DrawSettings settings, bool deferBegin)
            => throw new NotSupportedException($"{nameof(DialogueScreenLayoutTests)} never draws.");

        public void ApplyFrame(UpdateBaseArgs updateArgs)
        {
            UpdateArgs = updateArgs;
            Input.Update(updateArgs);
        }

        public void RegisterView(IUIView view)
        {
        }
    }

    private sealed class TestSurface : IUISurface
    {
        private readonly Rectangle _bounds;
        private readonly IUIRenderTarget _renderTarget;

        public TestSurface(Rectangle bounds, IUIRenderTarget renderTarget)
        {
            _bounds = bounds;
            _renderTarget = renderTarget;
        }

        public Rectangle GetBounds() => _bounds;

        public IUIRenderTarget GetRenderTarget() => _renderTarget;
    }

    private sealed class TestAssetProvider : IUIAssetProvider
    {
        private readonly Dictionary<string, TestImageResource> _images = new(StringComparer.OrdinalIgnoreCase);

        public IUIImageResource LoadImage(string assetName)
        {
            if (!_images.TryGetValue(assetName, out TestImageResource? image))
            {
                image = new TestImageResource(assetName, 16, 16);
                _images[assetName] = image;
            }

            return image;
        }

        public bool TryLoadImage(string assetName, out IUIImageResource image)
        {
            image = LoadImage(assetName);
            return true;
        }
    }

    private class TestImageResource : IUIImageResource
    {
        public string Id { get; }
        public int Width { get; }
        public int Height { get; }
        public bool IsDisposed => false;

        public TestImageResource(string id, int width, int height)
        {
            Id = id;
            Width = width;
            Height = height;
        }
    }

    private sealed class TestRenderTarget : TestImageResource, IUIRenderTarget
    {
        public TestRenderTarget(int width, int height)
            : base("dialogue-screen-layout-test-render-target", width, height)
        {
        }
    }

    private sealed class TestTextEngine : ITextMeasurementEngine
    {
        private readonly string _defaultFamily;

        public TestTextEngine(string defaultFamily)
        {
            _defaultFamily = defaultFamily;
        }

        public ResolvedFont ResolveFont(FontSpec spec)
        {
            int size = Math.Max(1, spec.Size);
            FontSpec effectiveSpec = string.IsNullOrWhiteSpace(spec.Family)
                ? FontSpec.Normal(_defaultFamily, size)
                : spec;

            return new ResolvedFont(effectiveSpec, size, 1.0f, 1.0f, size, Math.Max(1.0f, size * 0.5f), Vector2.Zero, false, new object());
        }

        public Vector2 MeasureText(ResolvedFont font, string text)
        {
            float width = (text?.Length ?? 0) * Math.Max(font.SpaceWidth, 1.0f);
            return new Vector2(width, font.LineHeight);
        }

        public GlyphMetrics MeasureGlyph(ResolvedFont font, char character)
            => new(0.0f, Math.Max(font.SpaceWidth, 1.0f), 0.0f, font.LineHeight);

        public float GetLineHeight(ResolvedFont font) => font.LineHeight;

        public float GetSpaceWidth(ResolvedFont font) => font.SpaceWidth;

        public void InvalidateCache()
        {
        }
    }
}
