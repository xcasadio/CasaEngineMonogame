using CasaEngine.Framework.UI.Backend.MonoGame;
using FontStashSharp;
using MGUI.FontStashSharp;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace CasaEngine.Tests.UI.Backend;

/// <summary>
/// Real-GPU proof that <see cref="CasaDrawTransaction"/> draws a span of characters (MGUI ADR-0026) exactly like the string with the
/// same characters, and without allocating. The text engine is FontStashSharp on <c>arial.ttf</c>, the engine the editor and the games
/// use. All GPU work goes through <see cref="GpuDeviceHost"/>.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class TextSpanGpuTests
{
    private const int TargetWidth = 128;
    private const int TargetHeight = 32;
    private const int WarmupIterations = 400;
    private const int MeasuredIterations = 200;
    private const string Sample = "Value 10.42";

    private sealed class TestRenderHost : IRenderHost, IRawInputSource
    {
        private readonly HeadlessGame _game;

        public TestRenderHost(HeadlessGame game)
        {
            _game = game;
        }

        public GraphicsDevice GraphicsDevice => _game.GraphicsDevice;
        public Rectangle GetBounds() => new(0, 0, TargetWidth, TargetHeight);
        public object GetService(Type serviceType) => _game.Services.GetService(serviceType);
        public MouseState GetMouseState() => default;
        public KeyboardState GetKeyboardState() => default;

        public event EventHandler<TimeSpan> PreviewUpdate;
        public event EventHandler<EventArgs> EndUpdate;
    }

    private static CasaDesktopRuntime CreateRuntime(out ResolvedFont font)
    {
        CasaDesktopRuntime runtime = CasaMonoGameBackendBootstrap.Create(new TestRenderHost(GpuDeviceHost.Instance.Game)).Runtime;
        byte[] ttfData = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Content", "Fonts", "ttf", "arial.ttf"));
        FontStashSharpTextEngine engine = new();
        FontSystem fontSystem = new();
        fontSystem.AddFont(ttfData);
        engine.AddFontSystem("Arial", CustomFontStyles.Normal, fontSystem, ttfData);
        runtime.TextEngine = engine;

        font = runtime.TextEngine.ResolveFont(FontSpec.Normal("Arial", 12));
        Assert.NotNull(font.NativeFont);
        return runtime;
    }

    private static CasaDrawTransaction CreateTransaction(CasaDesktopRuntime runtime)
        => (CasaDrawTransaction)runtime.BeginDrawTransaction(MGUI.Shared.Rendering.DrawSettings.Default, deferBegin: true);

    [GpuFact]
    public void DrawTextViaEngine_OfASpan_DrawsThePixelsOfTheSameString()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            CasaDesktopRuntime runtime = CreateRuntime(out ResolvedFont font);
            char[] buffer = Sample.ToCharArray();

            Color[] expected = Render(runtime, dt => dt.DrawTextViaEngine(font, Sample, new Vector2(2, 2), Color.White, font.DrawOrigin, font.ExactScale, 0f, 0f, UIDrawFlip.None));
            Color[] actual = Render(runtime, dt => dt.DrawTextViaEngine(font, buffer.AsSpan(), new Vector2(2, 2), Color.White, font.DrawOrigin, font.ExactScale));

            Assert.Contains(expected, x => x != Color.Black);
            Assert.Equal(expected, actual);
        });
    }

    [GpuFact]
    public void DrawTextViaEngine_OfASpan_AllocatesNothing()
    {
        long allocated = GpuDeviceHost.Instance.Invoke(() =>
        {
            CasaDesktopRuntime runtime = CreateRuntime(out ResolvedFont font);
            char[] buffer = Sample.ToCharArray();
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            using RenderTarget2D target = new(device, TargetWidth, TargetHeight, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
            device.SetRenderTarget(target);
            try
            {
                //  A first transaction grows the sprite batch to more items than the measured one draws, and rasterizes the glyphs.
                using (CasaDrawTransaction warmup = CreateTransaction(runtime))
                {
                    for (int i = 0; i < WarmupIterations; i++)
                    {
                        warmup.DrawTextViaEngine(font, buffer.AsSpan(), new Vector2(2, 2), Color.White, font.DrawOrigin, font.ExactScale);
                    }
                }

                using CasaDrawTransaction dt = CreateTransaction(runtime);
                //  The first draw begins the batch: it is outside the measured window.
                dt.DrawTextViaEngine(font, buffer.AsSpan(), new Vector2(2, 2), Color.White, font.DrawOrigin, font.ExactScale);

                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < MeasuredIterations; i++)
                {
                    dt.DrawTextViaEngine(font, buffer.AsSpan(), new Vector2(2, 2), Color.White, font.DrawOrigin, font.ExactScale);
                }
                return GC.GetAllocatedBytesForCurrentThread() - before;
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });

        Assert.Equal(0, allocated);
    }

    private static Color[] Render(CasaDesktopRuntime runtime, Action<CasaDrawTransaction> draw)
    {
        GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
        using RenderTarget2D target = new(device, TargetWidth, TargetHeight, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
        device.SetRenderTarget(target);
        device.Clear(Color.Black);
        using (CasaDrawTransaction dt = CreateTransaction(runtime))
        {
            draw(dt);
        }

        device.SetRenderTarget(null);
        Color[] pixels = new Color[TargetWidth * TargetHeight];
        target.GetData(pixels);
        return pixels;
    }
}
