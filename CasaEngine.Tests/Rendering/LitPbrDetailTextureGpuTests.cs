using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using Xunit;
using Xunit.Abstractions;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// The detail texture of <c>LitPbr.fx</c> on a real device (ADR-0116). Every case renders one pixel (a quad facing the camera at 5
/// units on +Z) into a floating-point target with the flat ambient 1, no light, no shadow and the linear colour pipeline off, so the
/// pixel is the diffuse albedo: the base colour factor (mid grey) times a white base texture times <c>2 * detail</c>. Skipped where no
/// device can be created.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class LitPbrDetailTextureGpuTests
{
    private readonly ITestOutputHelper _output;

    public LitPbrDetailTextureGpuTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static readonly Color MidBaseColor = new(128, 128, 128, 255);

    [GpuFact]
    public void WithoutADetailTexture_TheImageIsTheSameWhetherOrNotOneWasSetAndCleared()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var white = CreateUniformTexture(new Color(255, 255, 255, 255));
            using var detail = CreateUniformTexture(new Color(64, 64, 64, 255));

            var untouched = new LitPbrMaterial { BaseColor = MidBaseColor, BaseColorTexture = white };
            var setThenCleared = new LitPbrMaterial { BaseColor = MidBaseColor, BaseColorTexture = white };
            setThenCleared.DetailTexture = detail;
            setThenCleared.DetailTiling = 24.0f;
            setThenCleared.DetailTexture = null;

            Vector3 reference = Render(untouched);
            Vector3 cleared = Render(setThenCleared);

            // (a) no detail texture: bit-identical, and the pixel is the mid grey base colour.
            _output.WriteLine($"(a) untouched {reference.X:G6}, set then cleared {cleared.X:G6}, level {reference.X * 255.0f:F2}");
            Assert.Equal(reference, cleared);
            Assert.InRange(reference.X * 255.0f, 126.0f, 130.0f);
        });
    }

    [GpuFact]
    public void AMidGreyDetailTexture_LeavesEveryChannelWithinOneLevelOfTheImageWithoutIt()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var white = CreateUniformTexture(new Color(255, 255, 255, 255));
            using var grey128 = CreateUniformTexture(new Color(128, 128, 128, 255));

            Vector3 without = Render(new LitPbrMaterial { BaseColor = MidBaseColor, BaseColorTexture = white });
            Vector3 with = Render(new LitPbrMaterial { BaseColor = MidBaseColor, BaseColorTexture = white, DetailTexture = grey128, DetailTiling = 24.0f });

            // (b) 2 * 128 / 255 = 1.0039: no 8-bit grey is exactly neutral, one level of tolerance.
            _output.WriteLine($"(b) without {without * 255.0f}, with grey 128 {with * 255.0f}");
            Assert.InRange(MathF.Abs(with.X - without.X) * 255.0f, 0.0f, 1.0f);
            Assert.InRange(MathF.Abs(with.Y - without.Y) * 255.0f, 0.0f, 1.0f);
            Assert.InRange(MathF.Abs(with.Z - without.Z) * 255.0f, 0.0f, 1.0f);
        });
    }

    [GpuFact]
    public void ADarkDetailTexture_DarkensTheBaseColorByTwiceItsValue()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var white = CreateUniformTexture(new Color(255, 255, 255, 255));
            using var grey64 = CreateUniformTexture(new Color(64, 64, 64, 255));

            Vector3 without = Render(new LitPbrMaterial { BaseColor = MidBaseColor, BaseColorTexture = white });
            Vector3 with = Render(new LitPbrMaterial { BaseColor = MidBaseColor, BaseColorTexture = white, DetailTexture = grey64, DetailTiling = 24.0f });

            // (c) the factor is 2 * 64 / 255 = 0.502: about half the base colour, far more than the 20 levels asked for.
            _output.WriteLine($"(c) without {without * 255.0f}, with grey 64 {with * 255.0f}, ratio {with.X / without.X:G6}");
            Assert.True((without.X - with.X) * 255.0f >= 20.0f, $"Grey 64 gave {with.X * 255.0f} against {without.X * 255.0f} without a detail texture.");
            Assert.InRange(with.X / without.X, 0.49f, 0.515f);
        });
    }

    [GpuFact]
    public void ADetailTextureWithoutABaseTexture_IsIgnored()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var grey64 = CreateUniformTexture(new Color(64, 64, 64, 255));

            Vector3 without = Render(new LitPbrMaterial { BaseColor = MidBaseColor });
            Vector3 with = Render(new LitPbrMaterial { BaseColor = MidBaseColor, DetailTexture = grey64 });

            Assert.Equal(without, with);
        });
    }

    [GpuFact]
    public void Bind_WritesTheDetailConstantsEveryTime_SoNothingLeaksIntoTheNextDraw()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var device = GpuDeviceHost.Instance.GraphicsDevice;
            var effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.LitPbrContentName);
            var shader = new ShaderWrapper(effect);
            var context = new RenderContext { Device = device };

            using var baseTexture = CreateUniformTexture(Color.White);
            using var detailTexture = CreateUniformTexture(Color.Gray);

            new LitPbrMaterial { BaseColorTexture = baseTexture, DetailTexture = detailTexture, DetailTiling = 24.0f }
                .Bind(shader, in context, Matrix.Identity);

            Assert.Equal(1.0f, effect.Parameters[ShaderParameterNames.HasDetailTexture]!.GetValueSingle());
            Assert.Equal(24.0f, effect.Parameters[ShaderParameterNames.DetailTiling]!.GetValueSingle());
            Assert.Same(detailTexture, effect.Parameters[ShaderParameterNames.DetailTexture]!.GetValueTexture2D());

            // A detail texture without a base texture is not bound as one.
            new LitPbrMaterial { DetailTexture = detailTexture }.Bind(shader, in context, Matrix.Identity);
            Assert.Equal(0.0f, effect.Parameters[ShaderParameterNames.HasDetailTexture]!.GetValueSingle());

            // A bare material resets the flag and the tiling left by the first one.
            new LitPbrMaterial().Bind(shader, in context, Matrix.Identity);
            Assert.Equal(0.0f, effect.Parameters[ShaderParameterNames.HasDetailTexture]!.GetValueSingle());
            Assert.Equal(1.0f, effect.Parameters[ShaderParameterNames.DetailTiling]!.GetValueSingle());
        });
    }

    private static Texture2D CreateUniformTexture(Color color)
    {
        var texture = new Texture2D(GpuDeviceHost.Instance.GraphicsDevice, 1, 1);
        texture.SetData(new[] { color });
        return texture;
    }

    /// <summary>
    /// One quad facing the camera (eye at (0, 0, 5)) into a 1x1 half-float target cleared to black, with the flat ambient 1 and no
    /// light, and returns the colour of the pixel.
    /// </summary>
    private static Vector3 Render(LitPbrMaterial material)
    {
        var device = GpuDeviceHost.Instance.GraphicsDevice;
        var effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.LitPbrContentName);
        var shader = new ShaderWrapper(effect);

        var eye = new Vector3(0.0f, 0.0f, 5.0f);
        var context = new RenderContext
        {
            Device = device,
            Frame = new RenderFrame(Matrix.Identity, Matrix.Identity, eye, new Rectangle(0, 0, 1, 1)),
        };

        effect.CurrentTechnique = effect.Techniques["LitPbr"]!;
        material.Bind(shader, in context, Matrix.Identity);

        shader.SetParameter("ActiveDirectionalLightCount", 0.0f);
        shader.SetParameter("ActivePointLightCount", 0.0f);
        shader.SetParameter("ActiveSpotLightCount", 0.0f);
        shader.SetParameter("AmbientColor", Vector3.One);
        shader.SetParameter("UseFlatAmbientSource", 1.0f);
        shader.SetParameter("HasEnvironmentCubeTexture", 0.0f);
        shader.SetParameter("HasIbl", 0.0f);
        shader.SetParameter("ActiveShadowLightCount", 0.0f);
        shader.SetParameter("ReceiveShadows", 0.0f);
        shader.SetParameter("ShadowApplyMode", 0.0f);
        shader.SetParameter("ColorPipelineIsLinear", 0.0f);
        shader.SetParameter("EnvironmentSpecularIntensity", 0.0f);

        var vertices = new[]
        {
            new VertexPositionNormalTexture(new Vector3(-1.0f, -1.0f, 0.0f), Vector3.UnitZ, new Vector2(0.0f, 1.0f)),
            new VertexPositionNormalTexture(new Vector3(-1.0f, 1.0f, 0.0f), Vector3.UnitZ, new Vector2(0.0f, 0.0f)),
            new VertexPositionNormalTexture(new Vector3(1.0f, -1.0f, 0.0f), Vector3.UnitZ, new Vector2(1.0f, 1.0f)),
            new VertexPositionNormalTexture(new Vector3(1.0f, 1.0f, 0.0f), Vector3.UnitZ, new Vector2(1.0f, 0.0f)),
        };

        var previousTargets = device.GetRenderTargets();
        var previousViewport = device.Viewport;
        var previousBlend = device.BlendState;
        var previousDepth = device.DepthStencilState;
        var previousRasterizer = device.RasterizerState;
        using var target = new RenderTarget2D(device, 1, 1, false, SurfaceFormat.HalfVector4, DepthFormat.None);
        try
        {
            device.SetRenderTarget(target);
            device.Clear(ClearOptions.Target, Vector4.UnitW, 0.0f, 0);
            device.BlendState = BlendState.AlphaBlend;
            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone;

            effect.CurrentTechnique.Passes[0].Apply();
            device.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices, 0, 2);

            device.SetRenderTarget(null);
            var pixel = new HalfVector4[1];
            target.GetData(pixel);
            Vector4 value = pixel[0].ToVector4();
            return new Vector3(value.X, value.Y, value.Z);
        }
        finally
        {
            device.SetRenderTargets(previousTargets);
            device.Viewport = previousViewport;
            device.BlendState = previousBlend;
            device.DepthStencilState = previousDepth;
            device.RasterizerState = previousRasterizer;
        }
    }
}
