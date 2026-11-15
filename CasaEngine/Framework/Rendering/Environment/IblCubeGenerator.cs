using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering.Environment;

/// <summary>
/// Generates the two convolved textures of the image-based lighting from an environment cube, on the GPU (ADR-0109): the diffuse
/// irradiance cube and the specular prefiltered atlas. Each one is drawn by <c>IblConvolution.fx</c> with a full-screen quad into
/// a <see cref="SurfaceFormat.Vector4"/> render target, read back with <see cref="Texture2D.GetData{T}(T[], int, int)"/> and
/// written into the result texture with <c>SetData</c>, so the result does not live in a render target. It works whatever the
/// format of the source.
/// <para/>
/// <b>Why an atlas and not a cube with mips.</b> Step 1b of the plan (T6.1) measured that the OpenGL chain of MonoGame ignores
/// the level of <c>texCUBElod</c> (the compiler emits a plain <c>textureCube</c>), so a cube whose mip levels carry the
/// roughness levels cannot be read by level. The prefiltered result is therefore a 2D texture of <see cref="RoughnessBandCount"/>
/// latitude-longitude bands stacked vertically, band n holding the roughness n / 5; the shader reads two bands with
/// <c>tex2D</c> and interpolates by hand.
/// <para/>
/// The generated textures always hold linear light: when the source is not linear (<c>sourceIsLinear</c> false) every sample is
/// decoded with <c>pow(abs(c), 2.2)</c> before it is accumulated. The generator is never called while a view target is bound
/// (decision P9): the caller runs it between views. The render targets, the viewport and the blend, depth, rasterizer and
/// sampler states it sets are put back before it returns. It allocates (render targets, read-back buffer, the result): it
/// belongs to the one-off generation path, never to a per-frame path.
/// </summary>
public sealed class IblCubeGenerator : IDisposable
{
    /// <summary>Side of one face of the irradiance cube, in texels (a single mip level).</summary>
    public const int IrradianceSize = 32;

    /// <summary>Width of one band of the prefiltered atlas, in texels (longitude, 360 degrees).</summary>
    public const int AtlasBandWidth = 512;

    /// <summary>Height of one band of the prefiltered atlas, in texels (latitude, 180 degrees).</summary>
    public const int AtlasBandHeight = 256;

    /// <summary>Number of bands of the atlas: the roughness values 0, 0.2, 0.4, 0.6, 0.8 and 1.</summary>
    public const int RoughnessBandCount = 6;

    /// <summary>Height of the whole atlas, in texels.</summary>
    public const int AtlasHeight = AtlasBandHeight * RoughnessBandCount;

    internal const string IrradianceTechniqueName = "Irradiance";
    internal const string PrefilterTechniqueName = "PrefilterGgx";

    private const string FaceForwardParameter = "FaceForward";
    private const string FaceRightParameter = "FaceRight";
    private const string FaceUpParameter = "FaceUp";
    private const string RoughnessParameter = "Roughness";
    private const string SourceIsLinearParameter = "SourceIsLinear";
    private const string SourceCubeParameter = "SourceCube";

    private static readonly CubeMapFace[] Faces =
    [
        CubeMapFace.PositiveX,
        CubeMapFace.NegativeX,
        CubeMapFace.PositiveY,
        CubeMapFace.NegativeY,
        CubeMapFace.PositiveZ,
        CubeMapFace.NegativeZ,
    ];

    private static readonly VertexPositionTexture[] Quad =
    [
        new(new Vector3(-1.0f, 1.0f, 0.0f), new Vector2(0.0f, 0.0f)),
        new(new Vector3(1.0f, 1.0f, 0.0f), new Vector2(1.0f, 0.0f)),
        new(new Vector3(-1.0f, -1.0f, 0.0f), new Vector2(0.0f, 1.0f)),
        new(new Vector3(1.0f, -1.0f, 0.0f), new Vector2(1.0f, 1.0f)),
    ];

