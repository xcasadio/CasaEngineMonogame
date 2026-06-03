using System.Reflection;
using CasaEngine.Editor.ContentBrowser.Models;
using CasaEngine.Editor.ContentBrowser.Views;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Assets;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Xunit;

namespace CasaEngine.Tests.ContentBrowser;

public class GridViewVirtualizationTests
{
    [Fact]
    public void ScrollToBottom_RealizesLastContentItem()
    {
        GridViewHarness harness = CreateHarness();

        for (int index = 0; index < 908; index++)
        {
            harness.Items.Add(new ContentItem($@"D:\TestAssets\Spritesheets\Item{index:D4}.png", false));
        }

        harness.GridView.SetItems(harness.Items);

        AdvanceFrame(harness.Runtime, harness.Desktop, 0);
        AdvanceFrame(harness.Runtime, harness.Desktop, 16);

        MGScrollViewer scrollViewer = GetRequiredPrivateField<MGScrollViewer>(harness.GridView, "_scrollViewer");
        VirtualizingWrapPanel itemsPanel = GetRequiredPrivateField<VirtualizingWrapPanel>(harness.GridView, "_itemsPanel");

        Assert.True(scrollViewer.MaxVerticalOffset > 0f);

        scrollViewer.VerticalOffset = scrollViewer.MaxVerticalOffset;

        AdvanceFrame(harness.Runtime, harness.Desktop, 32);
        AdvanceFrame(harness.Runtime, harness.Desktop, 48);

        Assert.Equal(harness.Items.Count - 1, itemsPanel.LastRealizedIndex);
        Assert.True(itemsPanel.TryGetRealizedElement(harness.Items.Count - 1, out MGElement? lastElement));
        Assert.False(lastElement.ActualLayoutBounds.IsEmpty);
    }

    private static GridViewHarness CreateHarness()
    {
        GridViewTestRuntime runtime = new(new Rectangle(0, 0, 640, 480));
        MGDesktop desktop = new(runtime);
        desktop.LoadDefaultResources();

        MGWindow window = new(desktop, 0, 0, 412, 240)
        {
            WindowStyle = WindowStyle.None,
        };

        GridView gridView = new(window, 96, static _ => null)
        {
        };

        window.SetContent(gridView.RootElement);
        desktop.Windows.Add(window);

        return new GridViewHarness(runtime, desktop, window, gridView, new List<ContentItem>());
    }

    private static void AdvanceFrame(GridViewTestRuntime runtime, MGDesktop desktop, int totalElapsedMs)
    {
        runtime.ApplyFrame(new UpdateBaseArgs(
            TimeSpan.FromMilliseconds(totalElapsedMs),
            TimeSpan.FromMilliseconds(16),
            new MouseState(),
            new KeyboardState()));
        desktop.Update();
    }

    private static T GetRequiredPrivateField<T>(object target, string fieldName)
        where T : class
    {
        FieldInfo? field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        object? value = field.GetValue(target);
        Assert.NotNull(value);
        return Assert.IsType<T>(value);
    }

    private readonly record struct GridViewHarness(
        GridViewTestRuntime Runtime,
        MGDesktop Desktop,
        MGWindow Window,
        GridView GridView,
        List<ContentItem> Items);

    private sealed class GridViewTestRuntime : IUIDesktopRuntime
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

        public GridViewTestRuntime(Rectangle surfaceBounds)
        {
            Surface = new GridViewTestSurface(surfaceBounds, new GridViewTestRenderTarget(surfaceBounds.Width, surfaceBounds.Height));
            AssetProvider = new GridViewTestAssetProvider();
            _textEngine = new GridViewTestTextEngine(DefaultFontFamily);
        }

        public IUIDrawTransaction CreateDrawTransaction(DrawSettings settings, bool deferBegin)
            => new GridViewNoOpDrawTransaction(this, settings ?? DrawSettings.Default);

        public void ApplyFrame(UpdateBaseArgs updateArgs)
        {
            UpdateArgs = updateArgs;
            Input.Update(updateArgs);
        }

