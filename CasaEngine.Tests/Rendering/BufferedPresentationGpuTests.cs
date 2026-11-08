using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// ADR-0086 on a real device. A scene on whole virtual pixels drawn straight at factor k, as <c>IntegerFit</c> does, and
/// the same scene drawn at factor 1 into a target that a <see cref="VirtualResolutionPresenter"/> shows at factor k in
/// <c>BufferedIntegerFit</c>, give the same pixels. <c>BufferedFit</c> fills the limiting side and leaves black bands. The
/// test back buffer is 256x192, so the scene is 64x48 (k = 4). Skipped where no device can be created.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class BufferedPresentationGpuTests
{
    private const int VirtualWidth = 64;
    private const int VirtualHeight = 48;

    /// <summary>Colored rectangles and a small checker texture, all on whole virtual pixels.</summary>
    private static void DrawScene(SpriteBatch batch, Texture2D pixel, Texture2D checker)
    {
        batch.Draw(pixel, new Rectangle(0, 0, VirtualWidth, VirtualHeight), new Color(20, 40, 90));
        batch.Draw(pixel, new Rectangle(3, 5, 17, 9), Color.Red);
        batch.Draw(pixel, new Rectangle(30, 20, 1, 1), Color.Yellow);
        batch.Draw(pixel, new Rectangle(40, 2, 20, 30), new Color(10, 200, 120));
        batch.Draw(checker, new Vector2(8, 26), Color.White);
        batch.Draw(checker, new Vector2(50, 38), Color.White);
    }

    private static Texture2D Checker(GraphicsDevice device)
    {
        var texture = new Texture2D(device, 8, 8);
        var data = new Color[64];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = ((i % 8) + (i / 8)) % 2 == 0 ? Color.White : new Color(200, 0, 200);
        }

        texture.SetData(data);
        return texture;
    }

    private static RenderView BufferedView(ViewManager viewManager, RenderTargetSurface target, VirtualResolutionPresenter presenter)
    {
        var id = viewManager.CreateView(new ViewDefinition
        {
            World = new World(),
            Camera = new Camera2dComponent(),
            Surface = target,
            Presenter = presenter,
            Name = "Default view",
        });
        Assert.True(viewManager.TryGetView(id, out var view));
        return view;
    }

    [GpuFact]
    public void BufferedIntegerFit_AtFactorK_MatchesDirectDrawingAtFactorK_PixelForPixel()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var device = GpuDeviceHost.Instance.GraphicsDevice;
            int windowWidth = device.PresentationParameters.BackBufferWidth;
            int windowHeight = device.PresentationParameters.BackBufferHeight;
            int k = Math.Min(windowWidth / VirtualWidth, windowHeight / VirtualHeight);
            Assert.Equal(new Point(VirtualWidth * k, VirtualHeight * k), new Point(windowWidth, windowHeight));

            using var pixel = new Texture2D(device, 1, 1);
            pixel.SetData(new[] { Color.White });
            using var checker = Checker(device);
            using var batch = new SpriteBatch(device);

            // Direct: the scene drawn at factor k into a window-sized target.
            using var direct = new RenderTarget2D(device, windowWidth, windowHeight);
            device.SetRenderTarget(direct);
            device.Clear(Color.Black);
            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, Matrix.CreateScale(k));
            DrawScene(batch, pixel, checker);
            batch.End();
            device.SetRenderTarget(null);
            var expected = new Color[windowWidth * windowHeight];
            direct.GetData(expected);

            // Buffered: the scene at factor 1 into the virtual target, then presented into the back buffer.
            var target = new RenderTargetSurface(device, VirtualWidth, VirtualHeight);
            var presenter = new VirtualResolutionPresenter(
                target, VirtualResolutionMode.BufferedIntegerFit, VirtualWidth, VirtualHeight, windowWidth, windowHeight);
            var view = BufferedView(new ViewManager(), target, presenter);
            target.Apply(device);
            device.Clear(Color.Black);
            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            DrawScene(batch, pixel, checker);
            batch.End();
            target.Restore(device);

            presenter.Present(device, view);
            var actual = new Color[windowWidth * windowHeight];
            device.GetBackBufferData(actual);
            presenter.Dispose();

            // The comparison is not vacuous: the direct image holds the scene's colours.
            Assert.Contains(Color.Red, expected);
            Assert.Contains(new Color(200, 0, 200), expected);
            Assert.Contains(Color.Yellow, expected);

            int differing = 0;
            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i] != actual[i])
                {
                    differing++;
                }
            }

            Assert.Equal(0, differing);
        });
    }

    [GpuFact]
    public void BufferedFit_FillsTheLimitingSide_AndLeavesBlackBands()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var device = GpuDeviceHost.Instance.GraphicsDevice;
            int windowWidth = device.PresentationParameters.BackBufferWidth;
            int windowHeight = device.PresentationParameters.BackBufferHeight;
            const int wideWidth = 100;

            var target = new RenderTargetSurface(device, wideWidth, VirtualHeight);
            var presenter = new VirtualResolutionPresenter(
                target, VirtualResolutionMode.BufferedFit, wideWidth, VirtualHeight, windowWidth, windowHeight);
            var view = BufferedView(new ViewManager(), target, presenter);

            target.Apply(device);
            device.Clear(Color.White);
            target.Restore(device);
            presenter.Present(device, view);
            var pixels = new Color[windowWidth * windowHeight];
            device.GetBackBufferData(pixels);
            var shown = presenter.PresentedRect;
            presenter.Dispose();

            Assert.Equal(windowWidth, shown.Width);
            Assert.True(shown.Height < windowHeight);
            for (int y = 0; y < windowHeight; y++)
            {
                bool inImage = y >= shown.Y && y < shown.Bottom;
                var color = pixels[y * windowWidth + windowWidth / 2];
                Assert.Equal(inImage ? Color.White : Color.Black, color);
            }
        });
    }
}
