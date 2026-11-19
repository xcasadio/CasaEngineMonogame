using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.PostProcess;
using CasaEngine.Framework.Rendering.Shaders;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;
using Xunit.Abstractions;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// The FXAA pass on a real device (plan T1.2, ADR-0114): it smooths a sharp oblique edge, leaves a flat area alone, does not shift
/// the image, puts the device states back, and the pipeline runs it only in the view that composes its interface afterwards, with
/// its target evicted after a size change. Skipped where no device can be created.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class FxaaGpuTests
{
    private const int Size = 64;

    private readonly ITestOutputHelper _output;

    public FxaaGpuTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [GpuFact]
    public void ASharpObliqueEdge_GainsAtLeastFiftyPercentIntermediateLevels_AndAFlatAreaStaysTheSame()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            // White below a shallow line (slope 0.4), black above: long stair steps, the case FXAA exists for.
            var input = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    input[y * Size + x] = y > 0.4f * x + 8.0f ? Color.White : Color.Black;
                }
            }

            var output = RunFxaa(input, Size, Size);

            int before = CountIntermediate(input);
            int after = CountIntermediate(output);
            _output.WriteLine($"Intermediate levels (32..223): {before} before, {after} after.");

            Assert.True(after > before);
            Assert.True(after >= 1.5 * before, $"expected at least 50 % more, got {before} -> {after}");
            Assert.True(after >= 40, $"a 64 pixel long edge must gain a visible band of intermediate pixels, got {after}");

            // Pixels of a flat area (at least 6 pixels from the edge) are the same within one level.
            int checkedPixels = 0;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float distance = Math.Abs(y - (0.4f * x + 8.0f));
                    if (distance <= 6.0f)
                    {
                        continue;
                    }

                    checkedPixels++;
                    Assert.InRange(Math.Abs(output[y * Size + x].R - input[y * Size + x].R), 0, 1);
                }
            }

            Assert.True(checkedPixels > 2000);
        });
    }

    [GpuFact]
    public void AUniformImage_IsReturnedWithinOneLevel_WithAlphaOne()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var input = new Color[32 * 32];
            Array.Fill(input, new Color(120, 90, 200, 17));

            var output = RunFxaa(input, 32, 32);

            foreach (var pixel in output)
            {
                Assert.InRange(Math.Abs(pixel.R - 120), 0, 1);
                Assert.InRange(Math.Abs(pixel.G - 90), 0, 1);
                Assert.InRange(Math.Abs(pixel.B - 200), 0, 1);
                Assert.Equal(255, pixel.A);
            }
        });
    }

    [GpuFact]
    public void TheOutput_HasTheSizeOfTheInput_AndDoesNotShiftTheImage()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            // A white square on black: pixels 24..39 in both directions, so its centre lies between pixels 31 and 32.
            var input = new Color[Size * Size];
            for (int y = 24; y < 40; y++)
            {
                for (int x = 24; x < 40; x++)
                {
                    input[y * Size + x] = Color.White;
                }
            }

            var output = RunFxaa(input, Size, Size);
            Assert.Equal(Size * Size, output.Length);

            double sum = 0.0;
            double sumX = 0.0;
            double sumY = 0.0;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    double level = output[y * Size + x].R;
                    sum += level;
                    sumX += level * x;
                    sumY += level * y;
                }
            }

            double centroidX = sumX / sum;
            double centroidY = sumY / sum;
            _output.WriteLine($"Centroid of the square after FXAA: ({centroidX:F3}, {centroidY:F3}), expected (31.5, 31.5).");

            Assert.InRange(centroidX, 31.4, 31.6);
            Assert.InRange(centroidY, 31.4, 31.6);
            Assert.True(output[31 * Size + 31].R >= 250);
            Assert.True(output[32 * Size + 32].R >= 250);
            Assert.True(output[10 * Size + 10].R <= 1);
        });
    }

    [GpuFact]
    public void TheDraw_PutsTheDeviceStatesBack_AndCountsItself()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var device = GpuDeviceHost.Instance.GraphicsDevice;
            var effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.FxaaContentName);
            using var spriteBatch = new SpriteBatch(device);
            var renderer = new FxaaRenderer(device, spriteBatch);
            var shader = new ShaderWrapper(effect);

            using var source = new Texture2D(device, 16, 16);
            using var target = new RenderTarget2D(device, 16, 16);
            device.SetRenderTarget(target);
            device.Viewport = new Viewport(0, 0, 16, 16);

            device.BlendState = BlendState.Additive;
            device.DepthStencilState = DepthStencilState.DepthRead;
            device.RasterizerState = RasterizerState.CullClockwise;
            device.SamplerStates[0] = SamplerState.AnisotropicWrap;

            renderer.Draw(shader, source, 16, 16);

            Assert.Same(BlendState.Additive, device.BlendState);
            Assert.Same(DepthStencilState.DepthRead, device.DepthStencilState);
            Assert.Same(RasterizerState.CullClockwise, device.RasterizerState);
            Assert.Same(SamplerState.AnisotropicWrap, device.SamplerStates[0]);
            Assert.Equal(1, renderer.DrawCount);

            device.SetRenderTarget(null);
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullCounterClockwise;
            device.SamplerStates[0] = SamplerState.LinearWrap;
        });
    }

    [GpuFact]
    public void TheFxaaPass_RunsOnlyInTheViewThatComposesItsInterfaceAfter()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var scope = new PipelineScope(32, 24);
            var sceneView = scope.AddView(32, 24, pipeline: null, Fxaa());
            scope.Pipeline.Render(new[] { sceneView });

            Assert.Equal(1, sceneView.RenderStats.FxaaPasses);
            Assert.Equal(1, scope.Pipeline.FxaaDrawCount);

            // A pipeline that renders the whole view at once has no scene phase: the interface would be filtered with it.
            var wholeView = scope.AddView(32, 24, new WholeViewPipeline(), Fxaa());
            scope.Pipeline.Render(new[] { wholeView });

            Assert.Equal(0, wholeView.RenderStats.FxaaPasses);
            Assert.Equal(1, scope.Pipeline.FxaaDrawCount);
            Assert.Equal(1, scope.Pipeline.FxaaTargetCreationCount);

            // Without the setting the pass does not run in a scene view either.
            var plainView = scope.AddView(32, 24, pipeline: null, AntiAliasingMode.None);
            scope.Pipeline.Render(new[] { plainView });

            Assert.Equal(0, plainView.RenderStats.FxaaPasses);
            Assert.Equal(1, scope.Pipeline.FxaaDrawCount);
        });
    }

    [GpuFact]
    public void TheFxaaTarget_IsReleasedAfterTheSizeChanges_AndTheBloomCountersDoNotMove()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var scope = new PipelineScope(32, 24);
            var viewA = scope.AddView(32, 24, pipeline: null, Fxaa());
            scope.Pipeline.Render(new[] { viewA });

            Assert.Equal(1, scope.Pipeline.FxaaTargetCreationCount);
            Assert.Equal(1, scope.Pipeline.FxaaDrawCount);

            // The scene view at size A is no longer rendered; a view of another size renders more often than the eviction delay.
            var viewB = scope.AddView(40, 30, pipeline: null, AntiAliasingMode.None);
            for (int render = 0; render < HdrTargetEviction.MaxIdleRenders + 2; render++)
            {
                scope.Pipeline.Render(new[] { viewB });
            }

            // Nothing free of the FXAA key (A, Color, no depth) is left in the shared pool: the sweep disposed it.
            Assert.Equal(0, scope.Pool.TrimFree(32, 24, SurfaceFormat.Color, DepthFormat.None));
            Assert.Equal(1, scope.Pipeline.FxaaTargetDisposalCount);
            Assert.Equal(0, scope.Pipeline.BloomTargetCreationCount);
            Assert.Equal(0, scope.Pipeline.BloomTargetDisposalCount);
        });
    }

    private static AntiAliasingMode Fxaa() => AntiAliasingMode.Fxaa;

    /// <summary>Draws <paramref name="pixels"/> through the FXAA pass 1:1 into a target of the same size and reads the result back.</summary>
    private static Color[] RunFxaa(Color[] pixels, int width, int height)
    {
        var device = GpuDeviceHost.Instance.GraphicsDevice;
        var effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.FxaaContentName);
        using var spriteBatch = new SpriteBatch(device);
        var renderer = new FxaaRenderer(device, spriteBatch);
        var shader = new ShaderWrapper(effect);

        using var source = new Texture2D(device, width, height);
        source.SetData(pixels);
        using var target = new RenderTarget2D(device, width, height);

        device.SetRenderTarget(target);
        device.Viewport = new Viewport(0, 0, width, height);
        device.Clear(new Color(255, 0, 255, 255));
        renderer.Draw(shader, source, width, height);
        device.SetRenderTarget(null);

        Assert.Equal(width, target.Width);
        Assert.Equal(height, target.Height);

        var output = new Color[width * height];
        target.GetData(output);
        return output;
    }

    private static int CountIntermediate(Color[] pixels)
    {
        int count = 0;
        foreach (var pixel in pixels)
        {
            if (pixel.R is >= 32 and <= 223)
            {
                count++;
            }
        }

        return count;
    }

    private sealed class WholeViewPipeline : IViewRenderPipeline
    {
        public void RenderView(GraphicsDevice graphicsDevice, RenderView view, in RenderFrame frame, IReadOnlyList<IViewFlushableRenderer> renderers)
        {
        }
    }

    /// <summary>A render pipeline with the tonemap and FXAA effects, on a private pool installed as the shared one for the test.</summary>
    private sealed class PipelineScope : IDisposable
    {
        private readonly RenderTargetPool _previousShared;
        private readonly SpriteBatch _spriteBatch;
        private readonly List<IDisposable> _surfaces = new();

        public PipelineScope(int width, int height)
        {
            var device = GpuDeviceHost.Instance.GraphicsDevice;
            _previousShared = RenderTargetPool.Shared;
            Pool = new RenderTargetPool(device);
            RenderTargetPool.Shared = Pool;

            _spriteBatch = new SpriteBatch(device);
            Pipeline = new RenderPipeline(device, Array.Empty<IViewFlushableRenderer>(), _spriteBatch);
            var content = GpuDeviceHost.Instance.Game.Content;
            Pipeline.SetTonemapEffect(content.Load<Effect>(BuiltInShaderCatalog.TonemapContentName));
            Pipeline.SetFxaaEffect(content.Load<Effect>(BuiltInShaderCatalog.FxaaContentName));
        }

        public RenderTargetPool Pool { get; }

        public RenderPipeline Pipeline { get; }

        public RenderView AddView(int width, int height, IViewRenderPipeline? pipeline, AntiAliasingMode antiAliasing)
        {
            var device = GpuDeviceHost.Instance.GraphicsDevice;
            var surface = new RenderTargetSurface(device, width, height, SurfaceFormat.Color, DepthFormat.Depth24, Pool);
            _surfaces.Add(surface);

            var world = new World();
            world.EnvironmentSettings.ColorPipeline.Mode = ColorPipelineMode.LinearHdr;
            world.EnvironmentSettings.ColorPipeline.AntiAliasing = antiAliasing;
            world.EnvironmentSettings.MarkDirty();

            var camera = new PerspectiveCameraComponent();
            camera.SetPositionAndTarget(new Vector3(0.0f, 0.0f, -5.0f), Vector3.Zero);
            camera.OnScreenResized(width, height);
            camera.FarPlane = 1000.0f;
            camera.NearPlane = 1.0f;

            var view = new RenderView(world, camera, surface);
            if (pipeline != null)
            {
                view.Pipeline = pipeline;
            }

            return view;
        }

        public void Dispose()
        {
            foreach (var surface in _surfaces)
            {
                surface.Dispose();
            }

            Pipeline.Dispose();
            _spriteBatch.Dispose();
            RenderTargetPool.Shared = _previousShared;
            Pool.Dispose();
        }
    }
}
