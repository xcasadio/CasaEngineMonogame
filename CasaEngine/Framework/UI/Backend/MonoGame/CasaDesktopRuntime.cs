using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.VectorDraw;
using MGUI.Backend.MonoGame;
using MGUI.Shared.Assets;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;
using CasaEngine.Framework.UI.Backend.MonoGame.Assets;

namespace CasaEngine.Framework.UI.Backend.MonoGame;

public sealed class CasaDesktopRuntime : IMonoGameDesktopBackend
{
    public IRenderHost Host { get; }
    public IRawInputSource RawInputSource { get; }
    public IUISurface Surface { get; }
    private List<IUIView> MutableViews { get; } = new();
    public IReadOnlyList<IUIView> Views => MutableViews;

    public GraphicsDevice GraphicsDevice => Host.GraphicsDevice;
    public SpriteBatch SpriteBatch { get; }
    public PrimitiveBatch PrimitiveBatch { get; }
    internal CasaRenderTargetPool RenderTargetPool { get; } = new();

    public ContentManager Content { get; }

    public FontManager FontManager { get; }
    public string DefaultFontFamily => FontManager.DefaultFontFamily;
    public IUIAssetProvider AssetProvider { get; }

    private ITextMeasurementEngine _textEngine;
    public ITextMeasurementEngine TextEngine
    {
        get => _textEngine;
        set
        {
            if (value is not ITextDrawEngine)
            {
                throw new ArgumentException($"{nameof(TextEngine)} must also implement {nameof(ITextDrawEngine)} for the CasaEngine MonoGame backend.", nameof(value));
            }

            ITextMeasurementEngine previous = TextEngine;
            if (_textEngine != null && !ReferenceEquals(_textEngine, value))
            {
                _textEngine.InvalidateCache();
            }

            _textEngine = value ?? throw new ArgumentNullException(nameof(value));
            TextEngineChanged?.Invoke(this, new EventArgs<ITextMeasurementEngine>(previous, TextEngine));
        }
    }

    internal ITextDrawEngine GetTextRenderer() => (ITextDrawEngine)TextEngine;

    public event EventHandler<EventArgs<ITextMeasurementEngine>>? TextEngineChanged;
    public event EventHandler<EventArgs>? EndUpdate;

    public InputTracker Input { get; }
    public UpdateBaseArgs UpdateArgs { get; private set; }

    private TimeSpan _previousUpdateTimeSpan = TimeSpan.Zero;

    public readonly Texture2D ScrollMarker;

    private readonly Dictionary<Color, SolidColorTexture> _solidColorTextures = new();
    private readonly Dictionary<int, Texture2D> _circleTextures = new();
    private const int MinimumCircleTextureRadius = 32;
    private const int MaximumCircleTextureRadius = 1024;

    public CasaDesktopRuntime(IRenderHost host, IRawInputSource? rawInputSource = null, IUISurface? surface = null, IUIAssetProvider? assetProvider = null)
    {
        ArgumentNullException.ThrowIfNull(host);

        Host = host;
        RawInputSource = ResolveInputSource(host, rawInputSource);
        Surface = surface ?? new CasaBackBufferSurface(host);
        SpriteBatch = new SpriteBatch(GraphicsDevice);
        PrimitiveBatch = new PrimitiveBatch(GraphicsDevice, 1024);
        Content = new ContentManager(host, "Content");
        FontManager = new FontManager(Content, "Arial");
        AssetProvider = assetProvider ?? new CasaUIAssetProvider(Content, FontManager);
        TextEngine = new SpriteFontTextEngine(FontManager);
        Input = new InputTracker();

        ScrollMarker = Content.Load<Texture2D>(Path.Combine("Icons", "ScrollMarker"));

        Host.PreviewUpdate += (_, elapsed) =>
        {
            UpdateArgs = new UpdateBaseArgs(
                elapsed,
                elapsed.Subtract(_previousUpdateTimeSpan),
                RawInputSource.GetMouseState(),
                RawInputSource.GetKeyboardState());
            _previousUpdateTimeSpan = elapsed;
            Input.Update(UpdateArgs);
        };

        Host.EndUpdate += (_, _) =>
        {
            Input.Mouse.UpdateHandlers();
            Input.Keyboard.UpdateHandlers();
            EndUpdate?.Invoke(this, EventArgs.Empty);
        };
    }

