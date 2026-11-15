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
/// unchanged, on a transaction created once, and on the real path, where <see cref="MGDesktop.Draw(float, DrawSettings)"/> asks the runtime for
/// the one transaction it owns (ADR-0097). What is not: see <see cref="WhatTheFrameStillAllocates"/>.
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

    /// <summary>The fixture and its runtime. With <c>holdTheTransaction</c> (the default), one transaction of the runtime is opened and kept for the
    /// whole run, as the MGUI measurements do: a frame ends the batch of the transaction the way its disposal does. Without it, a frame is the real
    /// path, <see cref="MGDesktop.Draw(float, DrawSettings)"/>, which asks the runtime for its transaction and disposes it.</summary>
    private sealed class Harness
    {
        private readonly TestRenderHost _host;
        private int _elapsedMilliseconds;

        public CasaDesktopRuntime Runtime { get; }
        public MGDesktop Desktop { get; }
        public CasaDrawTransaction Transaction { get; }
        public MGSlider Slider { get; }

        public Harness(bool holdTheTransaction = true)
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

            if (holdTheTransaction)
            {
                Transaction = (CasaDrawTransaction)Runtime.BeginDrawTransaction(DrawSettings.Default, deferBegin: true);
            }

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
            if (Transaction == null)
            {
                Desktop.Draw();
                return;
            }

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
        //  Rounded up: a total under one byte per iteration must not read as zero.
        return (after - before + MeasuredIterations - 1) / MeasuredIterations;
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
        Color[] pixels = new Color[target.Width * target.Height];
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

    //  The transaction that the runtime owns (ADR-0097) -------------------------------------------------------------------------------------

    /// <summary>Total bytes allocated by <see cref="MeasuredIterations"/> calls, after a warm-up: the exact total, not divided.</summary>
    private static long MeasureTotal(Action action)
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

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    /// <summary>Total bytes allocated by the updates and by the draws of <see cref="MeasuredIterations"/> frames, after a warm-up. Each frame
    /// updates then draws, as a game does: a draw measured alone would draw the same state again and again.</summary>
    private static (long Update, long Draw) MeasureFrames(Action update, Action draw)
    {
        for (int i = 0; i < WarmupIterations; i++)
        {
            update();
            draw();
        }

        long updateBytes = 0;
        long drawBytes = 0;
        StartWindow();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            long a = GC.GetAllocatedBytesForCurrentThread();
            update();
            long b = GC.GetAllocatedBytesForCurrentThread();
            draw();
            updateBytes += b - a;
            drawBytes += GC.GetAllocatedBytesForCurrentThread() - b;
        }

        return (updateBytes, drawBytes);
    }

    private static void ClearTarget(GraphicsDevice device)
        => device.Clear(ClearOptions.Target | ClearOptions.Stencil | ClearOptions.DepthBuffer, Color.CornflowerBlue, 1.0f, 0);

    /// <summary>The hash of the pixels of a target, which is the render target of the device again afterwards.</summary>
    private static string HashOf(RenderTarget2D target, GraphicsDevice device)
    {
        string hash = PixelsOf(target, device);
        device.SetRenderTarget(target);
        return hash;
    }

    private sealed record DeviceState(
        RasterizerState Rasterizer, BlendState Blend, DepthStencilState DepthStencil, SamplerState Sampler, Rectangle Scissor, Viewport View, RenderTarget2D Target)
    {
        public static DeviceState Of(GraphicsDevice device)
        {
            RenderTargetBinding[] targets = device.GetRenderTargets();
            return new DeviceState(device.RasterizerState, device.BlendState, device.DepthStencilState, device.SamplerStates[0], device.ScissorRectangle,
                device.Viewport, targets.Length > 0 ? (RenderTarget2D)targets[0].RenderTarget : null);
        }
    }

    /// <summary>The real path: <c>MGDesktop.Update()</c> then <c>MGDesktop.Draw()</c>, which asks the runtime for its transaction. At rest, nothing
    /// is allocated by the update or by the draw, the total over the measured frames being exactly zero, and the frames do draw.</summary>
    [GpuFact]
    public void TheRuntimeFrame_AtRest_AllocatesNothing()
    {
        (long update, long draw, bool drew) = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            try
            {
                Harness harness = new(holdTheTransaction: false);
                using RenderTarget2D target = CreateTarget(device);
                string blank = HashOf(target, device);
                long updateBytes = MeasureTotal(() => harness.Update(Harness.Outside, ButtonState.Released));
                long drawBytes = MeasureTotal(() => harness.Draw());
                return (updateBytes, drawBytes, blank != HashOf(target, device));
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });

        _output.WriteLine($"runtime frame, idle: Update {update} B, Draw {draw} B over {MeasuredIterations} frames");
        Assert.True(drew);
        Assert.Equal(0, update);
        Assert.Equal(0, draw);
    }

    [GpuFact]
    public void TheRuntimeFrame_DuringADrag_AllocatesNothing()
    {
        (long update, long draw, bool dragging, bool drew) = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            try
            {
                Harness harness = new(holdTheTransaction: false);
                using RenderTarget2D target = CreateTarget(device);
                string blank = HashOf(target, device);
                (Point first, Point second) = harness.StartDrag();
                int frame = 0;
                (long updateBytes, long drawBytes) = MeasureFrames(
                    () => harness.Update(frame++ % 2 == 0 ? first : second, ButtonState.Pressed),
                    () => harness.Draw());
                return (updateBytes, drawBytes, harness.Slider.IsDraggingThumb, blank != HashOf(target, device));
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });

        _output.WriteLine($"runtime frame, drag: Update {update} B, Draw {draw} B over {MeasuredIterations} frames");
        Assert.True(dragging);
        Assert.True(drew);
        Assert.Equal(0, update);
        Assert.Equal(0, draw);
    }

    /// <summary>The frames of the real path, which reuse one transaction, draw the pixels frozen from the code that created one per frame.</summary>
    [GpuFact]
    public void TheRuntimeFrame_DrawsTheSamePixelsAsWithAFreshTransactionPerFrame()
    {
        (string rest, string drag) = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            Harness harness = new(holdTheTransaction: false);

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

    /// <summary>A second request while the transaction is open is a programming error: it throws, with a message that says what to do, and it
    /// changes nothing: the device is as it was, and the first transaction draws the reference pixels.</summary>
    [GpuFact]
    public void ASecondRequestWhileTheTransactionIsOpen_Throws_AndTheFirstStaysUsable()
    {
        (string pixels, DeviceState before, DeviceState after, string message, string deferredMessage) = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            try
            {
                Harness harness = new(holdTheTransaction: false);
                using RenderTarget2D target = CreateTarget(device);
                IUIDrawTransaction first = harness.Runtime.BeginDrawTransaction(DrawSettings.Default, deferBegin: false);
                DeviceState stateBefore = DeviceState.Of(device);
                InvalidOperationException request = Assert.Throws<InvalidOperationException>(
                    () => harness.Runtime.BeginDrawTransaction(DrawSettings.Default, deferBegin: false));
                InvalidOperationException deferredRequest = Assert.Throws<InvalidOperationException>(
                    () => harness.Runtime.BeginDrawTransaction(DrawSettings.Default, deferBegin: true));
                DeviceState stateAfter = DeviceState.Of(device);

                harness.Desktop.Draw(first);
                first.Dispose();
                return (PixelsOf(target, device), stateBefore, stateAfter, request.Message, deferredRequest.Message);
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });

        Assert.Equal(before, after);
        Assert.Contains("dispose", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("already open", message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(message, deferredMessage);
        Assert.Equal(RestPixels, pixels);
    }

    /// <summary>After <c>Dispose</c>, the next request returns the same object, in the state of a new transaction.</summary>
    [GpuFact]
    public void AfterDispose_TheNextRequestReturnsTheSameObject_Rearmed()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            try
            {
                Harness harness = new(holdTheTransaction: false);
                using RenderTarget2D target = CreateTarget(device);
                DrawSettings changed = DrawSettings.Default with { BlendType = BlendType.Opaque };

                CasaDrawTransaction first = (CasaDrawTransaction)harness.Runtime.BeginDrawTransaction(DrawSettings.Default, deferBegin: false);
                first.SetDrawSettings(changed);
                using (first.PushRectangleClip(new Rectangle(10, 10, 50, 50), true))
                {
                    first.DrawTextureTo(first.WhitePixel, null, new Rectangle(0, 0, 100, 100));
                }

                Assert.True(first.ClipDiagnostics.ScissorClipCount > 0);
                first.Dispose();

                DrawSettings initial = DrawSettings.Default with { SamplerType = SamplerType.LinearClamp };
                CasaDrawTransaction second = (CasaDrawTransaction)harness.Runtime.BeginDrawTransaction(initial, deferBegin: true);
                Assert.Same(first, second);
                Assert.Same(initial, second.CurrentSettings);
                Assert.Equal(DrawSettings.Default, second.PreviousSettings);
                Assert.Equal(0, second.ClipDiagnostics.ScissorClipCount);
                second.Dispose();

                CasaDrawTransaction third = (CasaDrawTransaction)harness.Runtime.BeginDrawTransaction(DrawSettings.Default, deferBegin: false);
                Assert.Same(first, third);
                third.Dispose();
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });
    }

    /// <summary>A change of the viewport between two frames: the projection of the primitives follows it, as it does for a runtime that never
    /// drew at the first size.</summary>
    [GpuFact]
    public void AViewportChangeBetweenTwoFrames_DrawsThePixelsOfAFreshRuntime()
    {
        (string reused, string fresh) = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            try
            {
                using RenderTarget2D large = CreateTarget(device);
                using RenderTarget2D small = new(device, TargetWidth / 2, TargetHeight / 2, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);

                Harness reusedHarness = new(holdTheTransaction: false);
                device.SetRenderTarget(small);
                ClearTarget(device);
                reusedHarness.Frame(Harness.Outside, ButtonState.Released);
                string reusedPixels = PixelsOf(small, device);

                device.SetRenderTarget(small);
                Harness freshHarness = new(holdTheTransaction: false);
                ClearTarget(device);
                freshHarness.Frame(Harness.Outside, ButtonState.Released);
                return (reusedPixels, PixelsOf(small, device));
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });

        Assert.NotEqual(RestPixels, reused);
        Assert.Equal(fresh, reused);
    }

    /// <summary>A draw that throws inside the transaction of the runtime: after <c>Dispose</c> the next request returns the same object, and the
    /// next frame draws the reference pixels.</summary>
    [GpuFact]
    public void ADrawThatThrows_LeavesTheTransactionUsableAfterDispose()
    {
        (bool same, string pixels) = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            try
            {
                Harness harness = new(holdTheTransaction: false);
                CasaDrawTransaction known = (CasaDrawTransaction)harness.Runtime.BeginDrawTransaction(DrawSettings.Default, deferBegin: true);
                known.Dispose();

                using RenderTarget2D target = CreateTarget(device);
                CasaDrawTransaction failing = (CasaDrawTransaction)harness.Runtime.BeginDrawTransaction(DrawSettings.Default, deferBegin: false);
                failing.DrawTextureTo(failing.WhitePixel, null, new Rectangle(0, 0, 64, 64));
                Assert.Throws<ArgumentNullException>(() => failing.DrawTextureTo((Texture2D)null, null, new Rectangle(0, 0, 8, 8)));
                failing.Dispose();

                CasaDrawTransaction next = (CasaDrawTransaction)harness.Runtime.BeginDrawTransaction(DrawSettings.Default, deferBegin: true);
                bool sameObject = ReferenceEquals(known, failing) && ReferenceEquals(known, next);
                next.Dispose();

                ClearTarget(device);
                harness.Draw();
                return (sameObject, PixelsOf(target, device));
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });

        Assert.True(same);
        Assert.Equal(RestPixels, pixels);
    }

    /// <summary>A <c>Dispose</c> that throws itself (the sprite batch of the runtime was ended by someone else while the transaction held it open,
    /// so the end of its batch throws): the transaction is closed all the same, the next request returns the same object, and the next frame draws the reference pixels.</summary>
    [GpuFact]
    public void ADisposeThatThrows_LeavesTheTransactionUsableAfterwards()
    {
        (bool disposeThrew, bool same, string pixels) = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            try
            {
                Harness harness = new(holdTheTransaction: false);
                CasaDrawTransaction known = (CasaDrawTransaction)harness.Runtime.BeginDrawTransaction(DrawSettings.Default, deferBegin: true);
                known.Dispose();

                using RenderTarget2D target = CreateTarget(device);
                CasaDrawTransaction failing = (CasaDrawTransaction)harness.Runtime.BeginDrawTransaction(DrawSettings.Default, deferBegin: false);
                failing.DrawTextureTo(failing.WhitePixel, null, new Rectangle(0, 0, 64, 64));
                harness.Runtime.SpriteBatch.End();
                bool threw = false;
                try
                {
                    failing.Dispose();
                }
                catch (Exception)
                {
                    threw = true;
                }

                CasaDrawTransaction next = (CasaDrawTransaction)harness.Runtime.BeginDrawTransaction(DrawSettings.Default, deferBegin: true);
                bool sameObject = ReferenceEquals(known, failing) && ReferenceEquals(known, next);
                next.Dispose();

                ClearTarget(device);
                harness.Draw();
                return (threw, sameObject, PixelsOf(target, device));
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });

        Assert.True(disposeThrew);
        Assert.True(same);
        Assert.Equal(RestPixels, pixels);
    }

    /// <summary>A request that begins the batch and whose begin throws (the sprite batch of the runtime was left begun) leaves the transaction
    /// closed: the request that follows succeeds, for the first request of a runtime and for the later ones.</summary>
    [GpuFact]
    public void ARequestWhoseBeginThrows_LeavesTheTransactionClosed()
    {
        (bool sameAfterFirst, bool sameAfterLater, string pixels) = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            try
            {
                //  The first request of a runtime.
                CasaDesktopRuntime fresh = CasaMonoGameBackendBootstrap.Create(new TestRenderHost(GpuDeviceHost.Instance.Game)).Runtime;
                fresh.SpriteBatch.Begin();
                Exception first = Assert.ThrowsAny<Exception>(() => fresh.BeginDrawTransaction(DrawSettings.Default, deferBegin: false));
                Assert.DoesNotContain("already open", first.Message, StringComparison.OrdinalIgnoreCase);
                fresh.SpriteBatch.End();
                IUIDrawTransaction afterFirst = fresh.BeginDrawTransaction(DrawSettings.Default, deferBegin: true);
                afterFirst.Dispose();
                IUIDrawTransaction again = fresh.BeginDrawTransaction(DrawSettings.Default, deferBegin: false);
                bool sameFirst = ReferenceEquals(afterFirst, again);
                again.Dispose();

                //  A later request.
                Harness harness = new(holdTheTransaction: false);
                IUIDrawTransaction known = harness.Runtime.BeginDrawTransaction(DrawSettings.Default, deferBegin: true);
                known.Dispose();
                harness.Runtime.SpriteBatch.Begin();
                Exception later = Assert.ThrowsAny<Exception>(() => harness.Runtime.BeginDrawTransaction(DrawSettings.Default, deferBegin: false));
                Assert.DoesNotContain("already open", later.Message, StringComparison.OrdinalIgnoreCase);
                harness.Runtime.SpriteBatch.End();

                using RenderTarget2D target = CreateTarget(device);
                IUIDrawTransaction next = harness.Runtime.BeginDrawTransaction(DrawSettings.Default, deferBegin: true);
                bool sameLater = ReferenceEquals(known, next);
                next.Dispose();
                harness.Draw();
                return (sameFirst, sameLater, PixelsOf(target, device));
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });

        Assert.True(sameAfterFirst);
        Assert.True(sameAfterLater);
        Assert.Equal(RestPixels, pixels);
    }

    /// <summary>Writes what a frame and its parts still allocate and does not assert: the stencil clip, the temporary changes of settings
    /// and of transform (the <see cref="IDisposable"/> of <c>CasaDrawStateController</c>, outside the clip files), and a frame drawn by
    /// <see cref="MGDesktop.Draw(float, DrawSettings)"/> with the transaction of the runtime.</summary>
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
                //  The harness keeps the transaction of the runtime open: MGDesktop.Draw() asks for it, and a runtime has one.
                harness.Transaction.Dispose();
                long perFrameTransactionBytes = Measure(() => harness.Desktop.Draw());
                return $"stencil clip push+dispose {stencilBytes} B, SetDrawSettingsTemporary+dispose {settingsBytes} B, " +
                       $"SetTransformTemporary+dispose {transformBytes} B, MGDesktop.Draw() with the transaction of the runtime {perFrameTransactionBytes} B/frame";
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });

        _output.WriteLine(report);
    }
}
