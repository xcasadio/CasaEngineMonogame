using CasaEngine.Framework.UI.Backend.MonoGame;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Triangulation;
using Xunit;
using Xunit.Abstractions;
using WindingOrder = MonoGame.Extended.Triangulation.WindingOrder;

namespace CasaEngine.Tests.UI.Backend;

/// <summary>
/// T0.1 of the plan "end of the zero allocation of the drawing", for T1.1, on the real GPU, in the engine backend (<c>CasaLegacyShapeRenderer2D</c>): the bytes that one call of <see cref="CasaDrawTransaction.StrokePolygon"/> and of
/// <see cref="CasaDrawTransaction.StrokeAndFillPolygon"/> allocates, for three shapes (a triangle, a seven-vertex arrow and a 32-segment ellipse, the shapes of <c>FillPolygonGpuTests</c>), with the
/// lines centred on the vertices or inside them, with the winding given or computed, in the primitives context and in the sprites context. Each configuration runs
/// <see cref="WarmupIterations"/> calls, then <see cref="MeasuredIterations"/> are measured; the number asserted is the total of the measured calls.<para/>
/// The contour that goes through the sprites context with the lines inside the vertices ends in MonoGame.Extended's <c>SpriteBatch.DrawPolygon</c>: that call is measured alone too, on a polygon that is
/// already counter-clockwise (what <c>StrokePolygon</c> hands to it), so that its own share is known and what the rest of the method allocates is the difference. <c>StrokeAndFillPolygon</c> has no
/// context to choose: it fills in the primitives context and strokes in the primitives context, whatever the context that is current.<para/>
/// Each configuration proves that it draws: one call into a cleared target must change at least one pixel (a call that silently draws nothing, because it took another branch, would fail), and the
/// context that the call ends in is the one the configuration names (<see cref="CasaDrawTransaction"/> begins the batch of the context the call prefers).
/// The numbers are the figures before T1.1 (it replaces them by 0, or by the share of <c>DrawPolygon</c> that it cannot remove). All GPU work goes through <see cref="GpuDeviceHost"/>.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class StrokePolygonGpuTests
{
    private const int TargetSize = 96;
    private const int WarmupIterations = 500;
    private const int MeasuredIterations = 500;
    private const float Thickness = 2f;
    private static readonly Vector2 Origin = new(5, 3);
    private static readonly Color StrokeColor = Color.Lime;
    private static readonly Color FillColor = Color.Red;

    private readonly ITestOutputHelper _output;

    public StrokePolygonGpuTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static Vector2[] Triangle() => new Vector2[] { new(10, 10), new(70, 20), new(30, 70) };
    private static Vector2[] Arrow() => new Vector2[] { new(5, 30), new(45, 30), new(45, 15), new(80, 40), new(45, 65), new(45, 50), new(5, 50) };

    /// <summary>32 segments, as <c>MGEllipse</c> draws by default.</summary>
    private static Vector2[] Ellipse()
    {
        Vector2[] v = new Vector2[32];
        for (int i = 0; i < v.Length; i++)
        {
            double angle = i * Math.PI * 2 / v.Length;
            v[i] = new Vector2(40 + (float)(34 * Math.Cos(angle)), 38 + (float)(24 * Math.Sin(angle)));
        }

        return v;
    }

    private static IEnumerable<(string Name, Vector2[] Vertices)> Shapes()
    {
        yield return ("triangle", Triangle());
        yield return ("arrow of 7 vertices", Arrow());
        yield return ("ellipse of 32 segments", Ellipse());
    }

    /// <summary>The winding that <c>StrokePolygon</c> computes when it is not given one: the one of the closed polygon.</summary>
    private static WindingOrder WindingOf(Vector2[] vertices)
    {
        Vector2[] closed = new Vector2[vertices.Length + 1];
        vertices.CopyTo(closed, 0);
        closed[^1] = vertices[0];
        return Triangulator.DetermineWindingOrder(closed);
    }

    private sealed class TestRenderHost : IRenderHost, IRawInputSource
    {
        private readonly HeadlessGame _game;

        public TestRenderHost(HeadlessGame game)
        {
            _game = game;
        }

        public GraphicsDevice GraphicsDevice => _game.GraphicsDevice;
        public Rectangle GetBounds() => new(0, 0, TargetSize, TargetSize);
        public object GetService(Type serviceType) => _game.Services.GetService(serviceType);
        public MouseState GetMouseState() => default;
        public KeyboardState GetKeyboardState() => default;

        public event EventHandler<TimeSpan> PreviewUpdate;
        public event EventHandler<EventArgs> EndUpdate;
    }

    private static CasaDesktopRuntime CreateRenderer()
        => CasaMonoGameBackendBootstrap.Create(new TestRenderHost(GpuDeviceHost.Instance.Game)).Runtime;

    private static CasaDrawTransaction BeginTransaction(CasaDesktopRuntime runtime)
        => (CasaDrawTransaction)runtime.BeginDrawTransaction(DrawSettings.Default, deferBegin: true);

    /// <summary>Clears a target, opens a transaction, runs the drawing, ends the batch and reads the pixels back.</summary>
    private static Color[] Render(CasaDesktopRuntime renderer, Action<CasaDrawTransaction> draw)
    {
        GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
        using RenderTarget2D target = new(device, TargetSize, TargetSize, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
        device.SetRenderTarget(target);
        try
        {
            device.Clear(ClearOptions.Target | ClearOptions.Stencil | ClearOptions.DepthBuffer, Color.Black, 1.0f, 0);
            using (CasaDrawTransaction dt = BeginTransaction(renderer))
            {
                dt.ForceBeginDraw(DrawContext.Primitives);
                draw(dt);
            }
        }
        finally
        {
            device.SetRenderTarget(null);
        }

        Color[] pixels = new Color[TargetSize * TargetSize];
        target.GetData(pixels);
        return pixels;
    }

    private static bool DrawsSomething(CasaDesktopRuntime renderer, Action<CasaDrawTransaction> draw)
    {
        foreach (Color pixel in Render(renderer, draw))
        {
            if (pixel != Color.Black)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Total bytes allocated by <see cref="MeasuredIterations"/> calls after <see cref="WarmupIterations"/> of warm-up. In the sprites context the batch is ended and begun again between
    /// the two (<paramref name="resetBatch"/>): the sprites of the warm-up are drawn, so that the arrays of the batcher have already grown to what the measured calls need and do not grow in the window.</summary>
    private static long MeasureTotal(Action action, Action resetBatch = null)
    {
        for (int i = 0; i < WarmupIterations; i++)
        {
            action();
        }

        resetBatch?.Invoke();
        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            action();
        }

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private sealed record Config(string Key, Action<CasaDrawTransaction> Call, DrawContext Context);

    private static IEnumerable<Config> Configs(CasaDesktopRuntime renderer)
    {
        foreach ((string shapeName, Vector2[] vertices) in Shapes())
        {
            WindingOrder winding = WindingOf(vertices);
            foreach (bool centred in new[] { true, false })
            {
                foreach (bool given in new[] { false, true })
                {
                    WindingOrder? order = given ? winding : null;
                    string options = $"{shapeName}, lines {(centred ? "centred on the vertices" : "inside the vertices")}, winding {(given ? "given" : "computed")}";
                    foreach (DrawContext context in new[] { DrawContext.Primitives, DrawContext.Sprites })
                    {
                        yield return new Config($"StrokePolygon, {options}, {context}", dt => dt.StrokePolygon(Origin, vertices, StrokeColor, Thickness, centred, order, context), context);
                    }

                    yield return new Config($"StrokeAndFillPolygon, {options}, Primitives", dt => dt.StrokeAndFillPolygon(Origin, vertices, StrokeColor, FillColor, Thickness, centred, order), DrawContext.Primitives);
                }
            }

            //  SpriteBatch.DrawPolygon alone, on the counter-clockwise polygon that StrokePolygon hands to it.
            Vector2[] counterClockwise = Triangulator.EnsureWindingOrder((Vector2[])vertices.Clone(), WindingOrder.CounterClockwise);
            yield return new Config($"SpriteBatch.DrawPolygon alone, {shapeName}", dt => renderer.SpriteBatch.DrawPolygon(Origin, counterClockwise, StrokeColor, Thickness, 0.0f), DrawContext.Sprites);
        }
    }

    /// <summary>The totals over <see cref="MeasuredIterations"/> calls, as measured before T1.1. The key of a configuration is the text of the case.</summary>
    private static readonly Dictionary<string, long> Expected = new()
    {
        ["StrokePolygon, triangle, lines centred on the vertices, winding computed, Primitives"] = 140000L,
        ["StrokePolygon, triangle, lines centred on the vertices, winding computed, Sprites"] = 140000L,
        ["StrokeAndFillPolygon, triangle, lines centred on the vertices, winding computed, Primitives"] = 140000L,
        ["StrokePolygon, triangle, lines centred on the vertices, winding given, Primitives"] = 140000L,
        ["StrokePolygon, triangle, lines centred on the vertices, winding given, Sprites"] = 140000L,
        ["StrokeAndFillPolygon, triangle, lines centred on the vertices, winding given, Primitives"] = 140000L,
        ["StrokePolygon, triangle, lines inside the vertices, winding computed, Primitives"] = 200000L,
        ["StrokePolygon, triangle, lines inside the vertices, winding computed, Sprites"] = 84000L,
        ["StrokeAndFillPolygon, triangle, lines inside the vertices, winding computed, Primitives"] = 200000L,
        ["StrokePolygon, triangle, lines inside the vertices, winding given, Primitives"] = 140000L,
        ["StrokePolygon, triangle, lines inside the vertices, winding given, Sprites"] = 24000L,
        ["StrokeAndFillPolygon, triangle, lines inside the vertices, winding given, Primitives"] = 140000L,
        ["SpriteBatch.DrawPolygon alone, triangle"] = 0L,
        ["StrokePolygon, arrow of 7 vertices, lines centred on the vertices, winding computed, Primitives"] = 140000L,
        ["StrokePolygon, arrow of 7 vertices, lines centred on the vertices, winding computed, Sprites"] = 140000L,
        ["StrokeAndFillPolygon, arrow of 7 vertices, lines centred on the vertices, winding computed, Primitives"] = 140000L,
        ["StrokePolygon, arrow of 7 vertices, lines centred on the vertices, winding given, Primitives"] = 140000L,
        ["StrokePolygon, arrow of 7 vertices, lines centred on the vertices, winding given, Sprites"] = 140000L,
        ["StrokeAndFillPolygon, arrow of 7 vertices, lines centred on the vertices, winding given, Primitives"] = 140000L,
        ["StrokePolygon, arrow of 7 vertices, lines inside the vertices, winding computed, Primitives"] = 216000L,
        ["StrokePolygon, arrow of 7 vertices, lines inside the vertices, winding computed, Sprites"] = 116000L,
        ["StrokeAndFillPolygon, arrow of 7 vertices, lines inside the vertices, winding computed, Primitives"] = 216000L,
        ["StrokePolygon, arrow of 7 vertices, lines inside the vertices, winding given, Primitives"] = 140000L,
        ["StrokePolygon, arrow of 7 vertices, lines inside the vertices, winding given, Sprites"] = 40000L,
        ["StrokeAndFillPolygon, arrow of 7 vertices, lines inside the vertices, winding given, Primitives"] = 140000L,
        ["SpriteBatch.DrawPolygon alone, arrow of 7 vertices"] = 0L,
        ["StrokePolygon, ellipse of 32 segments, lines centred on the vertices, winding computed, Primitives"] = 140000L,
        ["StrokePolygon, ellipse of 32 segments, lines centred on the vertices, winding computed, Sprites"] = 140000L,
        ["StrokeAndFillPolygon, ellipse of 32 segments, lines centred on the vertices, winding computed, Primitives"] = 140000L,
        ["StrokePolygon, ellipse of 32 segments, lines centred on the vertices, winding given, Primitives"] = 140000L,
        ["StrokePolygon, ellipse of 32 segments, lines centred on the vertices, winding given, Sprites"] = 140000L,
        ["StrokeAndFillPolygon, ellipse of 32 segments, lines centred on the vertices, winding given, Primitives"] = 140000L,
        ["StrokePolygon, ellipse of 32 segments, lines inside the vertices, winding computed, Primitives"] = 316000L,
        ["StrokePolygon, ellipse of 32 segments, lines inside the vertices, winding computed, Sprites"] = 316000L,
        ["StrokeAndFillPolygon, ellipse of 32 segments, lines inside the vertices, winding computed, Primitives"] = 316000L,
        ["StrokePolygon, ellipse of 32 segments, lines inside the vertices, winding given, Primitives"] = 140000L,
        ["StrokePolygon, ellipse of 32 segments, lines inside the vertices, winding given, Sprites"] = 140000L,
        ["StrokeAndFillPolygon, ellipse of 32 segments, lines inside the vertices, winding given, Primitives"] = 140000L,
        ["SpriteBatch.DrawPolygon alone, ellipse of 32 segments"] = 0L,
    };

    [GpuFact]
    public void StrokePolygon_AndStrokeAndFillPolygon_AllocateTheMeasuredRemainder()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            CasaDesktopRuntime renderer = CreateRenderer();
            List<string> failures = new();
            List<Config> configs = new(Configs(renderer));

            //  The path: every configuration draws, in the context it names.
            foreach (Config config in configs)
            {
                if (!DrawsSomething(renderer, dt => { dt.ForceBeginDraw(config.Context); config.Call(dt); }))
                {
                    failures.Add($"{config.Key}: draws nothing");
                }
            }

            using RenderTarget2D target = new(device, TargetSize, TargetSize, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
            device.SetRenderTarget(target);
            try
            {
                device.Clear(ClearOptions.Target | ClearOptions.Stencil | ClearOptions.DepthBuffer, Color.Black, 1.0f, 0);
                using CasaDrawTransaction dt = BeginTransaction(renderer);
                foreach (Config config in configs)
                {
                    dt.ForceBeginDraw(config.Context);
                    long total = MeasureTotal(() => config.Call(dt), config.Context == DrawContext.Sprites
                        ? () => { dt.ForceEndDraw(DrawContext.Sprites); dt.ForceBeginDraw(DrawContext.Sprites); }
                        : null);
                    _output.WriteLine($"{config.Key}: {(total + MeasuredIterations - 1) / MeasuredIterations} B/call ({total} B over {MeasuredIterations} calls)");
                    if (!Expected.TryGetValue(config.Key, out long expected) || expected != total)
                    {
                        failures.Add($"{config.Key}: {total} B over {MeasuredIterations} calls, expected {(Expected.TryGetValue(config.Key, out long e) ? e.ToString() : "nothing")}");
                    }
                }
            }
            finally
            {
                device.SetRenderTarget(null);
            }

            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        });
    }
}
