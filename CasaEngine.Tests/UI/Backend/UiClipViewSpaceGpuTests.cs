using CasaEngine.Framework.UI.Backend.MonoGame;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Xunit;

namespace CasaEngine.Tests.UI.Backend;

/// <summary>
/// Real-GPU proof of ADR-0054: the clip rectangles of the UI are local to the view (MGUI computes them in pixels of the view),
/// while MonoGame applies <see cref="GraphicsDevice.ScissorRectangle"/> in absolute pixels of the render target and draws the
/// sprites relative to the origin of the viewport. A view that does not start at the corner of the target (the bands of a virtual
/// resolution, a split screen) must therefore get its clip shifted by the view origin when it is written to the device, and
/// brought back when it is read. All GPU work goes through <see cref="GpuDeviceHost"/>.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class UiClipViewSpaceGpuTests
{
    private static readonly Color Background = Color.CornflowerBlue;
    private static readonly Color Fill = Color.Red;

    /// <summary>Render host of the test: a real game and device, bounds of the view being drawn, no input.</summary>
    private sealed class TestRenderHost : IRenderHost, IRawInputSource
    {
        private readonly HeadlessGame _game;

        public Rectangle Bounds { get; set; }

        public TestRenderHost(HeadlessGame game)
        {
            _game = game;
        }

        public GraphicsDevice GraphicsDevice => _game.GraphicsDevice;
        public Rectangle GetBounds() => Bounds;
        public object GetService(Type serviceType) => _game.Services.GetService(serviceType);
        public MouseState GetMouseState() => default;
        public KeyboardState GetKeyboardState() => default;

        public event EventHandler<TimeSpan> PreviewUpdate;
        public event EventHandler<EventArgs> EndUpdate;
    }

    private static CasaDesktopRuntime CreateRuntime(Rectangle viewBounds)
    {
        TestRenderHost host = new(GpuDeviceHost.Instance.Game) { Bounds = viewBounds };
        return CasaMonoGameBackendBootstrap.Create(host).Runtime;
    }

    private static Color[] ReadBack(RenderTarget2D target)
    {
        Color[] pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        return pixels;
    }

    /// <summary>A view with an origin: the clip written by the transaction is shifted to the absolute space of the target.</summary>
    [GpuFact]
    public void ClipInAViewOffsetFromTheOrigin_ClipsTheDrawnElementAtItsOwnPlace()
    {
        Color[] pixels = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            using RenderTarget2D target = new(device, 256, 192, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
            device.SetRenderTarget(target);
            device.Clear(ClearOptions.Target | ClearOptions.Stencil, Background, 1.0f, 0);
            device.Viewport = new Viewport(64, 32, 128, 96);

            CasaDesktopRuntime runtime = CreateRuntime(new Rectangle(0, 0, 128, 96));
            using (CasaDrawTransaction transaction = (CasaDrawTransaction)runtime.CreateDrawTransaction(MGUI.Shared.Rendering.DrawSettings.Default, deferBegin: true))
            {
                using IDisposable clip = transaction.PushRectangleClip(new Rectangle(8, 8, 32, 32), IntersectWithCurrentClipTarget: true);
                transaction.FillRectangle(Vector2.Zero, new RectangleF(0, 0, 128, 96), Fill);
            }

            device.SetRenderTarget(null);
            return ReadBack(target);
        });

        // View-local (16, 16), inside the clip (8, 8, 32, 32), is the absolute pixel (80, 48).
        Assert.Equal(Fill, pixels[48 * 256 + 80]);
        // View-local (100, 80) is inside the view and outside the clip.
        Assert.Equal(Background, pixels[(32 + 80) * 256 + (64 + 100)]);
    }

    /// <summary>
    /// The device scissor left by a window resize (stale, smaller than the view) is cleared by
    /// <see cref="UiDeviceScissor.ResetToBackBuffer"/>; without it the first clip of the UI intersects with that stale value.
    /// </summary>
    [GpuFact]
    public void StaleDeviceScissor_AfterTheResetToTheBackBuffer_DoesNotCutTheFirstClip()
    {
        Rectangle scissorAfterReset = default;
        Color[] pixels = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            using RenderTarget2D target = new(device, 256, 192, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
            device.SetRenderTarget(target);
            device.Clear(ClearOptions.Target | ClearOptions.Stencil, Background, 1.0f, 0);
            device.Viewport = new Viewport(64, 32, 128, 96);
            device.ScissorRectangle = new Rectangle(0, 0, 96, 64);

            UiDeviceScissor.ResetToBackBuffer(device);
            scissorAfterReset = device.ScissorRectangle;

            CasaDesktopRuntime runtime = CreateRuntime(new Rectangle(0, 0, 128, 96));
            using (CasaDrawTransaction transaction = (CasaDrawTransaction)runtime.CreateDrawTransaction(MGUI.Shared.Rendering.DrawSettings.Default, deferBegin: true))
            {
                using IDisposable clip = transaction.PushRectangleClip(new Rectangle(0, 0, 128, 96), IntersectWithCurrentClipTarget: true);
                transaction.FillRectangle(Vector2.Zero, new RectangleF(0, 0, 128, 96), Fill);
            }

            device.SetRenderTarget(null);
            return ReadBack(target);
        });

        // Absolute (100, 80) is view-local (36, 48): outside the stale (0, 0, 96, 64) once shifted to (64, 32, 32, 32).
        Assert.Equal(Fill, pixels[80 * 256 + 100]);
        Assert.Equal(new Rectangle(0, 0, 256, 192), scissorAfterReset);
    }

    /// <summary>Witness: a view at the origin is unchanged by the shift, and it pins the vertical orientation of the read-back.</summary>
    [GpuFact]
    public void ClipInAViewAtTheOrigin_IsUnchanged()
    {
        Color[] pixels = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            using RenderTarget2D target = new(device, 256, 192, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
            device.SetRenderTarget(target);
            device.Clear(ClearOptions.Target | ClearOptions.Stencil, Background, 1.0f, 0);
            device.Viewport = new Viewport(0, 0, 256, 192);

            CasaDesktopRuntime runtime = CreateRuntime(new Rectangle(0, 0, 256, 192));
            using (CasaDrawTransaction transaction = (CasaDrawTransaction)runtime.CreateDrawTransaction(MGUI.Shared.Rendering.DrawSettings.Default, deferBegin: true))
            {
                using IDisposable clip = transaction.PushRectangleClip(new Rectangle(8, 8, 32, 32), IntersectWithCurrentClipTarget: true);
                transaction.FillRectangle(Vector2.Zero, new RectangleF(0, 0, 256, 192), Fill);
            }

            device.SetRenderTarget(null);
            return ReadBack(target);
        });

        Assert.Equal(Fill, pixels[16 * 256 + 16]);
        // Outside the clip, on the same row and on the same column: the orientation is not flipped.
        Assert.Equal(Background, pixels[16 * 256 + 100]);
        Assert.Equal(Background, pixels[100 * 256 + 16]);
    }

    /// <summary>The value written to the device is in the absolute space of the target, the value read back is view-local again.</summary>
    [GpuFact]
    public void ClipInABandedView_WritesAbsoluteScissor_AndReadsBackViewLocalBounds()
    {
        Rectangle deviceScissor = default;
        Rectangle? clipBounds = null;
        GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            using RenderTarget2D target = new(device, 2200, 1300, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
            device.SetRenderTarget(target);
            device.Viewport = new Viewport(521, 46, 1600, 1200);
            device.ScissorRectangle = new Rectangle(0, 0, 2200, 1300);

            CasaDesktopRuntime runtime = CreateRuntime(new Rectangle(0, 0, 1600, 1200));
            using (CasaDrawTransaction transaction = (CasaDrawTransaction)runtime.CreateDrawTransaction(MGUI.Shared.Rendering.DrawSettings.Default, deferBegin: true))
            {
                using IDisposable clip = transaction.PushRectangleClip(new Rectangle(40, 80, 840, 240), IntersectWithCurrentClipTarget: true);
                deviceScissor = device.ScissorRectangle;
                clipBounds = transaction.CurrentClipBounds;
            }

            device.SetRenderTarget(null);
        });

        Assert.Equal(new Rectangle(561, 126, 840, 240), deviceScissor);
        Assert.Equal(new Rectangle(40, 80, 840, 240), clipBounds);
    }
}