        public void RegisterView(IUIView view)
        {
        }
    }

    private sealed class GridViewNoOpDrawTransaction : IUIDrawTransaction
    {
        private Rectangle? _currentClipBounds;

        public DrawSettings CurrentSettings { get; private set; }
        public IUIDesktopRuntime Renderer { get; }
        public Rectangle? CurrentClipBounds => _currentClipBounds;

        public GridViewNoOpDrawTransaction(IUIDesktopRuntime renderer, DrawSettings settings)
        {
            Renderer = renderer;
            CurrentSettings = settings;
        }

        public void DrawTextureTo(IUIImageResource texture, Rectangle? source, Rectangle destination, Color colorMask) { }

        public void DrawTextureTo(IUIImageResource texture, Rectangle? source, Rectangle destination, Color colorMask,
            Vector2 origin, float rotation = 0f, float depth = 0f, UIDrawFlip flip = UIDrawFlip.None) { }

        public void DrawTextureAt(IUIImageResource texture, Rectangle? source, Vector2 destination, Color colorMask,
            Vector2 origin, float rotation = 0f, float scaleX = 1f, float scaleY = 1f, float depth = 0f, UIDrawFlip flip = UIDrawFlip.None) { }

        public void DrawTextViaEngine(ResolvedFont font, string text, Vector2 position, Color color, Vector2 origin, float scale,
            float rotation = 0f, float depth = 0f, UIDrawFlip flip = UIDrawFlip.None) { }

        public void FillRectangle(Vector2 origin, RectangleF destination, Color color) { }

        public void FillPoint(Vector2 center, Color color, float width) { }

        public void StrokeRectangle(Vector2 origin, RectangleF destination, Color color, Thickness thickness) { }

        public void StrokeAndFillRectangle(Vector2 origin, RectangleF destination, Color strokeColor, Color fillColor, Thickness strokeThickness) { }

        public void FillPolygon(Vector2 origin, IEnumerable<Vector2> vertices, Color color) { }

        public void StrokeAndFillPolygon(Vector2 origin, IEnumerable<Vector2> vertices, Color strokeColor, Color fillColor, float strokeThickness = 1.0f) { }

        public void FillTriangle(Vector2 origin, Vector2 v0, Color c0, Vector2 v1, Color c1, Vector2 v2, Color c2) { }

        public void FillQuadrilateralLinearClamp(Vector2 origin, Vector2 topLeft, Color topLeftColor, Vector2 topRight, Color topRightColor,
            Vector2 bottomRight, Color bottomRightColor, Vector2 bottomLeft, Color bottomLeftColor) { }

        public void StrokeLineSegment(Vector2 origin, Vector2 start, Vector2 end, Color color, float thickness = 1.0f) { }

        public void FillCircle(Vector2 center, Color color, float radius, int numSides = 32) { }

        public void StrokeCircle(Vector2 center, Color color, float radius, float thickness = 1.0f, int numSides = 32) { }

        public void StrokeAndFillCircle(Vector2 center, Color strokeColor, Color fillColor, float radius, float strokeThickness = 1.0f, int numSides = 32) { }

        public void SetDrawSettings(DrawSettings settings)
        {
            CurrentSettings = settings ?? DrawSettings.Default;
        }

        public IDisposable SetDrawSettingsTemporary(DrawSettings settings)
        {
            DrawSettings previous = CurrentSettings;
            SetDrawSettings(settings);
            return new GridViewDisposableAction(() => CurrentSettings = previous);
        }

        public IDisposable SetRenderTargetTemporary(IUIRenderTarget newRenderTarget, Color? clearColor)
            => new GridViewDisposableAction(static () => { });

        public IDisposable SetTransformTemporary(Matrix transform)
            => SetDrawSettingsTemporary(CurrentSettings with { Transform = transform });

        public ClipResolveResult ResolveClip(ClipDefinition definition)
        {
            ClipDefinition effective = definition ?? ClipDefinition.None();
            return new(effective, effective, ClipStrategy.Scissor, false);
        }

        public ClipScope PushClipTemporary(ClipDefinition definition)
        {
            ClipResolveResult resolution = ResolveClip(definition);
            Rectangle? previous = _currentClipBounds;
            _currentClipBounds = resolution.Effective.Kind == ClipKind.None ? null : resolution.Effective.Shape.Bounds;
            return new(resolution, () => _currentClipBounds = previous);
        }

        public ClipScope PushRectangleClip(Rectangle? bounds, bool intersectWithCurrentClipTarget)
        {
            ClipDefinition definition = bounds.HasValue
                ? ClipDefinition.Rectangle(bounds.Value, intersectWithCurrentClipTarget)
                : ClipDefinition.None(intersectWithCurrentClipTarget);
            return PushClipTemporary(definition);
        }

        public IDisposable SetClipTargetTemporary(Rectangle? bounds, bool intersectWithCurrentClipTarget)
            => PushRectangleClip(bounds, intersectWithCurrentClipTarget);

        public void Dispose()
        {
        }
    }

    private sealed class GridViewTestSurface : IUISurface
    {
        private readonly Rectangle _bounds;
        private readonly IUIRenderTarget _renderTarget;

        public GridViewTestSurface(Rectangle bounds, IUIRenderTarget renderTarget)
        {
            _bounds = bounds;
            _renderTarget = renderTarget;
        }

        public Rectangle GetBounds() => _bounds;

        public IUIRenderTarget GetRenderTarget() => _renderTarget;
    }

    private sealed class GridViewTestAssetProvider : IUIAssetProvider
    {
        private readonly Dictionary<string, GridViewTestImageResource> _images = new(StringComparer.OrdinalIgnoreCase);

        public IUIImageResource LoadImage(string assetName)
        {
            if (!_images.TryGetValue(assetName, out GridViewTestImageResource? image))
            {
                image = new GridViewTestImageResource(assetName, 16, 16);
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

    private class GridViewTestImageResource : IUIImageResource
    {
        public string Id { get; }
        public int Width { get; }
        public int Height { get; }
        public bool IsDisposed { get; private set; }

        public GridViewTestImageResource(string id, int width, int height)
        {
            Id = id;
            Width = width;
            Height = height;
        }
    }

    private sealed class GridViewTestRenderTarget : GridViewTestImageResource, IUIRenderTarget
    {
        public GridViewTestRenderTarget(int width, int height)
            : base("gridview-render-target", width, height)
        {
        }
    }

    private sealed class GridViewTestTextEngine : ITextMeasurementEngine
    {
        private readonly string _defaultFamily;

        public GridViewTestTextEngine(string defaultFamily)
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

    private sealed class GridViewDisposableAction : IDisposable
    {
        private readonly Action _disposeAction;
        private bool _disposed;

        public GridViewDisposableAction(Action disposeAction)
        {
            _disposeAction = disposeAction;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _disposeAction();
        }
    }
}