using CasaEngine.Framework.Materials.Runtime;
using CasaEngine.Framework.Rendering.Shaders;
using CasaEngine.Framework.Rendering.Shadows;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering.Draw;

/// <summary>
/// Renders static shadow casters into a single directional shadow atlas.
/// V1 only supports the first visible shadow-casting directional light.
/// </summary>
public sealed class ShadowPass : RenderPass
{
    private RenderTarget2D? _shadowMapAtlas;

    public ShaderWrapper? ShadowShader { get; set; }

    public ShadowPass() : base(RenderPassType.ShadowPass)
    {
    }

    public override void Execute(
        RenderContext context,
        IReadOnlyList<RenderItem> items,
        RenderStateCache stateCache,
        ShaderBindCache shaderCache,
        RenderShaderSelector shaderSelector)
    {
        if (ShadowShader is null || context.Shadows is null || context.Lighting is null)
        {
            return;
        }

        context.Shadows.Clear();

        int directionalLightIndex = FindShadowCastingDirectionalLightIndex(context.Lighting);
        if (directionalLightIndex < 0)
        {
            return;
        }

        int resolution = Math.Max(1, context.Shadows.Settings.Resolution);
        EnsureShadowMapAtlas(context.Device, resolution);
        if (_shadowMapAtlas is null)
        {
            return;
        }

        context.Shadows.ShadowMapAtlas = _shadowMapAtlas;

        Matrix lightViewProjection = BuildDirectionalShadowViewProjection(
            context.Frame.CameraPosition,
            context.Lighting.DirectionalLights[directionalLightIndex].Direction,
            context.Shadows.Settings.MaxDistance);

        context.Shadows.AddVisibleLight(new ShadowLight(
            ShadowLightType.Directional,
            directionalLightIndex,
            lightViewProjection,
            new Rectangle(0, 0, resolution, resolution),
            context.Shadows.Settings.DepthBias,
            context.Shadows.Settings.NormalBias));

        using var guard = new GraphicsStateGuard(context.Device);

        context.Device.SetRenderTarget(_shadowMapAtlas);
        context.Device.Viewport = new Viewport(0, 0, resolution, resolution);
        context.Device.Clear(Color.White);
        context.Device.BlendState = BlendState.Opaque;
        context.Device.DepthStencilState = DepthStencilState.Default;
        context.Device.RasterizerState = RasterizerState.CullCounterClockwise;
        context.Device.SetVertexBuffer(null);
        context.Device.Indices = null;

        if (context.Stats is not null)
        {
            context.Stats.EffectBinds++;
        }

        foreach (var item in items)
        {
            if (!item.EffectiveCastShadows
                || item.Material.Queue >= RenderQueue.Transparent
                || item.Mesh.VertexBuffer is null
                || item.Mesh.IndexBuffer is null)
            {
                continue;
            }

            DrawShadowCaster(item, lightViewProjection, context.Device, context.Stats);
        }
    }

    private void DrawShadowCaster(in RenderItem item, Matrix lightViewProjection, GraphicsDevice device, RenderStats? stats)
    {
        device.SetVertexBuffer(item.Mesh.VertexBuffer);
        device.Indices = item.Mesh.IndexBuffer;
        device.RasterizerState = item.CompiledMaterial?.RasterizerState
            ?? item.Material.RasterizerState
            ?? RasterizerState.CullCounterClockwise;

        bool usesTextureAlpha = ConfigureMaterialMask(item.Material, stats, out float alphaCutoff);

        ShadowShader!.SelectTechnique(usesTextureAlpha ? "ShadowDepth_Textured" : "ShadowDepth_Solid");
        ShadowShader.SetParameter(ShaderParameterNames.WorldViewProj, item.World * lightViewProjection);
        ShadowShader.SetParameter(ShaderParameterNames.AlphaCutoff, alphaCutoff);

        if (item.SubMesh is { } subMesh)
        {
            for (int passIndex = 0; passIndex < ShadowShader.PassCount; passIndex++)
            {
                ShadowShader.ApplyPass(passIndex);
                device.DrawIndexedPrimitives(item.Mesh.PrimitiveType, subMesh.VertexOffset, subMesh.IndexStart, subMesh.PrimitiveCount);
            }
        }
        else
        {
            int primitiveCount = item.Mesh.IndexBuffer.IndexCount / 3;
            for (int passIndex = 0; passIndex < ShadowShader.PassCount; passIndex++)
            {
                ShadowShader.ApplyPass(passIndex);
                device.DrawIndexedPrimitives(item.Mesh.PrimitiveType, 0, 0, primitiveCount);
            }
        }

        if (stats is not null)
        {
            stats.DrawCalls++;
        }
    }

