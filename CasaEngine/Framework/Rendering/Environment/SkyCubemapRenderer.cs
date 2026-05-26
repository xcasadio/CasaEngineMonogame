using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using XnaTextureCube = Microsoft.Xna.Framework.Graphics.TextureCube;

namespace CasaEngine.Framework.Rendering.Environment;

/// <summary>
/// Draws a fullscreen cubemap-backed sky using a dedicated effect and GPU state block.
/// </summary>
public sealed class SkyCubemapRenderer
{
    private const string TechniqueName = "SkyCubemap";
    private const string InverseViewProjectionParameter = "InverseViewProjection";
    private const string EyePositionParameter = "EyePosition";
    private const string EnvironmentCubeTextureParameter = "EnvironmentCubeTexture";

    private static readonly VertexPositionTexture[] Vertices =
    [
        new(new Vector3(-1.0f, -1.0f, 0.0f), new Vector2(0.0f, 1.0f)),
        new(new Vector3(-1.0f,  1.0f, 0.0f), new Vector2(0.0f, 0.0f)),
        new(new Vector3( 1.0f, -1.0f, 0.0f), new Vector2(1.0f, 1.0f)),
        new(new Vector3( 1.0f,  1.0f, 0.0f), new Vector2(1.0f, 0.0f)),
    ];

    private static readonly short[] Indices = [0, 1, 2, 2, 1, 3];

    private readonly Effect _effect;

    public SkyCubemapRenderer(Effect effect)
    {
        _effect = effect ?? throw new ArgumentNullException(nameof(effect));
        _effect.CurrentTechnique = _effect.Techniques[TechniqueName];
    }

    public bool CanDraw(in ResolvedEnvironmentSettings environment)
    {
        return environment.BackgroundMode == EnvironmentBackgroundMode.Environment
            && environment.BackgroundCubemap is not null;
    }

    public void Draw(in RenderContext context)
    {
        XnaTextureCube cubemap = context.Environment.BackgroundCubemap;
        if (cubemap is null)
        {
            return;
        }

        GraphicsDevice device = context.Device;
        BlendState previousBlendState = device.BlendState;
        DepthStencilState previousDepthStencilState = device.DepthStencilState;
        RasterizerState previousRasterizerState = device.RasterizerState;
        SamplerState previousSamplerState = device.SamplerStates[0];

        try
        {
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone;
            device.SamplerStates[0] = SamplerState.LinearClamp;

            Matrix inverseViewProjection = Matrix.Invert(context.Frame.ViewProjection);
            _effect.Parameters[InverseViewProjectionParameter]?.SetValue(inverseViewProjection);
            _effect.Parameters[EyePositionParameter]?.SetValue(context.Frame.CameraPosition);
            _effect.Parameters[EnvironmentCubeTextureParameter]?.SetValue(cubemap);

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    Vertices,
                    0,
                    Vertices.Length,
                    Indices,
                    0,
                    2);
            }
        }
        finally
        {
            device.BlendState = previousBlendState;
            device.DepthStencilState = previousDepthStencilState;
            device.RasterizerState = previousRasterizerState;
            device.SamplerStates[0] = previousSamplerState;
        }
    }
}