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
    private const SurfaceFormat PreferredShadowMapSurfaceFormat = SurfaceFormat.Single;
    private const SurfaceFormat FallbackShadowMapSurfaceFormat = SurfaceFormat.Color;

    private RenderTarget2D _shadowMapAtlas;

    public ShaderWrapper ShadowShader { get; set; }

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
        if (!context.Shadows.Settings.Enabled)
        {
            return;
        }

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
            context.Shadows.Settings.MaxDistance,
            context.Shadows.Settings.DepthRange,
            resolution);

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

    private void DrawShadowCaster(in RenderItem item, Matrix lightViewProjection, GraphicsDevice device, RenderStats stats)
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

    private bool ConfigureMaterialMask(MaterialBase material, RenderStats stats, out float alphaCutoff)
    {
        Texture2D maskTexture = material.ShadowMaskTexture;
        if (maskTexture is not null)
        {
            alphaCutoff = material.AlphaCutoff;
            ShadowShader!.SetTextureParameter(ShaderParameterNames.BasColorTexture, maskTexture, stats);
            return true;
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

        _shadowMapAtlas = TryAcquireShadowMapAtlas(device, resolution, PreferredShadowMapSurfaceFormat)
            ?? TryAcquireShadowMapAtlas(device, resolution, FallbackShadowMapSurfaceFormat);
    }

    private static RenderTarget2D TryAcquireShadowMapAtlas(GraphicsDevice device, int resolution, SurfaceFormat surfaceFormat)
    {
        try
        {
            var renderTargetPool = RenderTargetPool.Resolve(null);
            return renderTargetPool?.Acquire(resolution, resolution, surfaceFormat, DepthFormat.Depth24)
                ?? new RenderTarget2D(device, resolution, resolution, false, surfaceFormat, DepthFormat.Depth24);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
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

    /// <summary>
    /// Half-side, in world units, of the square the shadow map covers around the camera: the maximum distance, at least 1.
    /// </summary>
    internal static float ResolveLateralExtent(float maxDistance)
        => MathF.Max(1.0f, maxDistance);

    /// <summary>
    /// Distance, in world units, from the centre of the shadow map back to the light and (doubled) to the far plane: the
    /// depth range when it is positive (at least 1), otherwise the lateral extent.
    /// </summary>
    internal static float ResolveDepthExtent(float lateralExtent, float depthRange)
        => depthRange > 0.0f ? MathF.Max(1.0f, depthRange) : lateralExtent;

    internal static Matrix BuildDirectionalShadowViewProjection(Vector3 cameraPosition, Vector3 lightDirection, float maxDistance, int resolution)
        => BuildDirectionalShadowViewProjection(cameraPosition, lightDirection, maxDistance, 0.0f, resolution);

    /// <summary>
    /// Builds the orthographic light matrix of the shadow map. The map covers a square of half-side <paramref name="maxDistance"/>
    /// (the lateral extent) around the camera. With a <paramref name="depthRange"/> of 0 the light and the far plane follow the
    /// lateral extent; with a positive value the light is placed <paramref name="depthRange"/> upstream of the camera and the far
    /// plane at twice that distance, the lateral extent staying <paramref name="maxDistance"/>.
    /// </summary>
    internal static Matrix BuildDirectionalShadowViewProjection(Vector3 cameraPosition, Vector3 lightDirection, float maxDistance, float depthRange, int resolution)
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

        float clampedDistance = ResolveLateralExtent(maxDistance);
        float depthExtent = ResolveDepthExtent(clampedDistance, depthRange);
        Vector3 target = cameraPosition;
        Vector3 position = target - direction * depthExtent;
        Vector3 up = MathF.Abs(Vector3.Dot(direction, Vector3.Up)) > 0.99f ? Vector3.Forward : Vector3.Up;

        Matrix view = Matrix.CreateLookAt(position, target, up);

        // Move the map by whole texels only: a sub-texel shift as the camera moves would rasterize the casters at a
        // new offset every frame, and static shadow edges would crawl.
        float texelSize = clampedDistance * 2.0f / resolution;
        view.M41 = MathF.Round(view.M41 / texelSize) * texelSize;
        view.M42 = MathF.Round(view.M42 / texelSize) * texelSize;

        Matrix projection = Matrix.CreateOrthographic(clampedDistance * 2.0f, clampedDistance * 2.0f, 0.1f, depthExtent * 2.0f);
        return view * projection;
    }
}