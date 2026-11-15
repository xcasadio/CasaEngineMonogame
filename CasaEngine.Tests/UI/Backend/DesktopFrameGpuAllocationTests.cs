using System.Security.Cryptography;
using CasaEngine.Framework.UI.Backend.MonoGame;
using FontStashSharp;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.FontStashSharp;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;
using Xunit.Abstractions;

namespace CasaEngine.Tests.UI.Backend;

/// <summary>
/// Real-GPU proof that a desktop frame drawn by the engine backend allocates nothing: the default window (title bar) holding an
/// <see cref="MGStackPanel"/> with an <see cref="MGTextBlock"/> and an <see cref="MGSlider"/> (the <c>Full</c> fixture of the MGUI desktop
/// frame measurements), updated by an <see cref="MGDesktop"/> on a <see cref="CasaDesktopRuntime"/> and drawn through a
/// <see cref="CasaDrawTransaction"/> with the FontStashSharp text engine. All GPU work goes through <see cref="GpuDeviceHost"/>.<para/>
/// What is proven at zero: the update and the draw of that fixture, at rest and during a drag of the slider thumb that leaves the label text
/// unchanged, on a transaction created once. What is not: see <see cref="WhatTheFrameStillAllocates"/>.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class DesktopFrameGpuAllocationTests
{
    private const int TargetWidth = 640;
    private const int TargetHeight = 360;
    private const int WarmupIterations = 500;
    private const int MeasuredIterations = 500;

    //  Size in bytes and SHA-256 of the pixels (R, G, B, A of each pixel, row by row) of the 640 x 360 target, cleared to CornflowerBlue,
    //  after the fixture was drawn at rest (two frames), and after the thumb of the slider was dragged to two positions (the frame drawn at
    //  the second one). Taken from the code before the clip scopes of the backend were reused, on the same machine; the hash of a frame
    //  rendered by another GPU or driver could differ.
    private const string RestPixels = "921600:124E0E0FFD9AB38C0A9FE580081317A93241F77B68177662060220B0438C9F36";
    private const string DragPixels = "921600:A627D0D33AC2F348C4248983F467DA0000D31EA9DA8EA74A0022D86194D7BFA9";

    private readonly ITestOutputHelper _output;

    public DesktopFrameGpuAllocationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private sealed class TestRenderHost : IRenderHost, IRawInputSource
    {
        private readonly HeadlessGame _game;
        private MouseState _mouse;

        public TestRenderHost(HeadlessGame game)
        {
            _game = game;
        }

        public GraphicsDevice GraphicsDevice => _game.GraphicsDevice;
        public Rectangle GetBounds() => new(0, 0, TargetWidth, TargetHeight);
        public object GetService(Type serviceType) => _game.Services.GetService(serviceType);
        public MouseState GetMouseState() => _mouse;
        public KeyboardState GetKeyboardState() => default;

        public event EventHandler<TimeSpan> PreviewUpdate;
        public event EventHandler<EventArgs> EndUpdate;

        public void RaiseFrameStart(MouseState mouse, TimeSpan elapsed)
        {
            _mouse = mouse;
            PreviewUpdate?.Invoke(this, elapsed);
        }

        public void RaiseFrameEnd() => EndUpdate?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The fixture, its runtime and one transaction kept for the whole run, as the MGUI measurements do: a frame ends the batch of
    /// the transaction the way the disposal of a per-frame transaction does.</summary>
    private sealed class Harness
    {
        private readonly TestRenderHost _host;
        private int _elapsedMilliseconds;

        public CasaDesktopRuntime Runtime { get; }
        public MGDesktop Desktop { get; }
        public CasaDrawTransaction Transaction { get; }
        public MGSlider Slider { get; }

        public Harness()
        {
            _host = new TestRenderHost(GpuDeviceHost.Instance.Game);
            Runtime = CasaMonoGameBackendBootstrap.Create(_host).Runtime;
            Desktop = new MGDesktop(Runtime);

            byte[] ttfData = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Content", "Fonts", "ttf", "arial.ttf"));
            FontStashSharpTextEngine engine = new();
            FontSystem fontSystem = new();
            fontSystem.AddFont(ttfData);
            engine.AddFontSystem(Runtime.DefaultFontFamily, CustomFontStyles.Normal, fontSystem, ttfData);
            engine.MatchSpriteFontSizing(Runtime.FontManager);
            Desktop.TextEngine = engine;

            MGWindow window = new(Desktop, 0, 0, 300, 200);
            Desktop.Windows.Add(window);
            MGStackPanel panel = new(window, Orientation.Vertical);
            panel.TryAddChild(new MGTextBlock(window, "Value"));
            //  "F0" on [0, 1]: every value below 0.5 shows "0", so the drag does not change the label text.
            Slider = new MGSlider(window, 0, 1, 0.5f) { ShowValueLabel = true, ValueLabelFormat = "F0" };
            panel.TryAddChild(Slider);
            window.SetContent(panel);

            Transaction = (CasaDrawTransaction)Runtime.CreateDrawTransaction(DrawSettings.Default, deferBegin: true);

            Frame(Outside, ButtonState.Released);
            Frame(Outside, ButtonState.Released);
        }

        public static Point Outside => new(600, 340);

        public void Update(Point position, ButtonState left)
        {
            MouseState mouse = new(position.X, position.Y, 0, left, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _host.RaiseFrameStart(mouse, TimeSpan.FromMilliseconds(_elapsedMilliseconds));
            Desktop.Update();
            _host.RaiseFrameEnd();
            _elapsedMilliseconds += 16;
        }

        public void Draw()
        {
            Desktop.Draw(Transaction);
            Transaction.EndCurrentContext();
        }

        public void Frame(Point position, ButtonState left)
        {
            Update(position, left);
            Draw();
        }

        /// <summary>Presses on the thumb, moves to two positions along the track, and leaves the slider dragging.</summary>
        public (Point First, Point Second) StartDrag()
        {
            Rectangle bounds = Slider.LayoutBounds;
            Point thumb = Slider.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, bounds.Center);
            Point first = Slider.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, new Point(bounds.Left + bounds.Width / 5, bounds.Center.Y));
            Point second = Slider.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, new Point(bounds.Left + bounds.Width / 4, bounds.Center.Y));
            Frame(thumb, ButtonState.Released);
            Frame(thumb, ButtonState.Released);
            Frame(thumb, ButtonState.Pressed);
            Frame(first, ButtonState.Pressed);
            Frame(second, ButtonState.Pressed);
            Assert.True(Slider.IsDraggingThumb);
            return (first, second);
        }
    }

    /// <summary>Empties the allocation context of the thread with a blocking collection, then reads the counter: from there on it only
    /// moves when this thread allocates (a background collection could otherwise add the unused tail of the context to the window).</summary>
    private static long StartWindow()
    {
        GC.Collect(0, GCCollectionMode.Forced, blocking: true);
        return GC.GetAllocatedBytesForCurrentThread();
    }

    /// <summary>Bytes allocated per call, after a warm-up.</summary>
    private static long Measure(Action action)
    {
        for (int i = 0; i < WarmupIterations; i++)
        {
            action();
        }

        long before = StartWindow();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            action();
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        return (after - before) / MeasuredIterations;
    }

    private static RenderTarget2D CreateTarget(GraphicsDevice device)
    {
        RenderTarget2D target = new(device, TargetWidth, TargetHeight, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
        device.SetRenderTarget(target);
        device.Clear(ClearOptions.Target | ClearOptions.Stencil | ClearOptions.DepthBuffer, Color.CornflowerBlue, 1.0f, 0);
        return target;
    }

    private static string PixelsOf(RenderTarget2D target, GraphicsDevice device)
    {
        device.SetRenderTarget(null);
        Color[] pixels = new Color[TargetWidth * TargetHeight];
        target.GetData(pixels);
        byte[] bytes = new byte[pixels.Length * 4];
        for (int i = 0; i < pixels.Length; i++)
        {
            bytes[i * 4] = pixels[i].R;
            bytes[i * 4 + 1] = pixels[i].G;
            bytes[i * 4 + 2] = pixels[i].B;
            bytes[i * 4 + 3] = pixels[i].A;
        }

        return $"{bytes.Length}:{Convert.ToHexString(SHA256.HashData(bytes))}";
    }

    [GpuFact]
    public void AnIdleFrame_AllocatesNothing()
    {
        (long update, long draw, int clips) = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            using RenderTarget2D target = CreateTarget(device);
            try
            {
                Harness harness = new();
                int clipsBefore = harness.Transaction.ClipDiagnostics.ScissorClipCount;
                long updateBytes = Measure(() => harness.Update(Harness.Outside, ButtonState.Released));
                long drawBytes = Measure(() => harness.Draw());
                return (updateBytes, drawBytes, harness.Transaction.ClipDiagnostics.ScissorClipCount - clipsBefore);
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });

        _output.WriteLine($"idle: Update {update} B/frame, Draw {draw} B/frame");
        //  The frame pushed clips: a draw that did nothing would prove nothing.
        Assert.True(clips >= MeasuredIterations);
        Assert.Equal(0, update);
        Assert.Equal(0, draw);
    }

    [GpuFact]
    public void ADragFrame_AllocatesNothing()
    {
        (long update, long draw, bool dragging) = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            using RenderTarget2D target = CreateTarget(device);
            try
            {
                Harness harness = new();
                (Point first, Point second) = harness.StartDrag();
                int frame = 0;
                long updateBytes = Measure(() => harness.Update(frame++ % 2 == 0 ? first : second, ButtonState.Pressed));
                long drawBytes = Measure(() => harness.Draw());
                return (updateBytes, drawBytes, harness.Slider.IsDraggingThumb);
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });

        _output.WriteLine($"drag: Update {update} B/frame, Draw {draw} B/frame");
        Assert.True(dragging);
        Assert.Equal(0, update);
        Assert.Equal(0, draw);
    }

    [GpuFact]
    public void TheFrame_DrawsTheSamePixelsAsBeforeTheClipScopesWereReused()
    {
        (string rest, string drag) = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            Harness harness = new();

            using RenderTarget2D restTarget = CreateTarget(device);
            harness.Draw();
            string restPixels = PixelsOf(restTarget, device);

            (Point first, Point second) = harness.StartDrag();
            harness.Frame(first, ButtonState.Pressed);
            using RenderTarget2D dragTarget = CreateTarget(device);
            harness.Frame(second, ButtonState.Pressed);
            return (restPixels, PixelsOf(dragTarget, device));
        });

        Assert.Equal(RestPixels, rest);
        Assert.Equal(DragPixels, drag);
    }

    /// <summary>Four nested clips (rectangle, no clip, intersecting rectangle, clip removed) leave the scissor rectangle and the scissor test
    /// the same at each step as before the scopes were reused, from settings that test the scissor (the default) and from settings that do not,
    /// and a scope disposed twice restores once.</summary>
    [GpuFact]
    public void NestedClips_FromSettingsThatTestTheScissor_RestoreTheScissorAtEachLevel()
        => AssertNestedClips(scissorTestAtStart: true);

    [GpuFact]
    public void NestedClips_FromSettingsThatDoNotTestTheScissor_RestoreTheScissorAtEachLevel()
        => AssertNestedClips(scissorTestAtStart: false);

    private static void AssertNestedClips(bool scissorTestAtStart)
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            using RenderTarget2D target = CreateTarget(device);
            try
            {
                Harness harness = new();
                CasaDrawTransaction dt = harness.Transaction;
                Rectangle full = new(0, 0, TargetWidth, TargetHeight);
                Rectangle outer = new(10, 10, 200, 100);
                Rectangle inner = new(50, 20, 300, 50);
                Rectangle intersection = Rectangle.Intersect(outer, inner);
                dt.SetDrawSettings(dt.CurrentSettings with { RasterizerType = scissorTestAtStart ? RasterizerType.SolidScissorTest : RasterizerType.Solid });
                DrawSettings initial = dt.CurrentSettings;
                Assert.Equal(scissorTestAtStart, initial.UsesScissorTest);
                Assert.Equal(full, device.ScissorRectangle);

                ClipScope first = dt.PushRectangleClip(outer, true);
                Assert.True(dt.CurrentSettings.UsesScissorTest);
                Assert.Equal(outer, device.ScissorRectangle);
                Assert.Equal(outer, dt.CurrentClipBounds);
                Assert.Equal(ClipStrategy.Scissor, first.Resolution.Strategy);
                Assert.Equal(outer, first.Resolution.Effective.Shape.Bounds);
                Assert.True(first.Resolution.Effective.IntersectWithCurrentClip);

                ClipScope none = dt.PushClipTemporary(ClipDefinition.None());
                Assert.Equal(ClipStrategy.None, none.Resolution.Strategy);
                Assert.Equal(outer, device.ScissorRectangle);

                ClipScope second = dt.PushRectangleClip(inner, true);
                Assert.Equal(intersection, device.ScissorRectangle);
                Assert.Equal(inner, second.Resolution.Effective.Shape.Bounds);

                ClipScope removed = dt.PushRectangleClip(null, false);
                Assert.False(dt.CurrentSettings.UsesScissorTest);
                Assert.Null(dt.CurrentClipBounds);
                Assert.Equal(full, device.ScissorRectangle);
                Assert.Equal(ClipStrategy.None, removed.Resolution.Strategy);

                removed.Dispose();
                Assert.True(dt.CurrentSettings.UsesScissorTest);
                Assert.Equal(intersection, device.ScissorRectangle);

                second.Dispose();
                Assert.True(dt.CurrentSettings.UsesScissorTest);
                Assert.Equal(outer, device.ScissorRectangle);

                none.Dispose();
                Assert.Equal(outer, device.ScissorRectangle);

                first.Dispose();
                Assert.Equal(scissorTestAtStart, dt.CurrentSettings.UsesScissorTest);
                Assert.Equal(full, device.ScissorRectangle);
                Assert.Equal(initial, dt.CurrentSettings);

                //  A scope disposed again, before it is reused, restores nothing: the scissor of a clip pushed since is not touched.
                first.Dispose();
                Assert.Equal(full, device.ScissorRectangle);
                using (dt.PushRectangleClip(inner, false))
                {
                    Assert.Equal(inner, device.ScissorRectangle);
                    Assert.Equal(inner, dt.CurrentClipBounds);
                }

                Assert.Equal(full, device.ScissorRectangle);
                Assert.Equal(initial, dt.CurrentSettings);
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });
    }

    /// <summary>Writes what a frame and its parts still allocate and does not assert: the stencil clip, the temporary changes of settings
    /// and of transform (the <see cref="IDisposable"/> of <c>CasaDrawStateController</c>, outside the clip files), and the transaction that
    /// <see cref="MGDesktop.Draw(float, DrawSettings)"/> creates when it is not given one.</summary>
    [GpuFact]
    public void WhatTheFrameStillAllocates()
    {
        string report = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            using RenderTarget2D target = CreateTarget(device);
            try
            {
                Harness harness = new();
                CasaDrawTransaction dt = harness.Transaction;
                ClipGeometry geometry = new(
                    new List<Vector2> { new(10, 10), new(100, 10), new(10, 100) },
                    new List<int> { 0, 1, 2 });
                ClipDefinition stencil = ClipDefinition.ArbitraryGeometry(new Rectangle(10, 10, 90, 90), geometry);
                DrawSettings changed = dt.CurrentSettings with { BlendType = BlendType.Opaque };
                Matrix transform = Matrix.CreateTranslation(1, 2, 0);

                long stencilBytes = Measure(() => dt.PushClipTemporary(stencil).Dispose());
                long settingsBytes = Measure(() => dt.SetDrawSettingsTemporary(changed).Dispose());
                long transformBytes = Measure(() => dt.SetTransformTemporary(transform).Dispose());
                long perFrameTransactionBytes = Measure(() => harness.Desktop.Draw());
                return $"stencil clip push+dispose {stencilBytes} B, SetDrawSettingsTemporary+dispose {settingsBytes} B, " +
                       $"SetTransformTemporary+dispose {transformBytes} B, MGDesktop.Draw() with its own transaction {perFrameTransactionBytes} B/frame";
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });

        _output.WriteLine(report);
    }
}