    public Rectangle GetViewport(int margin) => Surface.GetBounds().GetCompressed(margin);

    public IUIDrawTransaction CreateDrawTransaction(DrawSettings settings, bool deferBegin)
        => new CasaDrawTransaction(this, settings, deferBegin);

    public void RegisterView(IUIView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (!MutableViews.Contains(view))
        {
            MutableViews.Add(view);
        }
    }

    public bool UnregisterView(IUIView? view) => view != null && MutableViews.Remove(view);

    public void UpdateViews()
    {
        foreach (IUIView view in MutableViews)
        {
            view.Update();
        }
    }

    public void DrawViews(float opacity = 1.0f, DrawSettings? initialDrawSettings = null)
    {
        using var drawTransaction = new CasaDrawTransaction(this, initialDrawSettings ?? DrawSettings.Default, false);
        foreach (IUIView view in MutableViews)
        {
            view.Draw(drawTransaction, opacity);
        }
    }

    public SolidColorTexture GetOrCreateSolidColorTexture(Color color)
    {
        if (!_solidColorTextures.TryGetValue(color, out SolidColorTexture? result))
        {
            result = new SolidColorTexture(GraphicsDevice, color);
            _solidColorTextures.Add(color, result);
        }

        return result;
    }

    public Texture2D GetOrCreateWhiteCircleTexture(float desiredRadius, int? minimumRadius = null, int? maximumRadius = null)
    {
        maximumRadius = Math.Clamp(maximumRadius ?? GeneralUtils.NextPowerOf2(desiredRadius), MinimumCircleTextureRadius, MaximumCircleTextureRadius);
        minimumRadius = Math.Clamp(minimumRadius ?? (int)Math.Floor(desiredRadius), MinimumCircleTextureRadius, maximumRadius.Value);

        IEnumerable<KeyValuePair<int, Texture2D>> matches = _circleTextures
            .Where(entry => entry.Value != null && !entry.Value.IsDisposed && entry.Key >= minimumRadius && entry.Key <= maximumRadius)
            .OrderBy(entry => Math.Abs(desiredRadius - entry.Key));

        foreach (KeyValuePair<int, Texture2D> match in matches)
        {
            return match.Value;
        }

        desiredRadius = Math.Min(desiredRadius, maximumRadius.Value);
        int actualRadius = Math.Clamp(GeneralUtils.NextPowerOf2(desiredRadius), minimumRadius.Value, maximumRadius.Value);
        Texture2D circle = TextureUtils.CreateCircleTexture(SpriteBatch, actualRadius, Color.White, true);
        _circleTextures[actualRadius] = circle;
        return circle;
    }

    public void ClearDisposedCircleTextures()
    {
        List<int>? invalidKeys = null;
        foreach (KeyValuePair<int, Texture2D> entry in _circleTextures)
        {
            if (entry.Value == null || entry.Value.IsDisposed)
            {
                invalidKeys ??= new List<int>();
                invalidKeys.Add(entry.Key);
            }
        }

        if (invalidKeys == null)
        {
            return;
        }

        foreach (int key in invalidKeys)
        {
            _circleTextures.Remove(key);
        }
    }

    private static IRawInputSource ResolveInputSource(IRenderHost host, IRawInputSource? rawInputSource)
    {
        if (rawInputSource != null)
        {
            return rawInputSource;
        }

        if (host is IRawInputSource hostInputSource)
        {
            return hostInputSource;
        }

        throw new ArgumentNullException(nameof(rawInputSource),
            $"{nameof(CasaDesktopRuntime)} requires an explicit {nameof(IRawInputSource)} when the supplied {nameof(IRenderHost)} does not implement {nameof(IRawInputSource)}.");
    }
}