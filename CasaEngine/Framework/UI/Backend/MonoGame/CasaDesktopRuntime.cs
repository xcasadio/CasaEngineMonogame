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
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Rendering.Shaders;
using CasaEngine.Framework.UI.Backend.MonoGame.Assets;

namespace CasaEngine.Framework.UI.Backend.MonoGame;

public sealed class CasaDesktopRuntime : IMonoGameDesktopBackend
{
    public CasaMonoGameBackendOptions Options { get; }
    public IRenderHost Host { get; }
    public IRawInputSource RawInputSource { get; }
    public IUISurface Surface { get; }
    private List<IUIView> MutableViews { get; } = new();
    public IReadOnlyList<IUIView> Views => MutableViews;

    public GraphicsDevice GraphicsDevice => Host.GraphicsDevice;
    public SpriteBatch SpriteBatch { get; }
    public PrimitiveBatch PrimitiveBatch { get; }
    /// <summary>Shared engine effect used by <see cref="CasaDrawTransaction.DrawTexturedTriangleList"/> (Content/Shaders/TexturedPrimitive.fx):
    /// unlit, textured, vertex-coloured, driven by a single pre-combined WorldViewProj. Owned like <see cref="PrimitiveBatch"/> (one per runtime).<para/>
    /// Loaded from this runtime's own <see cref="Content"/>, so it is not shared with any other consumer and needs no clone.</summary>
    internal Effect TexturedPrimitiveEffect { get; }
    internal CasaRenderTargetPool RenderTargetPool => Services.RenderTargetPool;
    internal CasaBackendAdapterRegistry AdapterRegistry => Services.AdapterRegistry;

    private CasaRuntimeBackendServices Services { get; }

    public ContentManager Content => Services.Content;

    public FontManager FontManager => Services.FontManager;
    public string DefaultFontFamily => FontManager.DefaultFontFamily;
    public IUIAssetProvider AssetProvider => Services.AssetProvider;

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

    public event EventHandler<EventArgs<ITextMeasurementEngine>> TextEngineChanged;
    public event EventHandler<EventArgs> EndUpdate;

    public InputTracker Input { get; }
    public UpdateBaseArgs UpdateArgs { get; private set; }

    private TimeSpan _previousUpdateTimeSpan = TimeSpan.Zero;

    public readonly Texture2D ScrollMarker;

    public CasaDesktopRuntime(
        IRenderHost host,
        IRawInputSource rawInputSource = null,
        IUISurface surface = null,
        IUIAssetProvider assetProvider = null,
        CasaMonoGameBackendOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(host);

        Options = options ?? CasaMonoGameBackendOptions.Default;
        Host = host;
        RawInputSource = ResolveInputSource(host, rawInputSource);
        Surface = surface ?? new CasaBackBufferSurface(host);
        SpriteBatch = new SpriteBatch(GraphicsDevice);
        PrimitiveBatch = new PrimitiveBatch(GraphicsDevice, 1024);
        Services = new CasaRuntimeBackendServices(host, GraphicsDevice, SpriteBatch, assetProvider, Options);
        TextEngine = Options.TextEngine ?? new SpriteFontTextEngine(FontManager);
        Input = new InputTracker();

        ScrollMarker = Content.Load<Texture2D>(Path.Combine("Icons", "ScrollMarker"));
        TexturedPrimitiveEffect = Content.Load<Effect>(BuiltInShaderCatalog.TexturedPrimitiveContentName);

        Host.PreviewUpdate += (_, elapsed) =>
        {
            UpdateArgs = new UpdateBaseArgs(
                elapsed,
                elapsed.Subtract(_previousUpdateTimeSpan),
                RawInputSource.GetMouseState(),
                RawInputSource.GetKeyboardState());
            _previousUpdateTimeSpan = elapsed;
            if (RawInputSource is IWindowTextInputSource textInputSource)
            {
                textInputSource.DrainTextInput(Input.Keyboard);
            }

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

    public bool UnregisterView(IUIView view) => view != null && MutableViews.Remove(view);

    public void UpdateViews()
    {
        foreach (IUIView view in MutableViews)
        {
            view.Update();
        }
    }

    public void DrawViews(float opacity = 1.0f, DrawSettings initialDrawSettings = null)
    {
        using var drawTransaction = new CasaDrawTransaction(this, initialDrawSettings ?? DrawSettings.Default, false);
        foreach (IUIView view in MutableViews)
        {
            view.Draw(drawTransaction, opacity);
        }
    }

    public SolidColorTexture GetOrCreateSolidColorTexture(Color color)
        => Services.TextureCache.GetOrCreateSolidColorTexture(color);

    public Texture2D GetOrCreateWhiteCircleTexture(float desiredRadius, int? minimumRadius = null, int? maximumRadius = null)
        => Services.TextureCache.GetOrCreateWhiteCircleTexture(desiredRadius, minimumRadius, maximumRadius);

    public void ClearDisposedCircleTextures() => Services.TextureCache.ClearDisposedCircleTextures();

    private static IRawInputSource ResolveInputSource(IRenderHost host, IRawInputSource rawInputSource)
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