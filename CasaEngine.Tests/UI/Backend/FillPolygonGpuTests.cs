using System.Security.Cryptography;
using CasaEngine.Framework.UI.Backend.MonoGame;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using MGUI.Tests.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.VectorDraw;
using Xunit;
using Xunit.Abstractions;

namespace CasaEngine.Tests.UI.Backend;

/// <summary>Real-GPU proof of <see cref="CasaDrawTransaction.FillPolygon"/> and of <see cref="CasaDrawTransaction.FillRectangle"/> in the primitives context, on the legacy
/// shape renderer of the engine backend (T1.2): they no longer call <c>PrimitiveDrawing.DrawSolidPolygon</c> (which triangulates with MonoGame.Extended's
/// <c>Triangulator</c> and allocates at each call); they triangulate with the <see cref="PolygonTriangulator"/> of the renderer. They allocate nothing, hand to the
/// <c>PrimitiveBatch</c> exactly the vertices that <c>DrawSolidPolygon</c> hands to it (read back from the private buffer of the batch, see
/// <see cref="PrimitiveBatchProbe"/>), and draw the pixels that <c>DrawSolidPolygon</c> draws. <c>DrawSolidPolygon</c> is the reference of every comparison.
/// All GPU work goes through <see cref="GpuDeviceHost"/>.</summary>
[Collection(GpuDeviceCollection.Name)]
public class FillPolygonGpuTests
{
    private const int TargetSize = 96;
    private const int WarmupIterations = 500;
    private const int MeasuredIterations = 500;
    private static readonly Vector2 Origin = new(5, 3);
    private static readonly Color FillColor = Color.Red;

    private readonly ITestOutputHelper _output;

    public FillPolygonGpuTests(ITestOutputHelper output)
    {
        _output = output;
    }

    //  The shapes. Both windings are covered: DrawSolidPolygon reverses a clockwise polygon before triangulating it.
    private static Vector2[] Triangle() => new Vector2[] { new(10, 10), new(70, 20), new(30, 70) };
    private static Vector2[] TriangleOtherWinding() => new Vector2[] { new(30, 70), new(70, 20), new(10, 10) };
    private static Vector2[] Concave() => new Vector2[] { new(10, 10), new(70, 10), new(70, 30), new(30, 30), new(30, 50), new(70, 50), new(70, 70), new(10, 70) };
    private static Vector2[] Arrow() => new Vector2[] { new(5, 30), new(45, 30), new(45, 15), new(80, 40), new(45, 65), new(45, 50), new(5, 50) };
    private static Vector2[] ArrowOtherWinding() { Vector2[] v = Arrow(); Array.Reverse(v); return v; }

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

    private static Vector2[] EllipseOtherWinding() { Vector2[] v = Ellipse(); Array.Reverse(v); return v; }

    private static IEnumerable<(string Name, Func<Vector2[]> Make)> Shapes()
    {
        yield return ("triangle", Triangle);
        yield return ("triangle (other winding)", TriangleOtherWinding);
        yield return ("concave", Concave);
        yield return ("arrow", Arrow);
        yield return ("arrow (other winding)", ArrowOtherWinding);
        yield return ("ellipse", Ellipse);
        yield return ("ellipse (other winding)", EllipseOtherWinding);
    }

