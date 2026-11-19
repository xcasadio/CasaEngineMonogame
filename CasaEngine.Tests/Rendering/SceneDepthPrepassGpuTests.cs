using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Materials.Runtime;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Draw;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Rendering.PostProcess;
using CasaEngine.Framework.Rendering.Shaders;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using Xunit;
using Xunit.Abstractions;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// The scene depth pre-pass on a real device (plan T3.2, ADR-0116): the static pass and the skinned draw (both skinning modes) write
/// the view depth of a plane at a known distance within 1 %, a cut-out leaves the background constant in its holes, the pass draws
/// exactly what the opaque pass draws (not the shadow casters), the device is given back as found, and the pipeline activates the
/// pre-pass for a view only when its four conditions hold (SSAO on, HDR view, perspective projection, scene and interface separated)
/// with its target evicted after a size change. Skipped where no device can be created.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class SceneDepthPrepassGpuTests
{
    private const int Size = 64;
    private const float Background = SceneDepthPrepass.BackgroundDepth;

    private readonly ITestOutputHelper _output;

    public SceneDepthPrepassGpuTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ---- The static pass ----

    [GpuFact]
    public void AStaticPlane_WritesItsViewDepthWithinOnePercent_AndTheRestOfTheTargetKeepsTheBackgroundConstant()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var rig = new Rig();
            var material = new LitDiffuseMaterial { RasterizerState = RasterizerState.CullNone };

            foreach (float distance in new[] { 10.0f, 50.0f, 500.0f })
            {
                rig.Begin();
                var items = new List<RenderItem> { rig.StaticItem(rig.Quad(material, left: true), material, distance) };
                rig.RunStaticPass(items);

                var depth = rig.ReadDepth();
                float worst = 0.0f;
                int covered = 0;
                int untouched = 0;
                for (int y = 4; y < Size - 4; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        float value = depth[y * Size + x];
                        if (x < Size / 2 - 2)
                        {
                            worst = MathF.Max(worst, MathF.Abs(value - distance) / distance);
                            covered++;
                        }
                        else if (x > Size / 2 + 2)
                        {
                            Assert.Equal(Background, value);
                            untouched++;
                        }
                    }
                }

                _output.WriteLine($"Plane at {distance}: {covered} covered pixels, worst error {worst * 100.0f:F4} %, {untouched} background pixels.");
                Assert.True(worst <= 0.01f, $"a plane at {distance} units reads back with {worst * 100.0f:F3} % error");
                Assert.Equal(1, rig.Stats.SceneDepthStaticDraws);
            }
        });
    }

    [GpuFact]
    public void ACutOut_LeavesTheBackgroundConstantInItsHoles_AndDrawsTheRestAtItsDepth()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var rig = new Rig();

            // Left texel opaque, right texel transparent: the right half of a full-screen plane is cut out. Point sampling, clamp.
            using var texture = new Texture2D(rig.Device, 2, 1);
            texture.SetData(new[] { new Color(255, 255, 255, 255), new Color(255, 255, 255, 0) });
            var material = new LitDiffuseMaterial
            {
                Queue = RenderQueue.AlphaTest,
                AlphaCutoff = 0.5f,
                BasColor = texture,
                RasterizerState = RasterizerState.CullNone,
                SamplerState = SamplerState.PointClamp,
            };
            Assert.Same(texture, material.ShadowMaskTexture);

            rig.Begin();
            rig.RunStaticPass(new List<RenderItem> { rig.StaticItem(rig.Quad(material, left: false), material, 20.0f) });

            var depth = rig.ReadDepth();
            int cutOut = 0;
            int drawn = 0;
            for (int y = 4; y < Size - 4; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float value = depth[y * Size + x];
                    if (x < Size / 2 - 3)
                    {
                        Assert.True(MathF.Abs(value - 20.0f) <= 0.2f, $"({x},{y}) outside the hole reads {value}");
                        drawn++;
                    }
                    else if (x > Size / 2 + 3)
                    {
                        Assert.Equal(Background, value);
                        cutOut++;
                    }
                }
            }

            _output.WriteLine($"Cut-out: {drawn} pixels at 20 units, {cutOut} pixels of the hole at the background constant {Background}.");
            Assert.True(drawn > 500 && cutOut > 500);
        });
    }

    [GpuFact]
    public void TheStaticDrawCount_EqualsTheOpaqueItemsOfTheOpaquePass_AndIsNotTheShadowCasterCount()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var rig = new Rig();
            var device = rig.Device;

            using var texture = new Texture2D(device, 1, 1);
            texture.SetData(new[] { Color.White });

            LitDiffuseMaterial Opaque() => new() { RasterizerState = RasterizerState.CullNone };
            var alphaTest = new LitDiffuseMaterial
            {
                Queue = RenderQueue.AlphaTest, BasColor = texture, RasterizerState = RasterizerState.CullNone,
            };
            var transparent = new LitDiffuseMaterial { Queue = RenderQueue.Transparent, RasterizerState = RasterizerState.CullNone };
            var noShadowMaterial = new LitDiffuseMaterial { CastShadows = false, RasterizerState = RasterizerState.CullNone };

            var quad = rig.Quad(Opaque(), left: true);
            var bufferless = new StaticModelMesh();

            var items = new List<RenderItem>();
            var plain = rig.StaticItem(quad, Opaque(), 10.0f);
            var componentNoShadow = rig.StaticItem(quad, Opaque(), 11.0f);
            componentNoShadow.ComponentCastShadows = false;
            var materialNoShadow = rig.StaticItem(quad, noShadowMaterial, 12.0f);
            var cutOut = rig.StaticItem(quad, alphaTest, 13.0f);
            var transparentItem = rig.StaticItem(quad, transparent, 14.0f);
            var withoutBuffers = rig.StaticItem(quad, Opaque(), 15.0f);
            withoutBuffers.Mesh = bufferless;
            var instanced = rig.StaticItem(quad, Opaque(), 16.0f);
            instanced.Features = ShaderFeature.Instanced;
            items.AddRange(new[] { plain, componentNoShadow, materialNoShadow, cutOut, transparentItem, withoutBuffers, instanced });
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                item.SortKey = SortKeyGenerator.Generate(item.Queue, 0, item.Material.Id.GetHashCode(), i, 0.0f);
                item.SubmissionIndex = i;
                items[i] = item;
            }

            items.Sort(RenderItemOrder.Comparison);

            // The shadow pass would keep only what casts a shadow and is below the transparent queue.
            int shadowCasters = 0;
            foreach (var item in items)
            {
                if (item.EffectiveCastShadows && item.Queue < RenderQueue.Transparent && item.Mesh.VertexBuffer is not null)
                {
                    shadowCasters++;
                }
            }

            // The real opaque pass, with the legacy lit effect as the only shader.
            var litForward = new ShaderWrapper(GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.LitForwardContentName));
            var opaqueStats = new RenderStats();
            rig.Begin();
            var opaqueContext = rig.Context(opaqueStats);
            opaqueContext.Lighting = new LightingContext();
            opaqueContext.SceneDepth = null;
            new OpaquePass().Execute(opaqueContext, items, new RenderStateCache(), new ShaderBindCache(), new RenderShaderSelector(litForward));

            rig.RunStaticPass(items);

            _output.WriteLine($"Opaque pass items {opaqueStats.OpaqueItems}, pre-pass static draws {rig.Stats.SceneDepthStaticDraws}, shadow casters {shadowCasters}.");
            Assert.Equal(4, opaqueStats.OpaqueItems);
            Assert.Equal(opaqueStats.OpaqueItems, rig.Stats.SceneDepthStaticDraws);

            // An object that casts no shadow still occludes: the shadow filter would have given a smaller count.
            Assert.True(shadowCasters < rig.Stats.SceneDepthStaticDraws, $"shadow casters {shadowCasters}");
        });
    }

    [GpuFact]
    public void WithoutAnActivePrepass_ThePassDrawsNothingAndTouchesNothing()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var rig = new Rig();
            var material = new LitDiffuseMaterial { RasterizerState = RasterizerState.CullNone };
            var items = new List<RenderItem> { rig.StaticItem(rig.Quad(material, left: true), material, 10.0f) };

            // Bound scene target, never activated: the pre-pass is inactive.
            rig.Device.SetRenderTarget(rig.Hdr);
            rig.Device.Viewport = new Viewport(0, 0, Size, Size);
            Assert.False(rig.Prepass.IsActive);

            rig.RunStaticPass(items);
            Assert.Equal(0, rig.Stats.SceneDepthStaticDraws);
            Assert.Equal(0, rig.Stats.DrawCalls);
            Assert.Same(rig.Hdr, rig.Device.GetRenderTargets()[0].RenderTarget);

            // Activated but without the effect: nothing either.
            rig.Begin();
            rig.Pass.SceneDepthShader = null;
            rig.RunStaticPass(items);
            Assert.Equal(0, rig.Stats.SceneDepthStaticDraws);
            Assert.All(rig.ReadDepth(), value => Assert.Equal(Background, value));
        });
    }

    [GpuFact]
    public void TheDraw_PutsTheTargetTheViewportTheScissorAndTheStatesBack_AndTheSceneTargetIsUntouched()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var rig = new Rig();
            var device = rig.Device;
            var material = new LitDiffuseMaterial { RasterizerState = RasterizerState.CullClockwise, SamplerState = SamplerState.LinearWrap };
            using var texture = new Texture2D(device, 1, 1);
            texture.SetData(new[] { Color.White });
            material.BasColor = texture;
            material.Queue = RenderQueue.AlphaTest;
            var items = new List<RenderItem> { rig.StaticItem(rig.Quad(material, left: true), material, 10.0f) };

            rig.Begin();
            var viewport = new Viewport(3, 4, 50, 40);
            var scissor = new Rectangle(1, 2, 30, 20);
            device.Viewport = viewport;
            device.ScissorRectangle = scissor;
            device.BlendState = BlendState.Additive;
            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullClockwise;
            device.SamplerStates[0] = SamplerState.PointWrap;

            rig.RunStaticPass(items);

            Assert.Equal(1, rig.Stats.SceneDepthStaticDraws);
            Assert.Equal(1, device.RenderTargetCount);
            Assert.Same(rig.Hdr, device.GetRenderTargets()[0].RenderTarget);
            Assert.Equal(viewport.Bounds, device.Viewport.Bounds);
            Assert.Equal(scissor, device.ScissorRectangle);
            Assert.Same(BlendState.Additive, device.BlendState);
            Assert.Same(DepthStencilState.None, device.DepthStencilState);
            Assert.Same(RasterizerState.CullClockwise, device.RasterizerState);
            Assert.Same(SamplerState.PointWrap, device.SamplerStates[0]);

            // The scene target keeps its clear colour everywhere: the depth went to the pre-pass target only.
            device.SetRenderTarget(null);
            var scene = new HalfVector4[Size * Size];
            rig.Hdr.GetData(scene);
            foreach (var texel in scene)
            {
                var v = texel.ToVector4();
                Assert.True(MathF.Abs(v.X - 0.25f) < 0.01f && MathF.Abs(v.Y - 0.5f) < 0.01f && MathF.Abs(v.Z - 0.75f) < 0.01f);
            }
        });
    }

    // ---- The skinned draw ----

    [GpuFact]
    public void ASkinnedPlane_WritesItsViewDepthWithinOnePercent_InBothSkinningModes()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var rig = new Rig();
            var effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.SkinEffectContentName);
            var shader = new ShaderWrapper(effect);
            Assert.NotNull(effect.Techniques[SceneDepthPrepass.SkinnedTechniqueName]);
            Assert.NotNull(effect.Techniques[SceneDepthPrepass.SkinnedDualQuaternionTechniqueName]);

            using var texture = new Texture2D(rig.Device, 1, 1);
            texture.SetData(new[] { Color.White });

            foreach (var mode in new[] { SkinningMode.LinearBlend, SkinningMode.DualQuaternion })
            {
                foreach (float distance in new[] { 10.0f, 80.0f })
                {
                    var mesh = SkinnedQuad(rig.Device, texture, left: true);
                    var pose = new TestPose(distance);

                    rig.Begin();
                    rig.RunSkinned(shader, mesh, pose, mode, texture, alphaCutoff: 0.0f);

                    var depth = rig.ReadDepth();
                    float worst = 0.0f;
                    for (int y = 4; y < Size - 4; y++)
                    {
                        for (int x = 0; x < Size; x++)
                        {
                            float value = depth[y * Size + x];
                            if (x < Size / 2 - 2)
                            {
                                worst = MathF.Max(worst, MathF.Abs(value - distance) / distance);
                            }
                            else if (x > Size / 2 + 2)
                            {
                                Assert.Equal(Background, value);
                            }
                        }
                    }

                    _output.WriteLine($"Skinned {mode} plane at {distance}: worst error {worst * 100.0f:F4} %.");
                    Assert.True(worst <= 0.01f, $"{mode}: a plane at {distance} units reads back with {worst * 100.0f:F3} % error");
                    Assert.Equal(1, rig.Stats.SceneDepthSkinnedDraws);
                }
            }
        });
    }

    [GpuFact]
    public void ASkinnedCutOut_LeavesTheBackgroundConstantInItsHoles_InBothSkinningModes()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var rig = new Rig();
            var shader = new ShaderWrapper(GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.SkinEffectContentName));
            using var texture = new Texture2D(rig.Device, 2, 1);
            texture.SetData(new[] { new Color(255, 255, 255, 255), new Color(255, 255, 255, 0) });

            foreach (var mode in new[] { SkinningMode.LinearBlend, SkinningMode.DualQuaternion })
            {
                var mesh = SkinnedQuad(rig.Device, texture, left: false);

                rig.Begin();
                rig.RunSkinned(shader, mesh, new TestPose(30.0f), mode, texture, alphaCutoff: 0.5f, samplerState: SamplerState.PointClamp);

                var depth = rig.ReadDepth();
                int holes = 0;
                for (int y = 4; y < Size - 4; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        float value = depth[y * Size + x];
                        if (x < Size / 2 - 3)
                        {
                            Assert.True(MathF.Abs(value - 30.0f) <= 0.3f, $"{mode} ({x},{y}) reads {value}");
                        }
                        else if (x > Size / 2 + 3)
                        {
                            Assert.Equal(Background, value);
                            holes++;
                        }
                    }
                }

                Assert.True(holes > 500);
            }
        });
    }

    [GpuFact]
    public void TheStaticAndSkinnedDepths_ShareOneDepthBuffer_SoTheNearerSurfaceWins()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var rig = new Rig();
            var shader = new ShaderWrapper(GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.SkinEffectContentName));
            using var texture = new Texture2D(rig.Device, 1, 1);
            texture.SetData(new[] { Color.White });
            var material = new LitDiffuseMaterial { RasterizerState = RasterizerState.CullNone };

            // A static plane at 20 over the whole width, then a skinned plane at 40 behind it over the left half: the pre-pass of the
            // skinned renderer comes after the static one in the same flush, and must lose the depth test where the static is nearer.
            rig.Begin();
            rig.RunStaticPass(new List<RenderItem> { rig.StaticItem(rig.Quad(material, left: false), material, 20.0f) });
            rig.RunSkinned(shader, SkinnedQuad(rig.Device, texture, left: true), new TestPose(40.0f), SkinningMode.LinearBlend, texture, 0.0f);

            var depth = rig.ReadDepth();
            Assert.True(MathF.Abs(depth[(Size / 2) * Size + 8] - 20.0f) <= 0.2f, "the static plane stays in front of the farther skinned one");

            // Reversed: the skinned plane at 10 is nearer than the static one at 20 and wins on the left half only.
            rig.Begin();
            rig.RunStaticPass(new List<RenderItem> { rig.StaticItem(rig.Quad(material, left: false), material, 20.0f) });
            rig.RunSkinned(shader, SkinnedQuad(rig.Device, texture, left: true), new TestPose(10.0f), SkinningMode.LinearBlend, texture, 0.0f);

            depth = rig.ReadDepth();
            Assert.True(MathF.Abs(depth[(Size / 2) * Size + 8] - 10.0f) <= 0.1f, "the nearer skinned plane wins on the left half");
            Assert.True(MathF.Abs(depth[(Size / 2) * Size + 56] - 20.0f) <= 0.2f, "the static plane stays on the right half");
        });
    }

    // ---- The pipeline ----

    [GpuFact]
    public void ThePipeline_ActivatesThePrepassForAPerspectiveSceneView_AndTheRendererDrawsItsDepthWithinOnePercent()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var scope = new PipelineScope();
            var view = scope.AddView(48, 32, pipeline: null, ssao: true, perspective: true);
            scope.Pipeline.Render(new[] { view });

            Assert.True(scope.Recorder.Seen, "the renderer sees an active pre-pass");
            Assert.Equal(0.0f, scope.Recorder.ProjectionM44);
            Assert.Equal(1, view.RenderStats.SceneDepthStaticDraws);
            Assert.Equal(1, scope.Pipeline.SceneDepthPrepassCount);
            Assert.Equal(1, scope.Pipeline.SceneDepthTargetCreationCount);

            float center = scope.Recorder.CenterDepth;
            _output.WriteLine($"Pipeline view: centre depth {center} for a plane at {DepthRecorder.Distance}.");
            Assert.True(MathF.Abs(center - DepthRecorder.Distance) / DepthRecorder.Distance <= 0.01f);

            // The state does not leak into the next view: SSAO off, the renderer sees an inactive pre-pass and nothing is acquired.
            var next = scope.AddView(48, 32, pipeline: null, ssao: false, perspective: true);
            scope.Pipeline.Render(new[] { next });
            Assert.False(scope.Recorder.Seen);
            Assert.Equal(0, next.RenderStats.SceneDepthStaticDraws);
            Assert.Equal(1, scope.Pipeline.SceneDepthPrepassCount);
        });
    }

    [GpuFact]
    public void WithSsaoOff_NoPrepassTargetIsAcquiredAndNothingIsDrawn()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var scope = new PipelineScope();
            var view = scope.AddView(48, 32, pipeline: null, ssao: false, perspective: true);
            scope.Pipeline.Render(new[] { view });

            Assert.False(scope.Recorder.Seen);
            Assert.Equal(0, view.RenderStats.SceneDepthStaticDraws);
            Assert.Equal(0, view.RenderStats.SceneDepthSkinnedDraws);
            Assert.Equal(0, scope.Pipeline.SceneDepthPrepassCount);
            Assert.Equal(0, scope.Pipeline.SceneDepthTargetCreationCount);
            Assert.Equal(0, scope.Pool.TrimFree(48, 32, SurfaceFormat.Single, DepthFormat.Depth24));
        });
    }

    [GpuFact]
    public void AnOrthographicView_WithSsaoOn_DoesNotActivateThePrepass()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var scope = new PipelineScope();
            var view = scope.AddView(48, 32, pipeline: null, ssao: true, perspective: false);
            scope.Pipeline.Render(new[] { view });

            Assert.True(scope.Recorder.Flushed, "the scene was rendered");
            Assert.Equal(1.0f, scope.Recorder.ProjectionM44);
            Assert.False(scope.Recorder.Seen);
            Assert.Equal(0, view.RenderStats.SceneDepthStaticDraws);
            Assert.Equal(0, scope.Pipeline.SceneDepthPrepassCount);
            Assert.Equal(0, scope.Pipeline.SceneDepthTargetCreationCount);
        });
    }

    [GpuFact]
    public void AViewThatRendersSceneAndInterfaceInOneGo_WithSsaoOn_DoesNotActivateThePrepass()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var scope = new PipelineScope();

            // The control: the same view with a scene pipeline does activate it.
            var sceneView = scope.AddView(48, 32, pipeline: null, ssao: true, perspective: true);
            scope.Pipeline.Render(new[] { sceneView });
            Assert.True(scope.Recorder.Seen);
            Assert.Equal(1, scope.Pipeline.SceneDepthTargetCreationCount);

            // A pipeline that is not an IScenePhaseViewPipeline (the editor's OverlayViewPipeline is one) flushes the renderers and
            // draws the interface in the same call: its interface lives in the HDR target, which SSAO would darken.
            var wholeView = scope.AddView(48, 32, new FlushingWholeViewPipeline(), ssao: true, perspective: true);
            scope.Pipeline.Render(new[] { wholeView });

            Assert.True(scope.Recorder.Flushed, "the renderers were flushed in that view");
            Assert.False(scope.Recorder.Seen, "but the pre-pass was not active");
            Assert.Equal(0, wholeView.RenderStats.SceneDepthStaticDraws);
            Assert.Equal(0, wholeView.RenderStats.SceneDepthSkinnedDraws);
            Assert.Equal(1, scope.Pipeline.SceneDepthPrepassCount);
            Assert.Equal(1, scope.Pipeline.SceneDepthTargetCreationCount);
            Assert.Equal(0, scope.Pipeline.SceneDepthTargetDisposalCount);
        });
    }

    [GpuFact]
    public void ALegacyView_WithSsaoOn_DoesNotActivateThePrepass()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var scope = new PipelineScope();
            var view = scope.AddView(48, 32, pipeline: null, ssao: true, perspective: true, mode: ColorPipelineMode.Legacy);
            scope.Pipeline.Render(new[] { view });

            Assert.True(scope.Recorder.Flushed);
            Assert.False(scope.Recorder.Seen);
            Assert.Equal(0, scope.Pipeline.SceneDepthPrepassCount);
            Assert.Equal(0, scope.Pipeline.SceneDepthTargetCreationCount);
        });
    }

    [GpuFact]
    public void TwoViewsRenderedInTheSameFrame_EachGetTheirOwnPrepassAndCounts_ReusingOneTarget()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var scope = new PipelineScope();
            var left = scope.AddView(48, 32, pipeline: null, ssao: true, perspective: true);
            var right = scope.AddView(48, 32, pipeline: null, ssao: true, perspective: true);
            scope.Pipeline.Render(new[] { left, right });

            Assert.Equal(1, left.RenderStats.SceneDepthStaticDraws);
            Assert.Equal(1, right.RenderStats.SceneDepthStaticDraws);
            Assert.Equal(2, scope.Pipeline.SceneDepthPrepassCount);

            // The first view released its target before the second one asked: one target serves both.
            Assert.Equal(1, scope.Pipeline.SceneDepthTargetCreationCount);
        });
    }

    [GpuFact]
    public void TheSceneDepthTarget_IsReleasedAfterTheSizeChanges_AndTheBloomAndFxaaCountersDoNotMove()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var scope = new PipelineScope();
            var viewA = scope.AddView(48, 32, pipeline: null, ssao: true, perspective: true);
            scope.Pipeline.Render(new[] { viewA });

            Assert.Equal(1, scope.Pipeline.SceneDepthTargetCreationCount);
            Assert.Equal(1, scope.Pipeline.SceneDepthPrepassCount);

            // The view at size A is no longer rendered; a view of another size renders more often than the eviction delay.
            var viewB = scope.AddView(40, 30, pipeline: null, ssao: false, perspective: true);
            for (int render = 0; render < HdrTargetEviction.MaxIdleRenders + 2; render++)
            {
                scope.Pipeline.Render(new[] { viewB });
            }

            // Nothing free of the pre-pass key (A, Single, Depth24) is left in the shared pool: the sweep disposed it.
            Assert.Equal(0, scope.Pool.TrimFree(48, 32, SurfaceFormat.Single, DepthFormat.Depth24));
            Assert.Equal(1, scope.Pipeline.SceneDepthTargetDisposalCount);
            Assert.Equal(0, scope.Pipeline.BloomTargetCreationCount);
            Assert.Equal(0, scope.Pipeline.BloomTargetDisposalCount);
            Assert.Equal(0, scope.Pipeline.FxaaTargetCreationCount);
            Assert.Equal(0, scope.Pipeline.FxaaTargetDisposalCount);
        });
    }

    // ---- Helpers ----

    // The test device has the Reach profile, which has no 32-bit indices (StaticModelMesh.Initialize makes them): the buffers are
    // built here with 16-bit indices and handed to the mesh, whose buffer properties have a private setter.
    private static void InitializeWithShortIndices(StaticModelMesh mesh, GraphicsDevice device)
    {
        var vertices = mesh.GetVertices().ToArray();
        var indices = mesh.GetIndices();
        var vertexBuffer = new VertexBuffer(device, typeof(VertexPositionNormalTexture), vertices.Length, BufferUsage.None);
        vertexBuffer.SetData(vertices);
        var indexBuffer = new IndexBuffer(device, IndexElementSize.SixteenBits, indices.Count, BufferUsage.None);
        indexBuffer.SetData(indices.Select(static index => (ushort)index).ToArray());
        SetPrivateProperty(mesh, "VertexBuffer", vertexBuffer);
        SetPrivateProperty(mesh, "IndexBuffer", indexBuffer);
    }

    private static void SetPrivateProperty(object target, string name, object value)
    {
        target.GetType().GetProperty(name)!.GetSetMethod(nonPublic: true)!.Invoke(target, new[] { value });
    }

    private static RiggedModel.RiggedModelMesh SkinnedQuad(GraphicsDevice device, Texture2D texture, bool left)
    {
        float x0 = -100.0f;
        float x1 = left ? 0.0f : 100.0f;
        VertexPositionTextureNormalTangentWeights Vertex(float x, float y, float u, float v) => new()
        {
            Position = new Vector3(x, y, 0.0f),
            Color = Vector4.One,
            Normal = Vector3.UnitZ,
            TextureCoordinate = new Vector2(u, v),
            Tangent = Vector3.UnitX,
            BiTangent = Vector3.UnitY,
            BlendIndices = Vector4.Zero,
            BlendWeights = new Vector4(1.0f, 0.0f, 0.0f, 0.0f),
        };

        var mesh = new RiggedModel.RiggedModelMesh
        {
            Texture = texture,
            Vertices = new[]
            {
                Vertex(x0, -100.0f, 0.0f, 1.0f), Vertex(x0, 100.0f, 0.0f, 0.0f),
                Vertex(x1, 100.0f, 1.0f, 0.0f), Vertex(x1, -100.0f, 1.0f, 1.0f),
            },
            Indices = new[] { 0, 1, 2, 0, 2, 3 },
        };

        // The test device has the Reach profile, which has no 32-bit indices: the buffers are made here with 16-bit ones, and the
        // mesh keeps them (its Initialize returns early when valid buffers of the same device are there).
        var vertexBuffer = new VertexBuffer(device, VertexPositionTextureNormalTangentWeights.VertexDeclaration, mesh.Vertices.Length, BufferUsage.None);
        vertexBuffer.SetData(mesh.Vertices);
        var indexBuffer = new IndexBuffer(device, IndexElementSize.SixteenBits, mesh.Indices.Length, BufferUsage.None);
        indexBuffer.SetData(Array.ConvertAll(mesh.Indices, static index => (ushort)index));
        SetPrivateProperty(mesh, "VertexBuffer", vertexBuffer);
        SetPrivateProperty(mesh, "IndexBuffer", indexBuffer);
        return mesh;
    }

    /// <summary>A skinned pose that puts bone 0 at (0, 0, -distance) in both skinning modes, with a rigid palette elsewhere.</summary>
    private sealed class TestPose : ISkinnedMeshPoseProvider
    {
        public TestPose(float distance)
        {
            SkinningPalette = new Matrix[128];
            for (int i = 0; i < SkinningPalette.Length; i++)
            {
                SkinningPalette[i] = Matrix.Identity;
            }

            SkinningPalette[0] = Matrix.CreateTranslation(0.0f, 0.0f, -distance);

            DualQuaternionSkinningPalette = new Vector4[256];
            for (int i = 0; i < 128; i++)
            {
                DualQuaternionSkinningPalette[i * 2] = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
                DualQuaternionSkinningPalette[i * 2 + 1] = Vector4.Zero;
            }

            // A translation t is the dual part (t / 2, 0) of a unit real part (0, 0, 0, 1).
            DualQuaternionSkinningPalette[1] = new Vector4(0.0f, 0.0f, -distance * 0.5f, 0.0f);
        }

        public Matrix[] SkinningPalette { get; }

        public Vector4[] DualQuaternionSkinningPalette { get; }

        public bool CanUseDualQuaternionSkinning => true;

        public VertexBuffer GetVertexBufferOverride(RiggedModel.RiggedModelMesh mesh, GraphicsDevice graphicsDevice, VertexDeclaration vertexDeclaration) => null;

        public Matrix GetMeshNodeTransform(RiggedModel.RiggedModelMesh mesh) => Matrix.Identity;
    }

    /// <summary>
    /// A device-level rig: a scene target (HalfVector4, Depth24Stencil8, as the HDR target of a view), a pre-pass target (Single,
    /// Depth24), a perspective camera of 60 degrees looking down -Z from the origin, and the static pass with its effect.
    /// </summary>
    private sealed class Rig : IDisposable
    {
        private readonly Matrix _view = Matrix.CreateLookAt(Vector3.Zero, -Vector3.UnitZ, Vector3.Up);
        private readonly Matrix _projection = Matrix.CreatePerspectiveFieldOfView(MathF.PI / 3.0f, 1.0f, 0.5f, 5000.0f);

        public Rig()
        {
            Device = GpuDeviceHost.Instance.GraphicsDevice;
            Hdr = new RenderTarget2D(Device, Size, Size, false, SurfaceFormat.HalfVector4, DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.PreserveContents);
            Depth = new RenderTarget2D(Device, Size, Size, false, SurfaceFormat.Single, DepthFormat.Depth24, 0, RenderTargetUsage.PreserveContents);
            Pass = new SceneDepthPass
            {
                SceneDepthShader = new ShaderWrapper(GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.SceneDepthContentName)),
            };
        }

        public GraphicsDevice Device { get; }

        public RenderTarget2D Hdr { get; }

        public RenderTarget2D Depth { get; }

        public SceneDepthPrepass Prepass { get; } = new();

        public SceneDepthPass Pass { get; }

        public RenderStats Stats { get; } = new();

        /// <summary>Binds the scene target, clears it, resets the counters and activates the pre-pass on its target.</summary>
        public void Begin()
        {
            Prepass.Deactivate();
            Stats.Reset();
            Device.SetRenderTarget(Hdr);
            Device.Viewport = new Viewport(0, 0, Size, Size);
            Device.BlendState = BlendState.Opaque;
            Device.DepthStencilState = DepthStencilState.Default;
            Device.RasterizerState = RasterizerState.CullCounterClockwise;
            Device.ScissorRectangle = new Rectangle(0, 0, Size, Size);
            Device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, new Vector4(0.25f, 0.5f, 0.75f, 1.0f), 1.0f, 0);
            Assert.True(Prepass.Activate(Device, Depth));
            Assert.Same(Hdr, Device.GetRenderTargets()[0].RenderTarget);
        }

        public RenderContext Context(RenderStats stats) => new()
        {
            Device = Device,
            Frame = new RenderFrame(_view, _projection, Vector3.Zero, new Rectangle(0, 0, Size, Size)),
            SceneDepth = Prepass,
            Stats = stats,
        };

        public StaticModelMesh Quad(MaterialBase material, bool left)
        {
            float x1 = left ? 0.0f : 1.0f;
            var mesh = new StaticModelMesh { Material = material };
            mesh.SetData(
                new[]
                {
                    new VertexPositionNormalTexture(new Vector3(-1.0f, -1.0f, 0.0f), Vector3.UnitZ, new Vector2(0.0f, 1.0f)),
                    new VertexPositionNormalTexture(new Vector3(-1.0f, 1.0f, 0.0f), Vector3.UnitZ, new Vector2(0.0f, 0.0f)),
                    new VertexPositionNormalTexture(new Vector3(x1, 1.0f, 0.0f), Vector3.UnitZ, new Vector2(1.0f, 0.0f)),
                    new VertexPositionNormalTexture(new Vector3(x1, -1.0f, 0.0f), Vector3.UnitZ, new Vector2(1.0f, 1.0f)),
                },
                new uint[] { 0, 1, 2, 0, 2, 3 });
            InitializeWithShortIndices(mesh, Device);
            return mesh;
        }

        /// <summary>A quad item at <paramref name="distance"/> in front of the camera, large enough to cover the whole view when full.</summary>
        public RenderItem StaticItem(StaticModelMesh mesh, MaterialBase material, float distance)
        {
            // The quad is 2 units wide: scale it so that it spans well over the field of view at that distance.
            float scale = distance * 2.0f;
            return new RenderItem
            {
                Mesh = mesh,
                Material = material,
                ComponentCastShadows = true,
                ComponentReceiveShadows = true,
                World = Matrix.CreateScale(scale) * Matrix.CreateTranslation(0.0f, 0.0f, -distance),
                WorldInverseTranspose = Matrix.Identity,
            };
        }

        public void RunStaticPass(List<RenderItem> items)
        {
            Pass.Execute(Context(Stats), items, new RenderStateCache(), new ShaderBindCache(), null!);
        }

        public void RunSkinned(
            ShaderWrapper shader,
            RiggedModel.RiggedModelMesh mesh,
            ISkinnedMeshPoseProvider pose,
            SkinningMode mode,
            Texture2D texture,
            float alphaCutoff,
            SamplerState? samplerState = null)
        {
            Assert.True(Prepass.BeginDraw(Device));
            try
            {
                var context = Context(Stats);
                SkinnedMeshRendererComponent.DrawSceneDepthMesh(
                    shader, mesh, Matrix.Identity, pose, mode, texture, alphaCutoff,
                    RasterizerState.CullNone, samplerState ?? SamplerState.PointClamp, in context);
            }
            finally
            {
                Prepass.EndDraw(Device);
            }
        }

        public float[] ReadDepth()
        {
            Device.SetRenderTarget(null);
            var values = new float[Size * Size];
            Depth.GetData(values);
            return values;
        }

        public void Dispose()
        {
            Prepass.Deactivate();
            Device.SetRenderTarget(null);
            Hdr.Dispose();
            Depth.Dispose();
        }
    }

    /// <summary>
    /// A renderer that does what the static renderer does for the pre-pass: when the frame carries an active pre-pass it runs the
    /// static pass on one plane at <see cref="Distance"/> in front of the camera, and it reads the centre of the pre-pass target back.
    /// </summary>
    private sealed class DepthRecorder : IViewFlushableRenderer
    {
        public const float Distance = 25.0f;

        private readonly GraphicsDevice _device;
        private readonly SceneDepthPass _pass;
        private readonly LitDiffuseMaterial _material = new() { RasterizerState = RasterizerState.CullNone };
        private readonly StaticModelMesh _mesh = new();

        public DepthRecorder(GraphicsDevice device)
        {
            _device = device;
            _pass = new SceneDepthPass
            {
                SceneDepthShader = new ShaderWrapper(GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.SceneDepthContentName)),
            };
            _mesh.Material = _material;
            _mesh.SetData(
                new[]
                {
                    new VertexPositionNormalTexture(new Vector3(-1.0f, -1.0f, 0.0f), Vector3.UnitZ, new Vector2(0.0f, 1.0f)),
                    new VertexPositionNormalTexture(new Vector3(-1.0f, 1.0f, 0.0f), Vector3.UnitZ, new Vector2(0.0f, 0.0f)),
                    new VertexPositionNormalTexture(new Vector3(1.0f, 1.0f, 0.0f), Vector3.UnitZ, new Vector2(1.0f, 0.0f)),
                    new VertexPositionNormalTexture(new Vector3(1.0f, -1.0f, 0.0f), Vector3.UnitZ, new Vector2(1.0f, 1.0f)),
                },
                new uint[] { 0, 1, 2, 0, 2, 3 });
            InitializeWithShortIndices(_mesh, device);
        }

        public bool Flushed { get; private set; }

        public bool Seen { get; private set; }

        public float ProjectionM44 { get; private set; }

        public float CenterDepth { get; private set; }

        public void Flush(in RenderFrame frame, RenderStats stats = null)
        {
            Flushed = true;
            ProjectionM44 = frame.Projection.M44;
            var prepass = frame.SceneDepth;
            Seen = prepass is { IsActive: true };
            if (!Seen)
            {
                return;
            }

            // The plane sits Distance in front of the camera whatever the camera is: world = T(0, 0, -d) * inverse(view).
            var item = new RenderItem
            {
                Mesh = _mesh,
                Material = _material,
                ComponentCastShadows = true,
                World = Matrix.CreateScale(Distance * 3.0f) * Matrix.CreateTranslation(0.0f, 0.0f, -Distance) * Matrix.Invert(frame.View),
            };
            var context = new RenderContext { Device = _device, Frame = frame, SceneDepth = prepass, Stats = stats };
            _pass.Execute(context, new List<RenderItem> { item }, new RenderStateCache(), new ShaderBindCache(), null!);

            var target = prepass.Target;
            var values = new float[target.Width * target.Height];
            target.GetData(values);
            CenterDepth = values[(target.Height / 2) * target.Width + target.Width / 2];
        }
    }

    /// <summary>A pipeline that renders the whole view at once (the editor's OverlayViewPipeline is one): not an <see cref="IScenePhaseViewPipeline"/>.</summary>
    private sealed class FlushingWholeViewPipeline : IViewRenderPipeline
    {
        public void RenderView(GraphicsDevice graphicsDevice, RenderView view, in RenderFrame frame, IReadOnlyList<IViewFlushableRenderer> renderers)
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                renderers[i].Flush(in frame, view.RenderStats);
            }
        }
    }

    /// <summary>A render pipeline with the tonemap effect and a recording renderer, on a private pool installed as the shared one.</summary>
    private sealed class PipelineScope : IDisposable
    {
        private readonly RenderTargetPool _previousShared;
        private readonly SpriteBatch _spriteBatch;
        private readonly List<IDisposable> _surfaces = new();

        public PipelineScope()
        {
            var device = GpuDeviceHost.Instance.GraphicsDevice;
            _previousShared = RenderTargetPool.Shared;
            Pool = new RenderTargetPool(device);
            RenderTargetPool.Shared = Pool;

            Recorder = new DepthRecorder(device);
            _spriteBatch = new SpriteBatch(device);
            Pipeline = new RenderPipeline(device, new IViewFlushableRenderer[] { Recorder }, _spriteBatch);
            Pipeline.SetTonemapEffect(GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.TonemapContentName));
        }

        public RenderTargetPool Pool { get; }

        public RenderPipeline Pipeline { get; }

        public DepthRecorder Recorder { get; }

        public RenderView AddView(
            int width,
            int height,
            IViewRenderPipeline? pipeline,
            bool ssao,
            bool perspective,
            ColorPipelineMode mode = ColorPipelineMode.LinearHdr)
        {
            var device = GpuDeviceHost.Instance.GraphicsDevice;
            var surface = new RenderTargetSurface(device, width, height, SurfaceFormat.Color, DepthFormat.Depth24, Pool);
            _surfaces.Add(surface);

            var world = new World();
            world.EnvironmentSettings.ColorPipeline.Mode = mode;
            world.EnvironmentSettings.ColorPipeline.SsaoEnabled = ssao;
            world.EnvironmentSettings.MarkDirty();

            CameraComponent camera;
            if (perspective)
            {
                var perspectiveCamera = new PerspectiveCameraComponent();
                perspectiveCamera.SetPositionAndTarget(new Vector3(0.0f, 0.0f, -5.0f), Vector3.Zero);
                perspectiveCamera.OnScreenResized(width, height);
                camera = perspectiveCamera;
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
