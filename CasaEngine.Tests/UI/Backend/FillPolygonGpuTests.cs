using System.Security.Cryptography;
using CasaEngine.Framework.UI.Backend.MonoGame;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;
using Xunit.Abstractions;

namespace CasaEngine.Tests.UI.Backend;

/// <summary>Real-GPU proof of <see cref="CasaDrawTransaction.FillPolygon"/> on the legacy shape renderer of the engine backend (T5.6, decision D17): it hands the vertices of the caller to
/// <c>PrimitiveDrawing.DrawSolidPolygon</c> without copying them when the caller gives an array or an indexable list, and draws the pixels that
/// <c>DrawSolidPolygon</c> draws when it is called directly with a fresh array. What <c>DrawSolidPolygon</c> allocates itself (MonoGame.Extended
/// triangulates the polygon at each call, <c>Triangulator</c> allocates) is measured and written, not asserted at zero: the assertion is that
/// <see cref="CasaDrawTransaction.FillPolygon"/> allocates no more than the call it makes. All GPU work goes through <see cref="GpuDeviceHost"/>.</summary>
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

    private static IEnumerable<(string Name, Func<Vector2[]> Make)> Shapes()
    {
        yield return ("triangle", Triangle);
        yield return ("triangle (other winding)", TriangleOtherWinding);
        yield return ("concave", Concave);
        yield return ("arrow", Arrow);
        yield return ("arrow (other winding)", ArrowOtherWinding);
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

    /// <summary>What step 2 of T5.6 needs to know: <c>DrawSolidPolygon</c> reads its array during the call and neither modifies it nor keeps it, so that
    /// the array of the caller, or one buffer shared by two polygons of the same length, can be handed to it.</summary>
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

    /// <summary>Polygons of the same length in one batch go through one buffer kept per length: each must be drawn from its own vertices.</summary>
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

    private static long Measure(Action action)
    {
        for (int i = 0; i < WarmupIterations; i++)
        {
            action();
        }

        GC.Collect(0, GCCollectionMode.Forced, blocking: true);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            action();
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        //  Rounded up: a total under one byte per call must not read as zero.
        return (after - before + MeasuredIterations - 1) / MeasuredIterations;
    }

    /// <summary>Bytes per call, after a warm-up, of <see cref="CasaDrawTransaction.FillPolygon"/> of an array and of a list, and of the call it ends with
    /// (<c>DrawSolidPolygon</c>, which triangulates the polygon and allocates in MonoGame.Extended). Written to the test output.</summary>
    [GpuFact]
    public void FillPolygon_OfAnArrayOrOfAList_AllocatesNothingBeyondDrawSolidPolygon()
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
                    long direct = Measure(() => dt.PrimitiveDrawing.DrawSolidPolygon(Origin, vertices, FillColor, false));
                    long fromArray = Measure(() => dt.FillPolygon(Origin, vertices, FillColor));
                    long fromList = Measure(() => dt.FillPolygon(Origin, list, FillColor));
                    _output.WriteLine($"{name} ({vertices.Length} vertices): DrawSolidPolygon alone {direct} B/call, FillPolygon of an array {fromArray} B/call, of a list {fromList} B/call");
                    if (fromArray > direct)
                    {
                        failures.Add($"{name}: FillPolygon of an array allocates {fromArray} B per call, DrawSolidPolygon alone {direct} B");
                    }

                    if (fromList > direct)
                    {
                        failures.Add($"{name}: FillPolygon of a list allocates {fromList} B per call, DrawSolidPolygon alone {direct} B");
                    }
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
