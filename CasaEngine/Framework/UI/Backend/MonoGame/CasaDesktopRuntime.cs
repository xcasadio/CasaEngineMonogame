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

    private CasaDrawTransaction _drawTransaction;
    private bool _isDrawTransactionOpen;

    /// <summary>Opens the draw transaction of this runtime. A runtime owns one <see cref="CasaDrawTransaction"/> for its whole life: the first
    /// call creates it, every later call puts the same object back in the state of a new transaction, so a frame allocates no transaction.
    /// Its <see cref="IDisposable.Dispose"/> ends the batch and closes it.<para/>
    /// One transaction is open at a time, and a transaction is not to be used after its <c>Dispose</c>: the next call hands the same object
    /// to the next draw. The shape renderer of <see cref="CasaMonoGameBackendOptions.CreateShapeRenderer"/> is created once per runtime, not
    /// once per frame.<para/>
    /// If the re-arm or the begin of the batch throws, the transaction stays closed and the next call succeeds.</summary>
    /// <exception cref="InvalidOperationException">The transaction of this runtime is already open: dispose it first.</exception>
    public IUIDrawTransaction BeginDrawTransaction(DrawSettings settings, bool deferBegin)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (_isDrawTransactionOpen)
        {
            throw new InvalidOperationException(
                $"The draw transaction of this {nameof(CasaDesktopRuntime)} is already open. A runtime has one transaction and one draw at a time: " +
                "dispose the transaction returned by the previous BeginDrawTransaction call (or let the draw that holds it finish) before asking for another.");
        }

        if (_drawTransaction == null)
        {
            //  Created with the begin deferred, then begun here: a begin that throws must not lose the object.
            _drawTransaction = new CasaDrawTransaction(this, settings, true);
            if (!deferBegin)
            {
                _drawTransaction.ForceBeginDraw(DrawContext.Sprites);
            }
        }
        else
        {
            _drawTransaction.Rearm(settings, deferBegin);
        }

        _isDrawTransactionOpen = true;
        return _drawTransaction;
    }

    /// <summary>Called by <see cref="CasaDrawTransaction.Dispose"/>, whether the end of its batch succeeded or threw.</summary>
    internal void CloseDrawTransaction(CasaDrawTransaction transaction)
    {
        if (ReferenceEquals(transaction, _drawTransaction))
        {
            _isDrawTransactionOpen = false;
        }
    }

    /// <summary>Required by <see cref="IUIDesktopRuntime"/>, called by <c>MGDesktop</c> with its view. The runtime keeps no list of views: nothing
    /// read it, and a list held by the runtime kept every registered view alive.</summary>
    public void RegisterView(IUIView view)
    {
        ArgumentNullException.ThrowIfNull(view);
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