    /// <summary>An enumeration that is neither an array nor a list: the only path that copies.</summary>
    private static IEnumerable<Vector2> Enumerate(Vector2[] vertices)
    {
        foreach (Vector2 vertex in vertices)
        {
            yield return vertex;
        }
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

    /// <summary>Clears a target, opens the batch of the primitives on a transaction, runs the drawing, ends the batch and reads the pixels back.</summary>
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

    private static string Hash(Color[] pixels)
    {
        byte[] bytes = new byte[pixels.Length * 4];
        for (int i = 0; i < pixels.Length; i++)
        {
            bytes[i * 4] = pixels[i].R;
            bytes[i * 4 + 1] = pixels[i].G;
            bytes[i * 4 + 2] = pixels[i].B;
            bytes[i * 4 + 3] = pixels[i].A;
        }

        return Convert.ToHexString(SHA256.HashData(bytes))[..16];
    }

    /// <summary>The reference: the call the old code ended with, on a fresh array.</summary>
    private static Color[] ReferencePixels(CasaDesktopRuntime renderer, params Vector2[][] polygons)
        => Render(renderer, dt =>
        {
            foreach (Vector2[] polygon in polygons)
            {
                dt.PrimitiveDrawing.DrawSolidPolygon(Origin, (Vector2[])polygon.Clone(), FillColor, false);
            }
        });

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

    /// <summary><c>DrawSolidPolygon</c>, the reference of this class and what a polygon of two vertices is still delegated to (P4), reads its array during the call and neither
    /// modifies it nor keeps it: the array of the caller, or the array of two vertices that the backend keeps, can be handed to it.</summary>
    [GpuFact]
    public void DrawSolidPolygon_NeitherModifiesItsArray_NorKeepsIt()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            CasaDesktopRuntime renderer = CreateRenderer();
            foreach ((string name, Func<Vector2[]> make) in Shapes())
            {
                //  Not modified: the array is the same after the call (a winding that is reversed is reversed in a copy).
                Vector2[] vertices = make();
                Vector2[] before = (Vector2[])vertices.Clone();
                Render(renderer, dt => dt.PrimitiveDrawing.DrawSolidPolygon(Origin, vertices, FillColor, false));
                Assert.Equal(before, vertices);

                //  Not kept: one array overwritten after each call and before the end of the batch draws what two arrays draw.
                Vector2[] first = make();
                Vector2[] shifted = new Vector2[first.Length];
                for (int i = 0; i < first.Length; i++)
                {
                    shifted[i] = first[i] + new Vector2(20, 5);
                }

                Color[] expected = ReferencePixels(renderer, first, shifted);
                Vector2[] shared = new Vector2[first.Length];
                Color[] actual = Render(renderer, dt =>
                {
                    first.CopyTo(shared, 0);
                    dt.PrimitiveDrawing.DrawSolidPolygon(Origin, shared, FillColor, false);
                    shifted.CopyTo(shared, 0);
                    dt.PrimitiveDrawing.DrawSolidPolygon(Origin, shared, FillColor, false);
                    Array.Clear(shared);
                });

                Assert.NotEqual(Cleared(), expected);
                AssertSamePixels(expected, actual, $"{name}: one array overwritten between two calls");
            }
        });
    }

    [GpuFact]
    public void FillPolygon_DrawsTheSamePixels_FromAnArray_AList_AndAnEnumeration_AsDrawSolidPolygonOnAFreshArray()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            CasaDesktopRuntime renderer = CreateRenderer();
            foreach ((string name, Func<Vector2[]> make) in Shapes())
            {
                Vector2[] vertices = make();
                Vector2[] before = (Vector2[])vertices.Clone();
                Color[] expected = ReferencePixels(renderer, vertices);
                Color[] fromArray = Render(renderer, dt => dt.FillPolygon(Origin, vertices, FillColor));
                Color[] fromList = Render(renderer, dt => dt.FillPolygon(Origin, new List<Vector2>(vertices), FillColor));
                Color[] fromReadOnlyList = Render(renderer, dt => dt.FillPolygon(Origin, Array.AsReadOnly(vertices), FillColor));
                Color[] fromEnumeration = Render(renderer, dt => dt.FillPolygon(Origin, Enumerate(vertices), FillColor));

                _output.WriteLine($"{name}: reference {Hash(expected)}, array {Hash(fromArray)}, list {Hash(fromList)}, read-only list {Hash(fromReadOnlyList)}, enumeration {Hash(fromEnumeration)}");
                Assert.NotEqual(Cleared(), expected);
                AssertSamePixels(expected, fromArray, $"{name}: array");
                AssertSamePixels(expected, fromList, $"{name}: list");
                AssertSamePixels(expected, fromReadOnlyList, $"{name}: read-only list");
                AssertSamePixels(expected, fromEnumeration, $"{name}: enumeration");
                Assert.Equal(before, vertices);
            }
        });
    }

    /// <summary>Polygons of the same length in one batch, from lists and from arrays: each must be drawn from its own vertices (the triangulator keeps one set of buffers that each polygon overwrites).</summary>
    [GpuFact]
    public void FillPolygon_OfSeveralLists_InOneBatch_DrawsEachFromItsOwnVertices()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            CasaDesktopRuntime renderer = CreateRenderer();
            Vector2[] triangleA = Triangle();
            Vector2[] triangleB = { new(60, 60), new(90, 70), new(70, 90) };
            Vector2[] concave = Concave();
            Vector2[] arrow = Arrow();
            Vector2[] triangleC = TriangleOtherWinding();
            Vector2[][] sequence = { triangleA, triangleB, concave, arrow, triangleC, concave, arrow };

            Color[] expected = ReferencePixels(renderer, sequence);
            Color[] fromLists = Render(renderer, dt =>
            {
                foreach (Vector2[] polygon in sequence)
                {
                    dt.FillPolygon(Origin, new List<Vector2>(polygon), FillColor);
                }
            });
            Color[] fromArrays = Render(renderer, dt =>
            {
                foreach (Vector2[] polygon in sequence)
                {
                    dt.FillPolygon(Origin, polygon, FillColor);
                }
            });

            _output.WriteLine($"sequence: reference {Hash(expected)}, lists {Hash(fromLists)}, arrays {Hash(fromArrays)}");
            Assert.NotEqual(Cleared(), expected);
            AssertSamePixels(expected, fromLists, "lists");
            AssertSamePixels(expected, fromArrays, "arrays");
        });
    }

    [GpuFact]
    public void FillPolygon_WithNoVertex_Throws()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            CasaDesktopRuntime renderer = CreateRenderer();
            Render(renderer, dt =>
            {
                Assert.Throws<ArgumentException>(() => dt.FillPolygon(Origin, null, FillColor));
                Assert.Throws<ArgumentException>(() => dt.FillPolygon(Origin, Array.Empty<Vector2>(), FillColor));
                Assert.Throws<ArgumentException>(() => dt.FillPolygon(Origin, new List<Vector2>(), FillColor));
                Assert.Throws<ArgumentException>(() => dt.FillPolygon(Origin, Enumerate(Array.Empty<Vector2>()), FillColor));
            });
        });
    }

    //  --------------------------------------------------------------------------------------------------------------------------------------
    //  Pixels: the rectangle in primitives, the polygons of two vertices
    //  --------------------------------------------------------------------------------------------------------------------------------------

    private static readonly RectangleF[] Rectangles =
    {
        new(10, 12, 40, 20), new(0, 0, 96, 96), new(30.25f, 40.5f, 17.75f, 9.125f), new(60, 5, 3, 70), new(20, 20, -15, 30), new(50, 60, 25, -18),
    };

    /// <summary>The rectangle as the old code drew it: the call it ended with, on a fresh array.</summary>
    private static void DrawRectangleOfTheOldCode(CasaDrawTransaction dt, RectangleF rectangle)
        => dt.PrimitiveDrawing.DrawSolidPolygon(rectangle.Position + Origin,
            new Vector2[] { new(0, 0), new(rectangle.Width, 0), new(rectangle.Width, rectangle.Height), new(0, rectangle.Height) }, FillColor, false);

    [GpuFact]
    public void FillRectangle_InThePrimitivesContext_DrawsTheSamePixels_AsDrawSolidPolygon()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            CasaDesktopRuntime renderer = CreateRenderer();
            foreach (RectangleF rectangle in Rectangles)
            {
                Color[] expected = Render(renderer, dt => DrawRectangleOfTheOldCode(dt, rectangle));
                Color[] actual = Render(renderer, dt => dt.FillRectangle(Origin, rectangle, FillColor, DrawContext.Primitives));
                _output.WriteLine($"{rectangle}: reference {Hash(expected)}, FillRectangle {Hash(actual)}");
                Assert.NotEqual(Cleared(), expected);
                AssertSamePixels(expected, actual, $"rectangle {rectangle}");
            }

            //  Rectangles in one batch, each from its own size.
            Color[] expectedAll = Render(renderer, dt => { foreach (RectangleF rectangle in Rectangles) { DrawRectangleOfTheOldCode(dt, rectangle); } });
            Color[] actualAll = Render(renderer, dt => { foreach (RectangleF rectangle in Rectangles) { dt.FillRectangle(Origin, rectangle, FillColor, DrawContext.Primitives); } });
            AssertSamePixels(expectedAll, actualAll, "rectangles in one batch");
        });
    }

    /// <summary>P4: a polygon of two vertices is still drawn by <c>DrawSolidPolygon</c> (an outline); one vertex draws nothing.</summary>
    [GpuFact]
    public void FillPolygon_OfTwoVerticesOrOne_DrawsWhatDrawSolidPolygonDraws()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            CasaDesktopRuntime renderer = CreateRenderer();
            Vector2[] two = { new(10, 10), new(80, 60) };
            Vector2[] one = { new(10, 10) };

            Color[] expectedTwo = ReferencePixels(renderer, two);
            Assert.NotEqual(Cleared(), expectedTwo);
            AssertSamePixels(expectedTwo, Render(renderer, dt => dt.FillPolygon(Origin, two, FillColor)), "two vertices, array");
            AssertSamePixels(expectedTwo, Render(renderer, dt => dt.FillPolygon(Origin, new List<Vector2>(two), FillColor)), "two vertices, list");
            AssertSamePixels(expectedTwo, Render(renderer, dt => dt.FillPolygon(Origin, Enumerate(two), FillColor)), "two vertices, enumeration");

            Color[] expectedOne = ReferencePixels(renderer, one);
            AssertSamePixels(expectedOne, Render(renderer, dt => dt.FillPolygon(Origin, one, FillColor)), "one vertex, array");
            AssertSamePixels(expectedOne, Render(renderer, dt => dt.FillPolygon(Origin, new List<Vector2>(one), FillColor)), "one vertex, list");
        });
    }

    //  --------------------------------------------------------------------------------------------------------------------------------------
    //  The vertices that the batch receives (step 3 of T1.2)
    //  --------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>A fractional origin: the sum <c>vertex + origin</c> must be made as <c>DrawSolidPolygon</c> makes it, bit for bit.</summary>
    private static readonly Vector2 FractionalOrigin = new(5.3f, 3.7f);

    /// <summary>For every polygon of the corpus of T1.1, as an array and as a list: the vertices the batch holds after <see cref="CasaDrawTransaction.FillPolygon"/> are
    /// those it holds after <c>DrawSolidPolygon</c> (position with Z, colour, order), from a count of 0 to 9 vertices, and the count grows by three per triangle.</summary>
    [GpuFact]
    public void FillPolygon_SendsToTheBatch_TheVerticesOfDrawSolidPolygon_OnTheWholeCorpus()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            CasaDesktopRuntime renderer = CreateRenderer();
            PrimitiveBatch batch = renderer.PrimitiveBatch;
            PrimitiveBatchProbe.AssertReadable(batch);

            List<PolygonCorpus.Case> corpus = PolygonCorpus.BuildCorpus();
            List<string> failures = new();
            int polygons = 0;
            int calls = 0;
            int withoutTriangle = 0;
            Render(renderer, dt =>
            {
                for (int i = 0; i < corpus.Count; i++)
                {
                    PolygonCorpus.Case c = corpus[i];
                    if (c.Vertices.Length == 2)
                    {
                        continue;
                    }

                    int triangles = PrimitiveBatchProbe.ReferenceTriangleCount(c.Vertices);
                    withoutTriangle += triangles == 0 ? 1 : 0;
                    int offset = 3 * (i % 4);
                    List<Vector2> list = new(c.Vertices);
                    string arrayDifference = PrimitiveBatchProbe.Compare(batch, offset, triangles,
                        () => dt.PrimitiveDrawing.DrawSolidPolygon(FractionalOrigin, c.Vertices, FillColor, false),
                        () => dt.FillPolygon(FractionalOrigin, c.Vertices, FillColor));
                    string listDifference = PrimitiveBatchProbe.Compare(batch, offset, triangles,
                        () => dt.PrimitiveDrawing.DrawSolidPolygon(FractionalOrigin, c.Vertices, FillColor, false),
                        () => dt.FillPolygon(FractionalOrigin, list, FillColor));
                    polygons++;
                    calls += 2;
                    if (arrayDifference != null && failures.Count < 20)
                    {
                        failures.Add($"{c.Category} / {c.Name}, array: {arrayDifference}");
                    }

                    if (listDifference != null && failures.Count < 20)
                    {
                        failures.Add($"{c.Category} / {c.Name}, list: {listDifference}");
                    }
                }
            });

            _output.WriteLine($"{polygons} polygons of the corpus compared ({calls} calls of FillPolygon against DrawSolidPolygon, array and list), {withoutTriangle} of them without any triangle; {failures.Count} difference(s) kept");
            Assert.True(polygons > 70000, $"only {polygons} polygons compared");
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        });
    }

    [GpuFact]
    public void FillRectangle_InThePrimitivesContext_SendsToTheBatch_TheVerticesOfDrawSolidPolygon()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            CasaDesktopRuntime renderer = CreateRenderer();
            PrimitiveBatch batch = renderer.PrimitiveBatch;
            PrimitiveBatchProbe.AssertReadable(batch);

            List<RectangleF> rectangles = new(Rectangles) { new(0.1f, 0.2f, 7.3f, 5.9f), new(-8, -9, 100.5f, 3.25f), new(1000.5f, 2000.25f, 0.5f, 0.75f) };
            List<string> failures = new();
            Render(renderer, dt =>
            {
                for (int i = 0; i < rectangles.Count; i++)
                {
                    RectangleF rectangle = rectangles[i];
                    string difference = PrimitiveBatchProbe.Compare(batch, 3 * (i % 4), 2,
                        () => dt.PrimitiveDrawing.DrawSolidPolygon(rectangle.Position + FractionalOrigin,
                            new Vector2[] { new(0, 0), new(rectangle.Width, 0), new(rectangle.Width, rectangle.Height), new(0, rectangle.Height) }, FillColor, false),
                        () => dt.FillRectangle(FractionalOrigin, rectangle, FillColor, DrawContext.Primitives));
                    if (difference != null)
                    {
                        failures.Add($"{rectangle}: {difference}");
                    }
                }
            });

            _output.WriteLine($"{rectangles.Count} rectangles in the primitives context compared; {failures.Count} difference(s)");
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        });
    }

    /// <summary>The guard of <c>DrawSolidPolygon</c> (a batch that is not ready) is kept: same exception type and message.</summary>
    [GpuFact]
    public void FillPolygon_AndFillRectangle_WhenTheBatchIsNotReady_ThrowWhatDrawSolidPolygonThrows()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            CasaDesktopRuntime renderer = CreateRenderer();
            PrimitiveBatch batch = renderer.PrimitiveBatch;
            Render(renderer, dt =>
            {
                batch.End();
                try
                {
                    Assert.False(batch.IsReady());
                    Vector2[] triangle = Triangle();
                    InvalidOperationException direct = Assert.Throws<InvalidOperationException>(() => dt.PrimitiveDrawing.DrawSolidPolygon(Origin, triangle, FillColor, false));
                    Assert.Equal("BeginCustomDraw must be called before drawing anything.", direct.Message);
                    InvalidOperationException[] fromNew =
                    {
                        Assert.Throws<InvalidOperationException>(() => dt.FillPolygon(Origin, triangle, FillColor)),
                        Assert.Throws<InvalidOperationException>(() => dt.FillPolygon(Origin, new List<Vector2>(triangle), FillColor)),
                        Assert.Throws<InvalidOperationException>(() => dt.FillPolygon(Origin, Enumerate(triangle), FillColor)),
                        Assert.Throws<InvalidOperationException>(() => dt.FillRectangle(Origin, new RectangleF(1, 2, 30, 40), FillColor, DrawContext.Primitives)),
                    };
                    foreach (InvalidOperationException e in fromNew)
                    {
                        Assert.Equal(direct.Message, e.Message);
                    }
                }
                finally
                {
                    Matrix identity = Matrix.Identity;
                    batch.Begin(ref identity, ref identity, BlendState.AlphaBlend);
                }
            });
        });
    }

    //  --------------------------------------------------------------------------------------------------------------------------------------
    //  No allocation
    //  --------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Total bytes allocated by <see cref="MeasuredIterations"/> calls after <see cref="WarmupIterations"/> of warm-up.</summary>
    private static long MeasureTotal(Action action)
    {
        for (int i = 0; i < WarmupIterations; i++)
        {
            action();
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            action();
        }

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    /// <summary>Rounded up: a total under one byte per call must not read as zero.</summary>
    private static long PerCall(long total) => (total + MeasuredIterations - 1) / MeasuredIterations;

    /// <summary>Bytes per call, after a warm-up, of <see cref="CasaDrawTransaction.FillPolygon"/> of an array and of a list and of <see cref="CasaDrawTransaction.FillRectangle"/> in
    /// the primitives context: zero. The call the old code ended with (<c>DrawSolidPolygon</c>) is measured too and written, for the record.</summary>
    [GpuFact]
    public void FillPolygon_OfAnArrayOrOfAList_AndFillRectangleInPrimitives_AllocateNothing()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            using RenderTarget2D target = new(device, TargetSize, TargetSize, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
            device.SetRenderTarget(target);
            try
            {
                device.Clear(ClearOptions.Target | ClearOptions.Stencil | ClearOptions.DepthBuffer, Color.Black, 1.0f, 0);
                CasaDesktopRuntime renderer = CreateRenderer();
                using CasaDrawTransaction dt = BeginTransaction(renderer);
                dt.ForceBeginDraw(DrawContext.Primitives);

                List<string> failures = new();
                foreach ((string name, Func<Vector2[]> make) in Shapes())
                {
                    Vector2[] vertices = make();
                    List<Vector2> list = new(vertices);
                    long direct = PerCall(MeasureTotal(() => dt.PrimitiveDrawing.DrawSolidPolygon(Origin, vertices, FillColor, false)));
                    long fromArray = MeasureTotal(() => dt.FillPolygon(Origin, vertices, FillColor));
                    long fromList = MeasureTotal(() => dt.FillPolygon(Origin, list, FillColor));
                    _output.WriteLine($"{name} ({vertices.Length} vertices): DrawSolidPolygon alone {direct} B/call, FillPolygon of an array {PerCall(fromArray)} B/call ({fromArray} B in total), of a list {PerCall(fromList)} B/call ({fromList} B in total)");
                    if (fromArray != 0)
                    {
                        failures.Add($"{name}: FillPolygon of an array allocates {fromArray} B in {MeasuredIterations} calls");
                    }

                    if (fromList != 0)
                    {
                        failures.Add($"{name}: FillPolygon of a list allocates {fromList} B in {MeasuredIterations} calls");
                    }
                }

                RectangleF rectangle = new(10, 12, 40, 20);
                long rectangleDirect = PerCall(MeasureTotal(() => DrawRectangleOfTheOldCode(dt, rectangle)));
                long rectangleTotal = MeasureTotal(() => dt.FillRectangle(Origin, rectangle, FillColor, DrawContext.Primitives));
                _output.WriteLine($"rectangle in the primitives context: old code {rectangleDirect} B/call, FillRectangle {PerCall(rectangleTotal)} B/call ({rectangleTotal} B in total)");
                if (rectangleTotal != 0)
                {
                    failures.Add($"FillRectangle in the primitives context allocates {rectangleTotal} B in {MeasuredIterations} calls");
                }

                Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });
    }
}
