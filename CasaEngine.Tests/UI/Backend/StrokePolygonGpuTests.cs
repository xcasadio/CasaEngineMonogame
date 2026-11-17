using CasaEngine.Framework.UI.Backend.MonoGame;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using MGUI.Tests.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Triangulation;
using MonoGame.Extended.VectorDraw;
using System.Security.Cryptography;
using Xunit;
using Xunit.Abstractions;
using WindingOrder = MonoGame.Extended.Triangulation.WindingOrder;

namespace CasaEngine.Tests.UI.Backend;

/// <summary>
/// T1.1 of the plan "end of the zero allocation of the drawing", on the real GPU, in the engine backend (<c>CasaLegacyShapeRenderer2D</c>): <see cref="CasaDrawTransaction.StrokePolygon"/> and <see cref="CasaDrawTransaction.StrokeAndFillPolygon"/> allocate
/// nothing when the vertices come as an array or as a list, for three shapes (a triangle, a seven-vertex arrow and a 32-segment ellipse, the shapes of <see cref="FillPolygonGpuTests"/>, each in both windings), with the
/// lines centred on the vertices or inside them, with the winding given or computed, in the primitives context and in the sprites context. Each configuration runs <see cref="WarmupIterations"/> calls, then
/// <see cref="MeasuredIterations"/> are measured; the number asserted is the total of the measured calls (T0.1 measured 84000 to 316000 bytes over the 500 calls of the rows that allocated, 140000 in the centred rows).<para/>
/// The contour that goes through the sprites context with the lines inside the vertices ends in MonoGame.Extended's <c>SpriteBatch.DrawPolygon</c>, which allocates nothing (measured alone too) and neither
/// modifies nor keeps the list it receives (proved below, so that the polygon of the caller, or a view of it, can be handed to it). <c>StrokeAndFillPolygon</c> has no context to choose: it fills in the primitives
/// context and strokes in the primitives context, whatever the context that is current. <c>CasaDrawTransaction.StrokeAndFillPolygon</c> hands the winding it receives to
/// <c>StrokePolygon</c> (the MGUI backend always passes <c>null</c>).<para/>
/// What the contour draws is unchanged: <see cref="OldStrokePolygon"/> is a literal copy of the lines of <c>StrokePolygon</c> before T1.1 (the iterator <c>SelectConsecutivePairs(true)</c>, the closed array
/// <c>Vertices.Append(Vertices[0]).ToArray()</c>, the copy <c>Vertices.ToArray()</c>), and the vertices that the <see cref="PrimitiveBatch"/> receives from the new code are compared with it bit for bit, on the whole
/// corpus of <see cref="PolygonCorpus"/> (both windings, the winding given or computed, three thicknesses, a fractional origin); the same corpus is also frozen as a fingerprint of the vertex stream per variant,
/// captured on the product code of before T1.1 (<see cref="FrozenStreams"/>), so that the reference itself is held to what the old product wrote. The pixels are compared in the test, on the real GPU, in both contexts.
/// Each allocation configuration proves that it draws: one call into a cleared target must change at least one pixel. All GPU work goes through <see cref="GpuDeviceHost"/>.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class StrokePolygonGpuTests
{
    private const int TargetSize = 96;
    private const int WarmupIterations = 500;
    private const int MeasuredIterations = 500;
    private const float Thickness = 2f;
    private static readonly Vector2 Origin = new(5, 3);
    private static readonly Vector2 FractionalOrigin = new(5.3f, 3.7f);
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

    /// <summary>The three shapes, each in both windings: the contour in the sprites context reverses the polygon that is not counter-clockwise.</summary>
    private static IEnumerable<(string Name, Vector2[] Vertices)> Shapes()
    {
        yield return ("triangle", Triangle());
        yield return ("triangle (other winding)", PolygonCorpus.Reversed(Triangle()));
        yield return ("arrow of 7 vertices", Arrow());
        yield return ("arrow of 7 vertices (other winding)", PolygonCorpus.Reversed(Arrow()));
        yield return ("ellipse of 32 segments", Ellipse());
        yield return ("ellipse of 32 segments (other winding)", PolygonCorpus.Reversed(Ellipse()));
    }

    /// <summary>An enumeration that is neither an array nor a list: the only path that copies.</summary>
    private static IEnumerable<Vector2> Enumerate(Vector2[] vertices)
    {
        foreach (Vector2 vertex in vertices)
        {
            yield return vertex;
        }
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

    private static Color[] Cleared()
    {
        Color[] cleared = new Color[TargetSize * TargetSize];
        Array.Fill(cleared, Color.Black);
        return cleared;
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

    private static void AssertSamePixels(Color[] expected, Color[] actual, string message)
    {
        int first = -1;
        for (int i = 0; i < expected.Length; i++)
        {
            if (expected[i] != actual[i])
            {
                first = i;
                break;
            }
        }

        Assert.True(first < 0, $"{message}: first differing pixel at ({first % TargetSize}, {first / TargetSize}).");
    }

    //  --------------------------------------------------------------------------------------------------------------------------------------
    //  The reference: the contour as it was drawn before T1.1
    //  --------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The body of <c>CasaDrawTransaction.FillTrianglePrimitiveCore</c> (its checks left out): what the old contour handed to the batch.</summary>
    private static void OldFillTrianglePrimitive(CasaDesktopRuntime renderer, Vector2 Origin, Vector2 v0, Vector2 v1, Vector2 v2, Color Color)
    {
        renderer.PrimitiveBatch.AddVertex(v0 + Origin, Color, PrimitiveType.TriangleList);
        renderer.PrimitiveBatch.AddVertex(v1 + Origin, Color, PrimitiveType.TriangleList);
        renderer.PrimitiveBatch.AddVertex(v2 + Origin, Color, PrimitiveType.TriangleList);
    }

    /// <summary>A literal copy of <c>CasaLegacyShapeRenderer2D.StrokePolygon</c> as it was before T1.1 (the lines are the old ones; <c>StrokeLineSegment</c>, which is unchanged, is the product's).</summary>
    private static void OldStrokePolygon(CasaDesktopRuntime renderer, CasaDrawTransaction dt, Vector2 Origin, IReadOnlyList<Vector2> Vertices, Color Color, float Thickness, bool CenterLinesOnVertices, WindingOrder? Order, DrawContext Ctx)
    {
        dt.ForceBeginDraw(Ctx);

        if (CenterLinesOnVertices)
        {
            var Edges = Vertices.SelectConsecutivePairs(true);
            foreach ((Vector2 Start, Vector2 End) in Edges)
            {
                dt.StrokeLineSegment(Origin, Start, End, Color, Thickness, Ctx);
            }
        }
        else
        {
            Order = Order ?? Triangulator.DetermineWindingOrder(Vertices.Append(Vertices[0]).ToArray());

            switch (Ctx)
            {
                case DrawContext.Sprites:
                    renderer.SpriteBatch.DrawPolygon(Origin, Triangulator.EnsureWindingOrder(Vertices.ToArray(), WindingOrder.CounterClockwise), Color, Thickness, 0.0f);
                    break;
                case DrawContext.Primitives:
                    var Edges = Vertices.SelectConsecutivePairs(true);
                    switch (Order.Value)
                    {
                        case WindingOrder.Clockwise:
                            foreach ((Vector2 Start, Vector2 End) in Edges)
                            {
                                Vector2 Offset = (End - Start).RightNormal().NormalizedCopy().Scale(Thickness);
                                OldFillTrianglePrimitive(renderer, Origin, Start, End, End + Offset, Color);
                                OldFillTrianglePrimitive(renderer, Origin, End + Offset, Start + Offset, Start, Color);
                            }
                            break;
                        case WindingOrder.CounterClockwise:
                            foreach ((Vector2 Start, Vector2 End) in Edges)
                            {
                                Vector2 Offset = (End - Start).LeftNormal().NormalizedCopy().Scale(Thickness);
                                OldFillTrianglePrimitive(renderer, Origin, Start, End, End + Offset, Color);
                                OldFillTrianglePrimitive(renderer, Origin, End + Offset, Start + Offset, Start, Color);
                            }
                            break;
                        default: throw new NotImplementedException($"Unrecognized {nameof(WindingOrder)}: {Order}");
                    }
                    break;
                default: throw new NotImplementedException($"Unrecognized {nameof(DrawContext)}: {Ctx}");
            }
        }
    }

    /// <summary>The old <c>StrokeAndFillPolygon</c>: the fill (unchanged) then the old contour in the primitives context, with the winding received (the engine backend passes it on).</summary>
    private static void OldStrokeAndFillPolygon(CasaDesktopRuntime renderer, CasaDrawTransaction dt, Vector2 Origin, IReadOnlyList<Vector2> Vertices, Color StrokeColor, Color FillColor, float StrokeThickness, bool CenterLinesOnVertices, WindingOrder? Order)
    {
        if (!CenterLinesOnVertices && StrokeColor == FillColor)
        {
            dt.FillPolygon(Origin, Vertices, StrokeColor);
        }
        else
        {
            dt.FillPolygon(Origin, Vertices, FillColor);
            OldStrokePolygon(renderer, dt, Origin, Vertices, StrokeColor, StrokeThickness, CenterLinesOnVertices, Order, DrawContext.Primitives);
        }
    }

    //  --------------------------------------------------------------------------------------------------------------------------------------
    //  Allocation
    //  --------------------------------------------------------------------------------------------------------------------------------------

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
        foreach ((string shapeName, Vector2[] array) in Shapes())
        {
            WindingOrder winding = WindingOf(array);
            List<Vector2> list = new(array);
            foreach ((string inputName, IReadOnlyList<Vector2> vertices) in new (string, IReadOnlyList<Vector2>)[] { ("array", array), ("list", list) })
            {
                foreach (bool centred in new[] { true, false })
                {
                    foreach (bool given in new[] { false, true })
                    {
                        WindingOrder? order = given ? winding : null;
                        string options = $"{shapeName}, {inputName}, lines {(centred ? "centred on the vertices" : "inside the vertices")}, winding {(given ? "given" : "computed")}";
                        foreach (DrawContext context in new[] { DrawContext.Primitives, DrawContext.Sprites })
                        {
                            yield return new Config($"StrokePolygon, {options}, {context}", dt => dt.StrokePolygon(Origin, vertices, StrokeColor, Thickness, centred, order, context), context);
                        }

                        yield return new Config($"StrokeAndFillPolygon, {options}, Primitives", dt => dt.StrokeAndFillPolygon(Origin, vertices, StrokeColor, FillColor, Thickness, centred, order), DrawContext.Primitives);
                    }
                }
            }

            //  SpriteBatch.DrawPolygon alone, on the counter-clockwise polygon that StrokePolygon hands to it.
            Vector2[] counterClockwise = Triangulator.EnsureWindingOrder((Vector2[])array.Clone(), WindingOrder.CounterClockwise);
            yield return new Config($"SpriteBatch.DrawPolygon alone, {shapeName}", dt => renderer.SpriteBatch.DrawPolygon(Origin, counterClockwise, StrokeColor, Thickness, 0.0f), DrawContext.Sprites);
        }
    }

    /// <summary>Every configuration allocates nothing over <see cref="MeasuredIterations"/> calls (before T1.1: 84000 to 316000 bytes in the rows that allocated, 140000 in the centred rows).</summary>
    [GpuFact]
    public void StrokePolygon_AndStrokeAndFillPolygon_AllocateNothing_FromAnArrayOrAList()
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
                    if (total != 0)
                    {
                        failures.Add($"{config.Key}: {total} B over {MeasuredIterations} calls, expected 0");
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

    /// <summary>An enumeration that is not a list keeps its copy (<c>ToList()</c> in the explicit <see cref="IUIDrawContext"/> member): rare, and the pixels are those of the array.</summary>
    [GpuFact]
    public void StrokeAndFillPolygon_FromAnEnumeration_DrawsThePixelsOfTheArray()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            CasaDesktopRuntime renderer = CreateRenderer();
            foreach ((string name, Vector2[] vertices) in Shapes())
            {
                Color[] expected = Render(renderer, dt => dt.StrokeAndFillPolygon(Origin, vertices, StrokeColor, FillColor, Thickness));
                Assert.NotEqual(Cleared(), expected);
                AssertSamePixels(expected, Render(renderer, dt => ((IUIDrawContext)dt).StrokeAndFillPolygon(Origin, Enumerate(vertices), StrokeColor, FillColor, Thickness)), $"{name}: enumeration");
                AssertSamePixels(expected, Render(renderer, dt => ((IUIDrawContext)dt).StrokeAndFillPolygon(Origin, new List<Vector2>(vertices), StrokeColor, FillColor, Thickness)), $"{name}: list");
            }
        });
    }

    [GpuFact]
    public void StrokePolygon_AndStrokeAndFillPolygon_KeepTheirExceptions_AndTheirEarlyReturn()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            CasaDesktopRuntime renderer = CreateRenderer();
            Render(renderer, dt =>
            {
                Assert.Throws<ArgumentException>(() => dt.StrokePolygon(Origin, null, StrokeColor, Thickness));
                Assert.Throws<ArgumentException>(() => dt.StrokePolygon(Origin, Array.Empty<Vector2>(), StrokeColor, Thickness));
                Assert.Throws<ArgumentException>(() => dt.StrokePolygon(Origin, new List<Vector2>(), StrokeColor, Thickness, false));
                Assert.Throws<ArgumentException>(() => dt.StrokeAndFillPolygon(Origin, null, StrokeColor, FillColor, Thickness));
                Assert.Throws<ArgumentException>(() => dt.StrokeAndFillPolygon(Origin, Array.Empty<Vector2>(), StrokeColor, FillColor, Thickness));
                Assert.Throws<ArgumentException>(() => ((IUIDrawContext)dt).StrokeAndFillPolygon(Origin, Enumerate(Array.Empty<Vector2>()), StrokeColor, FillColor, Thickness));

                //  A thickness that is not positive, or almost zero, returns before the vertices are looked at.
                dt.StrokePolygon(Origin, null, StrokeColor, 0f);
                dt.StrokePolygon(Origin, Array.Empty<Vector2>(), StrokeColor, -1f);
                dt.StrokePolygon(Origin, null, StrokeColor, 1e-12f);
            });
        });
    }

    /// <summary><c>SpriteBatch.DrawPolygon</c> of MonoGame.Extended 6.1.1 (read in the decompiled DLL: it reads <c>Count</c> and the indexer of the list, and hands the values to <c>SpriteBatch.Draw</c>) neither modifies
    /// the list nor keeps it: the polygon of the caller, or a view of it, can be handed to it. The contour does not keep the polygon either: one array overwritten between two contours in the same batch draws what
    /// two arrays draw.</summary>
    [GpuFact]
    public void DrawPolygon_NeitherModifiesItsList_NorKeepsIt_AndTheContourDoesNotKeepThePolygon()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            CasaDesktopRuntime renderer = CreateRenderer();
            foreach ((string name, Vector2[] vertices) in Shapes())
            {
                Vector2[] before = (Vector2[])vertices.Clone();
                Render(renderer, dt =>
                {
                    dt.ForceBeginDraw(DrawContext.Sprites);
                    renderer.SpriteBatch.DrawPolygon(Origin, vertices, StrokeColor, Thickness, 0.0f);
                    dt.StrokePolygon(Origin, vertices, StrokeColor, Thickness, false, null, DrawContext.Sprites);
                });
                Assert.Equal(before, vertices);

                Vector2[] first = (Vector2[])vertices.Clone();
                Vector2[] shifted = new Vector2[first.Length];
                for (int i = 0; i < first.Length; i++)
                {
                    shifted[i] = first[i] + new Vector2(20, 5);
                }

                foreach (bool centred in new[] { true, false })
                {
                    Color[] expected = Render(renderer, dt =>
                    {
                        dt.StrokePolygon(Origin, first, StrokeColor, Thickness, centred, null, DrawContext.Sprites);
                        dt.StrokePolygon(Origin, shifted, StrokeColor, Thickness, centred, null, DrawContext.Sprites);
                    });
                    Vector2[] shared = new Vector2[first.Length];
                    Color[] actual = Render(renderer, dt =>
                    {
                        first.CopyTo(shared, 0);
                        dt.StrokePolygon(Origin, shared, StrokeColor, Thickness, centred, null, DrawContext.Sprites);
                        shifted.CopyTo(shared, 0);
                        dt.StrokePolygon(Origin, shared, StrokeColor, Thickness, centred, null, DrawContext.Sprites);
                        Array.Clear(shared);
                    });

                    Assert.NotEqual(Cleared(), expected);
                    AssertSamePixels(expected, actual, $"{name}, {(centred ? "centred" : "inside")}: one array overwritten between two contours");
                }
            }
        });
    }

    //  --------------------------------------------------------------------------------------------------------------------------------------
    //  Pixels
    //  --------------------------------------------------------------------------------------------------------------------------------------

    private static List<(string Name, Vector2[] Vertices)> PixelCases()
    {
        List<(string, Vector2[])> cases = new(Shapes());
        cases.Add(("one vertex", new Vector2[] { new(20, 20) }));
        cases.Add(("two vertices", new Vector2[] { new(10, 10), new(70, 50) }));
        cases.Add(("three aligned vertices", new Vector2[] { new(10, 10), new(40, 10), new(70, 10) }));
        cases.Add(("two vertices at the same point", new Vector2[] { new(10, 10), new(10, 10) }));
        List<PolygonCorpus.Case> corpus = PolygonCorpus.BuildCorpus();
        for (int i = 0; i < corpus.Count; i += 500)
        {
            cases.Add(($"corpus #{i} {corpus[i].Category} / {corpus[i].Name}", corpus[i].Vertices));
        }

        return cases;
    }

    /// <summary>The pixels of the new contour are those of the old one, in both contexts, centred and inside, with the winding computed or given (either one, the right one or not), from an array and from a list.</summary>
    [GpuFact]
    public void StrokePolygon_AndStrokeAndFillPolygon_DrawTheSamePixels_AsTheOldContour()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            CasaDesktopRuntime renderer = CreateRenderer();
            List<(string Name, Vector2[] Vertices)> cases = PixelCases();
            List<string> failures = new();
            int compared = 0;
            int drawn = 0;
            foreach ((string name, Vector2[] vertices) in cases)
            {
                List<Vector2> list = new(vertices);
                foreach (DrawContext context in new[] { DrawContext.Primitives, DrawContext.Sprites })
                {
                    foreach (bool centred in new[] { true, false })
                    {
                        foreach (WindingOrder? order in new WindingOrder?[] { null, WindingOrder.Clockwise, WindingOrder.CounterClockwise })
                        {
                            Color[] expected = Render(renderer, dt => OldStrokePolygon(renderer, dt, Origin, vertices, StrokeColor, Thickness, centred, order, context));
                            drawn += expected.Any(p => p != Color.Black) ? 1 : 0;
                            foreach ((string inputName, IReadOnlyList<Vector2> input) in new (string, IReadOnlyList<Vector2>)[] { ("array", vertices), ("list", list) })
                            {
                                Color[] actual = Render(renderer, dt => dt.StrokePolygon(Origin, input, StrokeColor, Thickness, centred, order, context));
                                compared++;
                                if (!expected.AsSpan().SequenceEqual(actual) && failures.Count < 20)
                                {
                                    failures.Add($"{name}: StrokePolygon, {inputName}, {context}, {(centred ? "centred" : "inside")}, winding {order?.ToString() ?? "computed"}");
                                }
                            }
                        }
                    }
                }

                foreach (bool centred in new[] { true, false })
                {
                    foreach (WindingOrder? order in new WindingOrder?[] { null, WindingOrder.Clockwise, WindingOrder.CounterClockwise })
                    {
                        Color[] expected = Render(renderer, dt => OldStrokeAndFillPolygon(renderer, dt, Origin, vertices, StrokeColor, FillColor, Thickness, centred, order));
                        drawn += expected.Any(p => p != Color.Black) ? 1 : 0;
                        foreach ((string inputName, IReadOnlyList<Vector2> input) in new (string, IReadOnlyList<Vector2>)[] { ("array", vertices), ("list", list) })
                        {
                            Color[] actual = Render(renderer, dt => dt.StrokeAndFillPolygon(Origin, input, StrokeColor, FillColor, Thickness, centred, order));
                            compared++;
                            if (!expected.AsSpan().SequenceEqual(actual) && failures.Count < 20)
                            {
                                failures.Add($"{name}: StrokeAndFillPolygon, {inputName}, {(centred ? "centred" : "inside")}, winding {order?.ToString() ?? "computed"}");
                            }
                        }
                    }
                }
            }

            _output.WriteLine($"{cases.Count} polygons, {compared} renders compared with the old contour, {drawn} reference renders that draw something; {failures.Count} difference(s) kept");
            Assert.True(drawn > cases.Count * 6, $"only {drawn} reference renders draw something");
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        });
    }

    //  --------------------------------------------------------------------------------------------------------------------------------------
    //  The vertices that the batch receives
    //  --------------------------------------------------------------------------------------------------------------------------------------

    private sealed record Variant(string Name, bool Fill, bool Centred, WindingOrder? Order);

    private static readonly Variant[] Variants =
    {
        new("StrokePolygon, centred", false, true, null),
        new("StrokePolygon, inside, winding computed", false, false, null),
        new("StrokePolygon, inside, winding given clockwise", false, false, WindingOrder.Clockwise),
        new("StrokePolygon, inside, winding given counter-clockwise", false, false, WindingOrder.CounterClockwise),
        new("StrokeAndFillPolygon, centred", true, true, WindingOrder.Clockwise),
        new("StrokeAndFillPolygon, inside, winding computed", true, false, null),
        new("StrokeAndFillPolygon, inside, winding given clockwise", true, false, WindingOrder.Clockwise),
        new("StrokeAndFillPolygon, inside, winding given counter-clockwise", true, false, WindingOrder.CounterClockwise),
    };

    private static readonly float[] Thicknesses = { 1f, 2.5f, 7f };

    /// <summary>The fingerprint of the vertex stream of each variant over the whole corpus (the number of vertices and a SHA-256 of their bits), captured on the product code of before T1.1: <c>StrokePolygon</c> and
    /// <c>StrokeAndFillPolygon</c> were called on the polygons of <see cref="CorpusPlusSmallPolygons"/> (the thickness of the polygon <c>i</c> is <c>Thicknesses[i % 3]</c>, the origin <see cref="FractionalOrigin"/>) and
    /// the vertices that each call left in the <see cref="PrimitiveBatch"/> were hashed in order, as X, Y, Z (the bits of the three floats) and the packed colour.</summary>
    private static readonly Dictionary<string, (long Vertices, string Hash)> FrozenStreams = new()
    {
        ["StrokePolygon, centred"] = (2912760L, "AF6F4FACB7714D1F556AC65017F6BDB4"),
        ["StrokePolygon, inside, winding computed"] = (3165570L, "87304694E043FBF21B4CEC7D539DEB6B"),
        ["StrokePolygon, inside, winding given clockwise"] = (3165570L, "5F63875E377E6ABF6F264F72E682B715"),
        ["StrokePolygon, inside, winding given counter-clockwise"] = (3165570L, "261C1BD261CD61787AA7D67E32C41E02"),
        ["StrokeAndFillPolygon, centred"] = (3812235L, "09A77285DD87303311ECEA84B5D7B99A"),
        ["StrokeAndFillPolygon, inside, winding computed"] = (4065045L, "5E9DA9D5FC94F402A92DA17862787133"),
        ["StrokeAndFillPolygon, inside, winding given clockwise"] = (4065045L, "562BA7716A95FF291080D9547D9D4F86"),
        ["StrokeAndFillPolygon, inside, winding given counter-clockwise"] = (4065045L, "D58F254F9B644A75F8C66277C4AAA1FD"),
    };

    /// <summary>The corpus of <see cref="PolygonCorpus"/> and the polygons of one and two vertices (the corpus has none of one vertex, and the two-vertex polygon of <c>StrokePolygon</c> is a single edge).</summary>
    private static List<PolygonCorpus.Case> CorpusPlusSmallPolygons()
    {
        List<PolygonCorpus.Case> corpus = PolygonCorpus.BuildCorpus();
        corpus.Add(new PolygonCorpus.Case("small polygons", "one vertex", new Vector2[] { new(20.5f, 20.25f) }));
        corpus.Add(new PolygonCorpus.Case("small polygons", "two vertices", new Vector2[] { new(10.5f, 10.25f), new(70.125f, 50.5f) }));
        corpus.Add(new PolygonCorpus.Case("small polygons", "two vertices at the same point", new Vector2[] { new(10, 10), new(10, 10) }));
        corpus.Add(new PolygonCorpus.Case("small polygons", "two vertices, the second far", new Vector2[] { new(0, 0), new(1e6f, -3e5f) }));
        return corpus;
    }

    private static void AppendVertices(IncrementalHash hash, VertexPositionColor[] vertices)
    {
        byte[] buffer = new byte[16];
        foreach (VertexPositionColor v in vertices)
        {
            BitConverter.TryWriteBytes(buffer.AsSpan(0, 4), v.Position.X);
            BitConverter.TryWriteBytes(buffer.AsSpan(4, 4), v.Position.Y);
            BitConverter.TryWriteBytes(buffer.AsSpan(8, 4), v.Position.Z);
            BitConverter.TryWriteBytes(buffer.AsSpan(12, 4), v.Color.PackedValue);
            hash.AppendData(buffer);
        }
    }

    /// <summary>For every polygon of the corpus, as an array and as a list, and every variant: the vertices that the batch holds after the new call are those it holds after the old contour (position with Z,
    /// colour, order, bit for bit), and the stream of each variant is the one frozen on the old product code.</summary>
    [GpuFact]
    public void StrokePolygon_AndStrokeAndFillPolygon_SendToTheBatch_TheVerticesOfTheOldContour_OnTheWholeCorpus()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            CasaDesktopRuntime renderer = CreateRenderer();
            PrimitiveBatch batch = renderer.PrimitiveBatch;
            PrimitiveBatchProbe.AssertReadable(batch);

            List<PolygonCorpus.Case> corpus = CorpusPlusSmallPolygons();
            List<string> failures = new();
            IncrementalHash[] hashes = Variants.Select(_ => IncrementalHash.CreateHash(HashAlgorithmName.SHA256)).ToArray();
            long[] counts = new long[Variants.Length];
            long calls = 0;
            Render(renderer, dt =>
            {
                for (int i = 0; i < corpus.Count; i++)
                {
                    PolygonCorpus.Case c = corpus[i];
                    float thickness = Thicknesses[i % Thicknesses.Length];
                    int offset = 3 + 3 * (i % 4);
                    List<Vector2> list = new(c.Vertices);
                    for (int v = 0; v < Variants.Length; v++)
                    {
                        Variant variant = Variants[v];
                        foreach ((string inputName, IReadOnlyList<Vector2> input) in new (string, IReadOnlyList<Vector2>)[] { ("array", c.Vertices), ("list", list) })
                        {
                            Action reference = variant.Fill
                                ? () => OldStrokeAndFillPolygon(renderer, dt, FractionalOrigin, c.Vertices, StrokeColor, FillColor, thickness, variant.Centred, variant.Order)
                                : () => OldStrokePolygon(renderer, dt, FractionalOrigin, c.Vertices, StrokeColor, thickness, variant.Centred, variant.Order, DrawContext.Primitives);
                            Action product = variant.Fill
                                ? () => dt.StrokeAndFillPolygon(FractionalOrigin, input, StrokeColor, FillColor, thickness, variant.Centred, variant.Order)
                                : () => dt.StrokePolygon(FractionalOrigin, input, StrokeColor, thickness, variant.Centred, variant.Order, DrawContext.Primitives);
                            string difference = PrimitiveBatchProbe.CompareCalls(batch, offset, reference, product, out VertexPositionColor[] written);
                            calls++;
                            if (difference != null && failures.Count < 20)
                            {
                                failures.Add($"{c.Category} / {c.Name}, {variant.Name}, {inputName}: {difference}");
                            }

                            if (inputName == "array")
                            {
                                AppendVertices(hashes[v], written);
                                counts[v] += written.Length;
                            }
                        }
                    }
                }
            });

            List<string> actualStreams = new();
            for (int v = 0; v < Variants.Length; v++)
            {
                string hash = Convert.ToHexString(hashes[v].GetHashAndReset())[..32];
                actualStreams.Add($"[\"{Variants[v].Name}\"] = ({counts[v]}L, \"{hash}\"),");
                if (!FrozenStreams.TryGetValue(Variants[v].Name, out (long Vertices, string Hash) frozen) || frozen.Vertices != counts[v] || frozen.Hash != hash)
                {
                    failures.Add($"{Variants[v].Name}: stream of {counts[v]} vertices, hash {hash}; frozen {(FrozenStreams.TryGetValue(Variants[v].Name, out var f) ? $"{f.Vertices} vertices, hash {f.Hash}" : "nothing")}");
                }
            }

            _output.WriteLine($"{corpus.Count} polygons of the corpus, {calls} calls compared with the old contour, {failures.Count} difference(s) kept");
            _output.WriteLine(string.Join(Environment.NewLine, actualStreams));
            Assert.True(corpus.Count > 70000, $"only {corpus.Count} polygons compared");
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures) + Environment.NewLine + string.Join(Environment.NewLine, actualStreams));
        });
    }
}
