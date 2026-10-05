using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.UI.Backend;

/// <summary>Minimal, non-visible <see cref="Game"/> used only to obtain a real, GPU-backed <see cref="GraphicsDevice"/>.<para/>
/// Ported from <c>MGUI.Tests/Integration/GpuDeviceHost.cs</c>. Constructing a <see cref="GraphicsDeviceManager"/> and calling
/// <see cref="Game.RunOneFrame"/> is enough to create the device without ever showing the window (MonoGame's SDL platform only
/// shows it from <c>RunLoop()</c>, which backs <see cref="Game.Run()"/>). The back buffer is <see cref="BackBufferWidth"/> x
/// <see cref="BackBufferHeight"/>.</summary>
internal sealed class HeadlessGame : Game, IObservableUpdate
{
    public const int BackBufferWidth = 256;
    public const int BackBufferHeight = 192;

    public GraphicsDeviceManager Gdm { get; }

    // IObservableUpdate: required by the UI render host, never raised: these GPU tests draw directly through a draw transaction.
    public event EventHandler<TimeSpan> PreviewUpdate;
    public event EventHandler<EventArgs> EndUpdate;

    public HeadlessGame()
    {
        Gdm = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = BackBufferWidth,
            PreferredBackBufferHeight = BackBufferHeight,
            PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8,
            SynchronizeWithVerticalRetrace = false,
        };
        Content.RootDirectory = "Content";
        IsFixedTimeStep = false;
    }
}

/// <summary>Owns the single dedicated thread that every real-GPU test of <see cref="CasaEngine.Tests"/> must use.<para/>
/// MonoGame records the thread that first touches it as its "UI thread" and <c>GraphicsDevice</c> draw calls throw on any other
/// thread; nothing pumps <c>Threading.Run()</c> in this host, so all MonoGame work (device creation, drawing, GetData, disposal)
/// goes through <see cref="Invoke(Action)"/> on one long-lived background thread, whichever thread xunit runs a test on.</summary>
internal sealed class GpuDeviceHost
{
    public static readonly GpuDeviceHost Instance = new();

    private readonly BlockingCollection<Action> _work = new();
    private readonly Thread _thread;
    private readonly Lazy<(bool Available, string Reason)> _probe;

    private HeadlessGame _game;

    private GpuDeviceHost()
    {
        _thread = new Thread(RunLoop) { IsBackground = true, Name = "CasaEngine-GPU-Test-Thread" };
        _thread.Start();
        _probe = new Lazy<(bool, string)>(Probe, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>True if a real <see cref="GraphicsDevice"/> could be created in this process. Probed once, lazily.</summary>
    public bool IsAvailable => _probe.Value.Available;

    /// <summary>Null when <see cref="IsAvailable"/>; otherwise a human-readable explanation, usable as an xunit <c>Skip</c> reason.</summary>
    public string UnavailableReason => _probe.Value.Reason;

    /// <summary>The real device backing every GPU test in this process. Only valid inside an <see cref="Invoke(Action)"/> callback.</summary>
    public GraphicsDevice GraphicsDevice => _game.GraphicsDevice;

    /// <summary>The game backing <see cref="GraphicsDevice"/>. Only valid inside an <see cref="Invoke(Action)"/> callback.</summary>
    public HeadlessGame Game => _game;

    private (bool, string) Probe()
    {
        try
        {
            Invoke(() =>
            {
                _game = new HeadlessGame();
                _game.RunOneFrame();
                if (_game.GraphicsDevice == null)
                {
                    throw new InvalidOperationException($"{nameof(HeadlessGame)}.{nameof(HeadlessGame.GraphicsDevice)} was still null after {nameof(Game.RunOneFrame)}().");
                }
            });
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"No real GraphicsDevice could be created in this process (no GPU/display?): {ex}");
        }
    }

    /// <summary>Runs <paramref name="action"/> on the dedicated GPU thread and blocks the caller until it completes,
    /// re-throwing any exception on the calling thread.</summary>
    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        ExceptionDispatchInfo capturedError = null;
        using ManualResetEventSlim done = new(false);

        _work.Add(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                capturedError = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                done.Set();
            }
        });

        done.Wait();
        capturedError?.Throw();
    }

    /// <summary>Runs <paramref name="func"/> on the dedicated GPU thread and returns its result on the caller.</summary>
    public T Invoke<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        T result = default;
        Invoke(() => { result = func(); });
        return result;
    }

    private void RunLoop()
    {
        foreach (Action action in _work.GetConsumingEnumerable())
        {
            action();
        }
    }
}

/// <summary>Like <see cref="FactAttribute"/>, but skips (rather than fails) when no real <see cref="GraphicsDevice"/> can be
/// created in this process, e.g. a headless CI agent with no GPU or display.</summary>
public sealed class GpuFactAttribute : FactAttribute
{
    public GpuFactAttribute()
    {
        if (!GpuDeviceHost.Instance.IsAvailable)
        {
            Skip = GpuDeviceHost.Instance.UnavailableReason;
        }
    }
}

/// <summary>All real-GPU tests share one device and one thread, so they must not run concurrently with each other.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GpuDeviceCollection
{
    public const string Name = "CasaEngine real-GPU rendering";
}
