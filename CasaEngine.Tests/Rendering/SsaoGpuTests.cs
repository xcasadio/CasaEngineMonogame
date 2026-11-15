using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.PostProcess;
using CasaEngine.Framework.Rendering.Shaders;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using Xunit;
using Xunit.Abstractions;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// The screen-space ambient occlusion on a real device (plan T3.3, ADR-0115): a flat depth is not occluded, a depth step is occluded on
/// its hollow side, the sky is left alone, the composition multiplies the scene target and puts the device states back; and the
/// pipeline composes it only in a view whose scene depth pre-pass is active, counts it per view and evicts its targets after a size
/// change. Skipped where no device can be created.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class SsaoGpuTests
{
    private const int Size = 64;

    // A 60 degree vertical field of view and a square target: 1 / M11 = 1 / M22 = tan(30 degrees).
    private static readonly float ProjectionScale = MathF.Tan(MathF.PI / 6.0f);

    private readonly ITestOutputHelper _output;

    public SsaoGpuTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [GpuFact]
    public void AFlatDepth_IsNotOccluded_AtAnyPixel()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var rig = new Rig(Size, Size);
            var occlusion = rig.Run((_, _) => 20.0f, radius: 0.5f, bias: 0.02f, intensity: 1.0f);

            float minimum = occlusion.Min();
            _output.WriteLine($"Flat depth 20: minimum occlusion {minimum:F4} over {occlusion.Length} pixels.");
            Assert.True(minimum >= 0.98f, $"a plane facing the camera must stay open, minimum {minimum}");
        });
    }

    [GpuFact]
    public void ASlantedPlane_IsNotOccludedByItself()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var rig = new Rig(Size, Size);

            // For a plane, the inverse of the view depth is linear in the screen coordinates: a plane that leans away to the right.
            var occlusion = rig.Run((x, _) => 1.0f / (1.0f / 20.0f + 0.0003f * (x - 32)), radius: 0.5f, bias: 0.02f, intensity: 1.0f);

            float minimum = occlusion.Min();
            _output.WriteLine($"Slanted plane: minimum occlusion {minimum:F4}.");
            Assert.True(minimum >= 0.95f, $"a plane must not shadow itself, minimum {minimum}");
        });
    }

    [GpuFact]
    public void ADepthStep_IsOccludedOnItsHollowSideNearTheEdge_AndOpenAwayFromIt()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var rig = new Rig(Size, Size);

            // Left half: a back plane at 12 units. Right half: a nearer face at 10 units. The back plane meets the near face in a corner.
            var occlusion = rig.Run((x, _) => x < 32 ? 12.0f : 10.0f, radius: 3.0f, bias: 0.02f, intensity: 1.0f);

            float nearEdge = occlusion[32 * Size + 30];
            float farAway = occlusion[32 * Size + 3];
            float nearFace = occlusion[32 * Size + 40];
            _output.WriteLine($"Step: hollow side near the edge {nearEdge:F4}, far side away from the edge {farAway:F4}, near face {nearFace:F4}.");

            Assert.True(nearEdge < 0.8f, $"the hollow side near the edge must be occluded, got {nearEdge}");
            Assert.True(farAway >= 0.98f, $"the back plane away from the edge stays open, got {farAway}");
            Assert.True(nearFace >= 0.95f, $"the near face is not occluded by the plane behind it, got {nearFace}");
        });
    }

    [GpuFact]
    public void TheSky_IsNeverOccluded_AndGeometryNextToItIsNotDarkenedByIt()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var rig = new Rig(Size, Size);

            // Left half: nothing drawn (the background constant of the pre-pass). Right half: a plane at 10 units.
            var occlusion = rig.Run((x, _) => x < 32 ? SceneDepthPrepass.BackgroundDepth : 10.0f, radius: 3.0f, bias: 0.02f, intensity: 1.0f);

            float skyMinimum = 1.0f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    skyMinimum = MathF.Min(skyMinimum, occlusion[y * Size + x]);
                }
            }

            float besideSky = occlusion[32 * Size + 33];
            _output.WriteLine($"Sky: minimum occlusion {skyMinimum:F5}; geometry beside the sky {besideSky:F4}.");
            Assert.True(skyMinimum >= 0.9999f);
            Assert.True(besideSky >= 0.95f, $"geometry next to the sky is not occluded by it, got {besideSky}");

            // The scene target of a sky-only image is left as it was.
            var scene = rig.ReadScene();
            Assert.InRange(scene[32 * Size + 3].X, 1.99f, 2.01f);
            Assert.InRange(scene[32 * Size + 3].Y, 0.499f, 0.501f);
            Assert.InRange(scene[32 * Size + 3].Z, 7.99f, 8.01f);
        });
    }

    [GpuFact]
    public void TheComposition_MultipliesTheSceneByLerpOneOcclusionIntensity_AndKeepsItsAlpha()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var rig = new Rig(Size, Size);
            const float intensity = 0.5f;
            var occlusion = rig.Run((x, _) => x < 32 ? 12.0f : 10.0f, radius: 3.0f, bias: 0.02f, intensity: intensity);
            var scene = rig.ReadScene();

            int checkedPixels = 0;
            int darkened = 0;
            for (int y = 8; y < Size - 8; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float factor = 1.0f + intensity * (occlusion[y * Size + x] - 1.0f);
                    var pixel = scene[y * Size + x];
                    Assert.InRange(pixel.X, 2.0f * factor - 0.02f, 2.0f * factor + 0.02f);
                    Assert.InRange(pixel.Y, 0.5f * factor - 0.005f, 0.5f * factor + 0.005f);
                    Assert.InRange(pixel.Z, 8.0f * factor - 0.08f, 8.0f * factor + 0.08f);
                    Assert.InRange(pixel.W, 0.999f, 1.001f);
                    checkedPixels++;
                    if (factor < 0.95f)
                    {
                        darkened++;
                    }
                }
            }

            _output.WriteLine($"Composition: {checkedPixels} pixels match scene * lerp(1, occlusion, {intensity}); {darkened} of them darkened by more than 5 %.");
            Assert.True(darkened > 20, "the test image must exercise a real darkening");
        });
    }

    [GpuFact]
    public void ZeroIntensity_LeavesTheSceneTargetUnchanged()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var rig = new Rig(Size, Size);
            rig.Run((x, _) => x < 32 ? 12.0f : 10.0f, radius: 3.0f, bias: 0.02f, intensity: 0.0f);

            foreach (var pixel in rig.ReadScene())
            {
                Assert.Equal(2.0f, pixel.X, 3);
                Assert.Equal(0.5f, pixel.Y, 3);
                Assert.Equal(8.0f, pixel.Z, 3);
                Assert.Equal(1.0f, pixel.W, 3);
            }
        });
    }

    [GpuFact]
    public void TwoRuns_OnTheSameDepth_GiveTheSameOcclusion()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var rig = new Rig(Size, Size);
            var first = rig.Run((x, y) => x < 32 ? 12.0f : 10.0f + 0.01f * y, radius: 3.0f, bias: 0.02f, intensity: 1.0f);
            var second = rig.Run((x, y) => x < 32 ? 12.0f : 10.0f + 0.01f * y, radius: 3.0f, bias: 0.02f, intensity: 1.0f);

            Assert.Equal(first, second);
        });
    }

    [GpuFact]
    public void TheRender_PutsTheTargetTheViewportAndTheStatesBack_AndCountsItself()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var rig = new Rig(Size, Size);
            var device = GpuDeviceHost.Instance.GraphicsDevice;

            device.SetRenderTarget(rig.Scene);
            device.Viewport = new Viewport(3, 5, 20, 10);
            device.BlendState = BlendState.Additive;
            device.DepthStencilState = DepthStencilState.DepthRead;
            device.RasterizerState = RasterizerState.CullClockwise;
            device.SamplerStates[0] = SamplerState.AnisotropicWrap;

            rig.Renderer.Render(rig.Scene, rig.UploadDepth((_, _) => 20.0f), rig.Occlusion, rig.Partner, rig.Shader, ProjectionScale, ProjectionScale, 0.5f, 0.02f, 1.0f);

            Assert.Same(rig.Scene, device.GetRenderTargets()[0].RenderTarget);
            Assert.Equal(new Viewport(3, 5, 20, 10).Bounds, device.Viewport.Bounds);
            Assert.Same(BlendState.Additive, device.BlendState);
            Assert.Same(DepthStencilState.DepthRead, device.DepthStencilState);
            Assert.Same(RasterizerState.CullClockwise, device.RasterizerState);
            Assert.Same(SamplerState.AnisotropicWrap, device.SamplerStates[0]);
            Assert.Equal(1, rig.Renderer.PassCount);

            device.SetRenderTarget(null);
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullCounterClockwise;
            device.SamplerStates[0] = SamplerState.LinearWrap;
        });
    }

    // ---- The pipeline ----

    [GpuFact]
    public void ThePipeline_ComposesTheOcclusionInASceneView_AndCountsItPerView()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var scope = new PipelineScope();
            var view = scope.AddView(32, 24, pipeline: null, ssao: true);
            scope.Pipeline.Render(new[] { view });

            Assert.Equal(1, view.RenderStats.SsaoPasses);
            Assert.Equal(1, scope.Pipeline.SsaoDrawCount);
            Assert.Equal(2, scope.Pipeline.SsaoTargetCreationCount);

            // The count belongs to the view and the frame: the next frame starts from zero.
            view.RenderStats.Reset();
            Assert.Equal(0, view.RenderStats.SsaoPasses);
        });
    }

    [GpuFact]
    public void AViewThatRendersSceneAndInterfaceInOneGo_WithSsaoOn_ReceivesNoComposition()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var scope = new PipelineScope();

            // The control: a scene view with the same settings is composed.
            var sceneView = scope.AddView(32, 24, pipeline: null, ssao: true);
            scope.Pipeline.Render(new[] { sceneView });
            Assert.Equal(1, sceneView.RenderStats.SsaoPasses);
            Assert.Equal(1, scope.Pipeline.SsaoDrawCount);
            Assert.Equal(2, scope.Pipeline.SsaoTargetCreationCount);

            // A pipeline that renders the whole view at once (the editor's OverlayViewPipeline is one) has its interface in the HDR
            // target: the composition would darken it, so it has neither pre-pass nor composition.
            var wholeView = scope.AddView(32, 24, new WholeViewPipeline(), ssao: true);
            scope.Pipeline.Render(new[] { wholeView });

            Assert.Equal(0, wholeView.RenderStats.SsaoPasses);
            Assert.Equal(1, scope.Pipeline.SsaoDrawCount);
            Assert.Equal(2, scope.Pipeline.SsaoTargetCreationCount);
            Assert.Equal(1, scope.Pipeline.SceneDepthPrepassCount);
        });
    }

    [GpuFact]
    public void WithoutSsaoOnAnOrthographicOrALegacyView_NoCompositionAndNoTargetAreMade()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var scope = new PipelineScope();

            var off = scope.AddView(32, 24, pipeline: null, ssao: false);
            var orthographic = scope.AddView(32, 24, pipeline: null, ssao: true, perspective: false);
            var legacy = scope.AddView(32, 24, pipeline: null, ssao: true, mode: ColorPipelineMode.Legacy);
            scope.Pipeline.Render(new[] { off, orthographic, legacy });

            Assert.Equal(0, off.RenderStats.SsaoPasses);
            Assert.Equal(0, orthographic.RenderStats.SsaoPasses);
            Assert.Equal(0, legacy.RenderStats.SsaoPasses);
            Assert.Equal(0, scope.Pipeline.SsaoDrawCount);
            Assert.Equal(0, scope.Pipeline.SsaoTargetCreationCount);
        });
    }

    [GpuFact]
    public void AZeroIntensity_OrAMissingShader_SkipsTheComposition()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var scope = new PipelineScope();
            var flat = scope.AddView(32, 24, pipeline: null, ssao: true, intensity: 0.0f);
            scope.Pipeline.Render(new[] { flat });
            Assert.Equal(0, flat.RenderStats.SsaoPasses);
            Assert.Equal(0, scope.Pipeline.SsaoTargetCreationCount);

            using var withoutShader = new PipelineScope(loadSsaoShader: false);
            var view = withoutShader.AddView(32, 24, pipeline: null, ssao: true);
            withoutShader.Pipeline.Render(new[] { view });
            Assert.Equal(0, view.RenderStats.SsaoPasses);
            Assert.Equal(0, withoutShader.Pipeline.SsaoTargetCreationCount);
            Assert.Equal(1, withoutShader.Pipeline.SceneDepthPrepassCount);
        });
    }

    [GpuFact]
    public void TwoViewsInTheSameFrame_EachGetTheirOwnCompositionCount()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var scope = new PipelineScope();
            var left = scope.AddView(32, 24, pipeline: null, ssao: true);
            var right = scope.AddView(32, 24, pipeline: null, ssao: true);
            var plain = scope.AddView(32, 24, pipeline: null, ssao: false);
            scope.Pipeline.Render(new[] { left, right, plain });

            Assert.Equal(1, left.RenderStats.SsaoPasses);
            Assert.Equal(1, right.RenderStats.SsaoPasses);
            Assert.Equal(0, plain.RenderStats.SsaoPasses);
            Assert.Equal(2, scope.Pipeline.SsaoDrawCount);

            // The first view released its two targets before the second one asked.
            Assert.Equal(2, scope.Pipeline.SsaoTargetCreationCount);
        });
    }

    [GpuFact]
    public void TheSsaoTargets_AreReleasedAfterTheSizeChanges_AndTheBloomAndFxaaCountersDoNotMove()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var scope = new PipelineScope();
            var viewA = scope.AddView(32, 24, pipeline: null, ssao: true);
            scope.Pipeline.Render(new[] { viewA });

            Assert.Equal(2, scope.Pipeline.SsaoTargetCreationCount);
            Assert.Equal(1, scope.Pipeline.SsaoDrawCount);

            // The view at size A is no longer rendered; a view of another size renders more often than the eviction delay.
            var viewB = scope.AddView(40, 30, pipeline: null, ssao: false);
            for (int render = 0; render < HdrTargetEviction.MaxIdleRenders + 2; render++)
            {
                scope.Pipeline.Render(new[] { viewB });
            }

            // Nothing free of the SSAO key (A, Single, no depth) is left in the shared pool: the sweep disposed both targets.
            Assert.Equal(0, scope.Pool.TrimFree(32, 24, SurfaceFormat.Single, DepthFormat.None));
            Assert.Equal(2, scope.Pipeline.SsaoTargetDisposalCount);

            // The pre-pass target of the same view has its own key and its own counter; the bloom and FXAA ones never moved.
            Assert.Equal(1, scope.Pipeline.SceneDepthTargetDisposalCount);
            Assert.Equal(0, scope.Pipeline.BloomTargetCreationCount);
            Assert.Equal(0, scope.Pipeline.BloomTargetDisposalCount);
            Assert.Equal(0, scope.Pipeline.FxaaTargetCreationCount);
            Assert.Equal(0, scope.Pipeline.FxaaTargetDisposalCount);
        });
    }

    private sealed class WholeViewPipeline : IViewRenderPipeline
    {
        public void RenderView(GraphicsDevice graphicsDevice, RenderView view, in RenderFrame frame, IReadOnlyList<IViewFlushableRenderer> renderers)
        {
        }
    }

    /// <summary>
    /// The renderer on its own: a depth texture filled by a function of the pixel, the two one-channel targets and a scene target
    /// cleared to (2, 0.5, 8, 1).
    /// </summary>
    private sealed class Rig : IDisposable
    {
        private readonly GraphicsDevice _device = GpuDeviceHost.Instance.GraphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly List<IDisposable> _owned = new();

        public Rig(int width, int height)
        {
            Width = width;
            Height = height;
            _spriteBatch = new SpriteBatch(_device);
            Renderer = new SsaoRenderer(_device, _spriteBatch);
            Shader = new ShaderWrapper(GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.SsaoContentName));
            Occlusion = Own(new RenderTarget2D(_device, width, height, false, SurfaceFormat.Single, DepthFormat.None, 0, RenderTargetUsage.PreserveContents));
            Partner = Own(new RenderTarget2D(_device, width, height, false, SurfaceFormat.Single, DepthFormat.None, 0, RenderTargetUsage.PreserveContents));
            Scene = Own(new RenderTarget2D(_device, width, height, false, SurfaceFormat.HalfVector4, DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.PreserveContents));
        }

        public int Width { get; }

        public int Height { get; }

        public SsaoRenderer Renderer { get; }

        public ShaderWrapper Shader { get; }

        public RenderTarget2D Occlusion { get; }

        public RenderTarget2D Partner { get; }

        public RenderTarget2D Scene { get; }

        public Texture2D UploadDepth(Func<int, int, float> depth)
        {
            var values = new float[Width * Height];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    values[y * Width + x] = depth(x, y);
                }
            }

            var texture = Own(new Texture2D(_device, Width, Height, false, SurfaceFormat.Single));
            texture.SetData(values);
            return texture;
        }

        /// <summary>Clears the scene target, runs the renderer on <paramref name="depth"/> and returns the blurred occlusion.</summary>
        public float[] Run(Func<int, int, float> depth, float radius, float bias, float intensity)
        {
            var texture = UploadDepth(depth);

            _device.SetRenderTarget(Scene);
            _device.Viewport = new Viewport(0, 0, Width, Height);
            _device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, new Vector4(2.0f, 0.5f, 8.0f, 1.0f), 1.0f, 0);

            Renderer.Render(Scene, texture, Occlusion, Partner, Shader, ProjectionScale, ProjectionScale, radius, bias, intensity);

            _device.SetRenderTarget(null);

            var occlusion = new float[Width * Height];
            Occlusion.GetData(occlusion);
            return occlusion;
        }

        public Vector4[] ReadScene()
        {
            var packed = new HalfVector4[Width * Height];
            Scene.GetData(packed);
            var result = new Vector4[packed.Length];
            for (int i = 0; i < packed.Length; i++)
            {
                result[i] = packed[i].ToVector4();
            }

            return result;
        }

        public void Dispose()
        {
            foreach (var owned in _owned)
            {
                owned.Dispose();
            }

            _spriteBatch.Dispose();
        }

        private T Own<T>(T value) where T : IDisposable
        {
            _owned.Add(value);
            return value;
        }
    }

    /// <summary>A render pipeline with the tonemap and SSAO effects (no renderer: an empty scene), on a private pool installed as the shared one.</summary>
    private sealed class PipelineScope : IDisposable
    {
        private readonly RenderTargetPool _previousShared;
        private readonly SpriteBatch _spriteBatch;
        private readonly List<IDisposable> _surfaces = new();

        public PipelineScope(bool loadSsaoShader = true)
        {
            var device = GpuDeviceHost.Instance.GraphicsDevice;
            _previousShared = RenderTargetPool.Shared;
            Pool = new RenderTargetPool(device);
            RenderTargetPool.Shared = Pool;

            _spriteBatch = new SpriteBatch(device);
            Pipeline = new RenderPipeline(device, Array.Empty<IViewFlushableRenderer>(), _spriteBatch);
            var content = GpuDeviceHost.Instance.Game.Content;
            Pipeline.SetTonemapEffect(content.Load<Effect>(BuiltInShaderCatalog.TonemapContentName));
            if (loadSsaoShader)
            {
                Pipeline.SetSsaoEffect(content.Load<Effect>(BuiltInShaderCatalog.SsaoContentName));
            }
        }

        public RenderTargetPool Pool { get; }

        public RenderPipeline Pipeline { get; }

        public RenderView AddView(
            int width,
            int height,
            IViewRenderPipeline? pipeline,
            bool ssao,
            bool perspective = true,
            ColorPipelineMode mode = ColorPipelineMode.LinearHdr,
            float intensity = ColorPipelineSettings.DefaultSsaoIntensity)
        {
            var device = GpuDeviceHost.Instance.GraphicsDevice;
            var surface = new RenderTargetSurface(device, width, height, SurfaceFormat.Color, DepthFormat.Depth24, Pool);
            _surfaces.Add(surface);

            var world = new World();
            world.EnvironmentSettings.ColorPipeline.Mode = mode;
            world.EnvironmentSettings.ColorPipeline.SsaoEnabled = ssao;
            world.EnvironmentSettings.ColorPipeline.SsaoIntensity = intensity;
            world.EnvironmentSettings.MarkDirty();

            CameraComponent camera;
            if (perspective)
            {
                var arcBall = new ArcBallCameraComponent();
                arcBall.OnScreenResized(width, height);
                camera = arcBall;
            }
            else
            {
                var orthographic = new Camera2dComponent();
                orthographic.OnScreenResized(width, height);
                camera = orthographic;
            }

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