    private bool ConfigureMaterialMask(MaterialBase material, RenderStats? stats, out float alphaCutoff)
    {
        if (material.Queue == RenderQueue.AlphaTest)
        {
            alphaCutoff = material.AlphaCutoff;

            if (material is LitDiffuseMaterial litDiffuseMaterial && litDiffuseMaterial.BasColor is not null)
            {
                ShadowShader!.SetTextureParameter(ShaderParameterNames.BasColorTexture, litDiffuseMaterial.BasColor, stats);
                return true;
            }

            if (material is UnlitTextureMaterial unlitTextureMaterial && unlitTextureMaterial.BasColor is not null)
            {
                ShadowShader!.SetTextureParameter(ShaderParameterNames.BasColorTexture, unlitTextureMaterial.BasColor, stats);
                return true;
            }
        }

        alphaCutoff = 0.0f;
        return false;
    }

    private void EnsureShadowMapAtlas(GraphicsDevice device, int resolution)
    {
        if (_shadowMapAtlas is not null
            && !_shadowMapAtlas.IsDisposed
            && _shadowMapAtlas.Width == resolution
            && _shadowMapAtlas.Height == resolution)
        {
            return;
        }

        ReleaseShadowMapAtlas();

        var renderTargetPool = RenderTargetPool.Resolve(null);
        _shadowMapAtlas = renderTargetPool?.Acquire(resolution, resolution, SurfaceFormat.Color, DepthFormat.Depth24)
            ?? new RenderTarget2D(device, resolution, resolution, false, SurfaceFormat.Color, DepthFormat.Depth24);
    }

    private void ReleaseShadowMapAtlas()
    {
        if (_shadowMapAtlas is null)
        {
            return;
        }

        var renderTargetPool = RenderTargetPool.Resolve(null);
        if (renderTargetPool is not null)
        {
            renderTargetPool.Release(_shadowMapAtlas);
        }
        else
        {
            _shadowMapAtlas.Dispose();
        }

        _shadowMapAtlas = null;
    }

    private static int FindShadowCastingDirectionalLightIndex(LightingContext lighting)
    {
        for (int i = 0; i < lighting.ActiveDirectionalLightCount; i++)
        {
            if (lighting.DirectionalLights[i].CastShadows)
            {
                return i;
            }
        }

        return -1;
    }

    private static Matrix BuildDirectionalShadowViewProjection(Vector3 cameraPosition, Vector3 lightDirection, float maxDistance)
    {
        Vector3 direction = lightDirection;
        if (direction.LengthSquared() <= 0.0001f)
        {
            direction = Vector3.Normalize(new Vector3(-0.5f, -1.0f, -0.5f));
        }
        else
        {
            direction.Normalize();
        }

        float clampedDistance = MathF.Max(1.0f, maxDistance);
        Vector3 target = cameraPosition;
        Vector3 position = target - direction * clampedDistance;
        Vector3 up = MathF.Abs(Vector3.Dot(direction, Vector3.Up)) > 0.99f ? Vector3.Forward : Vector3.Up;

        Matrix view = Matrix.CreateLookAt(position, target, up);
        Matrix projection = Matrix.CreateOrthographic(clampedDistance * 2.0f, clampedDistance * 2.0f, 0.1f, clampedDistance * 2.0f);
        return view * projection;
    }
}