    private readonly GraphicsDevice _graphicsDevice;
    private readonly Effect _effect;
    private readonly EffectParameter _faceForward;
    private readonly EffectParameter _faceRight;
    private readonly EffectParameter _faceUp;
    private readonly EffectParameter _roughness;
    private readonly EffectParameter _sourceIsLinear;
    private readonly EffectParameter _sourceCube;
    private readonly EffectTechnique _irradianceTechnique;
    private readonly EffectTechnique _prefilterTechnique;

    /// <param name="graphicsDevice">The device the textures are generated on.</param>
    /// <param name="effect">The compiled <c>IblConvolution</c> effect (<see cref="CasaEngine.Framework.Rendering.Shaders.BuiltInShaderCatalog.IblConvolutionContentName"/>).</param>
    public IblCubeGenerator(GraphicsDevice graphicsDevice, Effect effect)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(effect);

        _graphicsDevice = graphicsDevice;
        _effect = effect;
        _irradianceTechnique = effect.Techniques[IrradianceTechniqueName]
            ?? throw new ArgumentException($"The effect has no technique {IrradianceTechniqueName}.", nameof(effect));
        _prefilterTechnique = effect.Techniques[PrefilterTechniqueName]
            ?? throw new ArgumentException($"The effect has no technique {PrefilterTechniqueName}.", nameof(effect));
        _faceForward = effect.Parameters[FaceForwardParameter];
        _faceRight = effect.Parameters[FaceRightParameter];
        _faceUp = effect.Parameters[FaceUpParameter];
        _roughness = effect.Parameters[RoughnessParameter];
        _sourceIsLinear = effect.Parameters[SourceIsLinearParameter];
        _sourceCube = effect.Parameters[SourceCubeParameter];
    }

    /// <summary>Duration of the last <see cref="GenerateIrradiance"/> or <see cref="GeneratePrefiltered"/> call, in milliseconds.</summary>
    public double LastGenerationMilliseconds { get; private set; }

    /// <summary>Perceptual roughness stored in a band of the prefiltered atlas: <c>band / 5</c>.</summary>
    public static float GetRoughnessForBand(int band)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(band);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(band, RoughnessBandCount);
        return band / (float)(RoughnessBandCount - 1);
    }

    /// <summary>
    /// Unit direction through the centre of the texel (<paramref name="x"/>, <paramref name="y"/>) of a face of
    /// <paramref name="size"/> texels per side; row 0 is the top row (the first row of the data given to
    /// <see cref="TextureCube.SetData{T}(CubeMapFace, T[])"/>). Same convention as
    /// <see cref="PanoramaEnvironmentGenerator.GetDirectionForFace"/>.
    /// </summary>
    public static Vector3 GetTexelDirection(CubeMapFace face, int x, int y, int size)
    {
        float u = (2.0f * (x + 0.5f) / size) - 1.0f;
        float v = 1.0f - (2.0f * (y + 0.5f) / size);
        return PanoramaEnvironmentGenerator.GetDirectionForFace(face, u, v);
    }

    /// <summary>
    /// Basis of a face: the direction of the texel at (u, v) in [-1, 1] (u to the right, v up) is
    /// <c>normalize(forward + u * right + v * up)</c>. The shader rebuilds the direction of a texel from it.
    /// </summary>
    public static void GetFaceBasis(CubeMapFace face, out Vector3 forward, out Vector3 right, out Vector3 up)
    {
        switch (face)
        {
            case CubeMapFace.PositiveX:
                forward = Vector3.UnitX;
                right = -Vector3.UnitZ;
                up = Vector3.UnitY;
                break;
            case CubeMapFace.NegativeX:
                forward = -Vector3.UnitX;
                right = Vector3.UnitZ;
                up = Vector3.UnitY;
                break;
            case CubeMapFace.PositiveY:
                forward = Vector3.UnitY;
                right = Vector3.UnitX;
                up = -Vector3.UnitZ;
                break;
            case CubeMapFace.NegativeY:
                forward = -Vector3.UnitY;
                right = Vector3.UnitX;
                up = Vector3.UnitZ;
                break;
            case CubeMapFace.PositiveZ:
                forward = Vector3.UnitZ;
                right = Vector3.UnitX;
                up = Vector3.UnitY;
                break;
            case CubeMapFace.NegativeZ:
                forward = -Vector3.UnitZ;
                right = -Vector3.UnitX;
                up = Vector3.UnitY;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(face), face, null);
        }
    }

    /// <summary>
    /// Unit direction through the centre of the texel (<paramref name="x"/>, <paramref name="y"/>) of one band of the prefiltered
    /// atlas (<see cref="AtlasBandWidth"/> by <see cref="AtlasBandHeight"/>, row 0 at the north pole): the inverse of
    /// <see cref="PanoramaEnvironmentGenerator.GetPanoramaUv"/>.
    /// </summary>
    public static Vector3 GetBandTexelDirection(int x, int y)
    {
        float u = (x + 0.5f) / AtlasBandWidth;
        float v = (y + 0.5f) / AtlasBandHeight;
        float longitude = (u - 0.5f) * (2.0f * MathF.PI);
        float latitude = (0.5f - v) * MathF.PI;
        float cosLatitude = MathF.Cos(latitude);
        return new Vector3(cosLatitude * MathF.Sin(longitude), MathF.Sin(latitude), -cosLatitude * MathF.Cos(longitude));
    }

    /// <summary>
    /// Texture coordinates, in the whole atlas, at which a shader reads the band <paramref name="band"/> in a direction: the
    /// latitude-longitude coordinates of <see cref="PanoramaEnvironmentGenerator.GetPanoramaUv"/> inside the band, with the
    /// vertical coordinate kept half a texel inside it so that the bilinear footprint never reaches a neighbouring band, then
    /// the band offset. <paramref name="band"/> is a whole number here; the shader interpolates between two bands by hand.
    /// </summary>
    public static Vector2 GetAtlasUv(Vector3 direction, int band)
    {
        Vector2 uv = PanoramaEnvironmentGenerator.GetPanoramaUv(direction);
        float halfTexel = 0.5f / AtlasBandHeight;
        float localV = Math.Clamp(uv.Y, halfTexel, 1.0f - halfTexel);
        return new Vector2(uv.X, (band + localV) / RoughnessBandCount);
    }

    /// <summary>
    /// Diffuse irradiance cube of <paramref name="source"/>: <see cref="IrradianceSize"/> texels per face, one mip level,
    /// <see cref="SurfaceFormat.Vector4"/>, linear. Each texel holds the cosine-weighted average of the incoming radiance around
    /// its direction (E / pi), so a uniform source gives back its own colour. The caller owns the returned cube.
    /// </summary>
    public TextureCube GenerateIrradiance(TextureCube source, bool sourceIsLinear)
    {
        ValidateSource(source);

        long start = Stopwatch.GetTimestamp();
        var result = new TextureCube(_graphicsDevice, IrradianceSize, false, SurfaceFormat.Vector4);
        var readBack = new Vector4[IrradianceSize * IrradianceSize];
        using var scope = new StateScope(_graphicsDevice);
        BeginGeneration(source, sourceIsLinear);

        try
        {
            _effect.CurrentTechnique = _irradianceTechnique;
            using var target = new RenderTarget2D(
                _graphicsDevice, IrradianceSize, IrradianceSize, false, SurfaceFormat.Vector4, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

            for (int faceIndex = 0; faceIndex < Faces.Length; faceIndex++)
            {
                CubeMapFace face = Faces[faceIndex];
                GetFaceBasis(face, out Vector3 forward, out Vector3 right, out Vector3 up);
                _faceForward.SetValue(forward);
                _faceRight.SetValue(right);
                _faceUp.SetValue(up);

                _graphicsDevice.SetRenderTarget(target);
                DrawQuad(new Viewport(0, 0, IrradianceSize, IrradianceSize));

                _graphicsDevice.SetRenderTarget(null);
                target.GetData(readBack, 0, readBack.Length);
                result.SetData(face, 0, null, readBack, 0, readBack.Length);
            }
        }
        finally
        {
            EndGeneration();
        }

        LastGenerationMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        return result;
    }

    /// <summary>
    /// Specular prefiltered atlas of <paramref name="source"/>: <see cref="RoughnessBandCount"/> latitude-longitude bands of
    /// <see cref="AtlasBandWidth"/> by <see cref="AtlasBandHeight"/> texels stacked vertically, no mips,
    /// <see cref="SurfaceFormat.Vector4"/>, linear. Band n holds the GGX-prefiltered radiance for the roughness n / 5
    /// (<see cref="GetRoughnessForBand"/>); band 0 is the source resampled. The caller owns the returned texture.
    /// </summary>
    public Texture2D GeneratePrefiltered(TextureCube source, bool sourceIsLinear)
    {
        ValidateSource(source);

        long start = Stopwatch.GetTimestamp();
        var result = new Texture2D(_graphicsDevice, AtlasBandWidth, AtlasHeight, false, SurfaceFormat.Vector4);
        var readBack = new Vector4[AtlasBandWidth * AtlasHeight];
        using var scope = new StateScope(_graphicsDevice);
        BeginGeneration(source, sourceIsLinear);

        try
        {
            _effect.CurrentTechnique = _prefilterTechnique;
            using var target = new RenderTarget2D(
                _graphicsDevice, AtlasBandWidth, AtlasHeight, false, SurfaceFormat.Vector4, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

            _graphicsDevice.SetRenderTarget(target);
            for (int band = 0; band < RoughnessBandCount; band++)
            {
                _roughness.SetValue(GetRoughnessForBand(band));
                DrawQuad(new Viewport(0, band * AtlasBandHeight, AtlasBandWidth, AtlasBandHeight));
            }

            _graphicsDevice.SetRenderTarget(null);
            target.GetData(readBack, 0, readBack.Length);
            result.SetData(readBack, 0, readBack.Length);
        }
        finally
        {
            EndGeneration();
        }

        LastGenerationMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        return result;
    }

    /// <summary>Does nothing: the generator owns no GPU resource between two calls. Kept so owners can dispose it with the pipeline.</summary>
    public void Dispose()
    {
    }

    private void ValidateSource(TextureCube source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(source));
        }

        if (!ReferenceEquals(source.GraphicsDevice, _graphicsDevice))
        {
            throw new ArgumentException("The source cube belongs to another graphics device.", nameof(source));
        }
    }

    private void BeginGeneration(TextureCube source, bool sourceIsLinear)
    {
        _sourceCube.SetValue(source);
        _sourceIsLinear.SetValue(sourceIsLinear ? 1.0f : 0.0f);
        _roughness.SetValue(0.0f);

        _graphicsDevice.BlendState = BlendState.Opaque;
        _graphicsDevice.DepthStencilState = DepthStencilState.None;
        _graphicsDevice.RasterizerState = RasterizerState.CullNone;
        _graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
    }

    private void EndGeneration()
    {
        // Do not keep the source alive through the effect parameter.
        _sourceCube.SetValue((TextureCube)null);
    }

    private void DrawQuad(Viewport viewport)
    {
        _graphicsDevice.Viewport = viewport;
        _effect.CurrentTechnique.Passes[0].Apply();
        _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, Quad, 0, 2);
    }

    /// <summary>Remembers the state of the device on creation and puts it back on disposal.</summary>
    private readonly struct StateScope : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly RenderTargetBinding[] _targets;
        private readonly Viewport _viewport;
        private readonly BlendState _blend;
        private readonly DepthStencilState _depth;
        private readonly RasterizerState _rasterizer;
        private readonly SamplerState _sampler;

        public StateScope(GraphicsDevice device)
        {
            _device = device;
            _targets = device.GetRenderTargets();
            _viewport = device.Viewport;
            _blend = device.BlendState;
            _depth = device.DepthStencilState;
            _rasterizer = device.RasterizerState;
            _sampler = device.SamplerStates[0];
        }

        public void Dispose()
        {
            if (_targets.Length == 0)
            {
                _device.SetRenderTarget(null);
            }
            else
            {
                _device.SetRenderTargets(_targets);
            }

            _device.Viewport = _viewport;
            _device.BlendState = _blend;
            _device.DepthStencilState = _depth;
            _device.RasterizerState = _rasterizer;
            _device.SamplerStates[0] = _sampler;
        }
    }
}
