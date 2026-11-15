using CasaEngine.Shaders;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using Xunit;
using Xunit.Abstractions;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// The facts the scene depth pre-pass and the SSAO of the plan (ai-agent/tasks/pbr-clearcoat-ssao-fxaa-tasks.md, T3.1) rest on, measured
/// on a real device with a throwaway shader compiled for the OpenGL profile: a floating-point target is created, cleared to a large
/// value, written with the view depth (the clip-space w of a perspective projection, in world units), read back by the CPU and by a
/// <c>tex2D</c> in a pixel shader, and it keeps its colour and its depth buffer when the device binds another target and comes back.
/// The multiplicative full-screen draw the SSAO composes with is checked on an HDR-like target too. The pre-pass of T3.2 and the
/// composition of T3.3 are built on these results; a change of the format, of the clear, of the preservation or of the blend fails
/// here first.
/// Skipped where no device can be created.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class SceneDepthTargetGpuTests
{
    private const int Size = 64;
    private const float Background = 60000.0f;
    private const float FieldOfView = (MathF.PI / 3.0f);
    private const float NearPlane = 0.5f;
    private const float FarPlane = 5000.0f;

    // One shader, both uses: the depth technique writes the clip-space w; the read technique is a pixel-shader-only effect for
    // SpriteBatch (the pattern of Tonemap.fx) that reads the depth texture with tex2D and divides it by 100.
    private const string ShaderSource = @"
#if OPENGL
#define VS_MODEL vs_3_0
#define PS_MODEL ps_3_0
#else
#define VS_MODEL vs_4_0
#define PS_MODEL ps_4_0
#endif

float4x4 WorldViewProj;

Texture2D DepthTexture;
sampler2D DepthTextureSampler = sampler_state { Texture = <DepthTexture>; };

struct VsInput { float4 Position : POSITION0; };
struct VsOutput { float4 Position : SV_POSITION; float ViewDepth : TEXCOORD0; };

VsOutput VsDepth(VsInput input)
{
    VsOutput output;
    output.Position = mul(input.Position, WorldViewProj);
    output.ViewDepth = output.Position.w;
    return output;
}

float4 PsDepth(VsOutput input) : COLOR0
{
    return float4(input.ViewDepth, 0.0f, 0.0f, 1.0f);
}

struct PsReadInput
{
    float4 position : SV_POSITION;
    float4 color : COLOR0;
    float2 textureCoordinates : TEXCOORD0;
};

float4 PsRead(PsReadInput input) : COLOR0
{
    return float4(tex2D(DepthTextureSampler, input.textureCoordinates).r / 100.0f, 0.0f, 0.0f, 1.0f);
}

technique Depth { pass P0 { VertexShader = compile VS_MODEL VsDepth(); PixelShader = compile PS_MODEL PsDepth(); } }
float4 PsOcclude(PsReadInput input) : COLOR0
{
    return float4(0.5f, 0.5f, 0.5f, 0.0f);
}

technique Read { pass P0 { PixelShader = compile PS_MODEL PsRead(); } }
technique Occlude { pass P0 { PixelShader = compile PS_MODEL PsOcclude(); } }
";

    private readonly ITestOutputHelper _output;

    public SceneDepthTargetGpuTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [GpuFact]
    public void ASingleTarget_IsCreated_ClearedToTheBackgroundConstant_AndWrittenWithTheViewDepthWithinOnePercent()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var device = GpuDeviceHost.Instance.GraphicsDevice;
            using var effect = CompileEffect(device);

            foreach (var format in new[] { SurfaceFormat.Single, SurfaceFormat.HalfSingle, SurfaceFormat.HalfVector4, SurfaceFormat.Vector4 })
            {
                using var target = new RenderTarget2D(device, Size, Size, false, format, DepthFormat.Depth24, 0, RenderTargetUsage.PreserveContents);

                device.SetRenderTarget(target);
                device.Viewport = new Viewport(0, 0, Size, Size);
                device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, new Vector4(Background, 0.0f, 0.0f, 0.0f), 1.0f, 0);
                var cleared = ReadRed(target);
                device.SetRenderTarget(null);

                float worstClear = 0.0f;
                foreach (float value in cleared)
                {
                    worstClear = MathF.Max(worstClear, MathF.Abs(value - Background) / Background);
                }

                var worstByDistance = new List<string>();
                foreach (float distance in new[] { 1.0f, 5.0f, 50.0f, 500.0f, 4000.0f })
                {
                    DrawPlaneAt(device, effect, target, distance, Size, Size, clear: true);
                    var values = ReadRed(target);
                    device.SetRenderTarget(null);

                    float worst = 0.0f;
                    foreach (float value in values)
                    {
                        worst = MathF.Max(worst, MathF.Abs(value - distance) / distance);
                    }

                    worstByDistance.Add($"{distance}: {worst * 100.0f:F4} %");
                    Assert.True(worst <= 0.01f, $"{format}: a plane at {distance} units reads back with {worst * 100.0f:F3} % error");
                }

                _output.WriteLine($"{format}: clear to {Background} worst relative error {worstClear:E2}; plane worst relative error by distance: {string.Join(", ", worstByDistance)}.");
                Assert.True(worstClear <= 0.001f, $"{format}: the background constant {Background} is not kept by Clear (worst error {worstClear:E2})");
            }
        });
    }

    [GpuFact]
    public void ATiltedPlane_ReadsBackItsViewDepth_NotTheDistanceToTheEye_InEveryCandidateFormat()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var device = GpuDeviceHost.Instance.GraphicsDevice;
            _output.WriteLine($"Adapter: {device.Adapter.Description}, graphics profile {device.GraphicsProfile}.");
            using var effect = CompileEffect(device);

            // The plane z = -10 + 0.5 x, seen from the origin looking down -Z: a ray (dx, dy, -1) t meets it at t = 10 / (1 + 0.5 dx),
            // and t is the view depth (the distance along the axis), not the length of the ray. The values are not exact in a
            // half-precision format, so this is where the precision of each candidate format shows.
            var camera = BuildMatrices(Size, Size);

            // What the host tests and the SSAO reads of the projection: M44 is 0 for a perspective projection and 1 for an orthographic
            // one (the pre-pass is not run for the latter), M34 is -1 (so clip w is the view depth), and M11 and M22 give the view-space
            // position of a pixel from its depth: x = ndc.x * depth / M11, y = ndc.y * depth / M22.
            Assert.Equal(0.0f, camera.Projection.M44);
            Assert.Equal(-1.0f, camera.Projection.M34);
            Assert.Equal(1.0f, Matrix.CreateOrthographic(Size, Size, NearPlane, FarPlane).M44);
            Assert.Equal(1.0f / MathF.Tan(FieldOfView * 0.5f), camera.Projection.M22, 5);
            Assert.Equal(camera.Projection.M22, camera.Projection.M11, 5);
            Assert.Equal(0.0f, camera.Projection.M31);
            Assert.Equal(0.0f, camera.Projection.M32);
            var vertices = new[]
            {
                new VertexPosition(new Vector3(-30.0f, -30.0f, -10.0f - 15.0f)),
                new VertexPosition(new Vector3(30.0f, -30.0f, -10.0f + 15.0f)),
                new VertexPosition(new Vector3(30.0f, 30.0f, -10.0f + 15.0f)),
                new VertexPosition(new Vector3(-30.0f, 30.0f, -10.0f - 15.0f)),
            };

            foreach (var format in new[] { SurfaceFormat.Single, SurfaceFormat.HalfSingle, SurfaceFormat.HalfVector4, SurfaceFormat.Vector4 })
            {
                using var target = new RenderTarget2D(device, Size, Size, false, format, DepthFormat.Depth24, 0, RenderTargetUsage.PreserveContents);

                device.SetRenderTarget(target);
                device.Viewport = new Viewport(0, 0, Size, Size);
                device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, new Vector4(Background, 0.0f, 0.0f, 0.0f), 1.0f, 0);
                Draw(device, effect, camera.ViewProjection, vertices);
                var values = ReadRed(target);
                device.SetRenderTarget(null);

                float halfHeight = MathF.Tan(FieldOfView * 0.5f);
                float worst = 0.0f;
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        float dx = (2.0f * (x + 0.5f) / Size - 1.0f) * halfHeight;
                        float expected = 10.0f / (1.0f + 0.5f * dx);
                        worst = MathF.Max(worst, MathF.Abs(values[y * Size + x] - expected) / expected);
                    }
                }

                _output.WriteLine($"Tilted plane in {format}: {Size * Size} pixels, worst relative error {worst * 100.0f:F4} %.");
                Assert.True(worst <= 0.002f, $"{format}: the view depth of a tilted plane is off by {worst * 100.0f:F3} %");
            }
        });
    }

    [GpuFact]
    public void ATargetKeepsItsColourAndItsDepthBuffer_WhenAnotherTargetIsBoundAndTheDeviceComesBack()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var device = GpuDeviceHost.Instance.GraphicsDevice;
            using var effect = CompileEffect(device);

            // The pre-pass target (Single, Depth24) and an HDR-scene-like target (HalfVector4, Depth24Stencil8, what RenderPipeline
            // acquires for a LinearHdr view), both in the PreserveContents usage the pool gives. Each is the one that goes away and
            // comes back, the other being the target drawn in between.
            using var prepass = new RenderTarget2D(device, Size, Size, false, SurfaceFormat.Single, DepthFormat.Depth24, 0, RenderTargetUsage.PreserveContents);
            using var scene = new RenderTarget2D(device, Size, Size, false, SurfaceFormat.HalfVector4, DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.PreserveContents);

            RoundTrip(device, effect, prepass, scene);
            RoundTrip(device, effect, scene, prepass);
        });
    }

    private void RoundTrip(GraphicsDevice device, Effect effect, RenderTarget2D target, RenderTarget2D other)
    {
        var camera = BuildMatrices(Size, Size);

        // A near plane (10 units) on the left half only, then another target is bound and drawn, then the first one comes back
        // WITHOUT a clear and gets a far plane (20 units) on the whole screen: where the near plane is, the depth buffer must
        // still hold it, so the far plane must lose the depth test and the target must still read 10.
        device.SetRenderTarget(target);
        device.Viewport = new Viewport(0, 0, Size, Size);
        device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, new Vector4(Background, 0.0f, 0.0f, 0.0f), 1.0f, 0);
        Draw(device, effect, camera.ViewProjection, Plane(10.0f, left: true, right: false));

        device.SetRenderTarget(other);
        device.Viewport = new Viewport(0, 0, Size, Size);
        device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.Red, 1.0f, 0);
        Draw(device, effect, camera.ViewProjection, Plane(5.0f, left: true, right: true));

        device.SetRenderTarget(target);
        device.Viewport = new Viewport(0, 0, Size, Size);
        Draw(device, effect, camera.ViewProjection, Plane(20.0f, left: true, right: true));
        var values = ReadRed(target);
        device.SetRenderTarget(null);

        int leftAtNear = 0;
        int rightAtFar = 0;
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float value = values[y * Size + x];
                if (x < Size / 2 - 1 && MathF.Abs(value - 10.0f) < 0.1f)
                {
                    leftAtNear++;
                }
                else if (x > Size / 2 + 1 && MathF.Abs(value - 20.0f) < 0.2f)
                {
                    rightAtFar++;
                }
            }
        }

        int leftPixels = (Size / 2 - 1) * Size;
        int rightPixels = (Size - Size / 2 - 2) * Size;
        _output.WriteLine($"{target.Format}/{target.DepthStencilFormat} after the round trip: {leftAtNear} of {leftPixels} left pixels still read 10 (colour and depth kept), {rightAtFar} of {rightPixels} right pixels read 20.");
        Assert.Equal(leftPixels, leftAtNear);
        Assert.Equal(rightPixels, rightAtFar);
    }

    [GpuFact]
    public void ATex2DReadOfTheTarget_ReturnsTheStoredDepth_AtTheSamePixel_WithAPointSampler()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var device = GpuDeviceHost.Instance.GraphicsDevice;
            using var effect = CompileEffect(device);
            using var spriteBatch = new SpriteBatch(device);
            using var depth = new RenderTarget2D(device, Size, Size, false, SurfaceFormat.Single, DepthFormat.Depth24, 0, RenderTargetUsage.PreserveContents);
            using var read = new RenderTarget2D(device, Size, Size, false, SurfaceFormat.HalfVector4, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            var camera = BuildMatrices(Size, Size);

            // 20 units on the top half and 50 on the bottom half, so a vertical flip between the write and the read shows.
            device.SetRenderTarget(depth);
            device.Viewport = new Viewport(0, 0, Size, Size);
            device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, new Vector4(Background, 0.0f, 0.0f, 0.0f), 1.0f, 0);
            Draw(device, effect, camera.ViewProjection, HalfPlane(20.0f, top: true));
            Draw(device, effect, camera.ViewProjection, HalfPlane(50.0f, top: false));
            var written = ReadRed(depth);

            device.SetRenderTarget(read);
            device.Viewport = new Viewport(0, 0, Size, Size);
            effect.CurrentTechnique = effect.Techniques["Read"];
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, effect);
            spriteBatch.Draw(depth, new Rectangle(0, 0, Size, Size), Color.White);
            spriteBatch.End();
            var sampled = ReadRed(read);

            // Information only (no assertion): the same read through a linear sampler, to note whether a 32-bit float texture is
            // filterable here. The pre-pass and its blur read with a point sampler either way.
            device.SetRenderTarget(read);
            device.Viewport = new Viewport(0, 0, Size, Size);
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, effect);
            spriteBatch.Draw(depth, new Rectangle(0, 0, Size, Size), Color.White);
            spriteBatch.End();
            var sampledLinear = ReadRed(read);
            device.SetRenderTarget(null);
            _output.WriteLine($"Linear sampler on the Single target: top pixel {sampledLinear[8 * Size + 32] * 100.0f:F3}, bottom pixel {sampledLinear[(Size - 8) * Size + 32] * 100.0f:F3}, pixel on the 20/50 boundary row {sampledLinear[(Size / 2) * Size + 32] * 100.0f:F3} (point: {sampled[(Size / 2) * Size + 32] * 100.0f:F3}).");

            float worst = 0.0f;
            for (int i = 0; i < Size * Size; i++)
            {
                worst = MathF.Max(worst, MathF.Abs(sampled[i] * 100.0f - written[i]) / written[i]);
            }

            float top = sampled[8 * Size + 32] * 100.0f;
            float bottom = sampled[(Size - 8) * Size + 32] * 100.0f;
            _output.WriteLine($"tex2D read: top pixel {top:F3} (written {written[8 * Size + 32]:F3}), bottom pixel {bottom:F3} (written {written[(Size - 8) * Size + 32]:F3}), worst relative difference to the CPU readback {worst * 100.0f:E3} %.");
            Assert.Equal(20.0f, written[8 * Size + 32], 0.2f);
            Assert.Equal(50.0f, written[(Size - 8) * Size + 32], 0.5f);
            Assert.True(worst <= 0.01f, $"tex2D returns a different depth than the CPU readback ({worst * 100.0f:F3} %)");
        });
    }

    [GpuFact]
    public void AMultiplicativeFullScreenDraw_ScalesTheColourOfAnHdrTarget_AndLeavesItsAlphaAndDepthAlone()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var device = GpuDeviceHost.Instance.GraphicsDevice;
            using var effect = CompileEffect(device);
            using var spriteBatch = new SpriteBatch(device);
            using var white = new Texture2D(device, 1, 1);
            white.SetData(new[] { Color.White });
            using var scene = new RenderTarget2D(device, Size, Size, false, SurfaceFormat.HalfVector4, DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.PreserveContents);
            var camera = BuildMatrices(Size, Size);

            // The composition of the SSAO: the HDR scene (here 2, 0.5, 8 with alpha 1 and a plane at 10 units in its depth buffer) is
            // multiplied by the occlusion (here 0.5) with a blend of destination * source colour, with no depth test and no depth
            // write; the alpha stays and the depth buffer keeps the plane.
            var multiply = new BlendState
            {
                ColorSourceBlend = Blend.Zero,
                ColorDestinationBlend = Blend.SourceColor,
                AlphaSourceBlend = Blend.Zero,
                AlphaDestinationBlend = Blend.One,
            };

            device.SetRenderTarget(scene);
            device.Viewport = new Viewport(0, 0, Size, Size);
            device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, new Vector4(2.0f, 0.5f, 8.0f, 1.0f), 1.0f, 0);
            Draw(device, effect, camera.ViewProjection, Plane(10.0f, left: true, right: false));

            // The colour the plane wrote (its view depth in red, alpha 1) is multiplied by 0.5 in the three colour channels.
            var before = ReadVector4(scene);
            effect.CurrentTechnique = effect.Techniques["Occlude"];
            spriteBatch.Begin(SpriteSortMode.Immediate, multiply, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, effect);
            spriteBatch.Draw(white, new Rectangle(0, 0, Size, Size), Color.White);
            spriteBatch.End();
            var after = ReadVector4(scene);

            // A far plane afterwards must still lose against the near plane: the multiplicative draw did not touch the depth buffer.
            Draw(device, effect, camera.ViewProjection, Plane(20.0f, left: true, right: true));
            var final = ReadVector4(scene);
            device.SetRenderTarget(null);

            int left = 8 * Size + 8;
            int right = 8 * Size + Size - 8;
            _output.WriteLine($"Multiplicative draw: right pixel {before[right]} -> {after[right]}; left pixel {before[left]} -> {after[left]}; after the far plane: left {final[left].X:F2}, right {final[right].X:F2}.");
            AssertClose(new Vector4(1.0f, 0.25f, 4.0f, 1.0f), after[right], 0.01f);
            AssertClose(new Vector4(5.0f, 0.0f, 0.0f, 1.0f), after[left], 0.01f);
            Assert.Equal(5.0f, final[left].X, 0.05f);
            Assert.Equal(20.0f, final[right].X, 0.2f);
        });
    }

    private static void AssertClose(Vector4 expected, Vector4 actual, float tolerance)
    {
        Assert.True(
            MathF.Abs(expected.X - actual.X) <= tolerance && MathF.Abs(expected.Y - actual.Y) <= tolerance
            && MathF.Abs(expected.Z - actual.Z) <= tolerance && MathF.Abs(expected.W - actual.W) <= tolerance,
            $"expected {expected}, got {actual}");
    }

    private static Vector4[] ReadVector4(RenderTarget2D target)
    {
        var halves = new HalfVector4[target.Width * target.Height];
        target.GetData(halves);
        var result = new Vector4[halves.Length];
        for (int i = 0; i < halves.Length; i++)
        {
            result[i] = halves[i].ToVector4();
        }

        return result;
    }

    // ---- Helpers ----

    private static Effect CompileEffect(GraphicsDevice device)
    {
        string path = Path.Combine(Path.GetTempPath(), "scene-depth-trial-" + Guid.NewGuid().ToString("N") + ".fx");
        File.WriteAllText(path, ShaderSource);
        try
        {
            var compiled = ShaderCompiler.Compile(path, string.Empty, TargetPlatform.DesktopGL, EffectProcessorDebugMode.Optimize);
            Assert.True(compiled.ByteCode is { Length: > 0 }, "the trial shader did not compile: " + compiled.Logs);
            return new Effect(device, compiled.ByteCode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static (Matrix View, Matrix Projection, Matrix ViewProjection) BuildMatrices(int width, int height)
    {
        var view = Matrix.CreateLookAt(Vector3.Zero, new Vector3(0.0f, 0.0f, -1.0f), Vector3.Up);
        var projection = Matrix.CreatePerspectiveFieldOfView(FieldOfView, (float)width / height, NearPlane, FarPlane);
        return (view, projection, view * projection);
    }

    private static void DrawPlaneAt(GraphicsDevice device, Effect effect, RenderTarget2D target, float distance, int width, int height, bool clear)
    {
        var camera = BuildMatrices(width, height);
        device.SetRenderTarget(target);
        device.Viewport = new Viewport(0, 0, width, height);
        if (clear)
        {
            device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, new Vector4(Background, 0.0f, 0.0f, 0.0f), 1.0f, 0);
        }

        Draw(device, effect, camera.ViewProjection, Plane(distance, left: true, right: true));
    }

    private static VertexPosition[] Plane(float distance, bool left, bool right)
    {
        float x0 = left ? -2.0f * distance : 0.0f;
        float x1 = right ? 2.0f * distance : 0.0f;
        return Quad(x0, x1, -2.0f * distance, 2.0f * distance, distance);
    }

    private static VertexPosition[] HalfPlane(float distance, bool top)
    {
        float y0 = top ? 0.0f : -2.0f * distance;
        float y1 = top ? 2.0f * distance : 0.0f;
        return Quad(-2.0f * distance, 2.0f * distance, y0, y1, distance);
    }

    private static VertexPosition[] Quad(float x0, float x1, float y0, float y1, float distance)
    {
        float z = -distance;
        return new[]
        {
            new VertexPosition(new Vector3(x0, y0, z)),
            new VertexPosition(new Vector3(x1, y0, z)),
            new VertexPosition(new Vector3(x1, y1, z)),
            new VertexPosition(new Vector3(x0, y1, z)),
        };
    }

    private static void Draw(GraphicsDevice device, Effect effect, Matrix viewProjection, VertexPosition[] quad)
    {
        device.BlendState = BlendState.Opaque;
        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullNone;
        effect.CurrentTechnique = effect.Techniques["Depth"];
        effect.Parameters["WorldViewProj"].SetValue(viewProjection);
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, quad, 0, 4, new short[] { 0, 1, 2, 0, 2, 3 }, 0, 2);
        }
    }

    private static float[] ReadRed(RenderTarget2D target)
    {
        int count = target.Width * target.Height;
        var result = new float[count];
        switch (target.Format)
        {
            case SurfaceFormat.Single:
                target.GetData(result);
                break;
            case SurfaceFormat.HalfSingle:
                var halves = new HalfSingle[count];
                target.GetData(halves);
                for (int i = 0; i < count; i++)
                {
                    result[i] = halves[i].ToSingle();
                }

                break;
            case SurfaceFormat.HalfVector4:
                var halves4 = new HalfVector4[count];
                target.GetData(halves4);
                for (int i = 0; i < count; i++)
                {
                    result[i] = halves4[i].ToVector4().X;
                }

                break;
            case SurfaceFormat.Vector4:
                var vectors = new Vector4[count];
                target.GetData(vectors);
                for (int i = 0; i < count; i++)
                {
                    result[i] = vectors[i].X;
                }

                break;
            default:
                throw new NotSupportedException(target.Format.ToString());
        }

        return result;
    }
}
