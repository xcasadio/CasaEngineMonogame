using MGUI.Core.UI;
using MGUI.Shared.Assets;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Tests.ContentBrowser;

/// <summary>Headless MGUI desktop used to drive content browser views without a graphics device.
/// These tests only update the UI tree, they never draw it.</summary>
internal sealed class ContentBrowserViewTestHarness
{
    private readonly TestRuntime _runtime;

    public MGDesktop Desktop { get; }
    public MGWindow Window { get; }

    private ContentBrowserViewTestHarness(TestRuntime runtime, MGDesktop desktop, MGWindow window)
    {
        _runtime = runtime;
        Desktop = desktop;
        Window = window;
    }

    public static ContentBrowserViewTestHarness Create(int windowWidth = 412, int windowHeight = 240)
    {
        TestRuntime runtime = new(new Rectangle(0, 0, 640, 480));
        MGDesktop desktop = new(runtime);
        desktop.LoadDefaultResources();

        MGWindow window = new(desktop, 0, 0, windowWidth, windowHeight)
        {
            WindowStyle = WindowStyle.None,
        };

        desktop.Windows.Add(window);
        return new ContentBrowserViewTestHarness(runtime, desktop, window);
    }

    public void AdvanceFrame(int totalElapsedMs, Point? position = null, MouseButton? pressedButton = null, Keys[]? pressedKeys = null)
    {
        Point mousePosition = position ?? Point.Zero;
        _runtime.ApplyFrame(new UpdateBaseArgs(
            TimeSpan.FromMilliseconds(totalElapsedMs),
            TimeSpan.FromMilliseconds(16),
            new MouseState(
                mousePosition.X,
                mousePosition.Y,
                0,
                pressedButton == MouseButton.Left ? ButtonState.Pressed : ButtonState.Released,
                pressedButton == MouseButton.Middle ? ButtonState.Pressed : ButtonState.Released,
                pressedButton == MouseButton.Right ? ButtonState.Pressed : ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released),
            pressedKeys == null ? new KeyboardState() : new KeyboardState(pressedKeys)));
        Desktop.Update();
    }

    /// <summary>Presses and releases the left mouse button at <paramref name="position"/>, advancing one frame per step.</summary>
    public int Click(int totalElapsedMs, Point position, int frameStepMs = 16)
    {
        AdvanceFrame(totalElapsedMs, position, MouseButton.Left);
        AdvanceFrame(totalElapsedMs + frameStepMs, position);
        return totalElapsedMs + frameStepMs * 2;
    }

    private sealed class TestRuntime : IUIDesktopRuntime
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
            => throw new NotSupportedException($"{nameof(ContentBrowserViewTestHarness)} never draws.");

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
            : base("content-browser-test-render-target", width, height)
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
